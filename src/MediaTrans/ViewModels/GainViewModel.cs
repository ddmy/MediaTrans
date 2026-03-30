using System;
using System.Windows.Input;
using MediaTrans.Commands;
using MediaTrans.Services;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 音量增益 ViewModel
    /// 管理 -20dB ~ +20dB 增益滑块，通知波形重渲染和播放音量
    /// </summary>
    public class GainViewModel : ViewModelBase
    {
        private readonly WaveformRenderService _renderService;
        private double _gainDb;
        private string _gainText;

        /// <summary>
        /// 增益变化事件 — 用于通知外部组件（如波形视图）刷新
        /// </summary>
        public event EventHandler GainChanged;

        /// <summary>
        /// 当前增益值（dB），范围 -20 ~ +20，步进 0.5
        /// </summary>
        public double GainDb
        {
            get { return _gainDb; }
            set
            {
                double clamped = GainService.ClampGainDb(value);
                double snapped = GainService.SnapToStep(clamped);
                if (SetProperty(ref _gainDb, snapped, "GainDb"))
                {
                    GainText = GainService.FormatGainText(_gainDb);
                    OnPropertyChanged("GainLinear");
                    OnPropertyChanged("PlaybackVolume");

                    // 清除波形缓存以触发重渲染
                    if (_renderService != null)
                    {
                        _renderService.ClearCache();
                    }

                    // 触发增益变化事件
                    EventHandler handler = GainChanged;
                    if (handler != null)
                    {
                        handler(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// 线性增益系数（只读）
        /// </summary>
        public double GainLinear
        {
            get { return GainService.DbToLinear(_gainDb); }
        }

        /// <summary>
        /// 播放音量（0.0~1.0 范围，将 dB 映射到线性音量）
        /// 用于绑定到 PlaybackViewModel.Volume
        /// </summary>
        public float PlaybackVolume
        {
            get
            {
                double linear = GainService.DbToLinear(_gainDb);
                // 钳位到 0~1 范围用于播放音量
                if (linear > 1.0) return 1.0f;
                if (linear < 0.0) return 0.0f;
                return (float)linear;
            }
        }

        /// <summary>
        /// 增益显示文本（如 "+6.0 dB"）
        /// </summary>
        public string GainText
        {
            get { return _gainText; }
            private set { SetProperty(ref _gainText, value, "GainText"); }
        }

        /// <summary>
        /// 增益增加命令（+0.5dB）
        /// </summary>
        public ICommand IncreaseGainCommand { get; private set; }

        /// <summary>
        /// 增益减少命令（-0.5dB）
        /// </summary>
        public ICommand DecreaseGainCommand { get; private set; }

        /// <summary>
        /// 重置增益命令（归零）
        /// </summary>
        public ICommand ResetGainCommand { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="renderService">波形渲染服务（可为 null）</param>
        public GainViewModel(WaveformRenderService renderService)
        {
            _renderService = renderService;
            _gainDb = 0.0;
            _gainText = GainService.FormatGainText(0.0);

            IncreaseGainCommand = new RelayCommand(
                new Action<object>(ExecuteIncreaseGain),
                new Func<object, bool>(o => _gainDb < GainService.MaxGainDb));

            DecreaseGainCommand = new RelayCommand(
                new Action<object>(ExecuteDecreaseGain),
                new Func<object, bool>(o => _gainDb > GainService.MinGainDb));

            ResetGainCommand = new RelayCommand(
                new Action<object>(ExecuteResetGain),
                new Func<object, bool>(o => Math.Abs(_gainDb) > 0.001));
        }

        /// <summary>
        /// 无参构造（无波形渲染集成）
        /// </summary>
        public GainViewModel() : this(null)
        {
        }

        /// <summary>
        /// 增加增益 +0.5dB
        /// </summary>
        private void ExecuteIncreaseGain(object parameter)
        {
            GainDb = _gainDb + GainService.GainStepDb;
        }

        /// <summary>
        /// 减少增益 -0.5dB
        /// </summary>
        private void ExecuteDecreaseGain(object parameter)
        {
            GainDb = _gainDb - GainService.GainStepDb;
        }

        /// <summary>
        /// 重置增益为 0dB
        /// </summary>
        private void ExecuteResetGain(object parameter)
        {
            GainDb = 0.0;
        }

        /// <summary>
        /// 获取当前增益应用后的 PCM 浮点采样数据
        /// </summary>
        /// <param name="samples">原始浮点采样数据</param>
        /// <returns>增益后的采样数据（如果 0dB 则返回原数组引用）</returns>
        public float[] ApplyGainToSamples(float[] samples)
        {
            if (samples == null)
            {
                return null;
            }

            if (Math.Abs(_gainDb) < 0.001)
            {
                return samples; // 0dB 无变化
            }

            return GainService.ApplyGainToFloat(samples, _gainDb);
        }
    }
}
