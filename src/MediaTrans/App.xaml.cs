using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MediaTrans.Services;

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

            // 初始化日志服务
            InitializeLogging();

            // 注册全局异常处理
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            var logger = LogService.Instance;
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
                MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception)
            {
                // 忽略提示框显示失败
            }

            e.Handled = true;
        }
    }
}
