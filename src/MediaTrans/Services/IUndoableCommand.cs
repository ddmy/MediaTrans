using System;

namespace MediaTrans.Services
{
    /// <summary>
    /// 可撤销命令接口 — Command 模式核心
    /// </summary>
    public interface IUndoableCommand
    {
        /// <summary>
        /// 命令描述（用于 UI 显示）
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 执行命令
        /// </summary>
        void Execute();

        /// <summary>
        /// 撤销命令
        /// </summary>
        void Undo();
    }
}
