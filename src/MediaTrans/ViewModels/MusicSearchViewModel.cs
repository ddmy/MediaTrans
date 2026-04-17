using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MediaTrans.Commands;
using MediaTrans.Models;
using MediaTrans.Services;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 音乐搜索 ViewModel
    /// </summary>
    public class MusicSearchViewModel : ViewModelBase, IDisposable
    {
        private readonly ConfigService _configService;
        private readonly FFmpegService _ffmpegService;
        private readonly PaywallService _paywallService;
        private NodeServiceManager _nodeService;
        private MusicApiClient _apiClient;
        private MusicDownloadService _downloadService;

        private string _searchKeyword;
        private string _statusText;
        private string _platformStatusText;
        private bool _isSearching;
        private bool _isDownloading;
        private bool _isServiceReady;
        private int _downloadProgress;
        private MusicSearchResult _selectedResult;
        private string _selectedDownloadFormat;
        private bool _disposed;
        private CancellationTokenSource _searchCts;

        // 在线播放相关
        private NAudio.Wave.WaveOut _waveOut;
        private NAudio.Wave.MediaFoundationReader _mediaReader;
        private bool _isPlaying;
        private string _playingUrl;
        private string _tempPlaybackFile;

        // 播放进度
        private DispatcherTimer _playbackTimer;
        private string _playbackPositionText;
        private double _playbackProgress;

        // 平台筛选
        private List<MusicSearchResult> _allResults;

        public MusicSearchViewModel(ConfigService configService, FFmpegService ffmpegService, PaywallService paywallService)
        {
            _configService = configService;
            _ffmpegService = ffmpegService;
            _paywallService = paywallService;
            _statusText = "点击搜索开始查找音乐";
            _platformStatusText = "";
            _selectedDownloadFormat = ".mp3";
            _playbackPositionText = "";
            _allResults = new List<MusicSearchResult>();

            Results = new ObservableCollection<MusicSearchResult>();
            PlatformFilters = new ObservableCollection<PlatformFilterItem>();
            DownloadFormats = new List<string> { ".mp3", ".flac", ".wav", ".aac", ".ogg" };

            SearchCommand = new RelayCommand(OnSearch, CanSearch);
            PlayCommand = new RelayCommand(OnPlay, CanPlay);
            StopPlayCommand = new RelayCommand(OnStopPlay, o => _isPlaying);
            DownloadCommand = new RelayCommand(OnDownload, CanDownload);
            PlayItemCommand = new RelayCommand(OnPlayItem, CanPlayItem);
            DownloadItemCommand = new RelayCommand(OnDownloadItem, CanDownloadItem);

            // 播放进度定时器
            _playbackTimer = new DispatcherTimer();
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(200);
            _playbackTimer.Tick += OnPlaybackTimerTick;
        }

        #region 属性

        public string SearchKeyword
        {
            get { return _searchKeyword; }
            set
            {
                if (SetProperty(ref _searchKeyword, value, "SearchKeyword"))
                {
                    SearchCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            set { SetProperty(ref _statusText, value, "StatusText"); }
        }

        public string PlatformStatusText
        {
            get { return _platformStatusText; }
            set { SetProperty(ref _platformStatusText, value, "PlatformStatusText"); }
        }

        public bool IsSearching
        {
            get { return _isSearching; }
            set
            {
                if (SetProperty(ref _isSearching, value, "IsSearching"))
                {
                    SearchCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsDownloading
        {
            get { return _isDownloading; }
            set
            {
                if (SetProperty(ref _isDownloading, value, "IsDownloading"))
                {
                    DownloadCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsServiceReady
        {
            get { return _isServiceReady; }
            set { SetProperty(ref _isServiceReady, value, "IsServiceReady"); }
        }

        public int DownloadProgress
        {
            get { return _downloadProgress; }
            set { SetProperty(ref _downloadProgress, value, "DownloadProgress"); }
        }

        public MusicSearchResult SelectedResult
        {
            get { return _selectedResult; }
            set
            {
                if (SetProperty(ref _selectedResult, value, "SelectedResult"))
                {
                    PlayCommand.RaiseCanExecuteChanged();
                    DownloadCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedDownloadFormat
        {
            get { return _selectedDownloadFormat; }
            set { SetProperty(ref _selectedDownloadFormat, value, "SelectedDownloadFormat"); }
        }

        public bool IsPlaying
        {
            get { return _isPlaying; }
            set
            {
                if (SetProperty(ref _isPlaying, value, "IsPlaying"))
                {
                    StopPlayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string PlaybackPositionText
        {
            get { return _playbackPositionText; }
            set { SetProperty(ref _playbackPositionText, value, "PlaybackPositionText"); }
        }

        public double PlaybackProgress
        {
            get { return _playbackProgress; }
            set { SetProperty(ref _playbackProgress, value, "PlaybackProgress"); }
        }

        public ObservableCollection<MusicSearchResult> Results { get; private set; }
        public ObservableCollection<PlatformFilterItem> PlatformFilters { get; private set; }
        public List<string> DownloadFormats { get; private set; }

        #endregion

        #region 命令

        public RelayCommand SearchCommand { get; private set; }
        public RelayCommand PlayCommand { get; private set; }
        public RelayCommand StopPlayCommand { get; private set; }
        public RelayCommand DownloadCommand { get; private set; }
        public RelayCommand PlayItemCommand { get; private set; }
        public RelayCommand DownloadItemCommand { get; private set; }

        #endregion

        #region 播放进度

        private void OnPlaybackTimerTick(object sender, EventArgs e)
        {
            if (_mediaReader == null)
            {
                PlaybackPositionText = "";
                PlaybackProgress = 0;
                return;
            }

            try
            {
                var current = _mediaReader.CurrentTime;
                var total = _mediaReader.TotalTime;
                PlaybackPositionText = string.Format("{0}:{1:D2} / {2}:{3:D2}",
                    (int)current.TotalMinutes, current.Seconds,
                    (int)total.TotalMinutes, total.Seconds);

                if (total.TotalSeconds > 0)
                {
                    PlaybackProgress = (current.TotalSeconds / total.TotalSeconds) * 100.0;
                }
            }
            catch
            {
                // reader 可能已被释放
            }
        }

        #endregion

        #region 平台筛选

        private void OnPlatformFilterChanged(object sender, EventArgs e)
        {
            ApplyPlatformFilter();
        }

        private void ApplyPlatformFilter()
        {
            // 收集已勾选的平台
            var checkedPlatforms = new HashSet<string>();
            foreach (var f in PlatformFilters)
            {
                if (f.IsChecked)
                {
                    checkedPlatforms.Add(f.Name);
                }
            }

            Results.Clear();
            foreach (var r in _allResults)
            {
                // 结果中至少有一个源属于已勾选平台
                bool match = false;
                if (r.Sources != null)
                {
                    foreach (var s in r.Sources)
                    {
                        if (checkedPlatforms.Contains(s.Platform))
                        {
                            match = true;
                            break;
                        }
                    }
                }
                if (match)
                {
                    Results.Add(r);
                }
            }

            StatusText = string.Format("显示 {0}/{1} 首歌曲", Results.Count, _allResults.Count);
        }

        #endregion

        #region 服务管理

        /// <summary>
        /// 初始化并启动 Node.js 音乐服务（按需调用）
        /// </summary>
        public void EnsureServiceStarted()
        {
            if (_isServiceReady) return;

            StatusText = "正在启动音乐服务...";

            Task.Run(() =>
            {
                try
                {
                    var config = _configService.CurrentConfig;
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;

                    string configNodePath = config != null && !string.IsNullOrEmpty(config.NodejsPath)
                        ? config.NodejsPath
                        : System.IO.Path.Combine(appDir, @"lib\nodejs\node.exe");
                    string serverScript = config != null && !string.IsNullOrEmpty(config.MusicServerScript)
                        ? config.MusicServerScript
                        : System.IO.Path.Combine(appDir, @"lib\music-server\server.js");
                    int port = config != null && config.MusicServerPort > 0
                        ? config.MusicServerPort
                        : 35200;

                    // 解析相对路径
                    if (!System.IO.Path.IsPathRooted(configNodePath))
                    {
                        configNodePath = System.IO.Path.Combine(appDir, configNodePath);
                    }
                    if (!System.IO.Path.IsPathRooted(serverScript))
                    {
                        serverScript = System.IO.Path.Combine(appDir, serverScript);
                    }

                    // 先检查 Node.js 是否可用
                    string foundNode = NodeServiceManager.FindNodePath(configNodePath);
                    if (string.IsNullOrEmpty(foundNode))
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            IsServiceReady = false;
                            StatusText = "未找到 Node.js 运行时，请先安装 Node.js";
                            Views.DarkMessageBox.Show(
                                "音乐搜索功能需要 Node.js 运行时支持。\n\n" +
                                "请从官网下载并安装 Node.js：\nhttps://nodejs.org\n\n" +
                                "安装完成后重启应用即可使用。",
                                "缺少 Node.js",
                                MessageBoxButton.OK,
                                Views.DarkMessageBoxIcon.Warning);
                        }));
                        return;
                    }

                    _nodeService = new NodeServiceManager(foundNode, serverScript, port);
                    _nodeService.StatusCallback = msg =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            StatusText = msg;
                        }));
                    };
                    _nodeService.Start();

                    _apiClient = new MusicApiClient(_nodeService.BaseUrl);
                    _downloadService = new MusicDownloadService(_ffmpegService, _configService);

                    // 等待服务就绪（最多 10 秒）
                    bool ready = false;
                    for (int i = 0; i < 20; i++)
                    {
                        System.Threading.Thread.Sleep(500);
                        if (_apiClient.CheckHealth())
                        {
                            ready = true;
                            break;
                        }
                    }

                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsServiceReady = ready;
                        if (ready)
                        {
                            StatusText = "音乐服务已就绪，请输入关键词搜索";
                        }
                        else
                        {
                            StatusText = "音乐服务启动失败，请检查 Node.js 运行时";
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsServiceReady = false;
                        StatusText = string.Format("服务启动失败: {0}", ex.Message);
                    }));
                }
            });
        }

        #endregion

        #region 搜索

        private bool CanSearch(object parameter)
        {
            return !_isSearching && !string.IsNullOrWhiteSpace(_searchKeyword);
        }

        private void OnSearch(object parameter)
        {
            if (!_isServiceReady)
            {
                StatusText = "音乐服务未就绪，正在启动...";
                EnsureServiceStarted();
                return;
            }

            // 取消之前的搜索
            if (_searchCts != null)
            {
                _searchCts.Cancel();
            }
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            string keyword = _searchKeyword.Trim();
            IsSearching = true;
            Results.Clear();
            _allResults.Clear();
            PlatformFilters.Clear();
            PlatformStatusText = "搜索中...";
            StatusText = string.Format("正在搜索: {0}", keyword);

            Task.Run(() =>
            {
                try
                {
                    var response = _apiClient.Search(keyword, token);

                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        IsSearching = false;

                        // 构建平台筛选项
                        if (response.PlatformStatuses != null && response.PlatformStatuses.Count > 0)
                        {
                            var statusParts = new List<string>();
                            foreach (var ps in response.PlatformStatuses)
                            {
                                statusParts.Add(ps.StatusText);

                                // 创建筛选项
                                var filterItem = new PlatformFilterItem();
                                filterItem.Name = ps.Name;
                                filterItem.DisplayName = ps.DisplayName;
                                filterItem.Count = ps.Count;
                                filterItem.IsChecked = true;
                                filterItem.CheckedChanged += OnPlatformFilterChanged;
                                PlatformFilters.Add(filterItem);
                            }
                            PlatformStatusText = string.Join("  ", statusParts);
                        }

                        // 缓存全部结果
                        if (response.Results != null)
                        {
                            foreach (var r in response.Results)
                            {
                                _allResults.Add(r);
                                Results.Add(r);
                            }
                        }

                        StatusText = string.Format("找到 {0} 首歌曲", response.TotalCount);
                    }));
                }
                catch (OperationCanceledException)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsSearching = false;
                        StatusText = "搜索已取消";
                    }));
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsSearching = false;
                        StatusText = string.Format("搜索失败: {0}", ex.Message);
                    }));
                }
            });
        }

        #endregion

        #region 播放

        private bool CanPlay(object parameter)
        {
            return _selectedResult != null;
        }

        private void OnPlay(object parameter)
        {
            if (!_isServiceReady)
            {
                StatusText = "音乐服务未就绪，请等待服务启动或安装 Node.js";
                EnsureServiceStarted();
                return;
            }

            if (_selectedResult == null || _selectedResult.Sources == null || _selectedResult.Sources.Count == 0)
            {
                StatusText = "无可用播放源";
                return;
            }

            // 停止当前播放
            StopCurrentPlayback();

            StatusText = string.Format("正在获取播放链接: {0}", _selectedResult.SongName);

            var result = _selectedResult;
            Task.Run(() =>
            {
                // 优先使用选中的平台源
                MusicStreamInfo streamInfo = null;
                string successPlatform = null;

                var orderedSources = BuildOrderedSources(result);
                foreach (var source in orderedSources)
                {
                    try
                    {
                        streamInfo = _apiClient.GetSongUrl(source.Platform, source.SongId, "320", result.SongName, result.Artist);
                        if (streamInfo != null && !string.IsNullOrEmpty(streamInfo.Url))
                        {
                            successPlatform = source.PlatformName;
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (streamInfo == null || string.IsNullOrEmpty(streamInfo.Url))
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        StatusText = "无法获取播放链接，请尝试其他歌曲";
                    }));
                    return;
                }

                // 下载到临时文件（CDN 通常需要 HTTP 请求头，MediaFoundationReader 直接打开 URL 会失败）
                string tempFile = null;
                try
                {
                    string ext = streamInfo.Format ?? "mp3";
                    tempFile = Path.Combine(Path.GetTempPath(),
                        string.Format("mt_play_{0}.{1}", Guid.NewGuid().ToString("N").Substring(0, 8), ext));
                    DownloadForPlayback(streamInfo.Url, tempFile);
                }
                catch (Exception ex)
                {
                    CleanupTempFile(tempFile);
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        StatusText = string.Format("播放失败: {0}", ex.Message);
                    }));
                    return;
                }

                var localFile = tempFile;
                var platform = successPlatform;
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        StartPlayback(localFile);
                        StatusText = string.Format("正在播放: {0} - {1} ({2})",
                            result.SongName, result.Artist, platform);
                    }
                    catch (Exception ex)
                    {
                        CleanupTempFile(localFile);
                        StatusText = string.Format("播放失败: {0}", ex.Message);
                    }
                }));
            });
        }

        /// <summary>
        /// 构建源列表：选中的源排第一，其余保持原序
        /// </summary>
        private List<MusicSource> BuildOrderedSources(MusicSearchResult result)
        {
            var ordered = new List<MusicSource>();
            if (result.SelectedSource != null)
            {
                ordered.Add(result.SelectedSource);
            }
            foreach (var s in result.Sources)
            {
                if (result.SelectedSource == null || s != result.SelectedSource)
                {
                    ordered.Add(s);
                }
            }
            return ordered;
        }

        private void StartPlayback(string localFile)
        {
            try
            {
                StopCurrentPlayback();
                _tempPlaybackFile = localFile;
                _playingUrl = localFile;

                _mediaReader = new NAudio.Wave.MediaFoundationReader(localFile);
                _waveOut = new NAudio.Wave.WaveOut();
                _waveOut.Init(_mediaReader);
                _waveOut.PlaybackStopped += (s, e) =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsPlaying = false;
                        _playbackTimer.Stop();
                        PlaybackPositionText = "";
                        PlaybackProgress = 0;
                    }));
                };
                _waveOut.Play();
                IsPlaying = true;
                _playbackTimer.Start();
            }
            catch
            {
                StopCurrentPlayback();
                throw;
            }
        }

        /// <summary>
        /// 下载音频到本地临时文件用于播放
        /// </summary>
        private void DownloadForPlayback(string url, string destPath)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var responseStream = response.GetResponseStream())
            using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[8192];
                int count;
                while ((count = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, count);
                }
            }
        }

        private void CleanupTempFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { }
        }

        private void OnStopPlay(object parameter)
        {
            StopCurrentPlayback();
            StatusText = "已停止播放";
        }

        private void StopCurrentPlayback()
        {
            _playbackTimer.Stop();
            PlaybackPositionText = "";
            PlaybackProgress = 0;

            if (_waveOut != null)
            {
                try
                {
                    _waveOut.Stop();
                    _waveOut.Dispose();
                }
                catch { }
                _waveOut = null;
            }
            if (_mediaReader != null)
            {
                try
                {
                    _mediaReader.Dispose();
                }
                catch { }
                _mediaReader = null;
            }
            _playingUrl = null;
            IsPlaying = false;
            CleanupTempFile(_tempPlaybackFile);
            _tempPlaybackFile = null;
        }

        #endregion

        #region 下载

        private bool CanDownload(object parameter)
        {
            return _selectedResult != null && !_isDownloading;
        }

        private void OnDownload(object parameter)
        {
            // 未激活时禁止下载
            if (!_paywallService.IsProfessional)
            {
                StatusText = "下载功能需要激活专业版";
                MessageBox.Show(
                    "下载功能仅限专业版用户使用。\n\n请前往「设置 → 许可证」激活专业版后再试。",
                    "功能受限",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!_isServiceReady)
            {
                StatusText = "音乐服务未就绪，请等待服务启动或安装 Node.js";
                EnsureServiceStarted();
                return;
            }

            if (_selectedResult == null || _selectedResult.Sources == null || _selectedResult.Sources.Count == 0)
            {
                StatusText = "无可用下载源";
                return;
            }

            // 弹出保存对话框
            string defaultFileName = string.Format("{0} - {1}{2}",
                _selectedResult.Artist, _selectedResult.SongName, _selectedDownloadFormat);
            // 清理非法文件名字符
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                defaultFileName = defaultFileName.Replace(c.ToString(), "_");
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.FileName = defaultFileName;
            saveDialog.DefaultExt = _selectedDownloadFormat;
            saveDialog.Filter = string.Format("音频文件 (*{0})|*{0}|所有文件|*.*", _selectedDownloadFormat);

            bool? dialogResult = saveDialog.ShowDialog();
            if (dialogResult != true) return;

            string outputPath = saveDialog.FileName;
            var result = _selectedResult;
            string format = _selectedDownloadFormat;

            IsDownloading = true;
            DownloadProgress = 0;
            StatusText = string.Format("正在下载: {0}", result.SongName);

            Task.Run(() =>
            {
                // 优先使用选中的平台源
                MusicStreamInfo streamInfo = null;
                string quality = format == ".flac" ? "flac" : "320";

                var orderedSources = BuildOrderedSources(result);
                foreach (var source in orderedSources)
                {
                    try
                    {
                        streamInfo = _apiClient.GetSongUrl(source.Platform, source.SongId, quality, result.SongName, result.Artist);
                        if (streamInfo != null && !string.IsNullOrEmpty(streamInfo.Url))
                        {
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (streamInfo == null || string.IsNullOrEmpty(streamInfo.Url))
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsDownloading = false;
                        StatusText = "无法获取下载链接，请尝试其他歌曲";
                    }));
                    return;
                }

                try
                {
                    _downloadService.DownloadAndConvertAsync(
                        streamInfo,
                        result.SongName,
                        result.Artist,
                        format,
                        outputPath,
                        pct =>
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                DownloadProgress = pct;
                            }));
                        },
                        CancellationToken.None).Wait();

                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsDownloading = false;
                        DownloadProgress = 100;
                        StatusText = string.Format("下载完成: {0}", outputPath);
                    }));
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsDownloading = false;
                        DownloadProgress = 0;
                        string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                        StatusText = string.Format("下载失败: {0}", msg);
                    }));
                }
            });
        }

        #endregion

        #region 列表项命令（双击播放、右键菜单）

        private bool CanPlayItem(object parameter)
        {
            return parameter is MusicSearchResult;
        }

        private bool CanDownloadItem(object parameter)
        {
            return parameter is MusicSearchResult && !_isDownloading;
        }

        /// <summary>
        /// 双击或右键播放指定项
        /// </summary>
        private void OnPlayItem(object parameter)
        {
            var item = parameter as MusicSearchResult;
            if (item == null) return;
            SelectedResult = item;
            OnPlay(null);
        }

        /// <summary>
        /// 右键下载指定项（弹出保存对话框）
        /// </summary>
        private void OnDownloadItem(object parameter)
        {
            var item = parameter as MusicSearchResult;
            if (item == null) return;
            SelectedResult = item;
            OnDownload(null);
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _playbackTimer.Stop();
                StopCurrentPlayback();
                // 取消订阅筛选事件
                foreach (var f in PlatformFilters)
                {
                    f.CheckedChanged -= OnPlatformFilterChanged;
                }
                if (_searchCts != null)
                {
                    _searchCts.Cancel();
                    _searchCts.Dispose();
                }
                if (_nodeService != null)
                {
                    _nodeService.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
