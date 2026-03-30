using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaTrans.Models;

namespace MediaTrans.Services
{
    /// <summary>
    /// PCM 数据块 — 存储一段连续采样的原始 PCM 数据
    /// </summary>
    public class PcmBlock
    {
        /// <summary>
        /// 起始采样帧索引（全局位置）
        /// </summary>
        public long StartSample { get; set; }

        /// <summary>
        /// 块内采样帧数
        /// </summary>
        public int SampleCount { get; set; }

        /// <summary>
        /// PCM 原始数据（16-bit signed, little-endian, interleaved）
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// 结束采样帧索引（不含）
        /// </summary>
        public long EndSample
        {
            get { return StartSample + SampleCount; }
        }

        /// <summary>
        /// 判断指定采样帧范围是否与本块重叠
        /// </summary>
        public bool Overlaps(long rangeStart, long rangeEnd)
        {
            return StartSample < rangeEnd && EndSample > rangeStart;
        }

        /// <summary>
        /// 获取指定采样帧在块内的偏移字节位置
        /// </summary>
        public int GetByteOffset(long globalSample, int bytesPerFrame)
        {
            long localSample = globalSample - StartSample;
            if (localSample < 0 || localSample >= SampleCount)
            {
                return -1;
            }
            return (int)(localSample * bytesPerFrame);
        }
    }

    /// <summary>
    /// 音频 PCM 缓存服务 — 流式按需加载、缓冲带管理
    /// 通过 FFmpeg 解码音频为 PCM 原始数据，按块缓存
    /// 仅保留可见区域 + 前后各 2x 缓冲带的数据，超出范围自动释放
    /// </summary>
    public class AudioPcmCacheService : IDisposable
    {
        private readonly string _ffmpegPath;
        private readonly JobObject _jobObject;
        private readonly object _lock = new object();
        private bool _disposed;

        // 当前加载的音频信息
        private AudioInfo _audioInfo;

        // 按起始采样帧排序的缓存块列表
        private readonly List<PcmBlock> _cachedBlocks;

        // 每个缓存块的默认采样帧数（约 1 秒）
        private int _blockSampleCount;

        // 缓冲带大小（以块数为单位，前后各 N 块）
        private int _bufferBlockCount;

        // 当前可见区域（采样帧范围）
        private long _visibleStart;
        private long _visibleEnd;

        // 后台加载控制
        private CancellationTokenSource _loadCts;
        private readonly object _loadLock = new object();

        /// <summary>
        /// 当前加载的音频信息
        /// </summary>
        public AudioInfo AudioInfo
        {
            get { return _audioInfo; }
        }

        /// <summary>
        /// 每块的采样帧数
        /// </summary>
        public int BlockSampleCount
        {
            get { return _blockSampleCount; }
        }

        /// <summary>
        /// 缓冲带块数
        /// </summary>
        public int BufferBlockCount
        {
            get { return _bufferBlockCount; }
        }

        /// <summary>
        /// 当前缓存的块数
        /// </summary>
        public int CachedBlockCount
        {
            get
            {
                lock (_lock)
                {
                    return _cachedBlocks.Count;
                }
            }
        }

        /// <summary>
        /// 当前缓存的总字节数
        /// </summary>
        public long CachedBytes
        {
            get
            {
                lock (_lock)
                {
                    long total = 0;
                    foreach (var block in _cachedBlocks)
                    {
                        if (block.Data != null)
                        {
                            total += block.Data.Length;
                        }
                    }
                    return total;
                }
            }
        }

        /// <summary>
        /// 创建 PCM 缓存服务
        /// </summary>
        /// <param name="ffmpegPath">FFmpeg 可执行文件路径</param>
        /// <param name="blockSampleCount">每块采样帧数，默认 44100（约1秒@44.1kHz）</param>
        /// <param name="bufferBlockCount">前后缓冲带块数，默认 10（前后各 10 块≈10秒）</param>
        public AudioPcmCacheService(string ffmpegPath, int blockSampleCount = 44100, int bufferBlockCount = 10)
        {
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                throw new ArgumentNullException("ffmpegPath");
            }
            _ffmpegPath = ffmpegPath;
            _jobObject = new JobObject();
            _blockSampleCount = blockSampleCount > 0 ? blockSampleCount : 44100;
            _bufferBlockCount = bufferBlockCount > 0 ? bufferBlockCount : 10;
            _cachedBlocks = new List<PcmBlock>();
        }

        /// <summary>
        /// 加载音频文件，获取基本信息（不立即解码全部数据）
        /// </summary>
        public AudioInfo LoadAudioFile(string filePath, int sampleRate, int channels, double durationSeconds)
        {
            lock (_lock)
            {
                // 清理之前的缓存
                ClearCache();

                _audioInfo = new AudioInfo
                {
                    FilePath = filePath,
                    SampleRate = sampleRate,
                    Channels = channels,
                    BytesPerSample = 2, // 16-bit PCM
                    DurationSeconds = durationSeconds,
                    TotalSamples = (long)(durationSeconds * sampleRate)
                };

                return _audioInfo;
            }
        }

        /// <summary>
        /// 更新可见区域并触发按需加载
        /// 自动释放超出缓冲带的数据块
        /// </summary>
        /// <param name="visibleStartSample">可见区域起始采样帧</param>
        /// <param name="visibleEndSample">可见区域结束采样帧</param>
        public void UpdateVisibleRange(long visibleStartSample, long visibleEndSample)
        {
            if (_audioInfo == null || _disposed)
            {
                return;
            }

            lock (_lock)
            {
                _visibleStart = Math.Max(0, visibleStartSample);
                _visibleEnd = Math.Min(_audioInfo.TotalSamples, visibleEndSample);
            }

            // 计算需要缓存的范围（可见区域 + 前后缓冲带）
            long visibleBlockSpan = (_visibleEnd - _visibleStart);
            long bufferSize = visibleBlockSpan * 2; // 前后各 2x 屏宽
            long cacheStart = Math.Max(0, _visibleStart - bufferSize);
            long cacheEnd = Math.Min(_audioInfo.TotalSamples, _visibleEnd + bufferSize);

            // 清理超出缓冲范围的块
            EvictOutOfRange(cacheStart, cacheEnd);

            // 异步加载缺失的块
            LoadMissingBlocksAsync(cacheStart, cacheEnd);
        }

        /// <summary>
        /// 获取指定采样帧范围的 PCM 数据
        /// 如果数据尚未缓存，返回 null
        /// </summary>
        public float[] GetSamples(long startSample, int sampleCount, int channel)
        {
            if (_audioInfo == null || sampleCount <= 0)
            {
                return null;
            }

            long endSample = startSample + sampleCount;
            int bytesPerFrame = _audioInfo.BytesPerFrame;
            int channels = _audioInfo.Channels;

            if (channel < 0 || channel >= channels)
            {
                return null;
            }

            var result = new float[sampleCount];
            bool hasData = false;

            lock (_lock)
            {
                foreach (var block in _cachedBlocks)
                {
                    if (!block.Overlaps(startSample, endSample))
                    {
                        continue;
                    }

                    // 计算重叠区域
                    long overlapStart = Math.Max(startSample, block.StartSample);
                    long overlapEnd = Math.Min(endSample, block.EndSample);

                    for (long s = overlapStart; s < overlapEnd; s++)
                    {
                        int blockByteOffset = block.GetByteOffset(s, bytesPerFrame);
                        if (blockByteOffset < 0)
                        {
                            continue;
                        }

                        int channelByteOffset = blockByteOffset + channel * 2; // 16-bit = 2 bytes
                        if (channelByteOffset + 1 < block.Data.Length)
                        {
                            short sample = (short)(block.Data[channelByteOffset]
                                | (block.Data[channelByteOffset + 1] << 8));
                            int resultIndex = (int)(s - startSample);
                            if (resultIndex >= 0 && resultIndex < result.Length)
                            {
                                result[resultIndex] = sample / 32768f;
                                hasData = true;
                            }
                        }
                    }
                }
            }

            return hasData ? result : null;
        }

        /// <summary>
        /// 通过 FFmpeg 解码指定范围的音频为 PCM 数据块
        /// 使用 -ss 精确 seek + -t 限制时长
        /// </summary>
        public PcmBlock DecodeBlock(long startSample, int sampleCount)
        {
            if (_audioInfo == null)
            {
                return null;
            }

            double startTime = _audioInfo.SamplesToSeconds(startSample);
            double duration = _audioInfo.SamplesToSeconds(sampleCount);
            int sampleRate = _audioInfo.SampleRate;
            int channels = _audioInfo.Channels;

            // 构建 FFmpeg 命令：解码音频为 16-bit PCM raw 数据
            string arguments = string.Format(
                "-ss {0} -t {1} -i \"{2}\" -f s16le -c:a pcm_s16le -ar {3} -ac {4} -",
                startTime.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                duration.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                _audioInfo.FilePath,
                sampleRate,
                channels);

            byte[] pcmData = RunFFmpegForPcm(arguments);
            if (pcmData == null || pcmData.Length == 0)
            {
                return null;
            }

            int bytesPerFrame = channels * 2; // 16-bit
            int actualSamples = pcmData.Length / bytesPerFrame;

            return new PcmBlock
            {
                StartSample = startSample,
                SampleCount = actualSamples,
                Data = pcmData
            };
        }

        /// <summary>
        /// 构建 FFmpeg 解码命令参数（供外部测试使用）
        /// </summary>
        public static string BuildDecodeArguments(string filePath, double startTime, double duration,
            int sampleRate, int channels)
        {
            return string.Format(
                "-ss {0} -t {1} -i \"{2}\" -f s16le -c:a pcm_s16le -ar {3} -ac {4} -",
                startTime.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                duration.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                filePath,
                sampleRate,
                channels);
        }

        /// <summary>
        /// 释放超出指定范围的缓存块
        /// </summary>
        private void EvictOutOfRange(long cacheStart, long cacheEnd)
        {
            lock (_lock)
            {
                _cachedBlocks.RemoveAll(block =>
                    block.EndSample <= cacheStart || block.StartSample >= cacheEnd);
            }
        }

        /// <summary>
        /// 释放超出指定范围的缓存块（公开方法，供测试使用）
        /// </summary>
        public void Evict(long cacheStart, long cacheEnd)
        {
            EvictOutOfRange(cacheStart, cacheEnd);
        }

        /// <summary>
        /// 异步加载缺失的数据块
        /// </summary>
        private void LoadMissingBlocksAsync(long cacheStart, long cacheEnd)
        {
            // 取消之前的加载操作
            lock (_loadLock)
            {
                if (_loadCts != null)
                {
                    _loadCts.Cancel();
                }
                _loadCts = new CancellationTokenSource();
            }

            var cts = _loadCts;
            Task.Run(() =>
            {
                try
                {
                    LoadMissingBlocks(cacheStart, cacheEnd, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 取消属于正常流程
                }
                catch (Exception)
                {
                    // 加载失败静默忽略
                }
            });
        }

        /// <summary>
        /// 同步加载缺失的数据块
        /// </summary>
        private void LoadMissingBlocks(long cacheStart, long cacheEnd, CancellationToken cancellationToken)
        {
            // 计算需要加载的块范围
            long blockStart = (cacheStart / _blockSampleCount) * _blockSampleCount;

            for (long pos = blockStart; pos < cacheEnd; pos += _blockSampleCount)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // 检查是否已缓存
                bool alreadyCached = false;
                lock (_lock)
                {
                    foreach (var block in _cachedBlocks)
                    {
                        if (block.StartSample == pos)
                        {
                            alreadyCached = true;
                            break;
                        }
                    }
                }

                if (alreadyCached)
                {
                    continue;
                }

                // 计算实际采样数（最后一块可能不满）
                int actualCount = _blockSampleCount;
                if (_audioInfo != null && pos + actualCount > _audioInfo.TotalSamples)
                {
                    actualCount = (int)(_audioInfo.TotalSamples - pos);
                }

                if (actualCount <= 0)
                {
                    break;
                }

                // 解码
                var newBlock = DecodeBlock(pos, actualCount);
                if (newBlock != null)
                {
                    lock (_lock)
                    {
                        // 插入到正确位置（保持排序）
                        int insertIndex = 0;
                        for (int i = 0; i < _cachedBlocks.Count; i++)
                        {
                            if (_cachedBlocks[i].StartSample > pos)
                            {
                                break;
                            }
                            insertIndex = i + 1;
                        }
                        _cachedBlocks.Insert(insertIndex, newBlock);
                    }
                }
            }
        }

        /// <summary>
        /// 直接添加块（仅供测试使用）
        /// </summary>
        public void AddBlockForTest(PcmBlock block)
        {
            lock (_lock)
            {
                _cachedBlocks.Add(block);
            }
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearCache()
        {
            lock (_lock)
            {
                _cachedBlocks.Clear();
            }
        }

        /// <summary>
        /// 运行 FFmpeg 进程并收集 stdout 的二进制输出
        /// </summary>
        private byte[] RunFFmpegForPcm(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                _jobObject.AssignProcess(process.Handle);

                // 读取二进制 PCM 输出
                byte[] result;
                using (var ms = new MemoryStream())
                {
                    var stdout = process.StandardOutput.BaseStream;
                    var buffer = new byte[8192];
                    int read;
                    while ((read = stdout.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                    }
                    result = ms.ToArray();
                }

                // 等待进程退出
                if (!process.WaitForExit(30000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception) { }
                }

                return result;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            lock (_loadLock)
            {
                if (_loadCts != null)
                {
                    _loadCts.Cancel();
                    _loadCts = null;
                }
            }
            ClearCache();
            if (_jobObject != null)
            {
                _jobObject.Dispose();
            }
        }
    }
}
