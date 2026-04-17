using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MediaTrans.Models;
using MediaTrans.Services;
using MediaTrans.ViewModels;

namespace MediaTrans.Views
{
    /// <summary>
    /// 主窗口代码隐藏 — 无边框窗口拖拽、缩放、标题栏按钮、文件拖放、快捷键、波形交互
    /// </summary>
    public partial class MainWindow : Window
    {
        private ShortcutService _shortcutService;
        private bool _isDraggingWaveform;

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
        /// 波形区域鼠标按下 — 开始拖拽或单击定位
        /// </summary>
        private void WaveformBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            if (border == null) return;

            _isDraggingWaveform = true;
            border.CaptureMouse();
            SeekWaveformAtPosition(border, e.GetPosition(border));
        }

        /// <summary>
        /// 波形区域鼠标移动 — 拖拽进度
        /// </summary>
        private void WaveformBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingWaveform) return;
            var border = sender as System.Windows.Controls.Border;
            if (border == null) return;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                SeekWaveformAtPosition(border, e.GetPosition(border));
            }
        }

        /// <summary>
        /// 波形区域鼠标松开 — 结束拖拽
        /// </summary>
        private void WaveformBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            if (border == null) return;
            _isDraggingWaveform = false;
            border.ReleaseMouseCapture();
            SeekWaveformAtPosition(border, e.GetPosition(border));
        }

        /// <summary>
        /// 根据鼠标位置在波形中定位播放进度
        /// </summary>
        private void SeekWaveformAtPosition(System.Windows.Controls.Border border, Point position)
        {
            double width = border.ActualWidth;
            if (width <= 0) return;

            double ratio = position.X / width;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;

            var vm = DataContext as MainViewModel;
            if (vm != null && vm.EditorVm != null && vm.EditorVm.IsAudioReady)
            {
                vm.EditorVm.SeekToRatio(ratio);
            }
        }

        /// <summary>
        /// 拼接模式下，点击已选中文件也能重复添加到拼接列表
        /// </summary>
        private void FileListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null || !vm.IsSpliceMode || vm.EditorVm == null)
            {
                return;
            }

            // 从点击位置向上找到 ListBoxItem
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is ListBoxItem))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            var item = dep as ListBoxItem;
            if (item == null) return;

            var file = item.DataContext as MediaFileInfo;
            if (file == null) return;

            // 仅当点击的是已选中的同一项时手动添加（不同项由 setter 自动添加）
            if (file == vm.SelectedFile)
            {
                vm.EditorVm.AddFileToSplice(file);
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

        /// <summary>
        /// 音乐搜索输入框回车触发搜索
        /// </summary>
        private void MusicSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as MainViewModel;
                if (vm != null && vm.MusicSearchVm != null
                    && vm.MusicSearchVm.SearchCommand.CanExecute(null))
                {
                    vm.MusicSearchVm.SearchCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 音乐搜索列表双击播放
        /// </summary>
        private void MusicResultList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 从点击位置向上找到 ListBoxItem，避免空白区域双击触发
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is ListBoxItem))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }
            if (dep == null) return;

            var item = dep as ListBoxItem;
            if (item == null) return;

            var result = item.DataContext as MusicSearchResult;
            if (result == null) return;

            var vm = DataContext as MainViewModel;
            if (vm != null && vm.MusicSearchVm != null
                && vm.MusicSearchVm.PlayItemCommand.CanExecute(result))
            {
                vm.MusicSearchVm.PlayItemCommand.Execute(result);
                e.Handled = true;
            }
        }
    }
}
