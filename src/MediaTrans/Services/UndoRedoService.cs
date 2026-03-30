using System;
using System.Collections.Generic;

namespace MediaTrans.Services
{
    /// <summary>
    /// 撤销/重做服务 — 管理命令栈
    /// </summary>
    public class UndoRedoService
    {
        private readonly List<IUndoableCommand> _undoStack;
        private readonly List<IUndoableCommand> _redoStack;
        private int _maxDepth;

        /// <summary>
        /// 栈状态变化事件
        /// </summary>
        public event EventHandler StateChanged;

        /// <summary>
        /// 最大撤销深度
        /// </summary>
        public int MaxDepth
        {
            get { return _maxDepth; }
            set { _maxDepth = value > 1 ? value : 1; }
        }

        /// <summary>
        /// 是否可撤销
        /// </summary>
        public bool CanUndo
        {
            get { return _undoStack.Count > 0; }
        }

        /// <summary>
        /// 是否可重做
        /// </summary>
        public bool CanRedo
        {
            get { return _redoStack.Count > 0; }
        }

        /// <summary>
        /// 撤销栈深度
        /// </summary>
        public int UndoCount
        {
            get { return _undoStack.Count; }
        }

        /// <summary>
        /// 重做栈深度
        /// </summary>
        public int RedoCount
        {
            get { return _redoStack.Count; }
        }

        /// <summary>
        /// 下一个可撤销命令的描述（如果有）
        /// </summary>
        public string UndoDescription
        {
            get
            {
                if (_undoStack.Count == 0) return string.Empty;
                return _undoStack[_undoStack.Count - 1].Description;
            }
        }

        /// <summary>
        /// 下一个可重做命令的描述（如果有）
        /// </summary>
        public string RedoDescription
        {
            get
            {
                if (_redoStack.Count == 0) return string.Empty;
                return _redoStack[_redoStack.Count - 1].Description;
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="maxDepth">最大撤销深度</param>
        public UndoRedoService(int maxDepth)
        {
            _maxDepth = maxDepth > 1 ? maxDepth : 50;
            _undoStack = new List<IUndoableCommand>();
            _redoStack = new List<IUndoableCommand>();
        }

        /// <summary>
        /// 默认构造（50 步深度）
        /// </summary>
        public UndoRedoService() : this(50)
        {
        }

        /// <summary>
        /// 执行命令并压入撤销栈
        /// </summary>
        /// <param name="command">要执行的命令</param>
        public void ExecuteCommand(IUndoableCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            command.Execute();
            _undoStack.Add(command);

            // 新命令执行后清空重做栈
            _redoStack.Clear();

            // 超出深度时移除最早的命令
            while (_undoStack.Count > _maxDepth)
            {
                _undoStack.RemoveAt(0);
            }

            RaiseStateChanged();
        }

        /// <summary>
        /// 撤销最后一个命令
        /// </summary>
        /// <returns>是否成功撤销</returns>
        public bool Undo()
        {
            if (_undoStack.Count == 0)
            {
                return false;
            }

            int lastIndex = _undoStack.Count - 1;
            IUndoableCommand command = _undoStack[lastIndex];
            _undoStack.RemoveAt(lastIndex);

            command.Undo();
            _redoStack.Add(command);

            RaiseStateChanged();
            return true;
        }

        /// <summary>
        /// 重做最后撤销的命令
        /// </summary>
        /// <returns>是否成功重做</returns>
        public bool Redo()
        {
            if (_redoStack.Count == 0)
            {
                return false;
            }

            int lastIndex = _redoStack.Count - 1;
            IUndoableCommand command = _redoStack[lastIndex];
            _redoStack.RemoveAt(lastIndex);

            command.Execute();
            _undoStack.Add(command);

            // 超出深度时移除最早的命令
            while (_undoStack.Count > _maxDepth)
            {
                _undoStack.RemoveAt(0);
            }

            RaiseStateChanged();
            return true;
        }

        /// <summary>
        /// 清空所有撤销/重做记录
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            RaiseStateChanged();
        }

        /// <summary>
        /// 获取撤销栈中所有命令描述（从最新到最旧）
        /// </summary>
        public List<string> GetUndoDescriptions()
        {
            var result = new List<string>();
            for (int i = _undoStack.Count - 1; i >= 0; i--)
            {
                result.Add(_undoStack[i].Description);
            }
            return result;
        }

        /// <summary>
        /// 获取重做栈中所有命令描述（从最新到最旧）
        /// </summary>
        public List<string> GetRedoDescriptions()
        {
            var result = new List<string>();
            for (int i = _redoStack.Count - 1; i >= 0; i--)
            {
                result.Add(_redoStack[i].Description);
            }
            return result;
        }

        /// <summary>
        /// 触发状态变化事件
        /// </summary>
        private void RaiseStateChanged()
        {
            EventHandler handler = StateChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
