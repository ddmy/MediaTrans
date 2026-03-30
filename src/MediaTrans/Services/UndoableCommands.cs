using System;
using MediaTrans.Models;
using MediaTrans.ViewModels;

namespace MediaTrans.Services
{
    /// <summary>
    /// 裁剪区间变更命令 — 记录选区起止位置的变化
    /// </summary>
    public class SelectionChangeCommand : IUndoableCommand
    {
        private readonly SelectionViewModel _selectionVm;
        private readonly long _oldStart;
        private readonly long _oldEnd;
        private readonly long _newStart;
        private readonly long _newEnd;

        public string Description
        {
            get { return "裁剪区间变更"; }
        }

        /// <summary>
        /// 构造选区变更命令
        /// </summary>
        /// <param name="selectionVm">选区 ViewModel</param>
        /// <param name="oldStart">旧起点采样帧</param>
        /// <param name="oldEnd">旧终点采样帧</param>
        /// <param name="newStart">新起点采样帧</param>
        /// <param name="newEnd">新终点采样帧</param>
        public SelectionChangeCommand(SelectionViewModel selectionVm,
            long oldStart, long oldEnd, long newStart, long newEnd)
        {
            if (selectionVm == null)
            {
                throw new ArgumentNullException("selectionVm");
            }
            _selectionVm = selectionVm;
            _oldStart = oldStart;
            _oldEnd = oldEnd;
            _newStart = newStart;
            _newEnd = newEnd;
        }

        public void Execute()
        {
            // 先扩大范围再缩小，避免 EnsureOrder 交换
            if (_newEnd >= _newStart)
            {
                _selectionVm.SelectionEndSample = _newEnd;
                _selectionVm.SelectionStartSample = _newStart;
            }
            else
            {
                _selectionVm.SelectionStartSample = _newStart;
                _selectionVm.SelectionEndSample = _newEnd;
            }
        }

        public void Undo()
        {
            if (_oldEnd >= _oldStart)
            {
                // 先设为 0（或小值）以避免交换
                _selectionVm.SelectionStartSample = Math.Min(_oldStart, _oldEnd);
                _selectionVm.SelectionEndSample = _oldEnd;
                _selectionVm.SelectionStartSample = _oldStart;
            }
            else
            {
                _selectionVm.SelectionEndSample = _oldEnd;
                _selectionVm.SelectionStartSample = _oldStart;
            }
        }
    }

    /// <summary>
    /// 片段添加命令
    /// </summary>
    public class ClipAddCommand : IUndoableCommand
    {
        private readonly TimelineTrackViewModel _trackVm;
        private readonly TimelineClip _clip;
        private readonly int _insertIndex;
        private readonly bool _isInsert;

        public string Description
        {
            get { return "添加片段"; }
        }

        /// <summary>
        /// 添加到末尾
        /// </summary>
        public ClipAddCommand(TimelineTrackViewModel trackVm, TimelineClip clip)
        {
            if (trackVm == null) throw new ArgumentNullException("trackVm");
            if (clip == null) throw new ArgumentNullException("clip");
            _trackVm = trackVm;
            _clip = clip;
            _insertIndex = -1;
            _isInsert = false;
        }

        /// <summary>
        /// 插入到指定位置
        /// </summary>
        public ClipAddCommand(TimelineTrackViewModel trackVm, TimelineClip clip, int insertIndex)
        {
            if (trackVm == null) throw new ArgumentNullException("trackVm");
            if (clip == null) throw new ArgumentNullException("clip");
            _trackVm = trackVm;
            _clip = clip;
            _insertIndex = insertIndex;
            _isInsert = true;
        }

        public void Execute()
        {
            if (_isInsert)
            {
                _trackVm.InsertClip(_insertIndex, _clip);
            }
            else
            {
                _trackVm.AddClip(_clip);
            }
        }

        public void Undo()
        {
            _trackVm.RemoveClip(_clip);
        }
    }

    /// <summary>
    /// 片段删除命令
    /// </summary>
    public class ClipRemoveCommand : IUndoableCommand
    {
        private readonly TimelineTrackViewModel _trackVm;
        private readonly TimelineClip _clip;
        private readonly int _index;

        public string Description
        {
            get { return "删除片段"; }
        }

        /// <summary>
        /// 构造删除命令
        /// </summary>
        /// <param name="trackVm">时间轨道 ViewModel</param>
        /// <param name="clip">要删除的片段</param>
        /// <param name="index">片段在列表中的索引</param>
        public ClipRemoveCommand(TimelineTrackViewModel trackVm, TimelineClip clip, int index)
        {
            if (trackVm == null) throw new ArgumentNullException("trackVm");
            if (clip == null) throw new ArgumentNullException("clip");
            _trackVm = trackVm;
            _clip = clip;
            _index = index;
        }

        public void Execute()
        {
            _trackVm.RemoveClip(_clip);
        }

        public void Undo()
        {
            _trackVm.InsertClip(_index, _clip);
        }
    }

    /// <summary>
    /// 片段移动命令
    /// </summary>
    public class ClipMoveCommand : IUndoableCommand
    {
        private readonly TimelineTrackViewModel _trackVm;
        private readonly int _oldIndex;
        private readonly int _newIndex;

        public string Description
        {
            get { return "移动片段"; }
        }

        public ClipMoveCommand(TimelineTrackViewModel trackVm, int oldIndex, int newIndex)
        {
            if (trackVm == null) throw new ArgumentNullException("trackVm");
            _trackVm = trackVm;
            _oldIndex = oldIndex;
            _newIndex = newIndex;
        }

        public void Execute()
        {
            _trackVm.MoveClip(_oldIndex, _newIndex);
        }

        public void Undo()
        {
            _trackVm.MoveClip(_newIndex, _oldIndex);
        }
    }

    /// <summary>
    /// 增益调节命令
    /// </summary>
    public class GainChangeCommand : IUndoableCommand
    {
        private readonly GainViewModel _gainVm;
        private readonly double _oldGainDb;
        private readonly double _newGainDb;

        public string Description
        {
            get
            {
                return string.Format("增益调节 {0}", GainService.FormatGainText(_newGainDb));
            }
        }

        public GainChangeCommand(GainViewModel gainVm, double oldGainDb, double newGainDb)
        {
            if (gainVm == null) throw new ArgumentNullException("gainVm");
            _gainVm = gainVm;
            _oldGainDb = oldGainDb;
            _newGainDb = newGainDb;
        }

        public void Execute()
        {
            _gainVm.GainDb = _newGainDb;
        }

        public void Undo()
        {
            _gainVm.GainDb = _oldGainDb;
        }
    }
}
