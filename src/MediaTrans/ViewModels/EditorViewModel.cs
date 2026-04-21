using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaTrans.Commands;
using MediaTrans.Models;
using MediaTrans.Services;
using MediaTrans.Views;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 编辑器 ViewModel — 管理波形显示、裁剪、拼接、播放控制
    /// </summary>
    public class EditorViewModel : ViewModelBase, IDisposable
    {
        private const int MaxVirtualGapDurationMs = 30000;

        private MediaFileInfo _currentFile;
        private string _trimStartText = "00:00:00.000";
        private string _trimEndText = "00:00:00.000";
        private string _statusText = "请在文件列表中选择文件";
        private bool _isPlaying;
        private bool _isExporting;
        private bool _audioReady;
        private double _gainDb = 0.0;
        private string _currentTimeText = "00:00:00.000";
        private BitmapSource _waveformImage;
        private double _playbackProgress;
        private double _exportProgress;
        private string _selectedFormat = ".mp3";
        private CancellationTokenSource _exportCts;
        private System.Threading.Timer _playbackTimer;
        private bool _isPlayingSelection;
        private double _selectionPlayEndSeconds;

        /// <summary>
        /// 是否正在播放选区（供 UI 禁用选区操作）
        /// </summary>
        public bool IsPlayingSelection
        {
            get { return _isPlayingSelection; }
        }

        private readonly FFmpegService _ffmpegService;
        private readonly EditExportService _editExportService;
        private readonly AudioPlaybackService _playbackService;
        private readonly ConfigService _configService;
        private readonly PaywallService _paywallService;

        // 波形峰值数据
        private float[] _peakMin;
        private float[] _peakMax;
        private int _waveformWidth = 800;
        private int _waveformHeight = 120;

        // 拼接文件列表
        private ObservableCollection<SpliceEntry> _spliceFiles;

        /// <summary>
        /// 当前加载的文件
        /// </summary>
        public MediaFileInfo CurrentFile
        {
            get { return _currentFile; }
            private set
            {
                if (SetProperty(ref _currentFile, value, "CurrentFile"))
                {
                    OnPropertyChanged("HasFile");
                    OnPropertyChanged("FileInfoText");
                }
            }
        }

        /// <summary>
        /// 是否有文件已加载
        /// </summary>
        public bool HasFile
        {
            get { return _currentFile != null; }
        }

        /// <summary>
        /// 音频是否成功打开，可供播放和波形定位
        /// </summary>
        public bool IsAudioReady
        {
            get { return _audioReady; }
        }

        /// <summary>
        /// 文件信息摘要文本
        /// </summary>
        public string FileInfoText
        {
            get
            {
                if (_currentFile == null) return "";
                string info = string.Format("{0}  |  时长: {1}  |  格式: {2}",
                    _currentFile.FileName,
                    _currentFile.DurationText,
                    _currentFile.Format ?? "");
                if (_currentFile.HasVideo && _currentFile.Width > 0)
                {
                    info += string.Format("  |  {0}x{1}", _currentFile.Width, _currentFile.Height);
                }
                return info;
            }
        }

        /// <summary>
        /// 裁剪起始时间文本（HH:MM:SS.mmm）
        /// </summary>
        public string TrimStartText
        {
            get { return _trimStartText; }
            set
            {
                if (SetProperty(ref _trimStartText, value, "TrimStartText"))
                {
                    OnPropertyChanged("TrimStartPercent");
                }
            }
        }

        /// <summary>
        /// 裁剪结束时间文本（HH:MM:SS.mmm）
        /// </summary>
        public string TrimEndText
        {
            get { return _trimEndText; }
            set
            {
                if (SetProperty(ref _trimEndText, value, "TrimEndText"))
                {
                    OnPropertyChanged("TrimEndPercent");
                }
            }
        }

        /// <summary>
        /// 裁剪起始位置百分比（0‑100），供波形选区覆盖层绑定
        /// </summary>
        public double TrimStartPercent
        {
            get
            {
                if (_currentFile == null || _currentFile.DurationSeconds <= 0) return 0;
                double start;
                if (TryParseTimeText(_trimStartText, out start))
                {
                    return Math.Max(0, Math.Min(100, start / _currentFile.DurationSeconds * 100.0));
                }
                return 0;
            }
        }

        /// <summary>
        /// 裁剪结束位置百分比（0‑100），供波形选区覆盖层绑定
        /// </summary>
        public double TrimEndPercent
        {
            get
            {
                if (_currentFile == null || _currentFile.DurationSeconds <= 0) return 100;
                double end;
                if (TryParseTimeText(_trimEndText, out end))
                {
                    return Math.Max(0, Math.Min(100, end / _currentFile.DurationSeconds * 100.0));
                }
                return 100;
            }
        }

        /// <summary>
        /// 当前播放位置文本
        /// </summary>
        public string CurrentTimeText
        {
            get { return _currentTimeText; }
            private set { SetProperty(ref _currentTimeText, value, "CurrentTimeText"); }
        }

        /// <summary>
        /// 播放进度（0.0～1.0）
        /// </summary>
        public double PlaybackProgress
        {
            get { return _playbackProgress; }
            private set
            {
                if (SetProperty(ref _playbackProgress, value, "PlaybackProgress"))
                {
                    OnPropertyChanged("PlaybackProgressPercent");
                }
            }
        }

        /// <summary>
        /// 播放进度百分比（0～100），供 ProgressBar 绑定
        /// </summary>
        public double PlaybackProgressPercent
        {
            get { return _playbackProgress * 100.0; }
        }

        /// <summary>
        /// 导出进度百分比（0-100），供绑定状态栏进度条
        /// </summary>
        public double ExportProgress
        {
            get { return _exportProgress; }
            private set { SetProperty(ref _exportProgress, value, "ExportProgress"); }
        }

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value, "StatusText"); }
        }

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
                    OnPropertyChanged("PlayPauseLabel");
                    PlayPauseCommand.RaiseCanExecuteChanged();
                    StopCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 是否正在导出
        /// </summary>
        public bool IsExporting
        {
            get { return _isExporting; }
            private set
            {
                if (SetProperty(ref _isExporting, value, "IsExporting"))
                {
                    TrimExportCommand.RaiseCanExecuteChanged();
                    SpliceExportCommand.RaiseCanExecuteChanged();
                    StopExportCommand.RaiseCanExecuteChanged();
                    AddSilenceBlackSegmentCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 播放/暂停按钮标签
        /// </summary>
        public string PlayPauseLabel
        {
            get { return _isPlaying ? "⏸ 暂停" : "▶ 播放"; }
        }

        /// <summary>
        /// 波形图图像（BitmapSource）
        /// </summary>
        public BitmapSource WaveformImage
        {
            get { return _waveformImage; }
            private set { SetProperty(ref _waveformImage, value, "WaveformImage"); }
        }

        /// <summary>
        /// 增益值（dB），-20 ~ +20
        /// </summary>
        public double GainDb
        {
            get { return _gainDb; }
            set
            {
                if (SetProperty(ref _gainDb, value, "GainDb"))
                {
                    OnPropertyChanged("GainText");
                    // 同步更新播放音量预览（仅衰减范围有效，0 dB 对应 volume=1.0）
                    if (_playbackService != null)
                    {
                        double linearVol = GainService.DbToLinear(value);
                        if (linearVol > 1.0) linearVol = 1.0;
                        if (linearVol < 0.0) linearVol = 0.0;
                        _playbackService.SetVolume((float)linearVol);
                    }
                }
            }
        }

        /// <summary>
        /// 增益显示文本
        /// </summary>
        public string GainText
        {
            get
            {
                if (Math.Abs(_gainDb) < 0.05) return "0 dB";
                return string.Format("{0:+0.0;-0.0} dB", _gainDb);
            }
        }

        /// <summary>
        /// 导出格式（扩展名，如 ".mp3"）
        /// </summary>
        public string SelectedExportFormat
        {
            get { return _selectedFormat; }
            set { SetProperty(ref _selectedFormat, value, "SelectedExportFormat"); }
        }

        /// <summary>
        /// 可用导出格式列表
        /// </summary>
        public List<string> ExportFormats
        {
            get
            {
                var all = new List<string>
                {
                    ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".ts", ".mpg", ".mpeg",
                    ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".opus"
                };
                if (_paywallService != null)
                {
                    var filtered = new List<string>();
                    foreach (var fmt in all)
                    {
                        if (_paywallService.IsFormatAllowed(fmt))
                        {
                            filtered.Add(fmt);
                        }
                    }
                    return filtered;
                }
                return all;
            }
        }

        /// <summary>
        /// 拼接文件列表
        /// </summary>
        public ObservableCollection<SpliceEntry> SpliceFiles
        {
            get { return _spliceFiles; }
        }

        // ===== 命令 =====
        /// <summary>播放/暂停命令</summary>
        public RelayCommand PlayPauseCommand { get; private set; }
        /// <summary>停止命令</summary>
        public RelayCommand StopCommand { get; private set; }
        /// <summary>裁剪导出命令</summary>
        public RelayCommand TrimExportCommand { get; private set; }
        /// <summary>停止导出命令</summary>
        public RelayCommand StopExportCommand { get; private set; }
        /// <summary>标记为裁剪起始点命令</summary>
        public RelayCommand MarkInCommand { get; private set; }
        /// <summary>标记为裁剪结束点命令</summary>
        public RelayCommand MarkOutCommand { get; private set; }
        /// <summary>选择全部命令</summary>
        public RelayCommand SelectAllCommand { get; private set; }
        /// <summary>添加拼接文件命令</summary>
        public RelayCommand AddSpliceFileCommand { get; private set; }
        /// <summary>移除拼接文件命令</summary>
        public RelayCommand RemoveSpliceFileCommand { get; private set; }
        /// <summary>拼接导出命令</summary>
        public RelayCommand SpliceExportCommand { get; private set; }
        /// <summary>拼接文件上移命令</summary>
        public RelayCommand MoveSpliceUpCommand { get; private set; }
        /// <summary>拼接文件下移命令</summary>
        public RelayCommand MoveSpliceDownCommand { get; private set; }
        /// <summary>插入静音黑屏片段命令</summary>
        public RelayCommand AddSilenceBlackSegmentCommand { get; private set; }
        /// <summary>播放选区命令</summary>
        public RelayCommand PlaySelectionCommand { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public EditorViewModel(FFmpegService ffmpegService, ConfigService configService)
        {
            _ffmpegService = ffmpegService;
            _configService = configService;
            _paywallService = null;
            _editExportService = new EditExportService();
            _playbackService = new AudioPlaybackService();
            _playbackService.PlaybackStopped += OnPlaybackStopped;
            _ffmpegService.ProgressChanged += OnExportProgressChanged;
            _spliceFiles = new ObservableCollection<SpliceEntry>();
            InitializeCommands();
        }

        /// <summary>
        /// 带付费墙服务的构造函数
        /// </summary>
        public EditorViewModel(FFmpegService ffmpegService, ConfigService configService, PaywallService paywallService)
        {
            _ffmpegService = ffmpegService;
            _configService = configService;
            _paywallService = paywallService;
            _editExportService = new EditExportService(paywallService);
            _playbackService = new AudioPlaybackService();
            _playbackService.PlaybackStopped += OnPlaybackStopped;
            _ffmpegService.ProgressChanged += OnExportProgressChanged;
            _spliceFiles = new ObservableCollection<SpliceEntry>();
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            PlayPauseCommand = new RelayCommand(OnPlayPause, CanPlayPause);
            StopCommand = new RelayCommand(OnStop, CanStop);
            TrimExportCommand = new RelayCommand(OnTrimExport, CanExport);
            StopExportCommand = new RelayCommand(OnStopExport, CanStopExport);
            MarkInCommand = new RelayCommand(OnMarkIn, o => _currentFile != null);
            MarkOutCommand = new RelayCommand(OnMarkOut, o => _currentFile != null);
            SelectAllCommand = new RelayCommand(OnSelectAll, o => _currentFile != null);
            AddSpliceFileCommand = new RelayCommand(OnAddSpliceFile);
            RemoveSpliceFileCommand = new RelayCommand(OnRemoveSpliceFile);
            SpliceExportCommand = new RelayCommand(OnSpliceExport, CanSpliceExport);
            MoveSpliceUpCommand = new RelayCommand(OnMoveSpliceUp);
            MoveSpliceDownCommand = new RelayCommand(OnMoveSpliceDown);
            AddSilenceBlackSegmentCommand = new RelayCommand(OnAddSilenceBlackSegment, o => !_isExporting);
            PlaySelectionCommand = new RelayCommand(OnPlaySelection, o => _currentFile != null && _audioReady);
        }

        /// <summary>
        /// 加载文件到编辑器
        /// </summary>
        public void LoadFile(MediaFileInfo file)
        {
            if (file == null) return;

            // 停止当前播放
            StopPlayback();

            CurrentFile = file;

            // 重置时间
            TrimStartText = "00:00:00.000";
            TrimEndText = SecondsToTimeText(file.DurationSeconds);
            CurrentTimeText = "00:00:00.000";
            PlaybackProgress = 0;

            // 根据文件类型自动选择导出格式
            if (file.HasVideo)
            {
                SelectedExportFormat = string.IsNullOrEmpty(Path.GetExtension(file.FilePath))
                    ? ".mp4"
                    : Path.GetExtension(file.FilePath).ToLowerInvariant();
            }
            else
            {
                SelectedExportFormat = string.IsNullOrEmpty(Path.GetExtension(file.FilePath))
                    ? ".mp3"
                    : Path.GetExtension(file.FilePath).ToLowerInvariant();
            }

            StatusText = string.Format("正在加载波形: {0}", file.FileName);
            WaveformImage = null;

            // 在后台线程加载波形
            ThreadPool.QueueUserWorkItem(LoadWaveformAsync, file);

            // 尝试打开音频播放
            _audioReady = false;
            if (file.HasAudio)
            {
                try
                {
                    _playbackService.Open(file.FilePath);
                    _audioReady = true;
                }
                catch (Exception ex)
                {
                    StatusText = string.Format("无法打开音频: {0}", ex.Message);
                }
            }
            OnPropertyChanged("IsAudioReady");

            RaiseCommandsCanExecuteChanged();
        }

        private void LoadWaveformAsync(object state)
        {
            var file = (MediaFileInfo)state;
            try
            {
                float[] peakMin;
                float[] peakMax;
                ComputeAudioPeaks(file.FilePath, _waveformWidth, out peakMin, out peakMax);

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _peakMin = peakMin;
                    _peakMax = peakMax;

                    if (peakMin != null && peakMin.Length > 0)
                    {
                        WaveformImage = RenderWaveformBitmap(peakMin, peakMax, _waveformWidth, _waveformHeight);
                        StatusText = string.Format("已加载: {0}", file.FileName);
                    }
                    else
                    {
                        StatusText = string.Format("无法读取音频波形（可能是纯视频文件）: {0}", file.FileName);
                    }

                    RaiseCommandsCanExecuteChanged();
                }));
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    StatusText = string.Format("加载波形失败: {0}", ex.Message);
                }));
            }
        }

        /// <summary>
        /// 使用 NAudio 读取音频文件并计算峰值数据
        /// </summary>
        private static void ComputeAudioPeaks(string filePath, int pixelWidth,
            out float[] peakMin, out float[] peakMax)
        {
            peakMin = null;
            peakMax = null;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                using (var reader = new NAudio.Wave.AudioFileReader(filePath))
                {
                    int channels = reader.WaveFormat.Channels;
                    // Guard: channels must be > 0 to avoid divide-by-zero
                    if (channels <= 0) return;

                    // AudioFileReader.Length returns byte count;
                    // AudioFileReader always delivers 32-bit float samples (4 bytes each),
                    // so total frames = Length / (4 * channels)
                    long totalFrames = reader.Length / (4 * channels);

                    if (totalFrames <= 0 || pixelWidth <= 0)
                    {
                        return;
                    }

                    // 每像素对应的采样帧数
                    double framesPerPixel = (double)totalFrames / pixelWidth;
                    if (framesPerPixel < 1) framesPerPixel = 1;

                    // 按块读取 float 采样数据（AudioFileReader.Read 返回 float 数量）
                    const int ReadBufferFloats = 65536;
                    float[] readBuffer = new float[ReadBufferFloats];

                    peakMin = new float[pixelWidth];
                    peakMax = new float[pixelWidth];

                    long frameIndex = 0;
                    int floatsRead;
                    while ((floatsRead = reader.Read(readBuffer, 0, ReadBufferFloats)) > 0)
                    {
                        int framesRead = floatsRead / channels;

                        for (int i = 0; i < framesRead; i++)
                        {
                            float sample = readBuffer[i * channels]; // 左声道
                            int pixelX = (int)(frameIndex / framesPerPixel);
                            if (pixelX >= pixelWidth) break;

                            if (sample < peakMin[pixelX]) peakMin[pixelX] = sample;
                            if (sample > peakMax[pixelX]) peakMax[pixelX] = sample;
                            frameIndex++;
                        }
                    }
                }
            }
            catch
            {
                peakMin = null;
                peakMax = null;
            }
        }

        /// <summary>
        /// 使用 WPF DrawingVisual 渲染波形为 BitmapSource
        /// </summary>
        private static BitmapSource RenderWaveformBitmap(float[] peakMin, float[] peakMax,
            int width, int height)
        {
            if (peakMin == null || peakMax == null || width <= 0 || height <= 0)
            {
                return null;
            }

            var drawingVisual = new DrawingVisual();
            using (DrawingContext dc = drawingVisual.RenderOpen())
            {
                // 背景
                dc.DrawRectangle(
                    new SolidColorBrush(Color.FromRgb(30, 30, 46)),
                    null,
                    new Rect(0, 0, width, height));

                double centerY = height / 2.0;

                // 中心线
                var centerPen = new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 100)), 1);
                centerPen.Freeze();
                dc.DrawLine(centerPen, new Point(0, centerY), new Point(width, centerY));

                // 波形线
                var wavePen = new Pen(new SolidColorBrush(Color.FromRgb(91, 141, 239)), 1);
                wavePen.Freeze();

                int count = Math.Min(peakMin.Length, width);
                for (int x = 0; x < count; x++)
                {
                    float minVal = peakMin[x];
                    float maxVal = peakMax[x];

                    double yTop = centerY - (maxVal * centerY * 0.9);
                    double yBottom = centerY - (minVal * centerY * 0.9);

                    if (yTop > yBottom)
                    {
                        double tmp = yTop;
                        yTop = yBottom;
                        yBottom = tmp;
                    }

                    if (yBottom - yTop < 1.0)
                    {
                        yTop = centerY - 0.5;
                        yBottom = centerY + 0.5;
                    }

                    dc.DrawLine(wavePen, new Point(x + 0.5, yTop), new Point(x + 0.5, yBottom));
                }
            }

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);
            rtb.Freeze();
            return rtb;
        }

        // ===== 播放控制 =====

        /// <summary>
        /// 通过进度比例设置裁剪起始点，供波形拖拽调用
        /// </summary>
        public void SetTrimStartFromRatio(double ratio)
        {
            if (_currentFile == null) return;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            double seconds = _currentFile.DurationSeconds * ratio;
            TrimStartText = SecondsToTimeText(seconds);
        }

        /// <summary>
        /// 通过进度比例设置裁剪结束点，供波形拖拽调用
        /// </summary>
        public void SetTrimEndFromRatio(double ratio)
        {
            if (_currentFile == null) return;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            double seconds = _currentFile.DurationSeconds * ratio;
            TrimEndText = SecondsToTimeText(seconds);
        }

        /// <summary>
        /// 通过进度比例（0.0‑1.0）跳转到对应时间点，供波形点击/拖拽调用
        /// </summary>
        public void SeekToRatio(double ratio)
        {
            if (_currentFile == null || _playbackService == null || !_audioReady) return;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;

            double targetSeconds = _currentFile.DurationSeconds * ratio;
            long targetSample = (long)(targetSeconds * _playbackService.SampleRate);

            _playbackService.SeekToSample(targetSample);

            // 立即刷新进度显示
            PlaybackProgress = ratio;
            CurrentTimeText = SecondsToTimeText(targetSeconds);
        }

        private bool CanPlayPause(object parameter)
        {
            return _currentFile != null && _audioReady;
        }

        private bool CanStop(object parameter)
        {
            return _isPlaying || (_audioReady && _playbackService.State != PlaybackState.Stopped);
        }

        private void OnPlayPause(object parameter)
        {
            if (_currentFile == null) return;
            _isPlayingSelection = false;
            OnPropertyChanged("IsPlayingSelection");

            if (_isPlaying)
            {
                _playbackService.Pause();
                IsPlaying = false;
                StopPlaybackTimer();
                StatusText = "已暂停";
            }
            else
            {
                try
                {
                    _playbackService.Play();
                    IsPlaying = true;
                    StartPlaybackTimer();
                    StatusText = string.Format("正在播放: {0}", _currentFile.FileName);
                }
                catch (Exception ex)
                {
                    StatusText = string.Format("播放失败: {0}", ex.Message);
                }
            }
        }

        private void OnStop(object parameter)
        {
            StopPlayback();
            StatusText = "已停止";
        }

        private void StopPlayback()
        {
            _isPlayingSelection = false;
            OnPropertyChanged("IsPlayingSelection");
            if (_isPlaying || _playbackService.State != PlaybackState.Stopped)
            {
                _playbackService.Stop();
                IsPlaying = false;
                StopPlaybackTimer();
                PlaybackProgress = 0;
                CurrentTimeText = "00:00:00.000";
            }
        }

        private void OnPlaybackStopped(object sender, EventArgs e)
        {
            _isPlayingSelection = false;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                OnPropertyChanged("IsPlayingSelection");
            }));
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                IsPlaying = false;
                StopPlaybackTimer();
                PlaybackProgress = 0;
                CurrentTimeText = SecondsToTimeText(_playbackService.CurrentPositionSeconds);
            }));
        }

        private void StartPlaybackTimer()
        {
            StopPlaybackTimer();
            _playbackTimer = new System.Threading.Timer(OnPlaybackTimerTick, null, 100, 100);
        }

        private void StopPlaybackTimer()
        {
            if (_playbackTimer != null)
            {
                _playbackTimer.Dispose();
                _playbackTimer = null;
            }
        }

        private void OnPlaybackTimerTick(object state)
        {
            try
            {
                double pos = _playbackService.CurrentPositionSeconds;
                double dur = _currentFile != null ? _currentFile.DurationSeconds : 0;
                double progress = dur > 0 ? pos / dur : 0;

                if (_isPlayingSelection && pos >= _selectionPlayEndSeconds)
                {
                    _isPlayingSelection = false;
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        OnPropertyChanged("IsPlayingSelection");
                    }));
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _playbackService.Pause();
                        IsPlaying = false;
                        StopPlaybackTimer();
                        double startSec;
                        if (TryParseTimeText(TrimStartText, out startSec) && dur > 0)
                        {
                            SeekToRatio(startSec / dur);
                        }
                        StatusText = "选区播放完成";
                    }));
                    return;
                }

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    CurrentTimeText = SecondsToTimeText(pos);
                    PlaybackProgress = progress;
                }));
            }
            catch { }
        }

        // ===== 裁剪导出 =====

        private bool CanExport(object parameter)
        {
            return _currentFile != null && !_isExporting;
        }

        private bool CanStopExport(object parameter)
        {
            return _isExporting;
        }

        private void OnTrimExport(object parameter)
        {
            if (_currentFile == null || _isExporting) return;

            double startSec;
            double endSec;

            if (!TryParseTimeText(TrimStartText, out startSec))
            {
                StatusText = "起始时间格式错误（格式：HH:MM:SS.mmm）";
                return;
            }
            if (!TryParseTimeText(TrimEndText, out endSec))
            {
                StatusText = "结束时间格式错误（格式：HH:MM:SS.mmm）";
                return;
            }

            if (endSec <= startSec)
            {
                StatusText = "结束时间必须大于起始时间";
                return;
            }

            double duration = endSec - startSec;

            // 构建默认输出路径
            string ext = SelectedExportFormat ?? ".mp4";
            string suffix = string.Format("_trim_{0:D2}m{1:D2}s",
                (int)(startSec / 60), (int)(startSec % 60));
            string defaultOutputPath = BuildOutputPath(_currentFile.FilePath, suffix, ext);

            // 让用户选择保存路径
            var saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.FileName = Path.GetFileName(defaultOutputPath);
            saveDialog.InitialDirectory = GetSaveInitialDirectory(Path.GetDirectoryName(defaultOutputPath));
            saveDialog.DefaultExt = ext;
            saveDialog.Filter = MediaFileService.BuildSaveFilter(ext);
            bool? dialogResult = saveDialog.ShowDialog();
            if (dialogResult != true) return;
            string outputPath = saveDialog.FileName;
            SaveLastOutputDirectory(outputPath);

            var exportParams = new EditExportParams
            {
                SourceFilePath = _currentFile.FilePath,
                OutputFilePath = outputPath,
                TargetFormat = ext,
                TrimStartSeconds = startSec,
                TrimDurationSeconds = duration,
                GainDb = _gainDb
            };

            var errors = _editExportService.ValidateParams(exportParams);
            if (errors.Count > 0)
            {
                StatusText = string.Join("; ", errors);
                return;
            }

            string args = _editExportService.BuildExportArguments(exportParams);
            ExecuteExport(args, outputPath, duration);
        }

        private void ExecuteExport(string args, string outputPath, double totalDuration)
        {
            _exportCts = new CancellationTokenSource();
            IsExporting = true;
            ExportProgress = 0;
            StatusText = "正在导出...";

            var token = _exportCts.Token;

            _ffmpegService.ExecuteAsync(args, totalDuration, token).ContinueWith(t =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    IsExporting = false;

                    if (t.IsFaulted)
                    {
                        string msg = (t.Exception != null && t.Exception.InnerException != null)
                            ? t.Exception.InnerException.Message
                            : "未知错误";
                        ExportProgress = 0;
                        StatusText = string.Format("导出失败: {0}", msg);
                        return;
                    }

                    var result = t.Result;
                    if (result.Cancelled)
                    {
                        ExportProgress = 0;
                        StatusText = "导出已取消";
                    }
                    else if (result.Success)
                    {
                        ExportProgress = 100;
                        StatusText = string.Format("✅ 导出完成: {0}", outputPath);
                        DarkMessageBox.Show(
                            string.Format("导出完成！\n\n保存位置: {0}", outputPath),
                            "导出成功",
                            MessageBoxButton.OK,
                            DarkMessageBoxIcon.Success);
                    }
                    else
                    {
                        ExportProgress = 0;
                        StatusText = string.Format("导出失败: {0}", result.ErrorMessage);
                    }
                }));
            });
        }

        /// <summary>
        /// FFmpeg 进度回调，更新导出进度百分比
        /// </summary>
        private void OnExportProgressChanged(object sender, FFmpegProgressEventArgs e)
        {
            if (!_isExporting) return;
            double pct = e.Percentage;
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ExportProgress = pct;
            }));
        }

        private void OnStopExport(object parameter)
        {
            if (_exportCts != null && !_exportCts.IsCancellationRequested)
            {
                _exportCts.Cancel();
                StatusText = "正在取消导出...";
            }
        }

        // ===== 标记点 =====

        private void OnMarkIn(object parameter)
        {
            TrimStartText = SecondsToTimeText(_playbackService.CurrentPositionSeconds);
        }

        private void OnMarkOut(object parameter)
        {
            if (_isPlaying)
            {
                TrimEndText = SecondsToTimeText(_playbackService.CurrentPositionSeconds);
            }
            else if (_currentFile != null)
            {
                TrimEndText = SecondsToTimeText(_currentFile.DurationSeconds);
            }
        }

        private void OnSelectAll(object parameter)
        {
            TrimStartText = "00:00:00.000";
            if (_currentFile != null)
            {
                TrimEndText = SecondsToTimeText(_currentFile.DurationSeconds);
            }
        }

        private void OnPlaySelection(object parameter)
        {
            if (_currentFile == null || !_audioReady) return;

            double startSec;
            if (!TryParseTimeText(TrimStartText, out startSec)) return;
            double endSec;
            if (!TryParseTimeText(TrimEndText, out endSec)) return;
            if (endSec <= startSec) return;

            _isPlayingSelection = true;
            _selectionPlayEndSeconds = endSec;
            OnPropertyChanged("IsPlayingSelection");

            try
            {
                // 先 Play() 再 Seek，避免 Play() 在 Stopped 状态下把 Position 重置为 0
                _playbackService.Play();
                double ratio = startSec / _currentFile.DurationSeconds;
                SeekToRatio(ratio);
                IsPlaying = true;
                StartPlaybackTimer();
                StatusText = string.Format("播放选区: {0} → {1}", TrimStartText, TrimEndText);
            }
            catch (Exception ex)
            {
                StatusText = string.Format("播放失败: {0}", ex.Message);
                _isPlayingSelection = false;
                OnPropertyChanged("IsPlayingSelection");
            }
        }

        // ===== 拼接 =====

        private void OnAddSpliceFile(object parameter)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Multiselect = true;
            dialog.Filter = MediaFileService.FileDialogFilter;
            dialog.Title = "选择要拼接的文件";

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                foreach (var path in dialog.FileNames)
                {
                    AddSpliceFileByPath(path);
                }
                SpliceExportCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 从 MediaFileInfo 直接添加到拼接列表（供左侧面板调用）
        /// </summary>
        public void AddFileToSplice(MediaFileInfo file)
        {
            if (file == null) return;
            double dur = file.DurationSeconds;
            string durText = dur > 0 ? SecondsToTimeText(dur) : "未知时长";
            _spliceFiles.Add(new SpliceEntry
            {
                FilePath = file.FilePath,
                FileName = file.FileName,
                DurationText = durText,
                TrimStart = 0,
                TrimEnd = dur,
                HasAudio = file.HasAudio,
                HasVideo = file.HasVideo,
                IsVirtualGap = false,
                VideoWidth = file.Width,
                VideoHeight = file.Height
            });
            SpliceExportCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 移除拼接列表中匹配指定路径的所有条目
        /// </summary>
        public void RemoveSpliceEntriesByPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            for (int i = _spliceFiles.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_spliceFiles[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    _spliceFiles.RemoveAt(i);
                }
            }
            SpliceExportCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 通过文件路径添加到拼接列表（读取时长）
        /// </summary>
        private void AddSpliceFileByPath(string path)
        {
            double dur = 0;
            string durText = "--:--";
            try
            {
                using (var reader = new NAudio.Wave.AudioFileReader(path))
                {
                    dur = reader.TotalTime.TotalSeconds;
                    durText = SecondsToTimeText(dur);
                }
            }
            catch (Exception)
            {
                durText = "未知时长";
            }

            _spliceFiles.Add(new SpliceEntry
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                DurationText = durText,
                TrimStart = 0,
                TrimEnd = dur,
                HasAudio = true,
                HasVideo = true,
                IsVirtualGap = false,
                VideoWidth = 0,
                VideoHeight = 0
            });
        }

        private void OnAddSilenceBlackSegment(object parameter)
        {
            int gapMs;
            if (!ShowGapDurationInputDialog(out gapMs))
            {
                return;
            }

            string error;
            if (!TryAddVirtualGapSegment(gapMs, out error))
            {
                DarkMessageBox.Show(error, "输入错误", MessageBoxButton.OK, DarkMessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 添加一个可排序的静音黑屏虚拟片段（用于命令与测试复用）
        /// </summary>
        public bool TryAddVirtualGapSegment(int gapMs, out string error)
        {
            error = string.Empty;

            if (gapMs < 0 || gapMs > MaxVirtualGapDurationMs)
            {
                error = string.Format("毫秒范围必须在 0 - {0}。", MaxVirtualGapDurationMs);
                return false;
            }

            _spliceFiles.Add(CreateVirtualGapEntry(gapMs));
            SpliceExportCommand.RaiseCanExecuteChanged();
            return true;
        }

        private static SpliceEntry CreateVirtualGapEntry(int gapMs)
        {
            return new SpliceEntry
            {
                FilePath = string.Empty,
                FileName = string.Format("静音黑屏片段（{0}ms）", gapMs),
                DurationText = string.Format("{0} ms", gapMs),
                TrimStart = 0,
                TrimEnd = gapMs / 1000.0,
                HasAudio = true,
                HasVideo = true,
                IsVirtualGap = true,
                VirtualGapDurationMs = gapMs,
                VideoWidth = 0,
                VideoHeight = 0
            };
        }

        private void OnRemoveSpliceFile(object parameter)
        {
            var entry = parameter as SpliceEntry;
            if (entry != null && _spliceFiles.Contains(entry))
            {
                _spliceFiles.Remove(entry);
                SpliceExportCommand.RaiseCanExecuteChanged();
            }
        }

        private void OnMoveSpliceUp(object parameter)
        {
            var entry = parameter as SpliceEntry;
            if (entry == null) return;
            int idx = _spliceFiles.IndexOf(entry);
            if (idx > 0)
            {
                _spliceFiles.Move(idx, idx - 1);
            }
        }

        private void OnMoveSpliceDown(object parameter)
        {
            var entry = parameter as SpliceEntry;
            if (entry == null) return;
            int idx = _spliceFiles.IndexOf(entry);
            if (idx >= 0 && idx < _spliceFiles.Count - 1)
            {
                _spliceFiles.Move(idx, idx + 1);
            }
        }

        private bool CanSpliceExport(object parameter)
        {
            if (_isExporting)
            {
                return false;
            }

            if (_spliceFiles.Count < 2)
            {
                return false;
            }

            return CountRealSpliceEntries() >= 1;
        }

        private void OnSpliceExport(object parameter)
        {
            if (_isExporting) return;
            if (_spliceFiles.Count < 2)
            {
                StatusText = "至少需要 2 个片段才能导出";
                return;
            }
            if (CountRealSpliceEntries() < 1)
            {
                StatusText = "至少需要 1 个真实媒体文件";
                return;
            }

            string ext = SelectedExportFormat ?? ".mp4";
            string outputDir = string.Empty;

            if (_currentFile != null)
            {
                outputDir = Path.GetDirectoryName(_currentFile.FilePath);
            }
            else if (_spliceFiles.Count > 0)
            {
                outputDir = Path.GetDirectoryName(_spliceFiles[0].FilePath);
            }

            string defaultOutputPath = Path.Combine(outputDir ?? "",
                string.Format("splice_{0}{1}", DateTime.Now.ToString("yyyyMMdd_HHmmss"), ext));

            // 让用户选择保存路径
            var saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.FileName = Path.GetFileName(defaultOutputPath);
            saveDialog.InitialDirectory = GetSaveInitialDirectory(Path.GetDirectoryName(defaultOutputPath));
            saveDialog.DefaultExt = ext;
            saveDialog.Filter = MediaFileService.BuildSaveFilter(ext);
            bool? dialogResult = saveDialog.ShowDialog();
            if (dialogResult != true) return;
            string outputPath = saveDialog.FileName;
            SaveLastOutputDirectory(outputPath);

            var segments = new List<ClipSegment>();
            double totalDur = 0;
            bool hasVirtualGap = false;
            foreach (var entry in _spliceFiles)
            {
                if (entry.IsVirtualGap)
                {
                    hasVirtualGap = true;
                    double gapSec = entry.VirtualGapDurationMs / 1000.0;
                    segments.Add(new ClipSegment
                    {
                        SourceFilePath = string.Empty,
                        StartSeconds = 0,
                        DurationSeconds = gapSec,
                        HasAudio = true,
                        HasVideo = true,
                        IsVirtualGap = true,
                        GapDurationMs = entry.VirtualGapDurationMs,
                        VideoWidth = 0,
                        VideoHeight = 0
                    });
                    totalDur += gapSec;
                    continue;
                }

                double dur = entry.TrimEnd - entry.TrimStart;
                if (dur <= 0) dur = entry.TrimEnd;
                // 仅当用户进行了裁剪时才设置 DurationSeconds，
                // 整段文件不设 -t 可避免 NAudio 与 FFmpeg 时长微差导致截短
                bool isTrimmed = entry.TrimStart > 0.001;
                segments.Add(new ClipSegment
                {
                    SourceFilePath = entry.FilePath,
                    StartSeconds = entry.TrimStart,
                    DurationSeconds = isTrimmed ? dur : 0,
                    HasAudio = entry.HasAudio,
                    HasVideo = entry.HasVideo,
                    IsVirtualGap = false,
                    GapDurationMs = 0,
                    VideoWidth = entry.VideoWidth,
                    VideoHeight = entry.VideoHeight
                });
                totalDur += dur;
            }

            var exportParams = new EditExportParams
            {
                OutputFilePath = outputPath,
                TargetFormat = ext,
                Segments = segments,
                GainDb = _gainDb
            };

            var errors = _editExportService.ValidateParams(exportParams);
            if (errors.Count > 0)
            {
                StatusText = string.Join("; ", errors);
                return;
            }

            if (hasVirtualGap && ConversionService.IsAudioOnlyFormat(ext))
            {
                DarkMessageBox.Show(
                    "当前输出为纯音频格式，黑屏段将自动降级为静音段。",
                    "提示",
                    MessageBoxButton.OK,
                    DarkMessageBoxIcon.Information);
            }

            string args = _editExportService.BuildExportArguments(exportParams);
            ExecuteExport(args, outputPath, totalDur);
        }

        private int CountRealSpliceEntries()
        {
            int count = 0;
            foreach (var entry in _spliceFiles)
            {
                if (entry != null && !entry.IsVirtualGap)
                {
                    count++;
                }
            }
            return count;
        }

        private bool ShowGapDurationInputDialog(out int gapMs)
        {
            gapMs = 0;
            int selectedGapMs = 0;

            var bgBrush = new SolidColorBrush(Color.FromRgb(30, 30, 46));
            var panelBrush = new SolidColorBrush(Color.FromRgb(37, 37, 64));
            var textBrush = new SolidColorBrush(Color.FromRgb(238, 238, 238));
            var hintBrush = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            var borderBrush = new SolidColorBrush(Color.FromRgb(91, 141, 239));
            var inputBgBrush = new SolidColorBrush(Color.FromRgb(20, 20, 34));
            var primaryButtonBrush = new SolidColorBrush(Color.FromRgb(91, 141, 239));
            var secondaryButtonBrush = new SolidColorBrush(Color.FromRgb(62, 62, 94));

            var dialog = new Window();
            dialog.Title = "插入静音黑屏片段";
            dialog.Width = 360;
            dialog.Height = 210;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.ResizeMode = ResizeMode.NoResize;
            dialog.WindowStyle = WindowStyle.ToolWindow;
            dialog.Owner = Application.Current != null ? Application.Current.MainWindow : null;
            dialog.Background = bgBrush;

            var rootBorder = new Border();
            rootBorder.Margin = new Thickness(10);
            rootBorder.Padding = new Thickness(14);
            rootBorder.Background = panelBrush;
            rootBorder.BorderBrush = borderBrush;
            rootBorder.BorderThickness = new Thickness(1);
            rootBorder.CornerRadius = new CornerRadius(4);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tip = new TextBlock();
            tip.Text = "请输入片段时长（毫秒，0-30000）：";
            tip.Margin = new Thickness(0, 0, 0, 8);
            tip.Foreground = textBrush;
            tip.FontSize = 13;
            tip.FontWeight = FontWeights.SemiBold;
            Grid.SetRow(tip, 0);
            root.Children.Add(tip);

            var hint = new TextBlock();
            hint.Text = "建议值：500 - 2000（过长会影响节奏）";
            hint.Margin = new Thickness(0, 0, 0, 8);
            hint.Foreground = hintBrush;
            hint.FontSize = 11;
            Grid.SetRow(hint, 1);
            root.Children.Add(hint);

            var input = new TextBox();
            input.Text = "1000";
            input.Margin = new Thickness(0, 0, 0, 10);
            input.Padding = new Thickness(8, 6, 8, 6);
            input.Background = inputBgBrush;
            input.Foreground = textBrush;
            input.BorderBrush = borderBrush;
            input.BorderThickness = new Thickness(1);
            input.CaretBrush = textBrush;
            input.FontSize = 14;
            Grid.SetRow(input, 2);
            root.Children.Add(input);

            var buttonPanel = new StackPanel();
            buttonPanel.Orientation = Orientation.Horizontal;
            buttonPanel.HorizontalAlignment = HorizontalAlignment.Right;

            var okButton = new Button();
            okButton.Content = "确定";
            okButton.MinWidth = 70;
            okButton.Margin = new Thickness(0, 0, 8, 0);
            okButton.Padding = new Thickness(10, 4, 10, 4);
            okButton.Foreground = textBrush;
            okButton.Background = primaryButtonBrush;
            okButton.BorderBrush = primaryButtonBrush;

            var cancelButton = new Button();
            cancelButton.Content = "取消";
            cancelButton.MinWidth = 70;
            cancelButton.Padding = new Thickness(10, 4, 10, 4);
            cancelButton.Foreground = textBrush;
            cancelButton.Background = secondaryButtonBrush;
            cancelButton.BorderBrush = secondaryButtonBrush;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 3);
            root.Children.Add(buttonPanel);

            bool submitted = false;
            okButton.Click += (s, e) =>
            {
                int parsed;
                if (!int.TryParse(input.Text, out parsed))
                {
                    DarkMessageBox.Show("请输入有效整数毫秒值。", "输入错误", MessageBoxButton.OK, DarkMessageBoxIcon.Warning);
                    return;
                }
                if (parsed < 0 || parsed > MaxVirtualGapDurationMs)
                {
                    DarkMessageBox.Show(
                        string.Format("毫秒范围必须在 0 - {0}。", MaxVirtualGapDurationMs),
                        "输入错误",
                        MessageBoxButton.OK,
                        DarkMessageBoxIcon.Warning);
                    return;
                }

                selectedGapMs = parsed;
                submitted = true;
                dialog.DialogResult = true;
                dialog.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            rootBorder.Child = root;
            dialog.Content = rootBorder;
            bool? result = dialog.ShowDialog();
            if (result == true && submitted)
            {
                gapMs = selectedGapMs;
            }
            return result == true && submitted;
        }

        // ===== 辅助方法 =====

        /// <summary>
        private static string SecondsToTimeText(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int totalMs = (int)(seconds * 1000);
            int ms = totalMs % 1000;
            int totalSecs = totalMs / 1000;
            int secs = totalSecs % 60;
            int totalMins = totalSecs / 60;
            int mins = totalMins % 60;
            int hours = totalMins / 60;
            return string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}", hours, mins, secs, ms);
        }

        private static bool TryParseTimeText(string text, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrEmpty(text)) return false;

            // 支持格式: HH:MM:SS.mmm 或 MM:SS.mmm 或 SS.mmm 或 SS
            text = text.Trim();
            text = text.Replace('：', ':').Replace('，', '.').Replace('。', '.');
            string[] parts = text.Split(':');

            try
            {
                if (parts.Length == 3)
                {
                    // 兼容 MM:SS:ms（如 1:53:310）
                    if (parts[2].IndexOf('.') < 0)
                    {
                        int lastInt;
                        if (int.TryParse(parts[2], out lastInt) && lastInt > 59)
                        {
                            double mins = double.Parse(parts[0], CultureInfo.InvariantCulture);
                            double secs = double.Parse(parts[1], CultureInfo.InvariantCulture);
                            double ms = double.Parse(parts[2], CultureInfo.InvariantCulture);
                            seconds = mins * 60 + secs + (ms / 1000.0);
                            return seconds >= 0;
                        }
                    }

                    // HH:MM:SS.mmm
                    double hours = double.Parse(parts[0], CultureInfo.InvariantCulture);
                    double mins2 = double.Parse(parts[1], CultureInfo.InvariantCulture);
                    double secs2 = double.Parse(parts[2],
                        CultureInfo.InvariantCulture);
                    seconds = hours * 3600 + mins2 * 60 + secs2;
                    return seconds >= 0;
                }
                else if (parts.Length == 2)
                {
                    // MM:SS.mmm
                    double mins = double.Parse(parts[0], CultureInfo.InvariantCulture);
                    double secs = double.Parse(parts[1],
                        CultureInfo.InvariantCulture);
                    seconds = mins * 60 + secs;
                    return seconds >= 0;
                }
                else if (parts.Length == 1)
                {
                    seconds = double.Parse(parts[0],
                        CultureInfo.InvariantCulture);
                    return seconds >= 0;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static string BuildOutputPath(string sourcePath, string suffix, string ext)
        {
            string dir = Path.GetDirectoryName(sourcePath) ?? "";
            string name = Path.GetFileNameWithoutExtension(sourcePath);
            string outputName = name + suffix + ext;
            return Path.Combine(dir, outputName);
        }

        private string GetSaveInitialDirectory(string fallback)
        {
            var cfg = _configService != null ? _configService.CurrentConfig : null;
            if (cfg != null && !string.IsNullOrEmpty(cfg.LastOutputDirectory)
                && Directory.Exists(cfg.LastOutputDirectory))
            {
                return cfg.LastOutputDirectory;
            }
            return fallback;
        }

        private void SaveLastOutputDirectory(string filePath)
        {
            var cfg = _configService != null ? _configService.CurrentConfig : null;
            if (cfg == null) return;
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                cfg.LastOutputDirectory = dir;
                _configService.Save(cfg);
            }
        }

        private void RaiseCommandsCanExecuteChanged()
        {
            PlayPauseCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            TrimExportCommand.RaiseCanExecuteChanged();
            StopExportCommand.RaiseCanExecuteChanged();
            MarkInCommand.RaiseCanExecuteChanged();
            MarkOutCommand.RaiseCanExecuteChanged();
            SelectAllCommand.RaiseCanExecuteChanged();
            SpliceExportCommand.RaiseCanExecuteChanged();
            AddSilenceBlackSegmentCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 重置编辑器到初始状态，清空所有缓存
        /// </summary>
        public void Reset()
        {
            StopPlayback();
            _audioReady = false;

            CurrentFile = null;
            WaveformImage = null;
            _peakMin = null;
            _peakMax = null;

            TrimStartText = "00:00:00.000";
            TrimEndText = "00:00:00.000";
            CurrentTimeText = "00:00:00.000";
            PlaybackProgress = 0;
            ExportProgress = 0;
            GainDb = 0;
            IsExporting = false;
            StatusText = "请在文件列表中选择文件";

            _spliceFiles.Clear();

            OnPropertyChanged("IsAudioReady");
            RaiseCommandsCanExecuteChanged();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopPlayback();
            StopPlaybackTimer();
            if (_playbackService != null)
            {
                _playbackService.PlaybackStopped -= OnPlaybackStopped;
                _playbackService.Dispose();
            }
            if (_ffmpegService != null)
            {
                _ffmpegService.ProgressChanged -= OnExportProgressChanged;
            }
            if (_exportCts != null)
            {
                _exportCts.Dispose();
            }
        }
    }

    /// <summary>
    /// 拼接文件条目
    /// </summary>
    public class SpliceEntry
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string DurationText { get; set; }
        public double TrimStart { get; set; }
        public double TrimEnd { get; set; }
        public bool HasAudio { get; set; }
        public bool HasVideo { get; set; }
        public bool IsVirtualGap { get; set; }
        public int VirtualGapDurationMs { get; set; }
        public int VideoWidth { get; set; }
        public int VideoHeight { get; set; }
    }
}
