using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using MediaTrans.Models;

namespace MediaTrans.Services
{
    /// <summary>
    /// 批量转换任务队列管理器
    /// </summary>
    public class ConversionQueueService
    {
        private readonly ConversionService _conversionService;
        private readonly Queue<ConversionTask> _pendingQueue;
        private readonly List<ConversionTask> _activeTasks;
        private readonly object _lock;
        private CancellationTokenSource _globalCts;
        private readonly Dictionary<string, CancellationTokenSource> _taskCtsMap;
        private int _maxParallelTasks;
        private bool _isPaused;
        private bool _isRunning;

        /// <summary>
        /// 所有任务列表（含已完成）
        /// </summary>
        public ObservableCollection<ConversionTask> AllTasks { get; private set; }

        /// <summary>
        /// 当前正在执行的任务数
        /// </summary>
        public int ActiveCount
        {
            get
            {
                lock (_lock)
                {
                    return _activeTasks.Count;
                }
            }
        }

        /// <summary>
        /// 等待中的任务数
        /// </summary>
        public int PendingCount
        {
            get
            {
                lock (_lock)
                {
                    return _pendingQueue.Count;
                }
            }
        }

        /// <summary>
        /// 队列是否暂停
        /// </summary>
        public bool IsPaused
        {
            get { return _isPaused; }
        }

        /// <summary>
        /// 队列是否正在运行
        /// </summary>
        public bool IsRunning
        {
            get { return _isRunning; }
        }

        /// <summary>
        /// 最大并行任务数
        /// </summary>
        public int MaxParallelTasks
        {
            get { return _maxParallelTasks; }
            set
            {
                if (value < 1) value = 1;
                _maxParallelTasks = value;
            }
        }

        /// <summary>
        /// 单个任务完成时触发
        /// </summary>
        public event EventHandler<ConversionTaskCompletedEventArgs> TaskCompleted;

        /// <summary>
        /// 所有任务完成时触发
        /// </summary>
        public event EventHandler AllTasksCompleted;

        /// <summary>
        /// 进度更新时触发
        /// </summary>
        public event EventHandler<FFmpegProgressEventArgs> ProgressChanged;

        public ConversionQueueService(ConversionService conversionService, int maxParallelTasks)
        {
            _conversionService = conversionService;
            _maxParallelTasks = maxParallelTasks;
            _pendingQueue = new Queue<ConversionTask>();
            _activeTasks = new List<ConversionTask>();
            _taskCtsMap = new Dictionary<string, CancellationTokenSource>();
            _lock = new object();
            AllTasks = new ObservableCollection<ConversionTask>();
        }

        /// <summary>
        /// 添加任务到队列
        /// </summary>
        public void Enqueue(ConversionTask task)
        {
            lock (_lock)
            {
                _pendingQueue.Enqueue(task);
                AllTasks.Add(task);
            }
        }

        /// <summary>
        /// 批量添加任务到队列
        /// </summary>
        public void EnqueueRange(IEnumerable<ConversionTask> tasks)
        {
            lock (_lock)
            {
                foreach (var task in tasks)
                {
                    _pendingQueue.Enqueue(task);
                    AllTasks.Add(task);
                }
            }
        }

        /// <summary>
        /// 开始执行队列
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _isPaused = false;
            _globalCts = new CancellationTokenSource();

            ProcessQueue();
        }

        /// <summary>
        /// 暂停队列（当前执行的任务继续，不再启动新任务）
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
        }

        /// <summary>
        /// 恢复队列
        /// </summary>
        public void Resume()
        {
            _isPaused = false;
            ProcessQueue();
        }

        /// <summary>
        /// 取消单个任务
        /// </summary>
        public void CancelTask(string taskId)
        {
            lock (_lock)
            {
                CancellationTokenSource cts;
                if (_taskCtsMap.TryGetValue(taskId, out cts))
                {
                    cts.Cancel();
                }
                else
                {
                    // 如果任务还在等待队列中，标记为取消
                    foreach (var task in AllTasks)
                    {
                        if (task.Id == taskId && task.Status == ConversionStatus.Pending)
                        {
                            task.Status = ConversionStatus.Cancelled;
                            task.StatusText = "已取消";
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 取消所有任务
        /// </summary>
        public void CancelAll()
        {
            lock (_lock)
            {
                // 取消全局令牌
                if (_globalCts != null)
                {
                    _globalCts.Cancel();
                }

                // 取消所有活跃任务
                foreach (var pair in _taskCtsMap)
                {
                    pair.Value.Cancel();
                }

                // 标记所有等待中的任务为取消
                while (_pendingQueue.Count > 0)
                {
                    var task = _pendingQueue.Dequeue();
                    if (task.Status == ConversionStatus.Pending)
                    {
                        task.Status = ConversionStatus.Cancelled;
                        task.StatusText = "已取消";
                    }
                }

                _isRunning = false;
            }
        }

        /// <summary>
        /// 清空已完成/已取消/已失败的任务
        /// </summary>
        public void ClearCompleted()
        {
            lock (_lock)
            {
                for (int i = AllTasks.Count - 1; i >= 0; i--)
                {
                    var status = AllTasks[i].Status;
                    if (status == ConversionStatus.Completed ||
                        status == ConversionStatus.Cancelled ||
                        status == ConversionStatus.Failed)
                    {
                        AllTasks.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// 处理队列 — 启动待执行的任务（不超过最大并行数）
        /// </summary>
        private void ProcessQueue()
        {
            lock (_lock)
            {
                if (_isPaused || !_isRunning) return;
                if (_globalCts != null && _globalCts.IsCancellationRequested) return;

                while (_activeTasks.Count < _maxParallelTasks && _pendingQueue.Count > 0)
                {
                    var task = _pendingQueue.Dequeue();

                    // 跳过已取消的任务
                    if (task.Status == ConversionStatus.Cancelled)
                    {
                        continue;
                    }

                    _activeTasks.Add(task);
                    StartTask(task);
                }
            }
        }

        /// <summary>
        /// 启动单个转换任务
        /// </summary>
        private void StartTask(ConversionTask task)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
            lock (_lock)
            {
                _taskCtsMap[task.Id] = cts;
            }

            // 监听进度
            EventHandler<FFmpegProgressEventArgs> progressHandler = null;
            progressHandler = (s, e) =>
            {
                var handler = ProgressChanged;
                if (handler != null)
                {
                    handler(this, e);
                }
            };

            _conversionService.ProgressChanged += progressHandler;

            _conversionService.ConvertAsync(task, cts.Token).ContinueWith(t =>
            {
                _conversionService.ProgressChanged -= progressHandler;

                lock (_lock)
                {
                    _activeTasks.Remove(task);
                    _taskCtsMap.Remove(task.Id);
                }

                // 触发任务完成事件
                var completedHandler = TaskCompleted;
                if (completedHandler != null)
                {
                    var result = t.IsFaulted ? null : t.Result;
                    completedHandler(this, new ConversionTaskCompletedEventArgs(task, result));
                }

                // 继续处理队列中的下一个任务
                ProcessQueue();

                // 检查是否所有任务都完成了
                lock (_lock)
                {
                    if (_activeTasks.Count == 0 && _pendingQueue.Count == 0)
                    {
                        _isRunning = false;
                        var allDoneHandler = AllTasksCompleted;
                        if (allDoneHandler != null)
                        {
                            allDoneHandler(this, EventArgs.Empty);
                        }
                    }
                }
            });
        }
    }

    /// <summary>
    /// 任务完成事件参数
    /// </summary>
    public class ConversionTaskCompletedEventArgs : EventArgs
    {
        public ConversionTask Task { get; private set; }
        public FFmpegResult Result { get; private set; }

        public ConversionTaskCompletedEventArgs(ConversionTask task, FFmpegResult result)
        {
            Task = task;
            Result = result;
        }
    }
}
