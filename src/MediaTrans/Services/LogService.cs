using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MediaTrans.Services
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Fatal = 4
    }

    /// <summary>
    /// 日志服务 — 本地文件日志，支持大小轮转
    /// 线程安全，支持配置单文件最大大小和保留份数
    /// </summary>
    public class LogService : IDisposable
    {
        private readonly string _logDirectory;
        private readonly string _logFileBaseName;
        private readonly long _maxFileSize;
        private readonly int _maxFileCount;
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// 当前日志文件完整路径
        /// </summary>
        public string CurrentLogFilePath { get; private set; }

        /// <summary>
        /// 日志目录路径
        /// </summary>
        public string LogDirectory
        {
            get { return _logDirectory; }
        }

        /// <summary>
        /// 单文件最大大小（字节）
        /// </summary>
        public long MaxFileSize
        {
            get { return _maxFileSize; }
        }

        /// <summary>
        /// 最大保留文件数
        /// </summary>
        public int MaxFileCount
        {
            get { return _maxFileCount; }
        }

        /// <summary>
        /// 全局默认实例
        /// </summary>
        private static LogService _instance;
        private static readonly object _instanceLock = new object();

        /// <summary>
        /// 获取全局默认实例
        /// </summary>
        public static LogService Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    return _instance;
                }
            }
        }

        /// <summary>
        /// 初始化全局默认实例
        /// </summary>
        public static void Initialize(string logDirectory, long maxFileSize, int maxFileCount)
        {
            lock (_instanceLock)
            {
                if (_instance != null)
                {
                    _instance.Dispose();
                }
                _instance = new LogService(logDirectory, maxFileSize, maxFileCount);
            }
        }

        /// <summary>
        /// 创建日志服务实例
        /// </summary>
        /// <param name="logDirectory">日志文件目录</param>
        /// <param name="maxFileSize">单文件最大大小（字节），默认 10MB</param>
        /// <param name="maxFileCount">最大保留文件数，默认 5</param>
        public LogService(string logDirectory, long maxFileSize = 10485760, int maxFileCount = 5)
        {
            if (string.IsNullOrEmpty(logDirectory))
            {
                throw new ArgumentNullException("logDirectory");
            }

            _logDirectory = logDirectory;
            _logFileBaseName = "MediaTrans";
            _maxFileSize = maxFileSize > 0 ? maxFileSize : 10485760;
            _maxFileCount = maxFileCount > 0 ? maxFileCount : 5;

            // 确保日志目录存在
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            CurrentLogFilePath = Path.Combine(_logDirectory, _logFileBaseName + ".log");
        }

        /// <summary>
        /// 写入 Debug 级别日志
        /// </summary>
        public void Debug(string message)
        {
            WriteLog(LogLevel.Debug, message);
        }

        /// <summary>
        /// 写入 Info 级别日志
        /// </summary>
        public void Info(string message)
        {
            WriteLog(LogLevel.Info, message);
        }

        /// <summary>
        /// 写入 Warning 级别日志
        /// </summary>
        public void Warn(string message)
        {
            WriteLog(LogLevel.Warning, message);
        }

        /// <summary>
        /// 写入 Error 级别日志
        /// </summary>
        public void Error(string message)
        {
            WriteLog(LogLevel.Error, message);
        }

        /// <summary>
        /// 写入 Error 级别日志（含异常信息）
        /// </summary>
        public void Error(string message, Exception ex)
        {
            if (ex != null)
            {
                string fullMessage = string.Format("{0}\n异常类型: {1}\n异常消息: {2}\n堆栈跟踪:\n{3}",
                    message, ex.GetType().FullName, ex.Message, ex.StackTrace);

                // 记录内部异常
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullMessage = string.Format("{0}\n--- 内部异常 ---\n异常类型: {1}\n异常消息: {2}\n堆栈跟踪:\n{3}",
                        fullMessage, inner.GetType().FullName, inner.Message, inner.StackTrace);
                    inner = inner.InnerException;
                }

                WriteLog(LogLevel.Error, fullMessage);
            }
            else
            {
                WriteLog(LogLevel.Error, message);
            }
        }

        /// <summary>
        /// 写入 Fatal 级别日志（崩溃）
        /// </summary>
        public void Fatal(string message, Exception ex)
        {
            if (ex != null)
            {
                string fullMessage = string.Format("{0}\n异常类型: {1}\n异常消息: {2}\n堆栈跟踪:\n{3}",
                    message, ex.GetType().FullName, ex.Message, ex.StackTrace);

                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullMessage = string.Format("{0}\n--- 内部异常 ---\n异常类型: {1}\n异常消息: {2}\n堆栈跟踪:\n{3}",
                        fullMessage, inner.GetType().FullName, inner.Message, inner.StackTrace);
                    inner = inner.InnerException;
                }

                WriteLog(LogLevel.Fatal, fullMessage);
            }
            else
            {
                WriteLog(LogLevel.Fatal, message);
            }
        }

        /// <summary>
        /// 写入日志条目
        /// </summary>
        public void WriteLog(LogLevel level, string message)
        {
            if (_disposed)
            {
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string levelStr = level.ToString().ToUpperInvariant();
            string threadId = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();
            string logEntry = string.Format("[{0}] [{1}] [线程{2}] {3}", timestamp, levelStr, threadId, message);

            lock (_lock)
            {
                try
                {
                    // 检查是否需要轮转
                    RotateIfNeeded();

                    // 追加写入日志文件（UTF-8 编码）
                    using (var writer = new StreamWriter(CurrentLogFilePath, true, Encoding.UTF8))
                    {
                        writer.WriteLine(logEntry);
                    }
                }
                catch (Exception)
                {
                    // 日志写入失败时静默忽略，防止因日志问题导致应用崩溃
                }
            }
        }

        /// <summary>
        /// 检查当前日志文件大小，超过阈值时执行轮转
        /// </summary>
        private void RotateIfNeeded()
        {
            if (!File.Exists(CurrentLogFilePath))
            {
                return;
            }

            var fileInfo = new FileInfo(CurrentLogFilePath);
            if (fileInfo.Length < _maxFileSize)
            {
                return;
            }

            PerformRotation();
        }

        /// <summary>
        /// 执行日志轮转：
        /// MediaTrans.log → MediaTrans.1.log
        /// MediaTrans.1.log → MediaTrans.2.log
        /// ...
        /// 超出 MaxFileCount 的旧文件删除
        /// </summary>
        public void PerformRotation()
        {
            // 删除最旧的文件（超出保留份数）
            for (int i = _maxFileCount - 1; i >= 1; i--)
            {
                string oldPath = Path.Combine(_logDirectory,
                    string.Format("{0}.{1}.log", _logFileBaseName, i));
                string newPath = Path.Combine(_logDirectory,
                    string.Format("{0}.{1}.log", _logFileBaseName, i + 1));

                if (i == _maxFileCount - 1 && File.Exists(oldPath))
                {
                    // 最旧的文件直接删除（如果已满）
                    try
                    {
                        File.Delete(oldPath);
                    }
                    catch (Exception)
                    {
                        // 忽略删除失败
                    }
                }

                if (File.Exists(oldPath))
                {
                    try
                    {
                        if (File.Exists(newPath))
                        {
                            File.Delete(newPath);
                        }
                        File.Move(oldPath, newPath);
                    }
                    catch (Exception)
                    {
                        // 忽略移动失败
                    }
                }
            }

            // 当前文件 → .1.log
            string firstRotated = Path.Combine(_logDirectory,
                string.Format("{0}.1.log", _logFileBaseName));
            try
            {
                if (File.Exists(firstRotated))
                {
                    File.Delete(firstRotated);
                }
                File.Move(CurrentLogFilePath, firstRotated);
            }
            catch (Exception)
            {
                // 忽略
            }
        }

        /// <summary>
        /// 获取所有日志文件路径（包括轮转的文件）
        /// </summary>
        public string[] GetAllLogFiles()
        {
            if (!Directory.Exists(_logDirectory))
            {
                return new string[0];
            }

            return Directory.GetFiles(_logDirectory, _logFileBaseName + "*.log");
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
