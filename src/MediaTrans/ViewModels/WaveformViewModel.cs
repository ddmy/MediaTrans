using System;
using System.Windows.Input;
using MediaTrans.Commands;
using MediaTrans.Services;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 波形编辑器 ViewModel — 管理缩放、平移、视口状态
    /// </summary>
    public class WaveformViewModel : ViewModelBase
    {
        // 缩放参数
        private double _samplesPerPixel;       // 每像素对应的采样帧数
        private double _minSamplesPerPixel;    // 最大缩放（采样级别）
        private double _maxSamplesPerPixel;    // 最小缩放（全局概览）
        private double _zoomFactor;            // 每次缩放的倍率

        // 视口参数
        private long _viewportStartSample;     // 视口起始采样帧
        private int _viewportWidthPixels;      // 视口像素宽度
        private long _totalSamples;            // 音频总采样帧数
        private int _sampleRate;               // 采样率

        // 平移状态
        private bool _isPanning;
        private double _panStartMouseX;
        private long _panStartViewportSample;

        // 缩放级别文本
        private string _zoomLevelText;

        /// <summary>
        /// 当前缩放级别（每像素采样帧数），值越小放大越多
        /// </summary>
        public double SamplesPerPixel
        {
            get { return _samplesPerPixel; }
            private set
            {
                double clamped = Math.Max(_minSamplesPerPixel, Math.Min(_maxSamplesPerPixel, value));
                if (SetProperty(ref _samplesPerPixel, clamped, "SamplesPerPixel"))
                {
                    UpdateZoomLevelText();
                    OnPropertyChanged("ViewportDurationSeconds");
                    OnPropertyChanged("ViewportEndSample");
                }
            }
        }

        /// <summary>
        /// 视口起始采样帧
        /// </summary>
        public long ViewportStartSample
        {
            get { return _viewportStartSample; }
            private set
            {
                long maxStart = Math.Max(0, _totalSamples - ViewportSampleSpan);
                long clamped = Math.Max(0, Math.Min(maxStart, value));
                if (SetProperty(ref _viewportStartSample, clamped, "ViewportStartSample"))
                {
                    OnPropertyChanged("ViewportEndSample");
                    OnPropertyChanged("ViewportStartTimeText");
                }
            }
        }

        /// <summary>
        /// 视口结束采样帧
        /// </summary>
        public long ViewportEndSample
        {
            get
            {
                long end = _viewportStartSample + ViewportSampleSpan;
                return Math.Min(end, _totalSamples);
            }
        }

        /// <summary>
        /// 视口覆盖的采样帧数
        /// </summary>
        public long ViewportSampleSpan
        {
            get { return (long)(_viewportWidthPixels * _samplesPerPixel); }
        }

        /// <summary>
        /// 视口像素宽度
        /// </summary>
        public int ViewportWidthPixels
        {
            get { return _viewportWidthPixels; }
            set
            {
                if (SetProperty(ref _viewportWidthPixels, Math.Max(1, value), "ViewportWidthPixels"))
                {
                    OnPropertyChanged("ViewportSampleSpan");
                    OnPropertyChanged("ViewportEndSample");
                    OnPropertyChanged("ViewportDurationSeconds");
                    RecalculateMaxSamplesPerPixel();
                }
            }
        }

        /// <summary>
        /// 音频总采样帧数
        /// </summary>
        public long TotalSamples
        {
            get { return _totalSamples; }
        }

        /// <summary>
        /// 缩放级别显示文本
        /// </summary>
        public string ZoomLevelText
        {
            get { return _zoomLevelText; }
            private set { SetProperty(ref _zoomLevelText, value, "ZoomLevelText"); }
        }

        /// <summary>
        /// 视口时长（秒）
        /// </summary>
        public double ViewportDurationSeconds
        {
            get
            {
                if (_sampleRate <= 0) return 0;
                return ViewportSampleSpan / (double)_sampleRate;
            }
        }

        /// <summary>
        /// 视口起始时间文本
        /// </summary>
        public string ViewportStartTimeText
        {
            get { return FormatTime(SamplesToSeconds(_viewportStartSample)); }
        }

        /// <summary>
        /// 是否处于平移状态
        /// </summary>
        public bool IsPanning
        {
            get { return _isPanning; }
            private set { SetProperty(ref _isPanning, value, "IsPanning"); }
        }

        /// <summary>
        /// 缩放倍率
        /// </summary>
        public double ZoomFactor
        {
            get { return _zoomFactor; }
            set { _zoomFactor = value > 1 ? value : 1.2; }
        }

        /// <summary>
        /// 创建波形 ViewModel
        /// </summary>
        public WaveformViewModel()
        {
            _samplesPerPixel = 441;
            _minSamplesPerPixel = 1;
            _maxSamplesPerPixel = 44100; // 默认值，加载音频后会重算
            _zoomFactor = 1.3;
            _viewportWidthPixels = 800;
            _viewportStartSample = 0;
            _totalSamples = 0;
            _sampleRate = 44100;
            UpdateZoomLevelText();
        }

        /// <summary>
        /// 加载音频后初始化视口参数
        /// </summary>
        public void Initialize(long totalSamples, int sampleRate, int viewportWidthPixels)
        {
            _totalSamples = totalSamples;
            _sampleRate = sampleRate > 0 ? sampleRate : 44100;
            _viewportWidthPixels = viewportWidthPixels > 0 ? viewportWidthPixels : 800;
            _minSamplesPerPixel = 1; // 采样级别
            RecalculateMaxSamplesPerPixel();

            // 初始缩放：全局概览
            _samplesPerPixel = _maxSamplesPerPixel;
            _viewportStartSample = 0;

            OnPropertyChanged("SamplesPerPixel");
            OnPropertyChanged("ViewportStartSample");
            OnPropertyChanged("ViewportEndSample");
            OnPropertyChanged("ViewportWidthPixels");
            OnPropertyChanged("ViewportSampleSpan");
            OnPropertyChanged("ViewportDurationSeconds");
            OnPropertyChanged("TotalSamples");
            UpdateZoomLevelText();
        }

        /// <summary>
        /// 鼠标滚轮缩放 — 以鼠标位置为中心
        /// </summary>
        /// <param name="delta">滚轮增量（正=放大，负=缩小）</param>
        /// <param name="mouseXRatio">鼠标在视口中的 X 位置比例（0.0~1.0）</param>
        public void ZoomAtPosition(int delta, double mouseXRatio)
        {
            if (_totalSamples <= 0) return;

            mouseXRatio = Math.Max(0, Math.Min(1, mouseXRatio));

            // 鼠标指向的采样帧位置
            long mouseAtSample = _viewportStartSample + (long)(mouseXRatio * ViewportSampleSpan);

            // 计算新的缩放级别
            double newSpp;
            if (delta > 0)
            {
                // 放大 — 减小 SamplesPerPixel
                newSpp = _samplesPerPixel / _zoomFactor;
            }
            else
            {
                // 缩小 — 增大 SamplesPerPixel
                newSpp = _samplesPerPixel * _zoomFactor;
            }

            newSpp = Math.Max(_minSamplesPerPixel, Math.Min(_maxSamplesPerPixel, newSpp));

            // 调整视口起始位置，使鼠标指向的采样帧保持在同一像素位置
            long newViewportSpan = (long)(_viewportWidthPixels * newSpp);
            long newStart = mouseAtSample - (long)(mouseXRatio * newViewportSpan);

            SamplesPerPixel = newSpp;
            ViewportStartSample = newStart;
        }

        /// <summary>
        /// 放大（居中缩放）
        /// </summary>
        public void ZoomIn()
        {
            ZoomAtPosition(1, 0.5);
        }

        /// <summary>
        /// 缩小（居中缩放）
        /// </summary>
        public void ZoomOut()
        {
            ZoomAtPosition(-1, 0.5);
        }

        /// <summary>
        /// 缩放到适合全局
        /// </summary>
        public void ZoomToFit()
        {
            if (_totalSamples <= 0 || _viewportWidthPixels <= 0) return;
            SamplesPerPixel = _maxSamplesPerPixel;
            ViewportStartSample = 0;
        }

        /// <summary>
        /// 开始平移（右键按下）
        /// </summary>
        public void StartPan(double mouseX)
        {
            _isPanning = true;
            _panStartMouseX = mouseX;
            _panStartViewportSample = _viewportStartSample;
            IsPanning = true;
        }

        /// <summary>
        /// 平移中（右键拖拽）
        /// </summary>
        public void UpdatePan(double mouseX)
        {
            if (!_isPanning) return;

            double deltaPixels = _panStartMouseX - mouseX;
            long deltaSamples = (long)(deltaPixels * _samplesPerPixel);
            ViewportStartSample = _panStartViewportSample + deltaSamples;
        }

        /// <summary>
        /// 结束平移（右键释放）
        /// </summary>
        public void EndPan()
        {
            _isPanning = false;
            IsPanning = false;
        }

        /// <summary>
        /// 滚动到指定位置
        /// </summary>
        public void ScrollTo(long startSample)
        {
            ViewportStartSample = startSample;
        }

        /// <summary>
        /// 像素坐标转采样帧位置
        /// </summary>
        public long PixelToSample(double pixelX)
        {
            return _viewportStartSample + (long)(pixelX * _samplesPerPixel);
        }

        /// <summary>
        /// 采样帧位置转像素坐标
        /// </summary>
        public double SampleToPixel(long sample)
        {
            if (_samplesPerPixel <= 0) return 0;
            return (sample - _viewportStartSample) / _samplesPerPixel;
        }

        /// <summary>
        /// 采样帧转秒
        /// </summary>
        public double SamplesToSeconds(long samples)
        {
            if (_sampleRate <= 0) return 0;
            return samples / (double)_sampleRate;
        }

        /// <summary>
        /// 秒转采样帧
        /// </summary>
        public long SecondsToSamples(double seconds)
        {
            return (long)(seconds * _sampleRate);
        }

        /// <summary>
        /// 格式化时间为 HH:MM:SS.ms
        /// </summary>
        public static string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int totalSeconds = (int)seconds;
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int secs = totalSeconds % 60;
            int ms = (int)((seconds - totalSeconds) * 1000);
            return string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}", hours, minutes, secs, ms);
        }

        /// <summary>
        /// 重新计算最大缩放级别（全局概览）
        /// </summary>
        private void RecalculateMaxSamplesPerPixel()
        {
            if (_viewportWidthPixels > 0 && _totalSamples > 0)
            {
                _maxSamplesPerPixel = (double)_totalSamples / _viewportWidthPixels;
                if (_maxSamplesPerPixel < 1) _maxSamplesPerPixel = 1;
            }
        }

        /// <summary>
        /// 更新缩放级别显示文本
        /// </summary>
        private void UpdateZoomLevelText()
        {
            if (_maxSamplesPerPixel <= 0)
            {
                ZoomLevelText = "100%";
                return;
            }

            double ratio = _maxSamplesPerPixel / _samplesPerPixel;
            int percent = (int)(ratio * 100);
            ZoomLevelText = string.Format("{0}%", percent);
        }
    }
}
