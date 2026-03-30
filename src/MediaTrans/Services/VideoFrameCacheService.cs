using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaTrans.Services
{
    /// <summary>
    /// 视频帧缓存服务
    /// 使用 FFmpeg 提取视频帧，LRU 缓存策略管理帧数据
    /// </summary>
    public class VideoFrameCacheService : IDisposable
    {
        private readonly string _ffmpegPath;
        private readonly int _maxCachedFrames;
        private readonly object _cacheLock = new object();
        private readonly Dictionary<long, FrameCacheEntry> _cache;
        private readonly LinkedList<long> _lruOrder;
        private bool _disposed;

        // 当前加载的视频文件信息
        private string _videoFilePath;
        private double _frameRate;
        private double _totalDurationSeconds;
        private long _totalFrames;
        private int _videoWidth;
        private int _videoHeight;

        // 后台预取取消令牌
        private CancellationTokenSource _prefetchCts;

        /// <summary>
        /// 帧提取完成事件
        /// </summary>
        public event EventHandler<FrameReadyEventArgs> FrameReady;

        /// <summary>
        /// 创建视频帧缓存服务
        /// </summary>
        /// <param name="ffmpegPath">FFmpeg 可执行文件路径</param>
        /// <param name="maxCachedFrames">最大缓存帧数（LRU）</param>
        public VideoFrameCacheService(string ffmpegPath, int maxCachedFrames)
        {
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                throw new ArgumentNullException("ffmpegPath");
            }
            if (maxCachedFrames <= 0)
            {
                throw new ArgumentOutOfRangeException("maxCachedFrames", "最大缓存帧数必须大于 0");
            }

            _ffmpegPath = ffmpegPath;
            _maxCachedFrames = maxCachedFrames;
            _cache = new Dictionary<long, FrameCacheEntry>();
            _lruOrder = new LinkedList<long>();
        }

        /// <summary>
        /// 获取当前缓存帧数
        /// </summary>
        public int CachedFrameCount
        {
            get
            {
                lock (_cacheLock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// 获取最大缓存帧数
        /// </summary>
        public int MaxCachedFrames
        {
            get { return _maxCachedFrames; }
        }

        /// <summary>
        /// 视频帧率
        /// </summary>
        public double FrameRate
        {
            get { return _frameRate; }
        }

        /// <summary>
        /// 视频总帧数
        /// </summary>
        public long TotalFrames
        {
            get { return _totalFrames; }
        }

        /// <summary>
        /// 视频宽度
        /// </summary>
        public int VideoWidth
        {
            get { return _videoWidth; }
        }

        /// <summary>
        /// 视频高度
        /// </summary>
        public int VideoHeight
        {
            get { return _videoHeight; }
        }

        /// <summary>
        /// 视频总时长（秒）
        /// </summary>
        public double TotalDurationSeconds
        {
            get { return _totalDurationSeconds; }
        }

        /// <summary>
        /// 当前是否已加载视频
        /// </summary>
        public bool IsVideoLoaded
        {
            get { return !string.IsNullOrEmpty(_videoFilePath); }
        }

        /// <summary>
        /// 加载视频文件，解析元信息
        /// </summary>
        /// <param name="videoFilePath">视频文件路径</param>
        /// <param name="frameRate">帧率</param>
        /// <param name="totalDurationSeconds">总时长（秒）</param>
        /// <param name="videoWidth">视频宽度</param>
        /// <param name="videoHeight">视频高度</param>
        public void LoadVideo(string videoFilePath, double frameRate, double totalDurationSeconds,
            int videoWidth, int videoHeight)
        {
            if (string.IsNullOrEmpty(videoFilePath))
            {
                throw new ArgumentNullException("videoFilePath");
            }
            if (frameRate <= 0)
            {
                throw new ArgumentOutOfRangeException("frameRate", "帧率必须大于 0");
            }

            // 取消之前的预取任务
            CancelPrefetch();

            // 清空缓存
            ClearCache();

            _videoFilePath = videoFilePath;
            _frameRate = frameRate;
            _totalDurationSeconds = totalDurationSeconds;
            _videoWidth = videoWidth;
            _videoHeight = videoHeight;
            _totalFrames = (long)Math.Ceiling(totalDurationSeconds * frameRate);
        }

        /// <summary>
        /// 卸载当前视频
        /// </summary>
        public void UnloadVideo()
        {
            CancelPrefetch();
            ClearCache();
            _videoFilePath = null;
            _frameRate = 0;
            _totalDurationSeconds = 0;
            _totalFrames = 0;
            _videoWidth = 0;
            _videoHeight = 0;
        }

        /// <summary>
        /// 将帧号转为时间戳（秒）
        /// </summary>
        /// <param name="frameIndex">帧索引（从 0 开始）</param>
        /// <returns>时间戳（秒）</returns>
        public double FrameToTimestamp(long frameIndex)
        {
            if (_frameRate <= 0)
            {
                return 0;
            }
            return frameIndex / _frameRate;
        }

        /// <summary>
        /// 将时间戳（秒）转为帧号
        /// </summary>
        /// <param name="timestampSeconds">时间戳（秒）</param>
        /// <returns>帧索引</returns>
        public long TimestampToFrame(double timestampSeconds)
        {
            if (_frameRate <= 0)
            {
                return 0;
            }
            long frame = (long)(timestampSeconds * _frameRate);
            if (frame < 0) frame = 0;
            if (_totalFrames > 0 && frame >= _totalFrames) frame = _totalFrames - 1;
            return frame;
        }

        /// <summary>
        /// 尝试从缓存获取帧数据
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        /// <param name="frameData">帧图像数据（BMP 格式字节数组）</param>
        /// <returns>缓存命中返回 true</returns>
        public bool TryGetFrame(long frameIndex, out byte[] frameData)
        {
            lock (_cacheLock)
            {
                FrameCacheEntry entry;
                if (_cache.TryGetValue(frameIndex, out entry))
                {
                    // LRU 更新：移到最近使用位置
                    _lruOrder.Remove(entry.LruNode);
                    _lruOrder.AddLast(entry.LruNode);
                    frameData = entry.FrameData;
                    return true;
                }
            }

            frameData = null;
            return false;
        }

        /// <summary>
        /// 请求帧（如缓存命中立即返回，否则异步提取）
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        /// <returns>缓存命中则返回帧数据，否则返回 null（帧通过 FrameReady 事件异步返回）</returns>
        public byte[] RequestFrame(long frameIndex)
        {
            if (!IsVideoLoaded)
            {
                return null;
            }

            // 限定帧范围
            if (frameIndex < 0) frameIndex = 0;
            if (_totalFrames > 0 && frameIndex >= _totalFrames) frameIndex = _totalFrames - 1;

            byte[] frameData;
            if (TryGetFrame(frameIndex, out frameData))
            {
                return frameData;
            }

            // 缓存未命中，异步提取
            long capturedIndex = frameIndex;
            Task.Run(() =>
            {
                ExtractAndCacheFrame(capturedIndex);
            });

            return null;
        }

        /// <summary>
        /// 同步提取帧（阻塞调用）
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        /// <returns>帧数据字节数组，提取失败返回 null</returns>
        public byte[] ExtractFrameSync(long frameIndex)
        {
            if (!IsVideoLoaded)
            {
                return null;
            }

            // 限定帧范围
            if (frameIndex < 0) frameIndex = 0;
            if (_totalFrames > 0 && frameIndex >= _totalFrames) frameIndex = _totalFrames - 1;

            byte[] frameData;
            if (TryGetFrame(frameIndex, out frameData))
            {
                return frameData;
            }

            return ExtractAndCacheFrame(frameIndex);
        }

        /// <summary>
        /// 启动后台预取当前位置前后帧
        /// </summary>
        /// <param name="centerFrameIndex">中心帧索引</param>
        /// <param name="prefetchCount">前后各预取帧数</param>
        public void StartPrefetch(long centerFrameIndex, int prefetchCount)
        {
            if (!IsVideoLoaded || prefetchCount <= 0)
            {
                return;
            }

            CancelPrefetch();
            _prefetchCts = new CancellationTokenSource();
            var token = _prefetchCts.Token;
            long center = centerFrameIndex;
            int count = prefetchCount;

            Task.Run(() =>
            {
                PrefetchFrames(center, count, token);
            });
        }

        /// <summary>
        /// 取消后台预取
        /// </summary>
        public void CancelPrefetch()
        {
            if (_prefetchCts != null)
            {
                _prefetchCts.Cancel();
                _prefetchCts.Dispose();
                _prefetchCts = null;
            }
        }

        /// <summary>
        /// 清空帧缓存
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
                _lruOrder.Clear();
            }
        }

        /// <summary>
        /// 检查指定帧是否在缓存中
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        /// <returns>是否缓存命中</returns>
        public bool IsFrameCached(long frameIndex)
        {
            lock (_cacheLock)
            {
                return _cache.ContainsKey(frameIndex);
            }
        }

        /// <summary>
        /// 使用 FFmpeg 提取单帧并加入缓存
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        /// <returns>帧数据，提取失败返回 null</returns>
        private byte[] ExtractAndCacheFrame(long frameIndex)
        {
            // 再次检查是否已缓存（并发保护）
            byte[] existing;
            if (TryGetFrame(frameIndex, out existing))
            {
                return existing;
            }

            byte[] frameData = ExtractFrameFromVideo(frameIndex);
            if (frameData == null || frameData.Length == 0)
            {
                return null;
            }

            AddToCache(frameIndex, frameData);

            // 触发帧就绪事件
            var handler = FrameReady;
            if (handler != null)
            {
                handler(this, new FrameReadyEventArgs(frameIndex, frameData));
            }

            return frameData;
        }

        /// <summary>
        /// 调用 FFmpeg 从视频文件中提取指定帧
        /// 使用 -ss 精确 seek + 单帧解码输出 BMP
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        /// <returns>BMP 格式帧数据</returns>
        private byte[] ExtractFrameFromVideo(long frameIndex)
        {
            double timestamp = FrameToTimestamp(frameIndex);

            // 格式化时间戳为 HH:MM:SS.mmm
            int totalMilliseconds = (int)(timestamp * 1000);
            int hours = totalMilliseconds / 3600000;
            int minutes = (totalMilliseconds % 3600000) / 60000;
            int seconds = (totalMilliseconds % 60000) / 1000;
            int milliseconds = totalMilliseconds % 1000;
            string timeStr = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}",
                hours, minutes, seconds, milliseconds);

            // 创建临时文件路径（使用 GUID 避免冲突）
            string tempDir = Path.Combine(Path.GetTempPath(), "MediaTrans_Frames");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            string tempFile = Path.Combine(tempDir, string.Format("frame_{0}.bmp", Guid.NewGuid().ToString("N")));

            try
            {
                // FFmpeg 命令：-ss 放在 -i 前面实现快速 seek
                // -frames:v 1 只提取一帧
                // -c:v bmp 输出 BMP 格式
                string arguments = string.Format(
                    "-ss {0} -i \"{1}\" -frames:v 1 -c:v bmp -y \"{2}\"",
                    timeStr, _videoFilePath, tempFile);

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };

                using (var process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                    // 设置超时5秒（单帧提取应很快）
                    bool exited = process.WaitForExit(5000);
                    if (!exited)
                    {
                        try { process.Kill(); } catch (Exception) { }
                        return null;
                    }

                    if (process.ExitCode != 0)
                    {
                        return null;
                    }
                }

                // 读取输出文件
                if (File.Exists(tempFile))
                {
                    return File.ReadAllBytes(tempFile);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                // 清理临时文件
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch (Exception)
                {
                    // 忽略清理错误
                }
            }
        }

        /// <summary>
        /// 将帧数据添加到缓存，超出限制时淘汰最久未使用的帧
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        /// <param name="frameData">帧数据</param>
        private void AddToCache(long frameIndex, byte[] frameData)
        {
            lock (_cacheLock)
            {
                // 如果已存在则更新
                if (_cache.ContainsKey(frameIndex))
                {
                    var existingEntry = _cache[frameIndex];
                    _lruOrder.Remove(existingEntry.LruNode);
                    _lruOrder.AddLast(existingEntry.LruNode);
                    existingEntry.FrameData = frameData;
                    return;
                }

                // 淘汰最久未使用的帧（LRU 驱逐）
                while (_cache.Count >= _maxCachedFrames && _lruOrder.Count > 0)
                {
                    long oldestKey = _lruOrder.First.Value;
                    _lruOrder.RemoveFirst();
                    _cache.Remove(oldestKey);
                }

                // 添加新帧
                var node = _lruOrder.AddLast(frameIndex);
                var entry = new FrameCacheEntry
                {
                    FrameData = frameData,
                    LruNode = node
                };
                _cache[frameIndex] = entry;
            }
        }

        /// <summary>
        /// 后台预取指定中心帧前后的帧
        /// </summary>
        /// <param name="centerFrame">中心帧索引</param>
        /// <param name="prefetchCount">前后各预取帧数</param>
        /// <param name="cancellationToken">取消令牌</param>
        private void PrefetchFrames(long centerFrame, int prefetchCount, CancellationToken cancellationToken)
        {
            // 先提取中心帧，再向外扩展
            for (int offset = 0; offset <= prefetchCount; offset++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // 后方帧
                long forwardFrame = centerFrame + offset;
                if (forwardFrame >= 0 && (_totalFrames <= 0 || forwardFrame < _totalFrames))
                {
                    if (!IsFrameCached(forwardFrame))
                    {
                        ExtractAndCacheFrame(forwardFrame);
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // 前方帧（offset > 0 时才提取，避免重复）
                if (offset > 0)
                {
                    long backwardFrame = centerFrame - offset;
                    if (backwardFrame >= 0 && (_totalFrames <= 0 || backwardFrame < _totalFrames))
                    {
                        if (!IsFrameCached(backwardFrame))
                        {
                            ExtractAndCacheFrame(backwardFrame);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                CancelPrefetch();
                ClearCache();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 帧缓存条目
    /// </summary>
    internal class FrameCacheEntry
    {
        /// <summary>
        /// 帧图像数据（BMP 格式）
        /// </summary>
        public byte[] FrameData { get; set; }

        /// <summary>
        /// LRU 链表节点引用
        /// </summary>
        public LinkedListNode<long> LruNode { get; set; }
    }

    /// <summary>
    /// 帧就绪事件参数
    /// </summary>
    public class FrameReadyEventArgs : EventArgs
    {
        /// <summary>
        /// 帧索引
        /// </summary>
        public long FrameIndex { get; private set; }

        /// <summary>
        /// 帧图像数据
        /// </summary>
        public byte[] FrameData { get; private set; }

        public FrameReadyEventArgs(long frameIndex, byte[] frameData)
        {
            FrameIndex = frameIndex;
            FrameData = frameData;
        }
    }
}
