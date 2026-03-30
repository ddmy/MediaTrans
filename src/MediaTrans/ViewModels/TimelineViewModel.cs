using System;
using MediaTrans.Services;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 时间轴 ViewModel — 管理播放头定位、刻度尺计算、时间码显示
    /// </summary>
    public class TimelineViewModel : ViewModelBase
    {
        private readonly TimelineRulerService _rulerService;
        private readonly WaveformViewModel _waveformVm;

        // 播放头
        private long _playheadSample;        // 播放头位置（采样帧）
        private bool _isDraggingPlayhead;     // 是否正在拖动播放头

        // 时间码显示
        private string _playheadTimeText;     // 播放头时间码

        /// <summary>
        /// 播放头位置（采样帧）
        /// </summary>
        public long PlayheadSample
        {
            get { return _playheadSample; }
            set
            {
                long clamped = Math.Max(0, Math.Min(_waveformVm.TotalSamples, value));
                if (SetProperty(ref _playheadSample, clamped, "PlayheadSample"))
                {
                    UpdatePlayheadTimeText();
                    OnPropertyChanged("PlayheadPixelX");
                }
            }
        }

        /// <summary>
        /// 播放头在当前视口中的像素 X 位置
        /// </summary>
        public double PlayheadPixelX
        {
            get { return _waveformVm.SampleToPixel(_playheadSample); }
        }

        /// <summary>
        /// 播放头时间码（HH:MM:SS.ms）
        /// </summary>
        public string PlayheadTimeText
        {
            get { return _playheadTimeText; }
            private set { SetProperty(ref _playheadTimeText, value, "PlayheadTimeText"); }
        }

        /// <summary>
        /// 是否正在拖动播放头
        /// </summary>
        public bool IsDraggingPlayhead
        {
            get { return _isDraggingPlayhead; }
            private set { SetProperty(ref _isDraggingPlayhead, value, "IsDraggingPlayhead"); }
        }

        /// <summary>
        /// 波形 ViewModel 引用（只读）
        /// </summary>
        public WaveformViewModel WaveformVm
        {
            get { return _waveformVm; }
        }

        /// <summary>
        /// 刻度尺服务引用（只读）
        /// </summary>
        public TimelineRulerService RulerService
        {
            get { return _rulerService; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="waveformVm">波形 ViewModel</param>
        public TimelineViewModel(WaveformViewModel waveformVm)
            : this(waveformVm, new TimelineRulerService())
        {
        }

        /// <summary>
        /// 构造函数（可注入刻度尺服务）
        /// </summary>
        /// <param name="waveformVm">波形 ViewModel</param>
        /// <param name="rulerService">刻度尺服务</param>
        public TimelineViewModel(WaveformViewModel waveformVm, TimelineRulerService rulerService)
        {
            if (waveformVm == null) throw new ArgumentNullException("waveformVm");
            if (rulerService == null) throw new ArgumentNullException("rulerService");

            _waveformVm = waveformVm;
            _rulerService = rulerService;
            _playheadSample = 0;
            _playheadTimeText = WaveformViewModel.FormatTime(0);
        }

        /// <summary>
        /// 点击时间轴定位播放头
        /// </summary>
        /// <param name="pixelX">点击位置的像素 X 坐标</param>
        public void ClickToPosition(double pixelX)
        {
            long sample = _waveformVm.PixelToSample(pixelX);
            PlayheadSample = sample;
        }

        /// <summary>
        /// 开始拖动播放头
        /// </summary>
        /// <param name="pixelX">鼠标按下位置的像素 X 坐标</param>
        public void StartDragPlayhead(double pixelX)
        {
            IsDraggingPlayhead = true;
            ClickToPosition(pixelX);
        }

        /// <summary>
        /// 拖动播放头中
        /// </summary>
        /// <param name="pixelX">当前鼠标位置的像素 X 坐标</param>
        public void UpdateDragPlayhead(double pixelX)
        {
            if (!_isDraggingPlayhead) return;
            long sample = _waveformVm.PixelToSample(pixelX);
            PlayheadSample = sample;
        }

        /// <summary>
        /// 结束拖动播放头
        /// </summary>
        public void EndDragPlayhead()
        {
            IsDraggingPlayhead = false;
        }

        /// <summary>
        /// 获取当前视口内的刻度标记列表
        /// </summary>
        /// <returns>刻度标记列表</returns>
        public System.Collections.Generic.List<TickMark> GetVisibleTickMarks()
        {
            return _rulerService.CalculateTickMarks(
                _waveformVm.ViewportStartSample,
                _waveformVm.ViewportWidthPixels,
                _waveformVm.SamplesPerPixel,
                GetSampleRate());
        }

        /// <summary>
        /// 获取当前主刻度间隔（秒）
        /// </summary>
        public double GetCurrentMajorInterval()
        {
            return _rulerService.CalculateMajorInterval(
                _waveformVm.SamplesPerPixel,
                GetSampleRate());
        }

        /// <summary>
        /// 播放头时间（秒）
        /// </summary>
        public double PlayheadTimeSeconds
        {
            get { return _waveformVm.SamplesToSeconds(_playheadSample); }
        }

        /// <summary>
        /// 设置播放头时间（秒）
        /// </summary>
        public void SetPlayheadTime(double seconds)
        {
            PlayheadSample = _waveformVm.SecondsToSamples(seconds);
        }

        /// <summary>
        /// 播放头是否在当前可见区域内
        /// </summary>
        public bool IsPlayheadVisible
        {
            get
            {
                return _playheadSample >= _waveformVm.ViewportStartSample
                    && _playheadSample <= _waveformVm.ViewportEndSample;
            }
        }

        /// <summary>
        /// 滚动视口使播放头居中可见
        /// </summary>
        public void ScrollToPlayhead()
        {
            long centerStart = _playheadSample - _waveformVm.ViewportSampleSpan / 2;
            _waveformVm.ScrollTo(centerStart);
        }

        /// <summary>
        /// 更新播放头时间码文本
        /// </summary>
        private void UpdatePlayheadTimeText()
        {
            double seconds = _waveformVm.SamplesToSeconds(_playheadSample);
            PlayheadTimeText = WaveformViewModel.FormatTime(seconds);
        }

        /// <summary>
        /// 获取采样率（通过反射或公开属性）
        /// </summary>
        private int GetSampleRate()
        {
            // 通过 WaveformViewModel 的公开属性或内部字段获取采样率
            // 此处通过计算推导：如果 TotalSamples > 0 且 ViewportDurationSeconds > 0
            // sampleRate = TotalSamples / TotalDurationSeconds
            // 不过更好的办法是让 WaveformViewModel 公开 SampleRate 属性
            return _waveformVm.SampleRate;
        }
    }
}
