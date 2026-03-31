using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MediaTrans.Views
{
    /// <summary>
    /// 统一深色主题弹框，替代 System.Windows.MessageBox
    /// </summary>
    public partial class DarkMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;

        public DarkMessageBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 显示信息弹框（仅确定按钮）
        /// </summary>
        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, DarkMessageBoxIcon icon)
        {
            return ShowInternal(message, title, buttons, icon, null);
        }

        /// <summary>
        /// 显示信息弹框（仅确定按钮），指定 Owner
        /// </summary>
        public static MessageBoxResult Show(Window owner, string message, string title, MessageBoxButton buttons, DarkMessageBoxIcon icon)
        {
            return ShowInternal(message, title, buttons, icon, owner);
        }

        private static MessageBoxResult ShowInternal(string message, string title, MessageBoxButton buttons, DarkMessageBoxIcon icon, Window owner)
        {
            var dlg = new DarkMessageBox();
            dlg.TitleText.Text = title;
            dlg.MessageText.Text = message;

            // 图标
            switch (icon)
            {
                case DarkMessageBoxIcon.Information:
                    dlg.IconText.Text = "ℹ";
                    break;
                case DarkMessageBoxIcon.Question:
                    dlg.IconText.Text = "❓";
                    break;
                case DarkMessageBoxIcon.Warning:
                    dlg.IconText.Text = "⚠";
                    break;
                case DarkMessageBoxIcon.Error:
                    dlg.IconText.Text = "❌";
                    break;
                case DarkMessageBoxIcon.Success:
                    dlg.IconText.Text = "✅";
                    break;
                default:
                    dlg.IconText.Text = "";
                    break;
            }

            // 按钮
            dlg.CreateButtons(buttons);

            // Owner
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
                catch (Exception)
                {
                    // 忽略
                }
            }

            dlg.ShowDialog();
            return dlg._result;
        }

        private void CreateButtons(MessageBoxButton buttons)
        {
            ButtonPanel.Children.Clear();

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    AddButton("确定", MessageBoxResult.OK, true);
                    break;
                case MessageBoxButton.OKCancel:
                    AddButton("取消", MessageBoxResult.Cancel, false);
                    AddButton("确定", MessageBoxResult.OK, true);
                    break;
                case MessageBoxButton.YesNo:
                    AddButton("取消", MessageBoxResult.No, false);
                    AddButton("确定", MessageBoxResult.Yes, true);
                    break;
                case MessageBoxButton.YesNoCancel:
                    AddButton("取消", MessageBoxResult.Cancel, false);
                    AddButton("否", MessageBoxResult.No, false);
                    AddButton("是", MessageBoxResult.Yes, true);
                    break;
            }
        }

        private void AddButton(string text, MessageBoxResult result, bool isPrimary)
        {
            var btn = new Button();
            btn.Content = text;
            btn.Padding = new Thickness(20, 6, 20, 6);
            btn.Margin = new Thickness(6, 0, 0, 0);
            btn.FontSize = 13;
            btn.Cursor = Cursors.Hand;

            if (isPrimary)
            {
                btn.Style = (Style)FindResource("AccentButton");
            }
            else
            {
                btn.Style = (Style)FindResource("DarkButton");
            }

            btn.Click += (s, e) =>
            {
                _result = result;
                Close();
            };

            ButtonPanel.Children.Add(btn);

            // 主按钮获得焦点
            if (isPrimary)
            {
                btn.Loaded += (s, e) => { btn.Focus(); };
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                _result = MessageBoxResult.Cancel;
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                // 找到主按钮并点击
                foreach (var child in ButtonPanel.Children)
                {
                    var btn = child as Button;
                    if (btn != null && btn.IsFocused)
                    {
                        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 弹框图标类型
    /// </summary>
    public enum DarkMessageBoxIcon
    {
        None,
        Information,
        Question,
        Warning,
        Error,
        Success
    }
}
