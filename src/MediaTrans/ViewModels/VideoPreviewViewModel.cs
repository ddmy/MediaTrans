using System;
using MediaTrans.Commands;
using MediaTrans.Services;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 视频帧预览 ViewModel
    /// 管理视频帧的实时预览、缓存和后台预取
    /// </summary>
    public class VideoPreviewViewModel : ViewModelBase, IDisposable
    {
        private readonly VideoFrameCacheService _frameCacheService;
        private readonly WaveformViewModel _waveformViewModel;

        // 后台预取帧数（前后各 5 帧）
        private const int DefaultPrefetchCount = 5;

        private byte[] _currentFrameData;
        private long _currentFrameIndex;
        private bool _isLoading;
        private bool _hasVideo;
        private string _frameInfoText;
        private bool _disposed;

        /// <summary>
        /// 创建视频帧预览 ViewModel
        /// </summary>
        /// <param name="frameCacheService">帧缓存服务</param>
        /// <param name="waveformViewModel">波形 ViewModel（提供时间信息）</param>
        public VideoPreviewViewModel(VideoFrameCacheService frameCacheService,
            WaveformViewModel waveformViewModel)
        {
            if (frameCacheService == null)
            {
                throw new ArgumentNullException("frameCacheService");
            }

            _frameCacheService = frameCacheService;
            _waveformViewModel = waveformViewModel;

            // 订阅帧就绪事件
            _frameCacheService.FrameReady += OnFrameReady;

            _frameInfoText = "";
        }

        /// <summary>
        /// 当前帧图像数据（BMP 字节数组）
        /// </summary>
        public byte[] CurrentFrameData
        {
            get { return _currentFrameData; }
            private set { SetProperty(ref _currentFrameData, value, "CurrentFrameData"); }
        }

        /// <summary>
        /// 当前帧索引
        /// </summary>
        public long CurrentFrameIndex
        {
            get { return _currentFrameIndex; }
            private set { SetProperty(ref _currentFrameIndex, value, "CurrentFrameIndex"); }
        }

        /// <summary>
        /// 是否正在加载帧
        /// </summary>
        public bool IsLoading
        {
            get { return _isLoading; }
            private set { SetProperty(ref _isLoading, value, "IsLoading"); }
        }

        /// <summary>
        /// 是否已加载视频
        /// </summary>
        public bool HasVideo
        {
            get { return _hasVideo; }
            private set { SetProperty(ref _hasVideo, value, "HasVideo"); }
        }

        /// <summary>
        /// 帧信息文本（如 "帧 120 / 3600 | 00:00:04.000"）
        /// </summary>
        public string FrameInfoText
        {
            get { return _frameInfoText; }
            private set { SetProperty(ref _frameInfoText, value, "FrameInfoText"); }
        }

        /// <summary>
        /// 帧缓存服务
        /// </summary>
        public VideoFrameCacheService FrameCacheService
        {
            get { return _frameCacheService; }
        }

        /// <summary>
        /// 加载视频
        /// </summary>
        /// <param name="videoFilePath">视频文件路径</param>
        /// <param name="frameRate">帧率</param>
        /// <param name="totalDurationSeconds">总时长（秒）</param>
        /// <param name="videoWidth">视频宽度</param>
        /// <param name="videoHeight">视频高度</param>
        public void LoadVideo(string videoFilePath, double frameRate, double totalDurationSeconds,
            int videoWidth, int videoHeight)
        {
            _frameCacheService.LoadVideo(videoFilePath, frameRate, totalDurationSeconds,
                videoWidth, videoHeight);
            HasVideo = true;
            CurrentFrameIndex = 0;
            CurrentFrameData = null;
            UpdateFrameInfoText();
        }

        /// <summary>
        /// 卸载视频
        /// </summary>
        public void UnloadVideo()
        {
            _frameCacheService.UnloadVideo();
            HasVideo = false;
            CurrentFrameIndex = 0;
            CurrentFrameData = null;
            FrameInfoText = "";
        }

        /// <summary>
        /// 跳转到指定帧（由播放头拖动触发）
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        public void SeekToFrame(long frameIndex)
        {
            if (!HasVideo)
            {
                return;
            }

            // 限定范围
            if (frameIndex < 0) frameIndex = 0;
            long total = _frameCacheService.TotalFrames;
            if (total > 0 && frameIndex >= total) frameIndex = total - 1;

            CurrentFrameIndex = frameIndex;
            UpdateFrameInfoText();

            // 尝试从缓存获取
            byte[] frameData = _frameCacheService.RequestFrame(frameIndex);
            if (frameData != null)
            {
                // 缓存命中，立即显示
                CurrentFrameData = frameData;
                IsLoading = false;
            }
            else
            {
                // 缓存未命中，等待异步提取
                IsLoading = true;
            }

            // 启动后台预取
            _frameCacheService.StartPrefetch(frameIndex, DefaultPrefetchCount);
        }

        /// <summary>
        /// 根据时间戳跳转帧
        /// </summary>
        /// <param name="timestampSeconds">时间戳（秒）</param>
        public void SeekToTimestamp(double timestampSeconds)
        {
            if (!HasVideo)
            {
                return;
            }

            long frameIndex = _frameCacheService.TimestampToFrame(timestampSeconds);
            SeekToFrame(frameIndex);
        }

        /// <summary>
        /// 根据采样帧位置跳转视频帧
        /// </summary>
        /// <param name="samplePosition">采样帧位置</param>
        /// <param name="sampleRate">采样率</param>
        public void SeekToSamplePosition(long samplePosition, int sampleRate)
        {
            if (!HasVideo || sampleRate <= 0)
            {
                return;
            }

            double timestampSeconds = (double)samplePosition / sampleRate;
            SeekToTimestamp(timestampSeconds);
        }

        /// <summary>
        /// 帧就绪事件处理
        /// </summary>
        private void OnFrameReady(object sender, FrameReadyEventArgs e)
        {
            // 只更新当前帧
            if (e.FrameIndex == _currentFrameIndex)
            {
                CurrentFrameData = e.FrameData;
                IsLoading = false;
            }
        }

        /// <summary>
        /// 更新帧信息文本
        /// </summary>
        private void UpdateFrameInfoText()
        {
            if (!HasVideo)
            {
                FrameInfoText = "";
                return;
            }

            double timestamp = _frameCacheService.FrameToTimestamp(_currentFrameIndex);
            int totalMs = (int)(timestamp * 1000);
            int hours = totalMs / 3600000;
            int minutes = (totalMs % 3600000) / 60000;
            int seconds = (totalMs % 60000) / 1000;
            int ms = totalMs % 1000;
            string timeStr = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}",
                hours, minutes, seconds, ms);

            FrameInfoText = string.Format("帧 {0} / {1} | {2}",
                _currentFrameIndex, _frameCacheService.TotalFrames, timeStr);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _frameCacheService.FrameReady -= OnFrameReady;
                _disposed = true;
            }
        }
    }
}
