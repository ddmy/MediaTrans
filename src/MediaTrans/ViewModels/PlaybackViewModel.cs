using System;
using System.Windows.Input;
using MediaTrans.Commands;
using MediaTrans.Services;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 播放控制 ViewModel — 管理播放/暂停/停止、播放光标同步
    /// </summary>
    public class PlaybackViewModel : ViewModelBase, IDisposable
    {
        private readonly AudioPlaybackService _playbackService;
        private readonly TimelineViewModel _timelineVm;
        private readonly SelectionViewModel _selectionVm;

        private bool _isPlaying;
        private bool _isPaused;
        private float _volume;
        private string _playbackTimeText;
        private bool _disposed;

        // 定时器用于同步播放位置（在实际 WPF 中使用 DispatcherTimer）
        private System.Threading.Timer _positionTimer;

        /// <summary>
        /// 播放位置更新事件（用于外部 UI 同步）
        /// </summary>
        public event EventHandler PositionUpdated;

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying
        {
            get { return _isPlaying; }
            private set
            {
                if (SetProperty(ref _isPlaying, value, "IsPlaying"))
                {
                    OnPropertyChanged("CanPlay");
                    OnPropertyChanged("CanPause");
                    OnPropertyChanged("CanStop");
                }
            }
        }

        /// <summary>
        /// 是否已暂停
        /// </summary>
        public bool IsPaused
        {
            get { return _isPaused; }
            private set
            {
                if (SetProperty(ref _isPaused, value, "IsPaused"))
                {
                    OnPropertyChanged("CanPlay");
                    OnPropertyChanged("CanPause");
                }
            }
        }

        /// <summary>
        /// 音量 (0.0 ~ 1.0)
        /// </summary>
        public float Volume
        {
            get { return _volume; }
            set
            {
                float clamped = Math.Max(0f, Math.Min(1f, value));
                if (SetProperty(ref _volume, clamped, "Volume"))
                {
                    _playbackService.SetVolume(clamped);
                }
            }
        }

        /// <summary>
        /// 播放时间文本
        /// </summary>
        public string PlaybackTimeText
        {
            get { return _playbackTimeText; }
            private set { SetProperty(ref _playbackTimeText, value, "PlaybackTimeText"); }
        }

        /// <summary>
        /// 是否可以播放
        /// </summary>
        public bool CanPlay
        {
            get { return !_isPlaying || _isPaused; }
        }

        /// <summary>
        /// 是否可以暂停
        /// </summary>
        public bool CanPause
        {
            get { return _isPlaying && !_isPaused; }
        }

        /// <summary>
        /// 是否可以停止
        /// </summary>
        public bool CanStop
        {
            get { return _isPlaying || _isPaused; }
        }

        /// <summary>
        /// 播放服务引用
        /// </summary>
        public AudioPlaybackService PlaybackService
        {
            get { return _playbackService; }
        }

        /// <summary>
        /// 时间轴 ViewModel 引用
        /// </summary>
        public TimelineViewModel TimelineVm
        {
            get { return _timelineVm; }
        }

        /// <summary>
        /// 选区 ViewModel 引用
        /// </summary>
        public SelectionViewModel SelectionVm
        {
            get { return _selectionVm; }
        }

        /// <summary>
        /// 播放命令
        /// </summary>
        public ICommand PlayCommand { get; private set; }

        /// <summary>
        /// 暂停命令
        /// </summary>
        public ICommand PauseCommand { get; private set; }

        /// <summary>
        /// 停止命令
        /// </summary>
        public ICommand StopCommand { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public PlaybackViewModel(AudioPlaybackService playbackService,
            TimelineViewModel timelineVm, SelectionViewModel selectionVm)
        {
            if (playbackService == null) throw new ArgumentNullException("playbackService");
            if (timelineVm == null) throw new ArgumentNullException("timelineVm");
            if (selectionVm == null) throw new ArgumentNullException("selectionVm");

            _playbackService = playbackService;
            _timelineVm = timelineVm;
            _selectionVm = selectionVm;

            _volume = 1.0f;
            _isPlaying = false;
            _isPaused = false;
            _playbackTimeText = WaveformViewModel.FormatTime(0);

            PlayCommand = new RelayCommand(
                new Action<object>(ExecutePlay),
                new Func<object, bool>(o => CanPlay));
            PauseCommand = new RelayCommand(
                new Action<object>(ExecutePause),
                new Func<object, bool>(o => CanPause));
            StopCommand = new RelayCommand(
                new Action<object>(ExecuteStop),
                new Func<object, bool>(o => CanStop));

            _playbackService.PlaybackStopped += OnPlaybackStopped;
        }

        /// <summary>
        /// 执行播放
        /// </summary>
        public void ExecutePlay(object parameter)
        {
            _playbackService.Play();
            IsPlaying = true;
            IsPaused = false;
            StartPositionSync();
        }

        /// <summary>
        /// 执行暂停
        /// </summary>
        public void ExecutePause(object parameter)
        {
            _playbackService.Pause();
            IsPaused = true;
            StopPositionSync();
        }

        /// <summary>
        /// 执行停止
        /// </summary>
        public void ExecuteStop(object parameter)
        {
            _playbackService.Stop();
            IsPlaying = false;
            IsPaused = false;
            StopPositionSync();
            UpdatePlaybackPosition();
        }

        /// <summary>
        /// 开始播放位置同步定时器
        /// </summary>
        private void StartPositionSync()
        {
            StopPositionSync();
            // 每 30ms 更新一次播放位置（约 33fps）
            _positionTimer = new System.Threading.Timer(
                OnPositionTimerTick, null, 0, 30);
        }

        /// <summary>
        /// 停止播放位置同步定时器
        /// </summary>
        private void StopPositionSync()
        {
            if (_positionTimer != null)
            {
                _positionTimer.Dispose();
                _positionTimer = null;
            }
        }

        /// <summary>
        /// 定时器回调 — 更新播放位置
        /// </summary>
        private void OnPositionTimerTick(object state)
        {
            UpdatePlaybackPosition();
        }

        /// <summary>
        /// 更新播放位置到时间轴
        /// </summary>
        public void UpdatePlaybackPosition()
        {
            long currentSample = _playbackService.CurrentPositionSamples;
            _timelineVm.PlayheadSample = currentSample;

            double seconds = _playbackService.CurrentPositionSeconds;
            PlaybackTimeText = WaveformViewModel.FormatTime(seconds);

            EventHandler handler = PositionUpdated;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 播放停止回调
        /// </summary>
        private void OnPlaybackStopped(object sender, EventArgs e)
        {
            IsPlaying = false;
            IsPaused = false;
            StopPositionSync();
        }

        /// <summary>
        /// 从播放头位置开始播放
        /// </summary>
        public void PlayFromPlayhead()
        {
            _playbackService.SeekToSample(_timelineVm.PlayheadSample);
            ExecutePlay(null);
        }

        /// <summary>
        /// 播放选区
        /// </summary>
        public void PlaySelection()
        {
            if (!_selectionVm.HasSelection) return;
            _playbackService.SeekToSample(_selectionVm.SelectionStartSample);
            ExecutePlay(null);
        }

        /// <summary>
        /// 切换播放/暂停
        /// </summary>
        public void TogglePlayPause()
        {
            if (_isPlaying && !_isPaused)
            {
                ExecutePause(null);
            }
            else
            {
                ExecutePlay(null);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopPositionSync();
            _playbackService.PlaybackStopped -= OnPlaybackStopped;
        }
    }
}
