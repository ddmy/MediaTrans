using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MediaTrans.Views
{
    /// <summary>
    /// 主窗口代码隐藏 — 无边框窗口拖拽、缩放、标题栏按钮
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
