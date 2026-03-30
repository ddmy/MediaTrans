using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using MediaTrans.Commands;
using MediaTrans.Models;
using MediaTrans.Services;

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
        private MediaFileInfo _selectedFile;
        private CancellationTokenSource _importCts;
        private CancellationTokenSource _conversionCts;
        private bool _isConverting;
        private bool _isLicensed;
        private bool _isEditorMode;

        public MainViewModel()
        {
            _title = "MediaTrans";
            _statusText = "就绪";
            _configService = new ConfigService();
            _mediaFileService = new MediaFileService(_configService);
            _conversionService = new ConversionService(new FFmpegService(_configService.Load()), _configService);
            Files = new ObservableCollection<MediaFileInfo>();
            InitializeCommands();

            // 初始化授权状态
            InitializeLicenseStatus();
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
            _conversionService = new ConversionService(new FFmpegService(_configService.Load()), _configService);
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
                }
            }
        }

        /// <summary>
        /// 导入文件命令
        /// </summary>
        public RelayCommand ImportFilesCommand { get; private set; }

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
        /// 是否为编辑器模式
        /// </summary>
        public bool IsEditorMode
        {
            get { return _isEditorMode; }
            set { SetProperty(ref _isEditorMode, value, "IsEditorMode"); }
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
        /// 切换到编辑器模式命令
        /// </summary>
        public RelayCommand SwitchToEditorCommand { get; private set; }

        private void InitializeCommands()
        {
            ImportFilesCommand = new RelayCommand(OnImportFiles);
            OpenLicenseCommand = new RelayCommand(OnOpenLicense);
            StartConversionCommand = new RelayCommand(OnStartConversion, CanStartConversion);
            StopConversionCommand = new RelayCommand(OnStopConversion, CanStopConversion);
            SettingsVm = new ConversionSettingsViewModel(_configService);
            ProgressVm = new ConversionProgressViewModel();
            ExtractAudioCommand = new RelayCommand(OnExtractAudio, CanStartConversion);
            ExtractVideoCommand = new RelayCommand(OnExtractVideo, CanStartConversion);
            SwitchToConvertCommand = new RelayCommand(OnSwitchToConvert);
            SwitchToEditorCommand = new RelayCommand(OnSwitchToEditor);
            _conversionService.ProgressChanged += OnConversionProgressChanged;
        }

        /// <summary>
        /// 初始化授权状态
        /// </summary>
        private void InitializeLicenseStatus()
        {
            try
            {
                var machineCodeService = new MachineCodeService();
                var licenseService = new LicenseService(machineCodeService);
                licenseService.CheckOnStartup();
                _isLicensed = licenseService.IsActivated;
            }
            catch (Exception)
            {
                _isLicensed = false;
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
                var licenseService = new LicenseService(machineCodeService);
                licenseService.CheckOnStartup();

                var vm = new LicenseViewModel(licenseService, machineCodeService);
                var window = new Views.LicenseWindow();
                window.DataContext = vm;
                window.Owner = Application.Current.MainWindow;

                // 激活成功后刷新主窗口状态
                vm.ActivationSucceeded += (s, e) =>
                {
                    IsLicensed = true;
                    Title = "MediaTrans 专业版";
                };

                window.ShowDialog();
            }
            catch (Exception)
            {
                // 忽略许可证窗口打开失败
            }
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
            return _isConverting;
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
                string outputPath = _conversionService.GenerateOutputPath(source.FilePath, targetFormat);

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
            if (_conversionCts != null && !_conversionCts.IsCancellationRequested)
            {
                _conversionCts.Cancel();
                StatusText = "正在取消转换...";
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
                string targetFormat = ".mp3";
                string outputPath = _conversionService.GenerateOutputPath(source.FilePath, targetFormat);

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
                string targetFormat = ".mp4";
                string outputPath = _conversionService.GenerateOutputPath(source.FilePath, targetFormat);

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
        }

        private void OnSwitchToEditor(object parameter)
        {
            IsEditorMode = true;
        }

        /// <summary>
        /// 从当前设置构建预设（有自定义参数或已选预设时返回非 null）
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

        private void RaiseAllConversionCanExecuteChanged()
        {
            StartConversionCommand.RaiseCanExecuteChanged();
            StopConversionCommand.RaiseCanExecuteChanged();
            ExtractAudioCommand.RaiseCanExecuteChanged();
            ExtractVideoCommand.RaiseCanExecuteChanged();
        }
    }
}
