using System;
using System.Collections.Generic;
using Xunit;
using MediaTrans.Models;
using MediaTrans.Services;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 批量任务队列测试
    /// </summary>
    public class ConversionQueueServiceTests
    {
        private ConversionQueueService CreateQueueService(int maxParallel)
        {
            var configService = new ConfigService();
            var config = configService.Load();
            var ffmpegService = new FFmpegService(config);
            var conversionService = new ConversionService(ffmpegService, configService);
            return new ConversionQueueService(conversionService, maxParallel);
        }

        private ConversionTask CreateTestTask(string name)
        {
            var task = new ConversionTask();
            task.SourceFile = new MediaFileInfo
            {
                FilePath = string.Format(@"C:\test\{0}", name),
                FileName = name
            };
            task.OutputPath = string.Format(@"C:\test\output\{0}.mp4", System.IO.Path.GetFileNameWithoutExtension(name));
            task.TargetFormat = ".mp4";
            return task;
        }

        #region Enqueue 测试

        [Fact]
        public void Enqueue_SingleTask_AddsToAllTasks()
        {
            var queue = CreateQueueService(1);
            var task = CreateTestTask("test.avi");

            queue.Enqueue(task);

            Assert.Equal(1, queue.AllTasks.Count);
            Assert.Equal(1, queue.PendingCount);
        }

        [Fact]
        public void EnqueueRange_MultipleTasks_AddsAll()
        {
            var queue = CreateQueueService(1);
            var tasks = new List<ConversionTask>
            {
                CreateTestTask("test1.avi"),
                CreateTestTask("test2.avi"),
                CreateTestTask("test3.avi")
            };

            queue.EnqueueRange(tasks);

            Assert.Equal(3, queue.AllTasks.Count);
            Assert.Equal(3, queue.PendingCount);
        }

        #endregion

        #region MaxParallelTasks 测试

        [Fact]
        public void MaxParallelTasks_SetToZero_ClampsToOne()
        {
            var queue = CreateQueueService(1);
            queue.MaxParallelTasks = 0;
            Assert.Equal(1, queue.MaxParallelTasks);
        }

        [Fact]
        public void MaxParallelTasks_SetNegative_ClampsToOne()
        {
            var queue = CreateQueueService(1);
            queue.MaxParallelTasks = -5;
            Assert.Equal(1, queue.MaxParallelTasks);
        }

        [Fact]
        public void MaxParallelTasks_Default_IsSet()
        {
            var queue = CreateQueueService(2);
            Assert.Equal(2, queue.MaxParallelTasks);
        }

        #endregion

        #region CancelTask 测试

        [Fact]
        public void CancelTask_PendingTask_SetsToCancelled()
        {
            var queue = CreateQueueService(1);
            var task = CreateTestTask("test.avi");
            queue.Enqueue(task);

            queue.CancelTask(task.Id);

            Assert.Equal(ConversionStatus.Cancelled, task.Status);
            Assert.Equal("已取消", task.StatusText);
        }

        #endregion

        #region CancelAll 测试

        [Fact]
        public void CancelAll_PendingTasks_AllCancelled()
        {
            var queue = CreateQueueService(1);
            var tasks = new List<ConversionTask>
            {
                CreateTestTask("test1.avi"),
                CreateTestTask("test2.avi"),
                CreateTestTask("test3.avi")
            };
            queue.EnqueueRange(tasks);

            queue.CancelAll();

            foreach (var task in tasks)
            {
                Assert.Equal(ConversionStatus.Cancelled, task.Status);
            }
            Assert.False(queue.IsRunning);
        }

        #endregion

        #region ClearCompleted 测试

        [Fact]
        public void ClearCompleted_RemovesCompletedTasks()
        {
            var queue = CreateQueueService(1);
            var task1 = CreateTestTask("test1.avi");
            task1.Status = ConversionStatus.Completed;
            var task2 = CreateTestTask("test2.avi");
            task2.Status = ConversionStatus.Pending;
            var task3 = CreateTestTask("test3.avi");
            task3.Status = ConversionStatus.Failed;

            queue.AllTasks.Add(task1);
            queue.AllTasks.Add(task2);
            queue.AllTasks.Add(task3);

            queue.ClearCompleted();

            Assert.Equal(1, queue.AllTasks.Count);
            Assert.Equal(ConversionStatus.Pending, queue.AllTasks[0].Status);
        }

        #endregion

        #region 状态测试

        [Fact]
        public void InitialState_NotRunning()
        {
            var queue = CreateQueueService(1);
            Assert.False(queue.IsRunning);
            Assert.False(queue.IsPaused);
        }

        [Fact]
        public void Pause_SetsPaused()
        {
            var queue = CreateQueueService(1);
            queue.Pause();
            Assert.True(queue.IsPaused);
        }

        [Fact]
        public void Resume_ClearsPaused()
        {
            var queue = CreateQueueService(1);
            queue.Pause();
            Assert.True(queue.IsPaused);
            queue.Resume();
            Assert.False(queue.IsPaused);
        }

        #endregion

        #region ConversionTaskCompletedEventArgs 测试

        [Fact]
        public void ConversionTaskCompletedEventArgs_StoresTaskAndResult()
        {
            var task = CreateTestTask("test.avi");
            var result = new FFmpegResult { Success = true };
            var args = new ConversionTaskCompletedEventArgs(task, result);

            Assert.Same(task, args.Task);
            Assert.Same(result, args.Result);
        }

        #endregion
    }
}
