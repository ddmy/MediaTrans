using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MediaTrans.Views
{
    /// <summary>
    /// 深色主题阻断性加载对话框 — 阻止用户操作直到后台任务完成
    /// </summary>
    public partial class DarkLoadingDialog : Window
    {
        private Exception _error;
        private bool _completed;

        public DarkLoadingDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 显示加载对话框并执行后台任务。任务完成后自动关闭。
        /// </summary>
        /// <param name="message">显示的提示消息</param>
        /// <param name="task">后台执行的操作，参数为进度回调(0-100)和取消令牌</param>
        /// <param name="owner">父窗口（可选）</param>
        /// <returns>是否成功完成（无异常）</returns>
        public static bool RunWithLoading(string message, Action<Action<int>> task, Window owner)
        {
            var dlg = new DarkLoadingDialog();
            dlg.MessageText.Text = message;

            if (owner != null)
            {
                dlg.Owner = owner;
            }
            else
            {
                try
                {
                    var mainWin = Application.Current.MainWindow;
                    if (mainWin != null && mainWin.IsLoaded)
                    {
                        dlg.Owner = mainWin;
                    }
                }
                catch { }
            }

            // 后台执行任务
            Task.Run(() =>
            {
                try
                {
                    task(pct =>
                    {
                        dlg.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (pct > 0)
                            {
                                dlg.ProgressBarCtrl.Visibility = Visibility.Visible;
                                dlg.ProgressBarCtrl.Value = pct;
                            }
                        }));
                    });
                    dlg._completed = true;
                }
                catch (Exception ex)
                {
                    dlg._error = ex;
                }
                finally
                {
                    dlg.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        dlg.Close();
                    }));
                }
            });

            dlg.ShowDialog();

            if (dlg._error != null)
            {
                string msg = dlg._error.InnerException != null
                    ? dlg._error.InnerException.Message
                    : dlg._error.Message;
                DarkMessageBox.Show(
                    string.Format("操作失败: {0}", msg),
                    "错误",
                    MessageBoxButton.OK,
                    DarkMessageBoxIcon.Error);
                return false;
            }

            return dlg._completed;
        }

        /// <summary>
        /// 更新消息文本（从任意线程调用）
        /// </summary>
        public void UpdateMessage(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageText.Text = message;
            }));
        }

        // 阻止用户通过 Alt+F4 或其他方式关闭
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_completed && _error == null)
            {
                e.Cancel = true;
                return;
            }
            base.OnClosing(e);
        }
    }
}
