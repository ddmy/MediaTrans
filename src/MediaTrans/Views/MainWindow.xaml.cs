using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MediaTrans.Services;
using MediaTrans.ViewModels;

namespace MediaTrans.Views
{
    /// <summary>
    /// 主窗口代码隐藏 — 无边框窗口拖拽、缩放、标题栏按钮、文件拖放、快捷键
    /// </summary>
    public partial class MainWindow : Window
    {
        private ShortcutService _shortcutService;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置快捷键服务（由外部注入以便测试）
        /// </summary>
        public void SetShortcutService(ShortcutService shortcutService)
        {
            _shortcutService = shortcutService;
        }

        /// <summary>
        /// 全局键盘快捷键处理
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // 如果焦点在文本输入框内，不拦截快捷键（允许正常编辑）
            if (e.OriginalSource is System.Windows.Controls.TextBox)
            {
                return;
            }

            if (_shortcutService != null && _shortcutService.ProcessKeyDown(e.Key, Keyboard.Modifiers))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// 拖拽文件进入窗口时的处理
        /// </summary>
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// 文件拖放到窗口时导入文件
        /// </summary>
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var vm = DataContext as MainViewModel;
                if (vm != null && files != null)
                {
                    vm.ImportFilesByPath(files);
                }
            }
        }

        /// <summary>
        /// 标题栏拖拽移动窗口，双击切换最大化/还原
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                // 最大化状态下拖拽时先还原窗口
                if (WindowState == WindowState.Maximized)
                {
                    var point = PointToScreen(e.GetPosition(this));
                    WindowState = WindowState.Normal;
                    // 将窗口居中在鼠标位置
                    Left = point.X - (ActualWidth / 2);
                    Top = point.Y - 16;
                }
                DragMove();
            }
        }

        /// <summary>
        /// 最小化按钮
        /// </summary>
        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化/还原按钮
        /// </summary>
        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        /// <summary>
        /// 关闭按钮
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 切换最大化/还原状态
        /// </summary>
        private void ToggleMaximize()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                // 设置最大化区域为工作区（避免遮挡任务栏）
                MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
                MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
                WindowState = WindowState.Maximized;
            }
        }

        /// <summary>
        /// 边缘缩放把手拖拽处理
        /// </summary>
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                return;
            }

            var thumb = sender as Thumb;
            if (thumb == null)
            {
                return;
            }

            string tag = thumb.Tag as string;
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            double newWidth = ActualWidth;
            double newHeight = ActualHeight;
            double newLeft = Left;
            double newTop = Top;

            // 水平缩放
            if (tag.Contains("Right"))
            {
                newWidth = Math.Max(MinWidth, ActualWidth + e.HorizontalChange);
            }
            if (tag.Contains("Left"))
            {
                double delta = Math.Min(e.HorizontalChange, ActualWidth - MinWidth);
                newWidth = ActualWidth - delta;
                newLeft = Left + delta;
            }

            // 垂直缩放
            if (tag.Contains("Bottom"))
            {
                newHeight = Math.Max(MinHeight, ActualHeight + e.VerticalChange);
            }
            if (tag.Contains("Top"))
            {
                double delta = Math.Min(e.VerticalChange, ActualHeight - MinHeight);
                newHeight = ActualHeight - delta;
                newTop = Top + delta;
            }

            // 应用新尺寸
            if (newWidth >= MinWidth)
            {
                Width = newWidth;
                Left = newLeft;
            }
            if (newHeight >= MinHeight)
            {
                Height = newHeight;
                Top = newTop;
            }
        }
    }
}
