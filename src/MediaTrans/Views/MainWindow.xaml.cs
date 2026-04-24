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
        // 拖拽模式：0=None, 1=DragTrimStart, 2=DragTrimEnd, 3=PendingSeekOrSelection, 4=Selecting
        private int _dragMode;
        private Point _dragStartPoint;
        private const double WaveformHandleHitThreshold = 8.0;
        private const double WaveformSelectionThreshold = 4.0;

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

            // 如果焦点在可编辑输入控件内，不拦截快捷键（允许正常编辑）
            if (IsTypingContext(e))
            {
                return;
            }

            if (ProcessEditorHotkeys(e))
            {
                e.Handled = true;
                return;
            }

            if (_shortcutService != null && _shortcutService.ProcessKeyDown(e.Key, Keyboard.Modifiers))
            {
                e.Handled = true;
            }
        }

        private bool ProcessEditorHotkeys(KeyEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null || !vm.IsEditorMode || vm.EditorVm == null)
            {
                return false;
            }

            ModifierKeys modifiers = Keyboard.Modifiers;
            if (modifiers == ModifierKeys.None && e.Key == Key.Delete)
            {
                if (vm.EditorVm.DeleteSelectionCommand.CanExecute(null))
                {
                    vm.EditorVm.DeleteSelectionCommand.Execute(null);
                    return true;
                }
                return false;
            }

            if (modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                if (vm.EditorVm.UndoEditCommand.CanExecute(null))
                {
                    vm.EditorVm.UndoEditCommand.Execute(null);
                    return true;
                }
                return false;
            }

            if (modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                if (vm.EditorVm.RedoEditCommand.CanExecute(null))
                {
                    vm.EditorVm.RedoEditCommand.Execute(null);
                    return true;
                }
                return false;
            }

            return false;
        }

        private static bool IsTypingContext(KeyEventArgs e)
        {
            if (e == null)
            {
                return false;
            }

            var original = e.OriginalSource as DependencyObject;
            if (IsTextInputElement(original))
            {
                return true;
            }

            var focused = Keyboard.FocusedElement as DependencyObject;
            if (IsTextInputElement(focused))
            {
                return true;
            }

            return false;
        }

        private static bool IsTextInputElement(DependencyObject element)
        {
            if (element == null)
            {
                return false;
            }

            var textBoxBase = element as TextBoxBase;
            if (textBoxBase != null)
            {
                return true;
            }

            var passwordBox = element as PasswordBox;
            if (passwordBox != null)
            {
                return true;
            }

            var comboBox = element as ComboBox;
            if (comboBox != null && comboBox.IsEditable)
            {
                return true;
            }

            return false;
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
        /// 波形区域鼠标按下 — 判断拖拽模式（裁剪标记 or 定位播放）
        /// </summary>
        private void WaveformBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            if (border == null) return;

            var vm = DataContext as MainViewModel;
            if (vm == null || vm.EditorVm == null || !vm.EditorVm.IsAudioReady)
            {
                return;
            }

            var pos = e.GetPosition(border);
            _dragStartPoint = pos;
            _dragMode = DetectDragMode(border, pos);

            // 播放中禁止拖拽裁剪标记
            if (vm.EditorVm.IsPlaying && (_dragMode == 1 || _dragMode == 2))
            {
                _dragMode = 0;
                return;
            }

            border.CaptureMouse();

            if (_dragMode == 1 || _dragMode == 2)
            {
                ApplyDragAtPosition(border, pos);
            }
        }

        /// <summary>
        /// 波形区域鼠标移动 — 根据拖拽模式分派操作，无拖拽时切换光标
        /// </summary>
        private void WaveformBorder_MouseMove(object sender, MouseEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            if (border == null) return;
            var pos = e.GetPosition(border);

            if (_dragMode != 0 && e.LeftButton == MouseButtonState.Pressed)
            {
                if (_dragMode == 3)
                {
                    if (HasExceededSelectionThreshold(pos))
                    {
                        _dragMode = 4;
                        CreateSelectionAtPosition(pos);
                    }
                }
                else if (_dragMode == 4)
                {
                    CreateSelectionAtPosition(pos);
                    border.Cursor = Cursors.Cross;
                }
                else
                {
                    ApplyDragAtPosition(border, pos);
                }
            }
            else if (_dragMode == 0)
            {
                // 更新鼠标光标（靠近标记线时变为水平调整光标，选区播放中不显示）
                var vm2 = DataContext as MainViewModel;
                int mode = DetectDragMode(border, pos);
                border.Cursor = (mode == 1 || mode == 2) ? Cursors.SizeWE : Cursors.Hand;
            }
        }

        /// <summary>
        /// 波形区域鼠标松开 — 结束拖拽
        /// </summary>
        private void WaveformBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            if (border == null) return;

            if (_dragMode == 3)
            {
                SeekWaveformAtPosition(border, e.GetPosition(border));
            }
            else if (_dragMode == 4)
            {
                CreateSelectionAtPosition(e.GetPosition(border));
            }

            _dragMode = 0;
            border.ReleaseMouseCapture();
            border.Cursor = Cursors.Hand;
            DragTooltip.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 判断鼠标位置应触发何种拖拽模式：1=起始标记, 2=结束标记, 3=候选点击/框选
        /// </summary>
        private int DetectDragMode(System.Windows.Controls.Border border, Point position)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null || vm.EditorVm == null) return 3;

            double width = border.ActualWidth;
            if (width <= 0) return 3;

            if (!vm.EditorVm.SelectionVm.HasSelection)
            {
                return 3;
            }

            double startX = vm.EditorVm.SelectionVm.SelectionStartPixelX;
            double endX = vm.EditorVm.SelectionVm.SelectionEndPixelX;
            double mouseX = position.X;

            if (Math.Abs(mouseX - endX) <= WaveformHandleHitThreshold)
                return 2;
            if (Math.Abs(mouseX - startX) <= WaveformHandleHitThreshold)
                return 1;

            return 3;
        }

        /// <summary>
        /// 执行裁剪标记拖拽操作并更新时间提示
        /// </summary>
        private void ApplyDragAtPosition(System.Windows.Controls.Border border, Point position)
        {
            double width = border.ActualWidth;
            if (width <= 0) return;

            double ratio = position.X / width;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;

            var vm = DataContext as MainViewModel;
            if (vm == null || vm.EditorVm == null) return;

            string snapText;
            double x = SnapToMajorTickPixel(vm.EditorVm, position.X, out snapText);

            if (_dragMode == 1)
            {
                vm.EditorVm.UpdateSelectionStartFromPixel(x);
                ShowDragTooltip(position.X, width,
                    BuildSelectionHintText(vm.EditorVm, snapText));
                border.Cursor = Cursors.SizeWE;
            }
            else if (_dragMode == 2)
            {
                vm.EditorVm.UpdateSelectionEndFromPixel(x);
                ShowDragTooltip(position.X, width,
                    BuildSelectionHintText(vm.EditorVm, snapText));
                border.Cursor = Cursors.SizeWE;
            }
        }

        /// <summary>
        /// 显示拖拽时间提示气泡
        /// </summary>
        private void ShowDragTooltip(double mouseX, double containerWidth, string timeText)
        {
            DragTooltipText.Text = timeText;
            DragTooltip.Visibility = Visibility.Visible;
            // 居中于鼠标位置，避免超出边界
            DragTooltip.UpdateLayout();
            double tooltipWidth = DragTooltip.ActualWidth > 0 ? DragTooltip.ActualWidth : 80;
            double left = mouseX - tooltipWidth / 2;
            if (left < 0) left = 0;
            if (left + tooltipWidth > containerWidth) left = containerWidth - tooltipWidth;
            DragTooltip.Margin = new Thickness(left, 0, 0, 4);
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
        /// 波形区域滚轮缩放
        /// </summary>
        private void WaveformBorder_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            if (border == null) return;

            var vm = DataContext as MainViewModel;
            if (vm == null || vm.EditorVm == null || !vm.EditorVm.IsAudioReady) return;
            if (_dragMode == 1 || _dragMode == 2 || _dragMode == 4) return;

            double width = border.ActualWidth;
            if (width <= 0) return;

            Point position = e.GetPosition(border);
            double ratio = position.X / width;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;

            vm.EditorVm.ZoomWaveformAtRatio(e.Delta, ratio);
            e.Handled = true;
        }

        /// <summary>
        /// 波形区域尺寸变化后同步视口宽度
        /// </summary>
        private void WaveformBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null || vm.EditorVm == null) return;

            int width = (int)Math.Max(1, e.NewSize.Width);
            vm.EditorVm.UpdateWaveformViewportWidth(width);
        }

        private bool HasExceededSelectionThreshold(Point currentPosition)
        {
            return Math.Abs(currentPosition.X - _dragStartPoint.X) >= WaveformSelectionThreshold;
        }

        private void CreateSelectionAtPosition(Point currentPosition)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null || vm.EditorVm == null) return;

            string snapText;
            double x = SnapToMajorTickPixel(vm.EditorVm, currentPosition.X, out snapText);
            vm.EditorVm.CreateSelectionFromPixels(_dragStartPoint.X, x);
            ShowDragTooltip(currentPosition.X,
                Math.Max(1, WaveformBorder.ActualWidth),
                BuildSelectionHintText(vm.EditorVm, snapText));
        }

        private static string BuildSelectionHintText(EditorViewModel editorVm, string snapText)
        {
            if (editorVm == null)
            {
                return string.Empty;
            }

            double startSeconds;
            double endSeconds;
            bool parsedStart = TryParseTimeText(editorVm.TrimStartText, out startSeconds);
            bool parsedEnd = TryParseTimeText(editorVm.TrimEndText, out endSeconds);
            double duration = parsedStart && parsedEnd ? Math.Max(0, endSeconds - startSeconds) : 0;

            string text = string.Format("{0} → {1}  (Δ {2})",
                editorVm.TrimStartText,
                editorVm.TrimEndText,
                FormatDurationSeconds(duration));

            if (!string.IsNullOrEmpty(snapText))
            {
                text += string.Format("  [{0}]", snapText);
            }

            return text;
        }

        private static string FormatDurationSeconds(double seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }
            int totalMs = (int)Math.Round(seconds * 1000.0);
            int ms = totalMs % 1000;
            int totalSecs = totalMs / 1000;
            int mins = totalSecs / 60;
            int secs = totalSecs % 60;
            return string.Format("{0:D2}:{1:D2}.{2:D3}", mins, secs, ms);
        }

        private static bool TryParseTimeText(string text, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrEmpty(text)) return false;

            string normalized = text.Trim().Replace('：', ':').Replace('，', '.').Replace('。', '.');
            string[] parts = normalized.Split(':');
            try
            {
                if (parts.Length == 3)
                {
                    double hours = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                    double mins = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                    double secs = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                    seconds = hours * 3600 + mins * 60 + secs;
                    return seconds >= 0;
                }
                if (parts.Length == 2)
                {
                    double mins = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                    double secs = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                    seconds = mins * 60 + secs;
                    return seconds >= 0;
                }
                if (parts.Length == 1)
                {
                    seconds = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                    return seconds >= 0;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static double SnapToMajorTickPixel(EditorViewModel editorVm, double pixelX, out string snapText)
        {
            snapText = string.Empty;
            if (editorVm == null || editorVm.VisibleTickMarks == null || editorVm.VisibleTickMarks.Count == 0)
            {
                return pixelX;
            }

            TickMark nearest = null;
            double nearestDistance = double.MaxValue;
            int i;
            for (i = 0; i < editorVm.VisibleTickMarks.Count; i++)
            {
                TickMark tick = editorVm.VisibleTickMarks[i];
                if (tick == null || !tick.IsMajor)
                {
                    continue;
                }

                double distance = Math.Abs(tick.PixelX - pixelX);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = tick;
                }
            }

            if (nearest != null && nearestDistance <= editorVm.SnapThresholdPixels)
            {
                snapText = string.IsNullOrEmpty(nearest.Label)
                    ? string.Format("吸附 {0:0.###}s", nearest.TimeSeconds)
                    : string.Format("吸附 {0}", nearest.Label);
                return nearest.PixelX;
            }

            return pixelX;
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
