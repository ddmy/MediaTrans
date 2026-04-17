using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using MediaTrans.Services;
using MediaTrans.Views;

namespace MediaTrans
{
    /// <summary>
    /// 应用程序入口 — 全局异常捕获与日志初始化
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Win7 默认仅启用 TLS 1.0，强制启用 TLS 1.2 以兼容现代 HTTPS 服务
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls12;

            // 初始化日志服务
            InitializeLogging();

            // 注册全局异常处理
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            var logger = LogService.Instance;

            // 记录系统兼容性信息
            LogSystemCompatibility(logger);

            if (logger != null)
            {
                logger.Info("应用程序启动");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var logger = LogService.Instance;
            if (logger != null)
            {
                logger.Info("应用程序退出");
            }
            base.OnExit(e);
        }

        /// <summary>
        /// 记录系统兼容性信息到日志（启动时调用一次）
        /// </summary>
        private void LogSystemCompatibility(LogService logger)
        {
            try
            {
                var compat = new CompatibilityService();
                if (logger != null)
                {
                    logger.Info(compat.GetSystemSummary());
                    logger.Info(string.Format("DPI: {0}x{1} (缩放 {2:P0}x{3:P0})",
                        DpiHelper.DpiX, DpiHelper.DpiY, DpiHelper.ScaleX, DpiHelper.ScaleY));
                    logger.Info(string.Format("VC++ Runtime: {0}",
                        compat.IsVcRedistInstalled() ? "已安装" : "未检测到"));
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Warn(string.Format("系统兼容性信息采集失败: {0}", ex.Message));
                }
            }
        }

        /// <summary>
        /// 初始化日志服务（从配置读取参数）
        /// </summary>
        private void InitializeLogging()
        {
            try
            {
                var configService = new ConfigService();
                var config = configService.Load();

                // 日志目录默认在应用数据目录下
                string logDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "logs");

                LogService.Initialize(logDir, config.LogMaxFileSize, config.LogMaxFileCount);
            }
            catch (Exception)
            {
                // 配置加载失败时使用默认值
                string logDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "logs");
                LogService.Initialize(logDir, 10 * 1024 * 1024, 5);
            }
        }

        /// <summary>
        /// 非 UI 线程未处理异常
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            var logger = LogService.Instance;
            if (logger != null)
            {
                if (ex != null)
                {
                    logger.Fatal("应用程序发生未处理的异常（非UI线程）", ex);
                }
                else
                {
                    logger.Fatal(string.Format("应用程序发生未处理的异常（非UI线程）: {0}", e.ExceptionObject), null);
                }
            }
        }

        /// <summary>
        /// UI 线程（Dispatcher）未处理异常
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = LogService.Instance;
            if (logger != null)
            {
                logger.Fatal("应用程序发生未处理的异常（UI线程）", e.Exception);
            }

            // 标记为已处理，防止应用直接崩溃退出
            // 显示友好错误提示
            try
            {
                string message = "应用程序遇到了一个错误，但会尝试继续运行。\n如果问题持续出现，请重启应用。";
                if (e.Exception != null)
                {
                    message = string.Format("{0}\n\n错误信息: {1}", message, e.Exception.Message);
                }
                DarkMessageBox.Show(message, "错误", MessageBoxButton.OK, DarkMessageBoxIcon.Error);
            }
            catch (Exception)
            {
                // 忽略提示框显示失败
            }

            e.Handled = true;
        }
    }
}
