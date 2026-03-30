using System;
using System.Windows.Input;
using MediaTrans.ViewModels;

namespace MediaTrans.Services
{
    /// <summary>
    /// 键盘快捷键服务 — 集中处理全局快捷键分发
    /// </summary>
    public class ShortcutService
    {
        private readonly PlaybackViewModel _playbackVm;
        private readonly UndoRedoViewModel _undoRedoVm;
        private readonly TimelineTrackViewModel _trackVm;
        private readonly SelectionViewModel _selectionVm;
        private readonly TimelineViewModel _timelineVm;
        private readonly WaveformViewModel _waveformVm;
        private readonly int _playheadStepPixels;

        /// <summary>
        /// 构造快捷键服务
        /// </summary>
        /// <param name="playbackVm">播放控制 ViewModel（可为 null）</param>
        /// <param name="undoRedoVm">撤销/重做 ViewModel（可为 null）</param>
        /// <param name="trackVm">时间轴轨道 ViewModel（可为 null）</param>
        /// <param name="selectionVm">选区 ViewModel（可为 null）</param>
        /// <param name="timelineVm">时间轴 ViewModel（可为 null）</param>
        /// <param name="waveformVm">波形 ViewModel（可为 null）</param>
        /// <param name="playheadStepPixels">方向键步进像素数</param>
        public ShortcutService(
            PlaybackViewModel playbackVm,
            UndoRedoViewModel undoRedoVm,
            TimelineTrackViewModel trackVm,
            SelectionViewModel selectionVm,
            TimelineViewModel timelineVm,
            WaveformViewModel waveformVm,
            int playheadStepPixels)
        {
            _playbackVm = playbackVm;
            _undoRedoVm = undoRedoVm;
            _trackVm = trackVm;
            _selectionVm = selectionVm;
            _timelineVm = timelineVm;
            _waveformVm = waveformVm;
            _playheadStepPixels = playheadStepPixels > 0 ? playheadStepPixels : 10;
        }

        /// <summary>
        /// 处理键盘按下事件，返回是否已处理
        /// </summary>
        /// <param name="key">按下的键</param>
        /// <param name="modifiers">修饰键状态</param>
        /// <returns>true 表示已处理，false 表示未匹配任何快捷键</returns>
        public bool ProcessKeyDown(Key key, ModifierKeys modifiers)
        {
            // Ctrl 组合键
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                return ProcessCtrlCombo(key, modifiers);
            }

            // 无修饰键
            switch (key)
            {
                case Key.Space:
                    return HandlePlayPauseToggle();

                case Key.Delete:
                    return HandleDeleteSelected();

                case Key.Left:
                    return HandleMovePlayhead(-1);

                case Key.Right:
                    return HandleMovePlayhead(1);

                case Key.Home:
                    return HandleJumpToStart();

                case Key.End:
                    return HandleJumpToEnd();

                default:
                    return false;
            }
        }

        /// <summary>
        /// 处理 Ctrl 组合键
        /// </summary>
        private bool ProcessCtrlCombo(Key key, ModifierKeys modifiers)
        {
            switch (key)
            {
                case Key.Z:
                    // Ctrl+Shift+Z = 重做
                    if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    {
                        return HandleRedo();
                    }
                    // Ctrl+Z = 撤销
                    return HandleUndo();

                case Key.Y:
                    // Ctrl+Y = 重做
                    return HandleRedo();

                case Key.A:
                    // Ctrl+A = 全选
                    return HandleSelectAll();

                default:
                    return false;
            }
        }

        /// <summary>
        /// Space：播放/暂停切换
        /// </summary>
        private bool HandlePlayPauseToggle()
        {
            if (_playbackVm == null)
            {
                return false;
            }

            if (_playbackVm.CanPause)
            {
                // 正在播放 → 暂停
                _playbackVm.ExecutePause(null);
            }
            else if (_playbackVm.CanPlay)
            {
                // 已停止或已暂停 → 播放
                _playbackVm.ExecutePlay(null);
            }
            return true;
        }

        /// <summary>
        /// Ctrl+Z：撤销
        /// </summary>
        private bool HandleUndo()
        {
            if (_undoRedoVm == null || !_undoRedoVm.CanUndo)
            {
                return false;
            }
            _undoRedoVm.UndoCommand.Execute(null);
            return true;
        }

        /// <summary>
        /// Ctrl+Y / Ctrl+Shift+Z：重做
        /// </summary>
        private bool HandleRedo()
        {
            if (_undoRedoVm == null || !_undoRedoVm.CanRedo)
            {
                return false;
            }
            _undoRedoVm.RedoCommand.Execute(null);
            return true;
        }

        /// <summary>
        /// Delete：删除选中片段
        /// </summary>
        private bool HandleDeleteSelected()
        {
            if (_trackVm == null)
            {
                return false;
            }
            var cmd = _trackVm.DeleteSelectedCommand;
            if (cmd != null && cmd.CanExecute(null))
            {
                cmd.Execute(null);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Left/Right：微移播放头（步进随缩放级别调整）
        /// </summary>
        /// <param name="direction">-1 左移，+1 右移</param>
        private bool HandleMovePlayhead(int direction)
        {
            if (_timelineVm == null || _waveformVm == null)
            {
                return false;
            }

            // 步进 = 步进像素数 × 每像素采样数，自适应缩放级别
            long step = (long)(_playheadStepPixels * _waveformVm.SamplesPerPixel);
            if (step < 1)
            {
                step = 1;
            }

            _timelineVm.PlayheadSample = _timelineVm.PlayheadSample + direction * step;
            return true;
        }

        /// <summary>
        /// Home：跳转到起始位置
        /// </summary>
        private bool HandleJumpToStart()
        {
            if (_timelineVm == null)
            {
                return false;
            }
            _timelineVm.PlayheadSample = 0;
            return true;
        }

        /// <summary>
        /// End：跳转到结束位置
        /// </summary>
        private bool HandleJumpToEnd()
        {
            if (_timelineVm == null || _waveformVm == null)
            {
                return false;
            }
            _timelineVm.PlayheadSample = _waveformVm.TotalSamples;
            return true;
        }

        /// <summary>
        /// Ctrl+A：全选
        /// </summary>
        private bool HandleSelectAll()
        {
            if (_selectionVm == null)
            {
                return false;
            }
            _selectionVm.SelectAll();
            return true;
        }
    }
}
