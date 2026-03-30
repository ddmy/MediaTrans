using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using MediaTrans.Commands;
using MediaTrans.Services;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 转换进度 ViewModel — 进度条、剩余时间估算、日志面板
    /// </summary>
    public class ConversionProgressViewModel : ViewModelBase
    {
        private double _progressPercentage;
        private string _progressText;
        private string _remainingTimeText;
        private string _currentFileName;
        private bool _isConverting;
        private bool _hasStarted;
        private readonly Stopwatch _stopwatch;
        private int _maxLogLines;

        public ConversionProgressViewModel()
        {
            _stopwatch = new Stopwatch();
            _maxLogLines = 10000;
            _progressText = "就绪";
            _remainingTimeText = "";
            _currentFileName = "";
            LogEntries = new ObservableCollection<string>();
            ClearLogCommand = new RelayCommand(OnClearLog);
        }

        #region 属性

        /// <summary>
        /// 当前进度百分比（0-100）
        /// </summary>
        public double ProgressPercentage
        {
            get { return _progressPercentage; }
            set { SetProperty(ref _progressPercentage, value, "ProgressPercentage"); }
        }

        /// <summary>
        /// 进度文本（如 "45.2%"）
        /// </summary>
        public string ProgressText
        {
            get { return _progressText; }
            set { SetProperty(ref _progressText, value, "ProgressText"); }
        }

        /// <summary>
        /// 剩余时间文本
        /// </summary>
        public string RemainingTimeText
        {
            get { return _remainingTimeText; }
            set { SetProperty(ref _remainingTimeText, value, "RemainingTimeText"); }
        }

        /// <summary>
        /// 当前正在处理的文件名
        /// </summary>
        public string CurrentFileName
        {
            get { return _currentFileName; }
            set { SetProperty(ref _currentFileName, value, "CurrentFileName"); }
        }

        /// <summary>
        /// 是否正在转换
        /// </summary>
        public bool IsConverting
        {
            get { return _isConverting; }
            set
            {
                if (SetProperty(ref _isConverting, value, "IsConverting"))
                {
                    OnPropertyChanged("ShowProgressPanel");
                }
            }
        }

        /// <summary>
        /// 是否显示进度面板。
        /// 一旦开始过任何一次转换（<see cref="StartConversion"/> 被调用），
        /// 该属性即保持 true，使进度面板在转换完成后仍可见。
        /// </summary>
        public bool ShowProgressPanel
        {
            get { return _isConverting || _hasStarted; }
        }

        /// <summary>
        /// 日志条目列表
        /// </summary>
        public ObservableCollection<string> LogEntries { get; private set; }

        /// <summary>
        /// 清除日志命令
        /// </summary>
        public RelayCommand ClearLogCommand { get; private set; }

        /// <summary>
        /// 最大日志行数
        /// </summary>
        public int MaxLogLines
        {
            get { return _maxLogLines; }
            set { _maxLogLines = value; }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 开始转换计时
        /// </summary>
        public void StartConversion(string fileName)
        {
            _stopwatch.Restart();
            _hasStarted = true;
            IsConverting = true;
            CurrentFileName = fileName;
            ProgressPercentage = 0;
            ProgressText = "0%";
            RemainingTimeText = "计算中...";
            AddLogEntry(string.Format("[{0}] 开始转换: {1}",
                DateTime.Now.ToString("HH:mm:ss"), fileName));
        }

        /// <summary>
        /// 更新进度（由 FFmpegProgressEventArgs 驱动）
        /// </summary>
        public void UpdateProgress(FFmpegProgressEventArgs e)
        {
            if (e.Percentage >= 0)
            {
                ProgressPercentage = e.Percentage;
                ProgressText = string.Format("{0:F1}%", e.Percentage);

                // 估算剩余时间
                if (e.Percentage > 0 && _stopwatch.IsRunning)
                {
                    double elapsed = _stopwatch.Elapsed.TotalSeconds;
                    double estimated = (elapsed / e.Percentage) * (100 - e.Percentage);
                    RemainingTimeText = FormatTimeSpan(TimeSpan.FromSeconds(estimated));
                }
            }
            else
            {
                // 无法获取总时长时只显示已处理时长
                ProgressText = string.Format("已处理 {0}", FormatTimeSpan(TimeSpan.FromSeconds(e.ProcessedSeconds)));
                RemainingTimeText = "";
            }
        }

        /// <summary>
        /// 转换完成
        /// </summary>
        public void CompleteConversion(bool success, string message)
        {
            _stopwatch.Stop();
            IsConverting = false;

            if (success)
            {
                ProgressPercentage = 100;
                ProgressText = "完成";
                RemainingTimeText = string.Format("耗时 {0}", FormatTimeSpan(_stopwatch.Elapsed));
                AddLogEntry(string.Format("[{0}] 转换完成: {1} (耗时 {2})",
                    DateTime.Now.ToString("HH:mm:ss"), _currentFileName, FormatTimeSpan(_stopwatch.Elapsed)));
            }
            else
            {
                ProgressText = "失败";
                RemainingTimeText = "";
                AddLogEntry(string.Format("[{0}] 转换失败: {1} - {2}",
                    DateTime.Now.ToString("HH:mm:ss"), _currentFileName, message));
            }
        }

        /// <summary>
        /// 转换取消
        /// </summary>
        public void CancelConversion()
        {
            _stopwatch.Stop();
            IsConverting = false;
            ProgressText = "已取消";
            RemainingTimeText = "";
            AddLogEntry(string.Format("[{0}] 转换已取消: {1}",
                DateTime.Now.ToString("HH:mm:ss"), _currentFileName));
        }

        /// <summary>
        /// 添加日志条目（自动截断超限行）
        /// </summary>
        public void AddLogEntry(string entry)
        {
            LogEntries.Add(entry);

            // 防止内存溢出，超出最大行数时移除最早的
            while (LogEntries.Count > _maxLogLines)
            {
                LogEntries.RemoveAt(0);
            }
        }

        /// <summary>
        /// 清除日志
        /// </summary>
        private void OnClearLog(object parameter)
        {
            LogEntries.Clear();
        }

        /// <summary>
        /// 格式化时间跨度为易读字符串
        /// </summary>
        public static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
            {
                return string.Format("{0:D2}:{1:D2}:{2:D2}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            }
            if (ts.TotalMinutes >= 1)
            {
                return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
            }
            return string.Format("{0}秒", ts.Seconds);
        }

        /// <summary>
        /// 根据已处理时间和当前百分比估算剩余时间（纯函数，方便测试）
        /// </summary>
        public static double EstimateRemainingSeconds(double elapsedSeconds, double percentage)
        {
            if (percentage <= 0 || percentage >= 100)
            {
                return 0;
            }
            return (elapsedSeconds / percentage) * (100 - percentage);
        }

        #endregion
    }
}
