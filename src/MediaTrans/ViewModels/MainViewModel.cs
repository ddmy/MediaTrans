using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using MediaTrans.Commands;
using MediaTrans.Models;
using MediaTrans.Services;
using MediaTrans.Views;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private string _title;
        private string _statusText;
        private readonly MediaFileService _mediaFileService;
        private readonly ConfigService _configService;
        private readonly ConversionService _conversionService;
        private readonly FFmpegService _ffmpegService;
        private readonly PaywallService _paywallService;
        private readonly LicenseService _licenseService;
        private MediaFileInfo _selectedFile;
        private CancellationTokenSource _importCts;
        private CancellationTokenSource _conversionCts;
        private bool _isConverting;
        private bool _isLicensed;
        private bool _isEditorMode;
        private bool _isSpliceMode;
        private bool _isAudioToolMode;
        private bool _isMusicSearchMode;

        private static readonly List<string> _videoFormats =
            new List<string> { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".ts", ".mpg", ".mpeg" };
        private static readonly List<string> _audioFormats =
            new List<string> { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".opus" };


        public MainViewModel()
        {
            _title = "MediaTrans";
            _statusText = "就绪";
            _configService = new ConfigService();
            _mediaFileService = new MediaFileService(_configService);
            _ffmpegService = new FFmpegService(_configService.Load());

            // 初始化授权与付费墙
            _licenseService = InitializeLicenseService();
            _paywallService = new PaywallService(_licenseService, _configService);

            _conversionService = new ConversionService(_ffmpegService, _configService, _paywallService);
            Files = new ObservableCollection<MediaFileInfo>();
            InitializeCommands();
        }

        /// <summary>
        /// 用于测试的构造函数，允许注入 MediaFileService
        /// </summary>
        public MainViewModel(MediaFileService mediaFileService)
        {
            _title = "MediaTrans";
            _statusText = "就绪";
            _mediaFileService = mediaFileService;
            _configService = new ConfigService();
            _ffmpegService = new FFmpegService(_configService.Load());
            _paywallService = null;
            _conversionService = new ConversionService(_ffmpegService, _configService);
            Files = new ObservableCollection<MediaFileInfo>();
            InitializeCommands();
            _isLicensed = false;
        }

        /// <summary>
        /// 窗口标题
        /// </summary>
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value, "Title"); }
        }

        /// <summary>
        /// 状态栏文本
        /// </summary>
        public string StatusText
        {
            get { return _statusText; }
            set { SetProperty(ref _statusText, value, "StatusText"); }
        }

        /// <summary>
        /// 导入的文件集合
        /// </summary>
        public ObservableCollection<MediaFileInfo> Files { get; private set; }

        /// <summary>
        /// 当前选中的文件
        /// </summary>
        public MediaFileInfo SelectedFile
        {
            get { return _selectedFile; }
            set
            {
                if (SetProperty(ref _selectedFile, value, "SelectedFile"))
                {
                    RaiseAllConversionCanExecuteChanged();
                    // 切换文件时重置进度面板（转换进行中则不重置）
                    if (!_isConverting && ProgressVm != null)
                    {
                        ProgressVm.Reset();
                    }
                    // 如果处于编辑器模式，自动加载文件到编辑器
                    if (_isEditorMode && value != null && EditorVm != null)
                    {
                        EditorVm.LoadFile(value);
                    }
                    // 拼接模式下，选中文件自动添加到拼接列表
                    if (_isSpliceMode && value != null && EditorVm != null)
                    {
                        EditorVm.AddFileToSplice(value);
                    }
                    if (AddToSpliceCommand != null)
                    {
                        AddToSpliceCommand.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        /// <summary>
        /// 导入文件命令
        /// </summary>
        public RelayCommand ImportFilesCommand { get; private set; }

        /// <summary>
        /// 移除选中文件命令
        /// </summary>
        public RelayCommand RemoveFileCommand { get; private set; }

        /// <summary>
        /// 清空文件列表命令
        /// </summary>
        public RelayCommand ClearFilesCommand { get; private set; }

        /// <summary>
        /// 打开许可证管理命令
        /// </summary>
        public RelayCommand OpenLicenseCommand { get; private set; }

        /// <summary>
        /// 开始转换命令
        /// </summary>
        public RelayCommand StartConversionCommand { get; private set; }

        /// <summary>
        /// 停止转换命令
        /// </summary>
        public RelayCommand StopConversionCommand { get; private set; }

        /// <summary>
        /// 是否已授权（控制激活横幅可见性）
        /// </summary>
        public bool IsLicensed
        {
            get { return _isLicensed; }
            set { SetProperty(ref _isLicensed, value, "IsLicensed"); }
        }

        /// <summary>
        /// 是否为裁剪（编辑器）模式
        /// </summary>
        public bool IsEditorMode
        {
            get { return _isEditorMode; }
            set { SetProperty(ref _isEditorMode, value, "IsEditorMode"); }
        }

        /// <summary>
        /// 是否为拼接模式
        /// </summary>
        public bool IsSpliceMode
        {
            get { return _isSpliceMode; }
            set { SetProperty(ref _isSpliceMode, value, "IsSpliceMode"); }
        }

        /// <summary>
        /// 转换参数设置 ViewModel
        /// </summary>
        public ConversionSettingsViewModel SettingsVm { get; private set; }

        /// <summary>
        /// 转换进度 ViewModel
        /// </summary>
        public ConversionProgressViewModel ProgressVm { get; private set; }

        /// <summary>
        /// 编辑器 ViewModel
        /// </summary>
        public EditorViewModel EditorVm { get; private set; }

        /// <summary>
        /// 提取音频命令
        /// </summary>
        public RelayCommand ExtractAudioCommand { get; private set; }

        /// <summary>
        /// 提取视频命令
        /// </summary>
        public RelayCommand ExtractVideoCommand { get; private set; }

        /// <summary>
        /// 切换到转换模式命令
        /// </summary>
        public RelayCommand SwitchToConvertCommand { get; private set; }

        /// <summary>
        /// 切换到裁剪模式命令
        /// </summary>
        public RelayCommand SwitchToEditorCommand { get; private set; }

        /// <summary>
        /// 切换到拼接模式命令
        /// </summary>
        public RelayCommand SwitchToSpliceCommand { get; private set; }

        /// <summary>
        /// 关于我们命令
        /// </summary>
        public RelayCommand OpenAboutCommand { get; private set; }

        /// <summary>
        /// 切换到视频工具模式
        /// </summary>
        public RelayCommand SwitchToVideoToolCommand { get; private set; }

        /// <summary>
        /// 切换到音频工具模式
        /// </summary>
        public RelayCommand SwitchToAudioToolCommand { get; private set; }

        /// <summary>
        /// 将当前选中文件添加到拼接列表（可重复添加同一文件）
        /// </summary>
        public RelayCommand AddToSpliceCommand { get; private set; }

        /// <summary>
        /// 是否为音乐搜索模式
        /// </summary>
        public bool IsMusicSearchMode
        {
            get { return _isMusicSearchMode; }
            set { SetProperty(ref _isMusicSearchMode, value, "IsMusicSearchMode"); }
        }

        /// <summary>
        /// 音乐搜索 ViewModel
        /// </summary>
        public MusicSearchViewModel MusicSearchVm { get; private set; }

        /// <summary>
        /// 切换到音乐搜索模式命令
        /// </summary>
        public RelayCommand SwitchToMusicSearchCommand { get; private set; }

        /// <summary>
        /// 是否为音频工具模式（true=音频，false=视频）
        /// </summary>
        public bool IsAudioToolMode
        {
            get { return _isAudioToolMode; }
            set
            {
                if (SetProperty(ref _isAudioToolMode, value, "IsAudioToolMode"))
                {
                    OnPropertyChanged("FilteredOutputFormats");
                    // 同步更新设置面板的模式，以刷新预设过滤列表
                    if (SettingsVm != null)
                    {
                        SettingsVm.IsAudioMode = value;
                    }
                    // 自动切换到合适的默认格式
                    if (value)
                    {
                        SettingsVm.SelectedFormat = ".mp3";
                    }
                    else
                    {
                        SettingsVm.SelectedFormat = ".mp4";
                    }
                }
            }
        }

        /// <summary>
        /// 根据当前工具模式过滤后的输出格式列表
        /// </summary>
        public List<string> FilteredOutputFormats
        {
            get
            {
                var source = _isAudioToolMode ? _audioFormats : _videoFormats;
                if (_paywallService != null)
                {
                    var filtered = new List<string>();
                    foreach (var fmt in source)
                    {
                        if (_paywallService.IsFormatAllowed(fmt))
                        {
                            filtered.Add(fmt);
                        }
                    }
                    return filtered;
                }
                return source;
            }
        }

        private void InitializeCommands()
        {
            ImportFilesCommand = new RelayCommand(OnImportFiles);
            RemoveFileCommand = new RelayCommand(OnRemoveFile, o => _selectedFile != null);
            ClearFilesCommand = new RelayCommand(OnClearFiles, o => Files.Count > 0);
            OpenLicenseCommand = new RelayCommand(OnOpenLicense);
            StartConversionCommand = new RelayCommand(OnStartConversion, CanStartConversion);
            StopConversionCommand = new RelayCommand(OnStopConversion, CanStopConversion);
            SettingsVm = new ConversionSettingsViewModel(_configService);
            ProgressVm = new ConversionProgressViewModel();
            EditorVm = _paywallService != null
                ? new EditorViewModel(_ffmpegService, _configService, _paywallService)
                : new EditorViewModel(_ffmpegService, _configService);
            ExtractAudioCommand = new RelayCommand(OnExtractAudio, CanStartConversion);
            ExtractVideoCommand = new RelayCommand(OnExtractVideo, CanStartConversion);
            SwitchToConvertCommand = new RelayCommand(OnSwitchToConvert);
            SwitchToEditorCommand = new RelayCommand(OnSwitchToEditor);
            SwitchToSpliceCommand = new RelayCommand(OnSwitchToSplice);
            OpenAboutCommand = new RelayCommand(OnOpenAbout);
            SwitchToVideoToolCommand = new RelayCommand(o => { IsAudioToolMode = false; });
            SwitchToAudioToolCommand = new RelayCommand(o => { IsAudioToolMode = true; });
            AddToSpliceCommand = new RelayCommand(OnAddToSplice, o => _isSpliceMode && _selectedFile != null);
            SwitchToMusicSearchCommand = new RelayCommand(OnSwitchToMusicSearch);
            MusicSearchVm = new MusicSearchViewModel(_configService, _ffmpegService);
            _conversionService.ProgressChanged += OnConversionProgressChanged;
        }

        /// <summary>
        /// 初始化授权服务并返回
        /// </summary>
        private LicenseService InitializeLicenseService()
        {
            try
            {
                var machineCodeService = new MachineCodeService();
                var licenseService = new LicenseService(machineCodeService);
                licenseService.CheckOnStartup();
                _isLicensed = licenseService.IsActivated;
                return licenseService;
            }
            catch (Exception)
            {
                _isLicensed = false;
                // 返回一个未激活的 LicenseService
                var mc = new MachineCodeService();
                return new LicenseService(mc);
            }
        }

        /// <summary>
        /// 打开许可证管理窗口
        /// </summary>
        private void OnOpenLicense(object parameter)
        {
            try
            {
                var machineCodeService = new MachineCodeService();
                // 复用同一个 licenseService，激活后 _paywallService 即时感知新状态
                var vm = new LicenseViewModel(_licenseService, machineCodeService);
                var window = new Views.LicenseWindow();
                window.DataContext = vm;
                window.Owner = Application.Current.MainWindow;

                // 激活成功后刷新主窗口状态
                vm.ActivationSucceeded += (s, e) =>
                {
                    IsLicensed = true;
                    Title = "MediaTrans 专业版";
                    // 刷新格式列表（解锁无损格式）
                    OnPropertyChanged("FilteredOutputFormats");
                };

                window.ShowDialog();
            }
            catch (Exception)
            {
                // 忽略许可证窗口打开失败
            }
        }

        /// <summary>
        /// 打开关于我们对话框
        /// </summary>
        private void OnOpenAbout(object parameter)
        {
            DarkMessageBox.Show(
                "MediaTrans — 专业音视频处理工具\n\n技术支持:  QQ 841312998",
                "关于我们",
                MessageBoxButton.OK,
                DarkMessageBoxIcon.Information);
        }

        /// <summary>
        /// 打开文件对话框导入文件
        /// </summary>
        private void OnImportFiles(object parameter)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Multiselect = true;
            dialog.Filter = MediaFileService.FileDialogFilter;
            dialog.Title = "选择要导入的媒体文件";

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                ImportFilesByPath(dialog.FileNames);
            }
        }

        /// <summary>
        /// 通过文件路径列表导入文件（供拖拽导入调用）
        /// </summary>
        public void ImportFilesByPath(IEnumerable<string> filePaths)
        {
            var supportedFiles = MediaFileService.FilterSupportedFiles(filePaths);
            if (supportedFiles.Count == 0)
            {
                StatusText = "没有找到支持的媒体文件";
                return;
            }

            // 取消之前的导入任务
            if (_importCts != null)
            {
                _importCts.Cancel();
            }
            _importCts = new CancellationTokenSource();
            var token = _importCts.Token;

            StatusText = string.Format("正在导入 {0} 个文件...", supportedFiles.Count);

            // 先添加基本信息到列表（文件名显示），然后在后台读取元信息
            var newItems = new List<MediaFileInfo>();
            foreach (var path in supportedFiles)
            {
                // 避免重复导入
                bool exists = false;
                foreach (var f in Files)
                {
                    if (string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists) continue;

                var info = new MediaFileInfo();
                info.FilePath = path;
                info.FileName = System.IO.Path.GetFileName(path);

                // 获取文件大小
                try
                {
                    var fi = new System.IO.FileInfo(path);
                    if (fi.Exists)
                    {
                        info.FileSize = fi.Length;
                    }
                }
                catch { }

                Files.Add(info);
                newItems.Add(info);
            }

            if (newItems.Count == 0)
            {
                StatusText = "文件已在列表中";
                return;
            }

            // 如果当前无选中项，自动选中第一个新导入的文件
            if (SelectedFile == null)
            {
                SelectedFile = newItems[0];
            }

            // 后台读取元信息
            int total = newItems.Count;
            int completed = 0;
            foreach (var item in newItems)
            {
                var currentItem = item;
                System.Threading.Tasks.Task.Run(() =>
                {
                    if (token.IsCancellationRequested) return;

                    try
                    {
                        var fullInfo = _mediaFileService.GetMediaInfo(currentItem.FilePath, token);

                        // 回调 UI 线程更新信息
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            currentItem.DurationSeconds = fullInfo.DurationSeconds;
                            currentItem.Format = fullInfo.Format;
                            currentItem.VideoCodec = fullInfo.VideoCodec;
                            currentItem.AudioCodec = fullInfo.AudioCodec;
                            currentItem.Width = fullInfo.Width;
                            currentItem.Height = fullInfo.Height;
                            currentItem.FrameRate = fullInfo.FrameRate;
                            currentItem.VideoBitrate = fullInfo.VideoBitrate;
                            currentItem.AudioSampleRate = fullInfo.AudioSampleRate;
                            currentItem.AudioChannels = fullInfo.AudioChannels;
                            currentItem.AudioBitrate = fullInfo.AudioBitrate;
                            currentItem.HasVideo = fullInfo.HasVideo;
                            currentItem.HasAudio = fullInfo.HasAudio;
                            currentItem.MetadataLoaded = fullInfo.MetadataLoaded;

                            completed++;
                            StatusText = string.Format("已读取元信息 {0}/{1}", completed, total);
                            if (completed >= total)
                            {
                                StatusText = string.Format("就绪 - 共 {0} 个文件", Files.Count);
                            }
                        }));
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            completed++;
                            StatusText = string.Format("读取元信息失败: {0}", ex.Message);
                        }));
                    }
                });
            }
        }

        private bool CanStartConversion(object parameter)
        {
            return SelectedFile != null && !_isConverting;
        }

        private bool CanStopConversion(object parameter)
        {
            return _isConverting || ProgressVm.ShowProgressPanel;
        }

        private void OnStartConversion(object parameter)
        {
            if (SelectedFile == null)
            {
                StatusText = "请先选择要转换的文件";
                return;
            }

            if (_isConverting)
            {
                return;
            }

            try
            {
                var source = SelectedFile;
                string targetFormat = SettingsVm.SelectedFormat ?? (source.HasVideo ? ".mp4" : ".mp3");

                // 付费墙格式检查
                if (_paywallService != null && !_paywallService.IsFormatAllowed(targetFormat))
                {
                    DarkMessageBox.Show(
                        string.Format("免费版不支持 {0} 格式导出。\n\n请升级到专业版以解锁无损格式（FLAC/WAV）。", targetFormat),
                        "格式受限",
                        MessageBoxButton.OK,
                        DarkMessageBoxIcon.Warning);
                    return;
                }

                string defaultOutputPath = _conversionService.GenerateOutputPath(source.FilePath, targetFormat);

                // 让用户选择保存路径
                var saveDialog = new Microsoft.Win32.SaveFileDialog();
                saveDialog.FileName = System.IO.Path.GetFileName(defaultOutputPath);
                saveDialog.InitialDirectory = GetSaveInitialDirectory(System.IO.Path.GetDirectoryName(defaultOutputPath));
                saveDialog.DefaultExt = targetFormat;
                saveDialog.Filter = MediaFileService.BuildSaveFilter(targetFormat);
                bool? saveResult = saveDialog.ShowDialog();
                if (saveResult != true) return;
                string outputPath = saveDialog.FileName;
                SaveLastOutputDirectory(outputPath);

                ConversionPreset preset = GetPresetFromSettings();

                var task = new ConversionTask
                {
                    SourceFile = source,
                    OutputPath = outputPath,
                    TargetFormat = targetFormat,
                    Preset = preset
                };

                _conversionCts = new CancellationTokenSource();
                _isConverting = true;
                RaiseAllConversionCanExecuteChanged();
                ProgressVm.StartConversion(source.FileName);
                StatusText = string.Format("开始转换: {0}", source.FileName);

                _conversionService.ConvertAsync(task, _conversionCts.Token).ContinueWith(t =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _isConverting = false;
                        RaiseAllConversionCanExecuteChanged();

                        if (t.IsFaulted)
                        {
                            string errMsg = GetErrorMessage(t.Exception);
                            ProgressVm.CompleteConversion(false, errMsg);
                            StatusText = string.Format("转换失败: {0}", errMsg);
                            return;
                        }

                        var result = t.Result;
                        if (result.Cancelled)
                        {
                            ProgressVm.CancelConversion();
                            StatusText = "转换已取消";
                        }
                        else if (result.Success)
                        {
                            ProgressVm.CompleteConversion(true, "");
                            StatusText = string.Format("转换完成: {0}", outputPath);
                        }
                        else
                        {
                            ProgressVm.CompleteConversion(false, result.ErrorMessage);
                            StatusText = string.Format("转换失败: {0}", result.ErrorMessage);
                        }
                    }));
                });
            }
            catch (Exception ex)
            {
                _isConverting = false;
                RaiseAllConversionCanExecuteChanged();
                StatusText = string.Format("无法开始转换: {0}", ex.Message);
            }
        }

        private void OnStopConversion(object parameter)
        {
            if (_isConverting && _conversionCts != null && !_conversionCts.IsCancellationRequested)
            {
                _conversionCts.Cancel();
                StatusText = "正在取消转换...";
            }
            else if (!_isConverting)
            {
                // 转换已结束（完成/失败/取消），清空进度面板
                ProgressVm.Reset();
                RaiseAllConversionCanExecuteChanged();
                StatusText = "就绪";
            }
        }

        private void OnConversionProgressChanged(object sender, FFmpegProgressEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ProgressVm.UpdateProgress(e);
            }));
        }

        private void OnExtractAudio(object parameter)
        {
            if (SelectedFile == null || _isConverting)
            {
                return;
            }

            try
            {
                var source = SelectedFile;
                string targetFormat = GetEffectiveAudioExtractFormat();

                // 付费墙格式检查
                if (_paywallService != null && !_paywallService.IsFormatAllowed(targetFormat))
                {
                    DarkMessageBox.Show(
                        string.Format("免费版不支持 {0} 格式导出。\n\n请升级到专业版以解锁无损格式（FLAC/WAV）。", targetFormat),
                        "格式受限",
                        MessageBoxButton.OK,
                        DarkMessageBoxIcon.Warning);
                    return;
                }

                string defaultOutputPath = _conversionService.GenerateOutputPath(source.FilePath, targetFormat);

                // 让用户选择保存路径
                var saveDialog = new Microsoft.Win32.SaveFileDialog();
                saveDialog.FileName = System.IO.Path.GetFileName(defaultOutputPath);
                saveDialog.InitialDirectory = GetSaveInitialDirectory(System.IO.Path.GetDirectoryName(defaultOutputPath));
                saveDialog.DefaultExt = targetFormat;
                saveDialog.Filter = MediaFileService.BuildSaveFilter(targetFormat);
                bool? saveResult = saveDialog.ShowDialog();
                if (saveResult != true) return;
                string outputPath = saveDialog.FileName;
                SaveLastOutputDirectory(outputPath);

                ConversionPreset preset = GetPresetFromSettings();

                var task = new ConversionTask
                {
                    SourceFile = source,
                    OutputPath = outputPath,
                    TargetFormat = targetFormat,
                    Preset = preset
                };

                _conversionCts = new CancellationTokenSource();
                _isConverting = true;
                RaiseAllConversionCanExecuteChanged();
                ProgressVm.StartConversion(source.FileName);
                StatusText = string.Format("正在提取音频: {0}", source.FileName);

                _conversionService.ExtractAudioAsync(task, _conversionCts.Token).ContinueWith(t =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _isConverting = false;
                        RaiseAllConversionCanExecuteChanged();

                        if (t.IsFaulted)
                        {
                            string errMsg = GetErrorMessage(t.Exception);
                            ProgressVm.CompleteConversion(false, errMsg);
                            StatusText = string.Format("提取音频失败: {0}", errMsg);
                            return;
                        }

                        var result = t.Result;
                        if (result.Cancelled)
                        {
                            ProgressVm.CancelConversion();
                            StatusText = "提取音频已取消";
                        }
                        else if (result.Success)
                        {
                            ProgressVm.CompleteConversion(true, "");
                            StatusText = string.Format("提取音频完成: {0}", outputPath);
                        }
                        else
                        {
                            ProgressVm.CompleteConversion(false, result.ErrorMessage);
                            StatusText = string.Format("提取音频失败: {0}", result.ErrorMessage);
                        }
                    }));
                });
            }
            catch (Exception ex)
            {
                _isConverting = false;
                RaiseAllConversionCanExecuteChanged();
                StatusText = string.Format("无法提取音频: {0}", ex.Message);
            }
        }

        private void OnExtractVideo(object parameter)
        {
            if (SelectedFile == null || _isConverting)
            {
                return;
            }

            try
            {
                var source = SelectedFile;
                string targetFormat = GetEffectiveVideoExtractFormat();
                string defaultOutputPath = _conversionService.GenerateOutputPath(source.FilePath, targetFormat);

                // 让用户选择保存路径
                var saveDialog = new Microsoft.Win32.SaveFileDialog();
                saveDialog.FileName = System.IO.Path.GetFileName(defaultOutputPath);
                saveDialog.InitialDirectory = GetSaveInitialDirectory(System.IO.Path.GetDirectoryName(defaultOutputPath));
                saveDialog.DefaultExt = targetFormat;
                saveDialog.Filter = MediaFileService.BuildSaveFilter(targetFormat);
                bool? saveResult = saveDialog.ShowDialog();
                if (saveResult != true) return;
                string outputPath = saveDialog.FileName;
                SaveLastOutputDirectory(outputPath);

                ConversionPreset preset = GetPresetFromSettings();

                var task = new ConversionTask
                {
                    SourceFile = source,
                    OutputPath = outputPath,
                    TargetFormat = targetFormat,
                    Preset = preset
                };

                _conversionCts = new CancellationTokenSource();
                _isConverting = true;
                RaiseAllConversionCanExecuteChanged();
                ProgressVm.StartConversion(source.FileName);
                StatusText = string.Format("正在提取视频: {0}", source.FileName);

                _conversionService.ExtractVideoAsync(task, _conversionCts.Token).ContinueWith(t =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _isConverting = false;
                        RaiseAllConversionCanExecuteChanged();

                        if (t.IsFaulted)
                        {
                            string errMsg = GetErrorMessage(t.Exception);
                            ProgressVm.CompleteConversion(false, errMsg);
                            StatusText = string.Format("提取视频失败: {0}", errMsg);
                            return;
                        }

                        var result = t.Result;
                        if (result.Cancelled)
                        {
                            ProgressVm.CancelConversion();
                            StatusText = "提取视频已取消";
                        }
                        else if (result.Success)
                        {
                            ProgressVm.CompleteConversion(true, "");
                            StatusText = string.Format("提取视频完成: {0}", outputPath);
                        }
                        else
                        {
                            ProgressVm.CompleteConversion(false, result.ErrorMessage);
                            StatusText = string.Format("提取视频失败: {0}", result.ErrorMessage);
                        }
                    }));
                });
            }
            catch (Exception ex)
            {
                _isConverting = false;
                RaiseAllConversionCanExecuteChanged();
                StatusText = string.Format("无法提取视频: {0}", ex.Message);
            }
        }

        private void OnSwitchToConvert(object parameter)
        {
            IsEditorMode = false;
            IsSpliceMode = false;
            IsMusicSearchMode = false;
        }

        private void OnSwitchToEditor(object parameter)
        {
            IsEditorMode = true;
            IsSpliceMode = false;
            IsMusicSearchMode = false;
            // 自动加载当前选中文件到编辑器
            if (_selectedFile != null && EditorVm != null)
            {
                EditorVm.LoadFile(_selectedFile);
            }
        }

        private void OnSwitchToSplice(object parameter)
        {
            IsEditorMode = false;
            IsSpliceMode = true;
            IsMusicSearchMode = false;
            if (AddToSpliceCommand != null)
            {
                AddToSpliceCommand.RaiseCanExecuteChanged();
            }
        }

        private void OnAddToSplice(object parameter)
        {
            if (_selectedFile != null && EditorVm != null)
            {
                EditorVm.AddFileToSplice(_selectedFile);
            }
        }

        private void OnSwitchToMusicSearch(object parameter)
        {
            IsEditorMode = false;
            IsSpliceMode = false;
            IsMusicSearchMode = true;
            // 按需启动 Node.js 服务
            if (MusicSearchVm != null)
            {
                MusicSearchVm.EnsureServiceStarted();
            }
        }

        /// <summary>
        /// 从当前设置构建预设。
        /// 返回 null 时表示无自定义参数，ConversionService 将使用目标格式的默认编解码器。
        /// 返回非 null 时表示用户已选择预设或输入自定义参数，ConversionService 将使用返回的预设。
        /// </summary>
        private ConversionPreset GetPresetFromSettings()
        {
            if (SettingsVm.IsCustomMode || SettingsVm.SelectedPreset != null)
            {
                return SettingsVm.BuildCurrentPreset();
            }
            return null;
        }

        /// <summary>
        /// 获取保存对话框的初始目录：优先使用上次保存目录，否则回退到 fallback
        /// </summary>
        private string GetSaveInitialDirectory(string fallback)
        {
            var cfg = _configService != null ? _configService.CurrentConfig : null;
            if (cfg != null && !string.IsNullOrEmpty(cfg.LastOutputDirectory)
                && System.IO.Directory.Exists(cfg.LastOutputDirectory))
            {
                return cfg.LastOutputDirectory;
            }
            return fallback;
        }

        /// <summary>
        /// 将所选文件路径所在目录记录为 LastOutputDirectory 并持久化
        /// </summary>
        private void SaveLastOutputDirectory(string filePath)
        {
            var cfg = _configService != null ? _configService.CurrentConfig : null;
            if (cfg == null) return;
            string dir = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                cfg.LastOutputDirectory = dir;
                _configService.Save(cfg);
            }
        }

        /// <summary>
        /// 获取提取音频操作的目标格式：
        /// 若当前选中格式为音频格式则使用它，否则默认 .mp3。
        /// </summary>
        private string GetEffectiveAudioExtractFormat()
        {
            string selectedFmt = SettingsVm.SelectedFormat;
            if (!string.IsNullOrEmpty(selectedFmt) && ConversionService.IsAudioOnlyFormat(selectedFmt))
            {
                return selectedFmt;
            }
            return ".mp3";
        }

        /// <summary>
        /// 获取提取视频操作的目标格式：
        /// 若当前选中格式为视频格式则使用它，否则默认 .mp4。
        /// </summary>
        private string GetEffectiveVideoExtractFormat()
        {
            string selectedFmt = SettingsVm.SelectedFormat;
            if (!string.IsNullOrEmpty(selectedFmt) && !ConversionService.IsAudioOnlyFormat(selectedFmt))
            {
                return selectedFmt;
            }
            return ".mp4";
        }

        /// <summary>
        /// 从已完成（可能已故障）的任务异常中提取错误消息
        /// </summary>
        private static string GetErrorMessage(System.AggregateException ex)
        {
            if (ex != null && ex.InnerException != null)
            {
                return ex.InnerException.Message;
            }
            return "未知错误";
        }

        private void OnRemoveFile(object parameter)
        {
            if (_selectedFile == null) return;

            // 判断被移除的文件是否正在编辑器中
            bool removingEditorFile = EditorVm != null
                && EditorVm.CurrentFile != null
                && string.Equals(EditorVm.CurrentFile.FilePath, _selectedFile.FilePath, StringComparison.OrdinalIgnoreCase);

            // 同步清理拼接列表中的对应条目
            if (EditorVm != null)
            {
                EditorVm.RemoveSpliceEntriesByPath(_selectedFile.FilePath);
            }

            int index = Files.IndexOf(_selectedFile);
            Files.Remove(_selectedFile);

            // 自动选中相邻项
            if (Files.Count > 0)
            {
                if (index >= Files.Count) index = Files.Count - 1;
                SelectedFile = Files[index];
            }
            else
            {
                SelectedFile = null;
                // 列表已空，重置编辑器和进度
                if (EditorVm != null)
                {
                    EditorVm.Reset();
                }
                if (ProgressVm != null)
                {
                    ProgressVm.Reset();
                }
            }

            // 如果移除的是编辑器正在编辑的文件且列表不空，需要重置编辑器
            if (removingEditorFile && Files.Count > 0 && EditorVm != null)
            {
                if (_isEditorMode && _selectedFile != null)
                {
                    EditorVm.LoadFile(_selectedFile);
                }
                else
                {
                    EditorVm.Reset();
                }
            }

            ClearFilesCommand.RaiseCanExecuteChanged();
            StatusText = string.Format("就绪 - 共 {0} 个文件", Files.Count);
        }

        private void OnClearFiles(object parameter)
        {
            var result = DarkMessageBox.Show(
                "确定要清空文件列表吗？右侧操作区的所有内容缓存将被清除。",
                "确认清空",
                MessageBoxButton.YesNo,
                DarkMessageBoxIcon.Question);
            if (result != MessageBoxResult.Yes) return;

            Files.Clear();
            SelectedFile = null;

            // 重置编辑器缓存
            if (EditorVm != null)
            {
                EditorVm.Reset();
            }

            // 重置转换进度
            if (ProgressVm != null)
            {
                ProgressVm.Reset();
            }

            ClearFilesCommand.RaiseCanExecuteChanged();
            RaiseAllConversionCanExecuteChanged();
            StatusText = "文件列表已清空";
        }

        /// <summary>
        private void RaiseAllConversionCanExecuteChanged()
        {
            StartConversionCommand.RaiseCanExecuteChanged();
            StopConversionCommand.RaiseCanExecuteChanged();
            ExtractAudioCommand.RaiseCanExecuteChanged();
            ExtractVideoCommand.RaiseCanExecuteChanged();
            RemoveFileCommand.RaiseCanExecuteChanged();
            if (AddToSpliceCommand != null)
            {
                AddToSpliceCommand.RaiseCanExecuteChanged();
            }
        }
    }
}
