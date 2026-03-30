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
        private MediaFileInfo _selectedFile;
        private CancellationTokenSource _importCts;
        private bool _isLicensed;

        public MainViewModel()
        {
            _title = "MediaTrans";
            _statusText = "就绪";
            var configService = new ConfigService();
            _mediaFileService = new MediaFileService(configService);
            Files = new ObservableCollection<MediaFileInfo>();
            ImportFilesCommand = new RelayCommand(OnImportFiles);
            OpenLicenseCommand = new RelayCommand(OnOpenLicense);

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
            Files = new ObservableCollection<MediaFileInfo>();
            ImportFilesCommand = new RelayCommand(OnImportFiles);
            OpenLicenseCommand = new RelayCommand(OnOpenLicense);
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
            set { SetProperty(ref _selectedFile, value, "SelectedFile"); }
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
        /// 是否已授权（控制激活横幅可见性）
        /// </summary>
        public bool IsLicensed
        {
            get { return _isLicensed; }
            set { SetProperty(ref _isLicensed, value, "IsLicensed"); }
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
    }
}
