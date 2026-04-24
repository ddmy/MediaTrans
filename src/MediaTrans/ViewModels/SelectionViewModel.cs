using System;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 选区裁剪 ViewModel — 管理左右 Handle 拖拽选区、选区时长显示、精确微调
    /// </summary>
    public class SelectionViewModel : ViewModelBase
    {
        private readonly WaveformViewModel _waveformVm;

        // 选区范围（采样帧）
        private long _selectionStartSample;
        private long _selectionEndSample;
        private bool _hasSelection;

        // 拖拽状态
        private bool _isDraggingLeftHandle;
        private bool _isDraggingRightHandle;

        // 选区时长显示
        private string _selectionDurationText;
        private string _selectionStartTimeText;
        private string _selectionEndTimeText;

        /// <summary>
        /// 选区起始采样帧
        /// </summary>
        public long SelectionStartSample
        {
            get { return _selectionStartSample; }
            set
            {
                long clamped = Math.Max(0, Math.Min(_waveformVm.TotalSamples, value));
                if (SetProperty(ref _selectionStartSample, clamped, "SelectionStartSample"))
                {
                    EnsureOrder();
                    UpdateDisplayTexts();
                    OnPropertyChanged("SelectionStartPixelX");
                    OnPropertyChanged("SelectionWidthPixels");
                    OnPropertyChanged("SelectionDurationSamples");
                }
            }
        }

        /// <summary>
        /// 选区结束采样帧
        /// </summary>
        public long SelectionEndSample
        {
            get { return _selectionEndSample; }
            set
            {
                long clamped = Math.Max(0, Math.Min(_waveformVm.TotalSamples, value));
                if (SetProperty(ref _selectionEndSample, clamped, "SelectionEndSample"))
                {
                    EnsureOrder();
                    UpdateDisplayTexts();
                    OnPropertyChanged("SelectionEndPixelX");
                    OnPropertyChanged("SelectionWidthPixels");
                    OnPropertyChanged("SelectionDurationSamples");
                }
            }
        }

        /// <summary>
        /// 是否有选区
        /// </summary>
        public bool HasSelection
        {
            get { return _hasSelection; }
            private set { SetProperty(ref _hasSelection, value, "HasSelection"); }
        }

        /// <summary>
        /// 选区时长（采样帧数）
        /// </summary>
        public long SelectionDurationSamples
        {
            get { return _selectionEndSample - _selectionStartSample; }
        }

        /// <summary>
        /// 选区时长（秒）
        /// </summary>
        public double SelectionDurationSeconds
        {
            get { return _waveformVm.SamplesToSeconds(SelectionDurationSamples); }
        }

        /// <summary>
        /// 选区时长文本
        /// </summary>
        public string SelectionDurationText
        {
            get { return _selectionDurationText; }
            private set { SetProperty(ref _selectionDurationText, value, "SelectionDurationText"); }
        }

        /// <summary>
        /// 选区起始时间文本
        /// </summary>
        public string SelectionStartTimeText
        {
            get { return _selectionStartTimeText; }
            private set { SetProperty(ref _selectionStartTimeText, value, "SelectionStartTimeText"); }
        }

        /// <summary>
        /// 选区结束时间文本
        /// </summary>
        public string SelectionEndTimeText
        {
            get { return _selectionEndTimeText; }
            private set { SetProperty(ref _selectionEndTimeText, value, "SelectionEndTimeText"); }
        }

        /// <summary>
        /// 选区起始像素 X（相对于当前视口）
        /// </summary>
        public double SelectionStartPixelX
        {
            get { return _waveformVm.SampleToPixel(_selectionStartSample); }
        }

        /// <summary>
        /// 选区结束像素 X（相对于当前视口）
        /// </summary>
        public double SelectionEndPixelX
        {
            get { return _waveformVm.SampleToPixel(_selectionEndSample); }
        }

        /// <summary>
        /// 选区像素宽度（当前视口下）
        /// </summary>
        public double SelectionWidthPixels
        {
            get { return SelectionEndPixelX - SelectionStartPixelX; }
        }

        /// <summary>
        /// 是否正在拖拽左 Handle
        /// </summary>
        public bool IsDraggingLeftHandle
        {
            get { return _isDraggingLeftHandle; }
            private set { SetProperty(ref _isDraggingLeftHandle, value, "IsDraggingLeftHandle"); }
        }

        /// <summary>
        /// 是否正在拖拽右 Handle
        /// </summary>
        public bool IsDraggingRightHandle
        {
            get { return _isDraggingRightHandle; }
            private set { SetProperty(ref _isDraggingRightHandle, value, "IsDraggingRightHandle"); }
        }

        /// <summary>
        /// 是否正在拖拽任一 Handle
        /// </summary>
        public bool IsDragging
        {
            get { return _isDraggingLeftHandle || _isDraggingRightHandle; }
        }

        /// <summary>
        /// 波形 ViewModel 引用
        /// </summary>
        public WaveformViewModel WaveformVm
        {
            get { return _waveformVm; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SelectionViewModel(WaveformViewModel waveformVm)
        {
            if (waveformVm == null) throw new ArgumentNullException("waveformVm");
            _waveformVm = waveformVm;
            _selectionStartSample = 0;
            _selectionEndSample = 0;
            _hasSelection = false;
            _selectionDurationText = "";
            _selectionStartTimeText = "";
            _selectionEndTimeText = "";
        }

        /// <summary>
        /// 开始拖拽左 Handle
        /// </summary>
        public void StartDragLeftHandle()
        {
            IsDraggingLeftHandle = true;
        }

        /// <summary>
        /// 拖拽左 Handle 中
        /// </summary>
        /// <param name="pixelX">当前鼠标像素 X</param>
        public void UpdateDragLeftHandle(double pixelX)
        {
            if (!_isDraggingLeftHandle) return;
            long sample = _waveformVm.PixelToSample(pixelX);
            SelectionStartSample = sample;
            HasSelection = _selectionEndSample > _selectionStartSample;
        }

        /// <summary>
        /// 结束拖拽左 Handle
        /// </summary>
        public void EndDragLeftHandle()
        {
            IsDraggingLeftHandle = false;
        }

        /// <summary>
        /// 开始拖拽右 Handle
        /// </summary>
        public void StartDragRightHandle()
        {
            IsDraggingRightHandle = true;
        }

        /// <summary>
        /// 拖拽右 Handle 中
        /// </summary>
        /// <param name="pixelX">当前鼠标像素 X</param>
        public void UpdateDragRightHandle(double pixelX)
        {
            if (!_isDraggingRightHandle) return;
            long sample = _waveformVm.PixelToSample(pixelX);
            SelectionEndSample = sample;
            HasSelection = _selectionEndSample > _selectionStartSample;
        }

        /// <summary>
        /// 结束拖拽右 Handle
        /// </summary>
        public void EndDragRightHandle()
        {
            IsDraggingRightHandle = false;
        }

        /// <summary>
        /// 通过像素范围创建选区（如鼠标框选）
        /// </summary>
        public void CreateSelection(double startPixelX, double endPixelX)
        {
            long startSample = _waveformVm.PixelToSample(startPixelX);
            long endSample = _waveformVm.PixelToSample(endPixelX);

            if (startSample > endSample)
            {
                long temp = startSample;
                startSample = endSample;
                endSample = temp;
            }

            _selectionStartSample = Math.Max(0, startSample);
            _selectionEndSample = Math.Min(_waveformVm.TotalSamples, endSample);
            HasSelection = _selectionEndSample > _selectionStartSample;

            OnPropertyChanged("SelectionStartSample");
            OnPropertyChanged("SelectionEndSample");
            OnPropertyChanged("SelectionStartPixelX");
            OnPropertyChanged("SelectionEndPixelX");
            OnPropertyChanged("SelectionWidthPixels");
            OnPropertyChanged("SelectionDurationSamples");
            UpdateDisplayTexts();
        }

        /// <summary>
        /// 通过精确时间（秒）设置选区起始
        /// </summary>
        public void SetSelectionStartTime(double seconds)
        {
            SelectionStartSample = _waveformVm.SecondsToSamples(seconds);
            HasSelection = _selectionEndSample > _selectionStartSample;
        }

        /// <summary>
        /// 通过精确时间（秒）设置选区结束
        /// </summary>
        public void SetSelectionEndTime(double seconds)
        {
            SelectionEndSample = _waveformVm.SecondsToSamples(seconds);
            HasSelection = _selectionEndSample > _selectionStartSample;
        }

        /// <summary>
        /// 清除选区
        /// </summary>
        public void ClearSelection()
        {
            _selectionStartSample = 0;
            _selectionEndSample = 0;
            HasSelection = false;

            OnPropertyChanged("SelectionStartSample");
            OnPropertyChanged("SelectionEndSample");
            OnPropertyChanged("SelectionStartPixelX");
            OnPropertyChanged("SelectionEndPixelX");
            OnPropertyChanged("SelectionWidthPixels");
            OnPropertyChanged("SelectionDurationSamples");
            UpdateDisplayTexts();
        }

        /// <summary>
        /// 选区全选
        /// </summary>
        public void SelectAll()
        {
            _selectionStartSample = 0;
            _selectionEndSample = _waveformVm.TotalSamples;
            HasSelection = _selectionEndSample > 0;

            OnPropertyChanged("SelectionStartSample");
            OnPropertyChanged("SelectionEndSample");
            OnPropertyChanged("SelectionStartPixelX");
            OnPropertyChanged("SelectionEndPixelX");
            OnPropertyChanged("SelectionWidthPixels");
            OnPropertyChanged("SelectionDurationSamples");
            UpdateDisplayTexts();
        }

        /// <summary>
        /// 当波形视口缩放或平移后，刷新与像素位置相关的派生属性
        /// </summary>
        public void RefreshViewportState()
        {
            OnPropertyChanged("SelectionStartPixelX");
            OnPropertyChanged("SelectionEndPixelX");
            OnPropertyChanged("SelectionWidthPixels");
            OnPropertyChanged("SelectionDurationSamples");
            UpdateDisplayTexts();
        }

        /// <summary>
        /// 判断一个像素 X 坐标是否在左 Handle 附近（用于命中测试）
        /// </summary>
        /// <param name="pixelX">鼠标 X 坐标</param>
        /// <param name="hitRadius">命中半径（像素）</param>
        public bool HitTestLeftHandle(double pixelX, double hitRadius)
        {
            if (!_hasSelection) return false;
            return Math.Abs(pixelX - SelectionStartPixelX) <= hitRadius;
        }

        /// <summary>
        /// 判断一个像素 X 坐标是否在右 Handle 附近（用于命中测试）
        /// </summary>
        /// <param name="pixelX">鼠标 X 坐标</param>
        /// <param name="hitRadius">命中半径（像素）</param>
        public bool HitTestRightHandle(double pixelX, double hitRadius)
        {
            if (!_hasSelection) return false;
            return Math.Abs(pixelX - SelectionEndPixelX) <= hitRadius;
        }

        /// <summary>
        /// 确保 start <= end
        /// </summary>
        private void EnsureOrder()
        {
            if (_selectionStartSample > _selectionEndSample)
            {
                long temp = _selectionStartSample;
                _selectionStartSample = _selectionEndSample;
                _selectionEndSample = temp;
                OnPropertyChanged("SelectionStartSample");
                OnPropertyChanged("SelectionEndSample");
                OnPropertyChanged("SelectionStartPixelX");
                OnPropertyChanged("SelectionEndPixelX");
            }
        }

        /// <summary>
        /// 更新显示文本
        /// </summary>
        private void UpdateDisplayTexts()
        {
            double startSec = _waveformVm.SamplesToSeconds(_selectionStartSample);
            double endSec = _waveformVm.SamplesToSeconds(_selectionEndSample);
            double durSec = endSec - startSec;

            SelectionStartTimeText = WaveformViewModel.FormatTime(startSec);
            SelectionEndTimeText = WaveformViewModel.FormatTime(endSec);
            SelectionDurationText = WaveformViewModel.FormatTime(durSec);
        }
    }
}
