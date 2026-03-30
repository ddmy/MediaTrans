using System;
using System.Windows.Input;
using MediaTrans.Commands;
using MediaTrans.Services;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 撤销/重做 ViewModel — 为 UI 提供撤销/重做命令绑定
    /// </summary>
    public class UndoRedoViewModel : ViewModelBase
    {
        private readonly UndoRedoService _undoRedoService;
        private string _statusText;

        /// <summary>
        /// 撤销命令
        /// </summary>
        public ICommand UndoCommand { get; private set; }

        /// <summary>
        /// 重做命令
        /// </summary>
        public ICommand RedoCommand { get; private set; }

        /// <summary>
        /// 清空历史命令
        /// </summary>
        public ICommand ClearHistoryCommand { get; private set; }

        /// <summary>
        /// 是否可撤销
        /// </summary>
        public bool CanUndo
        {
            get { return _undoRedoService.CanUndo; }
        }

        /// <summary>
        /// 是否可重做
        /// </summary>
        public bool CanRedo
        {
            get { return _undoRedoService.CanRedo; }
        }

        /// <summary>
        /// 撤销栈深度
        /// </summary>
        public int UndoCount
        {
            get { return _undoRedoService.UndoCount; }
        }

        /// <summary>
        /// 重做栈深度
        /// </summary>
        public int RedoCount
        {
            get { return _undoRedoService.RedoCount; }
        }

        /// <summary>
        /// 状态文本（显示当前可撤销/重做操作描述）
        /// </summary>
        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value, "StatusText"); }
        }

        /// <summary>
        /// 底层撤销/重做服务引用
        /// </summary>
        public UndoRedoService Service
        {
            get { return _undoRedoService; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="undoRedoService">撤销/重做服务</param>
        public UndoRedoViewModel(UndoRedoService undoRedoService)
        {
            if (undoRedoService == null)
            {
                throw new ArgumentNullException("undoRedoService");
            }
            _undoRedoService = undoRedoService;
            _statusText = "无操作历史";

            UndoCommand = new RelayCommand(
                new Action<object>(ExecuteUndo),
                new Func<object, bool>(o => CanUndo));

            RedoCommand = new RelayCommand(
                new Action<object>(ExecuteRedo),
                new Func<object, bool>(o => CanRedo));

            ClearHistoryCommand = new RelayCommand(
                new Action<object>(ExecuteClearHistory),
                new Func<object, bool>(o => CanUndo || CanRedo));

            _undoRedoService.StateChanged += OnServiceStateChanged;
        }

        /// <summary>
        /// 执行命令并记录到撤销栈
        /// </summary>
        /// <param name="command">可撤销命令</param>
        public void ExecuteCommand(IUndoableCommand command)
        {
            _undoRedoService.ExecuteCommand(command);
        }

        /// <summary>
        /// 执行撤销
        /// </summary>
        private void ExecuteUndo(object parameter)
        {
            _undoRedoService.Undo();
        }

        /// <summary>
        /// 执行重做
        /// </summary>
        private void ExecuteRedo(object parameter)
        {
            _undoRedoService.Redo();
        }

        /// <summary>
        /// 清空历史
        /// </summary>
        private void ExecuteClearHistory(object parameter)
        {
            _undoRedoService.Clear();
        }

        /// <summary>
        /// 服务状态变化回调
        /// </summary>
        private void OnServiceStateChanged(object sender, EventArgs e)
        {
            OnPropertyChanged("CanUndo");
            OnPropertyChanged("CanRedo");
            OnPropertyChanged("UndoCount");
            OnPropertyChanged("RedoCount");
            UpdateStatusText();
        }

        /// <summary>
        /// 更新状态文本
        /// </summary>
        private void UpdateStatusText()
        {
            if (_undoRedoService.CanUndo)
            {
                StatusText = string.Format("撤销: {0} ({1}步)",
                    _undoRedoService.UndoDescription, _undoRedoService.UndoCount);
            }
            else
            {
                StatusText = "无操作历史";
            }
        }
    }
}
