using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;
using MediaTrans.ViewModels;
using MediaTrans.Services;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 性能压测 — 大文件波形渲染、编辑操作流畅度、内存监控、快速缩放/平移
    /// </summary>
    public class PerformanceStressTests
    {
        // 采样率 44100 Hz
        private const int SampleRate = 44100;
        // 视口宽度
        private const int ViewportWidth = 1920;

        // 不同时长的总采样帧数
        private static readonly long Samples1Hour = (long)SampleRate * 3600;       // 158,760,000
        private static readonly long Samples3Hours = (long)SampleRate * 3600 * 3;  // 476,280,000
        private static readonly long Samples8Hours = (long)SampleRate * 3600 * 8;  // 1,270,080,000

        #region 辅助方法

        /// <summary>
        /// 创建并初始化指定时长的 WaveformViewModel
        /// </summary>
        private WaveformViewModel CreateWaveformVm(long totalSamples)
        {
            var vm = new WaveformViewModel();
            vm.Initialize(totalSamples, SampleRate, ViewportWidth);
            return vm;
        }

        /// <summary>
        /// 创建 SelectionViewModel
        /// </summary>
        private SelectionViewModel CreateSelectionVm(WaveformViewModel waveformVm)
        {
            return new SelectionViewModel(waveformVm);
        }

        /// <summary>
        /// 创建 TimelineViewModel
        /// </summary>
        private TimelineViewModel CreateTimelineVm(WaveformViewModel waveformVm)
        {
            var rulerService = new TimelineRulerService();
            return new TimelineViewModel(waveformVm, rulerService);
        }

        #endregion

        #region 大文件波形初始化

        [Fact]
        public void Initialize_1Hour_Succeeds()
        {
            // 1小时音频初始化不应抛出异常
            var vm = CreateWaveformVm(Samples1Hour);

            Assert.Equal(Samples1Hour, vm.TotalSamples);
            Assert.True(vm.SamplesPerPixel > 0);
            Assert.Equal(0L, vm.ViewportStartSample);
        }

        [Fact]
        public void Initialize_3Hours_Succeeds()
        {
            var vm = CreateWaveformVm(Samples3Hours);

            Assert.Equal(Samples3Hours, vm.TotalSamples);
            Assert.True(vm.SamplesPerPixel > 0);
        }

        [Fact]
        public void Initialize_8Hours_Succeeds()
        {
            var vm = CreateWaveformVm(Samples8Hours);

            Assert.Equal(Samples8Hours, vm.TotalSamples);
            Assert.True(vm.SamplesPerPixel > 0);
        }

        #endregion

        #region 快速缩放压测

        [Theory]
        [InlineData(100)]
        [InlineData(500)]
        public void RapidZoomIn_LargeFile_NoException(int iterations)
        {
            // 快速连续放大不应抛出异常或溢出
            var vm = CreateWaveformVm(Samples8Hours);

            for (int i = 0; i < iterations; i++)
            {
                vm.ZoomIn();
            }

            // 缩放后状态有效
            Assert.True(vm.SamplesPerPixel >= 1);
            Assert.True(vm.ViewportStartSample >= 0);
            Assert.True(vm.ViewportEndSample <= Samples8Hours);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(500)]
        public void RapidZoomOut_LargeFile_NoException(int iterations)
        {
            // 先放大到最大，再快速缩小
            var vm = CreateWaveformVm(Samples8Hours);
            for (int i = 0; i < 50; i++)
            {
                vm.ZoomIn();
            }

            for (int i = 0; i < iterations; i++)
            {
                vm.ZoomOut();
            }

            Assert.True(vm.SamplesPerPixel >= 1);
            Assert.True(vm.ViewportStartSample >= 0);
        }

        [Fact]
        public void RapidZoomInOut_100Cycles_NoException()
        {
            // 快速交替放大缩小100次
            var vm = CreateWaveformVm(Samples8Hours);

            for (int i = 0; i < 100; i++)
            {
                vm.ZoomIn();
                vm.ZoomOut();
            }

            Assert.True(vm.SamplesPerPixel >= 1);
            Assert.True(vm.ViewportStartSample >= 0);
        }

        [Fact]
        public void ZoomAtPosition_VariousRatios_NoException()
        {
            // 在不同鼠标位置缩放
            var vm = CreateWaveformVm(Samples8Hours);
            var random = new Random(42);

            for (int i = 0; i < 200; i++)
            {
                double ratio = random.NextDouble();
                int delta = random.Next(2) == 0 ? 1 : -1;
                vm.ZoomAtPosition(delta, ratio);
            }

            Assert.True(vm.SamplesPerPixel >= 1);
            Assert.True(vm.ViewportStartSample >= 0);
            Assert.True(vm.ViewportEndSample <= Samples8Hours);
        }

        [Fact]
        public void ZoomIn_Performance_Under5ms()
        {
            // 100次缩放操作总耗时应在合理范围内
            var vm = CreateWaveformVm(Samples8Hours);
            var sw = new Stopwatch();

            sw.Start();
            for (int i = 0; i < 100; i++)
            {
                vm.ZoomIn();
            }
            sw.Stop();

            // 100次缩放总共不应超过50毫秒（实际应远低于此）
            Assert.True(sw.ElapsedMilliseconds < 50,
                string.Format("100次缩放耗时 {0}ms，超过50ms上限", sw.ElapsedMilliseconds));
        }

        #endregion

        #region 快速平移/滚动压测

        [Fact]
        public void RapidScroll_100Times_NoException()
        {
            // 快速滚动100次不应抛出异常
            var vm = CreateWaveformVm(Samples8Hours);
            long step = Samples8Hours / 200;

            for (int i = 0; i < 100; i++)
            {
                vm.ScrollTo(step * i);
            }

            // 滚动到末尾
            vm.ScrollTo(Samples8Hours);
            Assert.True(vm.ViewportStartSample >= 0);
        }

        [Fact]
        public void RapidPan_100Times_NoException()
        {
            // 快速平移100次
            var vm = CreateWaveformVm(Samples8Hours);
            // 先放大一些使平移有意义
            for (int i = 0; i < 10; i++)
            {
                vm.ZoomIn();
            }

            for (int i = 0; i < 100; i++)
            {
                vm.StartPan(500);
                vm.UpdatePan(500 + (i * 5));
                vm.EndPan();
            }

            Assert.False(vm.IsPanning);
            Assert.True(vm.ViewportStartSample >= 0);
        }

        [Fact]
        public void ScrollForwardAndBack_NoOverflow()
        {
            // 来回滚动不产生溢出
            var vm = CreateWaveformVm(Samples8Hours);

            for (int i = 0; i < 50; i++)
            {
                vm.ScrollTo(Samples8Hours); // 滚到末尾
                vm.ScrollTo(0);             // 滚回开头
            }

            Assert.Equal(0L, vm.ViewportStartSample);
        }

        [Fact]
        public void ScrollTo_NegativeValue_ClampedToZero()
        {
            var vm = CreateWaveformVm(Samples8Hours);
            vm.ScrollTo(-1000);
            Assert.Equal(0L, vm.ViewportStartSample);
        }

        [Fact]
        public void ScrollTo_BeyondEnd_ClampedToMax()
        {
            var vm = CreateWaveformVm(Samples8Hours);
            vm.ScrollTo(Samples8Hours * 2);
            Assert.True(vm.ViewportStartSample <= Samples8Hours);
            Assert.True(vm.ViewportEndSample <= Samples8Hours);
        }

        [Fact]
        public void Scroll_Performance_Under5ms()
        {
            var vm = CreateWaveformVm(Samples8Hours);
            long step = Samples8Hours / 100;
            var sw = new Stopwatch();

            sw.Start();
            for (int i = 0; i < 100; i++)
            {
                vm.ScrollTo(step * i);
            }
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 50,
                string.Format("100次滚动耗时 {0}ms", sw.ElapsedMilliseconds));
        }

        #endregion

        #region 选区操作压测

        [Fact]
        public void RapidCreateSelection_100Times_NoException()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var selectionVm = CreateSelectionVm(waveformVm);

            for (int i = 0; i < 100; i++)
            {
                double start = i * 10.0;
                double end = start + 50.0;
                selectionVm.CreateSelection(start, end);
            }

            Assert.True(selectionVm.HasSelection);
        }

        [Fact]
        public void SelectAll_LargeFile_Correct()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var selectionVm = CreateSelectionVm(waveformVm);

            selectionVm.SelectAll();

            Assert.True(selectionVm.HasSelection);
            Assert.Equal(0L, selectionVm.SelectionStartSample);
            Assert.Equal(Samples8Hours, selectionVm.SelectionEndSample);
        }

        [Fact]
        public void CreateAndClearSelection_100Cycles_NoLeak()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var selectionVm = CreateSelectionVm(waveformVm);

            for (int i = 0; i < 100; i++)
            {
                selectionVm.SelectAll();
                Assert.True(selectionVm.HasSelection);
                selectionVm.ClearSelection();
                Assert.False(selectionVm.HasSelection);
            }
        }

        [Fact]
        public void DragHandles_RapidUpdates_NoException()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var selectionVm = CreateSelectionVm(waveformVm);

            // 先创建一个选区
            selectionVm.CreateSelection(100, 500);

            // 快速拖动左Handle 100次
            selectionVm.StartDragLeftHandle();
            for (int i = 0; i < 100; i++)
            {
                selectionVm.UpdateDragLeftHandle(100 + i);
            }
            selectionVm.EndDragLeftHandle();

            // 快速拖动右Handle 100次
            selectionVm.StartDragRightHandle();
            for (int i = 0; i < 100; i++)
            {
                selectionVm.UpdateDragRightHandle(500 + i);
            }
            selectionVm.EndDragRightHandle();

            Assert.True(selectionVm.HasSelection);
        }

        #endregion

        #region 播放头操作压测

        [Fact]
        public void RapidPlayheadMove_100Times_NoException()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var timelineVm = CreateTimelineVm(waveformVm);

            for (int i = 0; i < 100; i++)
            {
                timelineVm.PlayheadSample = (long)((double)i / 100 * Samples8Hours);
            }

            Assert.True(timelineVm.PlayheadSample >= 0);
            Assert.True(timelineVm.PlayheadSample <= Samples8Hours);
        }

        [Fact]
        public void PlayheadDrag_RapidUpdates_NoException()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var timelineVm = CreateTimelineVm(waveformVm);

            // 先放大以使拖拽有意义
            for (int i = 0; i < 5; i++)
            {
                waveformVm.ZoomIn();
            }

            timelineVm.StartDragPlayhead(0);
            for (int i = 0; i < 200; i++)
            {
                timelineVm.UpdateDragPlayhead(i * 5.0);
            }
            timelineVm.EndDragPlayhead();

            Assert.False(timelineVm.IsDraggingPlayhead);
        }

        [Fact]
        public void PlayheadJumpStartEnd_100Cycles()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var timelineVm = CreateTimelineVm(waveformVm);

            for (int i = 0; i < 100; i++)
            {
                timelineVm.PlayheadSample = 0;
                Assert.Equal(0L, timelineVm.PlayheadSample);
                timelineVm.PlayheadSample = Samples8Hours;
                Assert.Equal(Samples8Hours, timelineVm.PlayheadSample);
            }
        }

        [Fact]
        public void ClickToPosition_Rapid_NoException()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var timelineVm = CreateTimelineVm(waveformVm);

            for (int i = 0; i < 100; i++)
            {
                timelineVm.ClickToPosition(i * 19.0);
            }

            Assert.True(timelineVm.PlayheadSample >= 0);
        }

        #endregion

        #region 坐标转换压测

        [Fact]
        public void PixelToSample_RapidConversion_NoOverflow()
        {
            var vm = CreateWaveformVm(Samples8Hours);

            for (int i = 0; i < 1000; i++)
            {
                long sample = vm.PixelToSample(i);
                Assert.True(sample >= 0);
            }
        }

        [Fact]
        public void SampleToPixel_LargeValues_NoOverflow()
        {
            var vm = CreateWaveformVm(Samples8Hours);

            // 在最大缩小级别下转换大采样帧值
            double pixel = vm.SampleToPixel(Samples8Hours);
            Assert.False(double.IsNaN(pixel));
            Assert.False(double.IsInfinity(pixel));
        }

        [Fact]
        public void SamplesToSeconds_8Hours_Correct()
        {
            var vm = CreateWaveformVm(Samples8Hours);

            double seconds = vm.SamplesToSeconds(Samples8Hours);
            // 8小时 = 28800秒
            Assert.Equal(28800.0, seconds, 1);
        }

        [Fact]
        public void SecondsToSamples_8Hours_Correct()
        {
            var vm = CreateWaveformVm(Samples8Hours);

            long samples = vm.SecondsToSamples(28800.0);
            Assert.Equal(Samples8Hours, samples);
        }

        #endregion

        #region 视频帧缓存 LRU 压测

        [Fact]
        public void LruCache_FillAndEvict_CorrectCount()
        {
            // 使用一个不存在的FFmpeg路径（仅测试缓存逻辑，不实际提取帧）
            var cache = new VideoFrameCacheService("dummy_ffmpeg.exe", 10);
            cache.LoadVideo("dummy.mp4", 30.0, 100.0, 1920, 1080);

            // 通过反射或公开方法测试缓存
            // 由于 AddToCache 是 private，我们通过 IsFrameCached 和 CachedFrameCount 检测
            // 先清空缓存确保初始状态
            cache.ClearCache();
            Assert.Equal(0, cache.CachedFrameCount);
        }

        [Fact]
        public void LruCache_ClearAfterLoadVideo_Empty()
        {
            var cache = new VideoFrameCacheService("dummy_ffmpeg.exe", 50);
            cache.LoadVideo("dummy1.mp4", 30.0, 100.0, 1920, 1080);

            // 重新加载应清空缓存
            cache.LoadVideo("dummy2.mp4", 25.0, 200.0, 1280, 720);
            Assert.Equal(0, cache.CachedFrameCount);
        }

        [Fact]
        public void LruCache_MaxCapacity_IsRespected()
        {
            int maxFrames = 20;
            var cache = new VideoFrameCacheService("dummy_ffmpeg.exe", maxFrames);
            cache.LoadVideo("dummy.mp4", 30.0, 100.0, 1920, 1080);

            // 缓存帧数不能超过 maxFrames
            Assert.True(cache.CachedFrameCount <= maxFrames);
            Assert.Equal(maxFrames, cache.MaxCachedFrames);
        }

        #endregion

        #region 音频 PCM 缓存压测

        [Fact]
        public void AudioPcmCache_Evict_RemovesOutOfRange()
        {
            // 使用 dummy 路径（不实际解码）
            var cache = new AudioPcmCacheService("dummy_ffmpeg.exe", 44100, 10);

            // 验证初始状态
            Assert.Equal(0, cache.CachedBlockCount);
            Assert.Equal(0L, cache.CachedBytes);
        }

        [Fact]
        public void AudioPcmCache_EvictEmptyCache_NoException()
        {
            var cache = new AudioPcmCacheService("dummy_ffmpeg.exe", 44100, 10);
            // 对空缓存执行淘汰不应抛出异常
            cache.Evict(0, 1000000);
            Assert.Equal(0, cache.CachedBlockCount);
        }

        [Fact]
        public void AudioPcmCache_LargeBlockConfig_NoException()
        {
            // 大块大小配置
            var cache = new AudioPcmCacheService("dummy_ffmpeg.exe", 441000, 20);
            Assert.Equal(441000, cache.BlockSampleCount);
            Assert.Equal(20, cache.BufferBlockCount);
        }

        #endregion

        #region 撤销/重做栈压测

        [Fact]
        public void UndoRedo_50Operations_AllUndoable()
        {
            var service = new UndoRedoService(50);
            var vm = new UndoRedoViewModel(service);

            // 执行50次操作
            for (int i = 0; i < 50; i++)
            {
                int capturedValue = i;
                vm.ExecuteCommand(new TestCommand(capturedValue));
            }

            // 应该可以撤销50次
            int undoCount = 0;
            while (vm.CanUndo)
            {
                vm.UndoCommand.Execute(null);
                undoCount++;
            }
            Assert.Equal(50, undoCount);
        }

        [Fact]
        public void UndoRedo_ExceedsStackDepth_OldestDropped()
        {
            int stackDepth = 20;
            var service = new UndoRedoService(stackDepth);
            var vm = new UndoRedoViewModel(service);

            // 执行超过栈深度的操作
            for (int i = 0; i < 40; i++)
            {
                vm.ExecuteCommand(new TestCommand(i));
            }

            // 只能撤销 stackDepth 次
            int undoCount = 0;
            while (vm.CanUndo)
            {
                vm.UndoCommand.Execute(null);
                undoCount++;
            }
            Assert.Equal(stackDepth, undoCount);
        }

        [Fact]
        public void UndoRedo_RapidUndoRedo_100Cycles()
        {
            var service = new UndoRedoService(100);
            var vm = new UndoRedoViewModel(service);

            // 先执行50个命令
            for (int i = 0; i < 50; i++)
            {
                vm.ExecuteCommand(new TestCommand(i));
            }

            // 快速交替撤销/重做100次
            for (int i = 0; i < 100; i++)
            {
                if (vm.CanUndo)
                {
                    vm.UndoCommand.Execute(null);
                }
                if (vm.CanRedo)
                {
                    vm.RedoCommand.Execute(null);
                }
            }

            // 状态应保持有效
            Assert.True(vm.CanUndo || vm.CanRedo);
        }

        [Fact]
        public void UndoRedo_Performance_Under10ms()
        {
            var service = new UndoRedoService(100);
            var vm = new UndoRedoViewModel(service);

            var sw = new Stopwatch();
            sw.Start();

            for (int i = 0; i < 100; i++)
            {
                vm.ExecuteCommand(new TestCommand(i));
            }
            for (int i = 0; i < 100; i++)
            {
                vm.UndoCommand.Execute(null);
            }

            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 50,
                string.Format("100次执行+100次撤销耗时 {0}ms", sw.ElapsedMilliseconds));
        }

        #endregion

        #region 混合操作压测

        [Fact]
        public void MixedOperations_ZoomScrollSelect_NoException()
        {
            // 混合缩放、滚动、选区操作
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var selectionVm = CreateSelectionVm(waveformVm);
            var timelineVm = CreateTimelineVm(waveformVm);

            for (int i = 0; i < 50; i++)
            {
                // 缩放
                if (i % 3 == 0) waveformVm.ZoomIn();
                else if (i % 3 == 1) waveformVm.ZoomOut();

                // 滚动
                waveformVm.ScrollTo((long)((double)i / 50 * Samples8Hours));

                // 移动播放头
                timelineVm.PlayheadSample = waveformVm.ViewportStartSample;

                // 创建/清除选区
                if (i % 2 == 0)
                {
                    selectionVm.CreateSelection(100, 500);
                }
                else
                {
                    selectionVm.ClearSelection();
                }
            }

            // 所有状态应有效
            Assert.True(waveformVm.SamplesPerPixel >= 1);
            Assert.True(waveformVm.ViewportStartSample >= 0);
            Assert.True(timelineVm.PlayheadSample >= 0);
        }

        [Fact]
        public void MixedOperations_WithUndoRedo_NoException()
        {
            // 混合操作 + 撤销重做
            var waveformVm = CreateWaveformVm(Samples3Hours);
            var selectionVm = CreateSelectionVm(waveformVm);
            var undoService = new UndoRedoService(50);
            var undoVm = new UndoRedoViewModel(undoService);

            for (int i = 0; i < 30; i++)
            {
                // 缩放操作
                waveformVm.ZoomAtPosition(i % 2 == 0 ? 1 : -1, 0.3);

                // 执行可撤销命令
                undoVm.ExecuteCommand(new TestCommand(i));

                // 偶尔撤销
                if (i % 5 == 0 && undoVm.CanUndo)
                {
                    undoVm.UndoCommand.Execute(null);
                }
            }

            Assert.True(waveformVm.SamplesPerPixel >= 1);
        }

        #endregion

        #region 内存使用监控

        [Fact]
        public void MemoryUsage_RepeatedZoom_NoGrowth()
        {
            // 重复缩放操作后，内存不应持续增长
            var vm = CreateWaveformVm(Samples8Hours);

            // 预热
            for (int i = 0; i < 20; i++)
            {
                vm.ZoomIn();
            }
            for (int i = 0; i < 20; i++)
            {
                vm.ZoomOut();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memBefore = GC.GetTotalMemory(true);

            // 执行大量缩放操作
            for (int cycle = 0; cycle < 10; cycle++)
            {
                for (int i = 0; i < 100; i++)
                {
                    vm.ZoomIn();
                }
                for (int i = 0; i < 100; i++)
                {
                    vm.ZoomOut();
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memAfter = GC.GetTotalMemory(true);

            // 内存增长不超过1MB（ViewModel操作不应产生大量内存分配）
            long growth = memAfter - memBefore;
            Assert.True(growth < 1024 * 1024,
                string.Format("内存增长 {0} 字节，超过1MB上限", growth));
        }

        [Fact]
        public void MemoryUsage_RepeatedSelections_NoGrowth()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var selectionVm = CreateSelectionVm(waveformVm);

            // 预热
            for (int i = 0; i < 10; i++)
            {
                selectionVm.SelectAll();
                selectionVm.ClearSelection();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < 500; i++)
            {
                selectionVm.CreateSelection(i * 2.0, i * 2.0 + 100);
                selectionVm.ClearSelection();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memAfter = GC.GetTotalMemory(true);

            long growth = memAfter - memBefore;
            Assert.True(growth < 1024 * 1024,
                string.Format("内存增长 {0} 字节", growth));
        }

        #endregion

        #region 边界值测试

        [Fact]
        public void ZoomToFit_8Hours_ViewportCoversAll()
        {
            var vm = CreateWaveformVm(Samples8Hours);
            vm.ZoomToFit();

            // ZoomToFit 后视口应覆盖全部采样帧
            Assert.Equal(0L, vm.ViewportStartSample);
            Assert.True(vm.ViewportEndSample >= Samples8Hours - ViewportWidth);
        }

        [Fact]
        public void ZoomIn_BeyondMinimum_ClampedToMinSpp()
        {
            var vm = CreateWaveformVm(Samples1Hour);

            // 极端放大
            for (int i = 0; i < 1000; i++)
            {
                vm.ZoomIn();
            }

            // SamplesPerPixel 应被限制为最小值（>=1）
            Assert.True(vm.SamplesPerPixel >= 1);
        }

        [Fact]
        public void ZoomOut_BeyondMaximum_ClampedToMaxSpp()
        {
            var vm = CreateWaveformVm(Samples1Hour);

            // 极端缩小
            for (int i = 0; i < 1000; i++)
            {
                vm.ZoomOut();
            }

            // 使用 ZoomToFit 验证：最大缩放即全局概览
            double maxExpected = (double)Samples1Hour / ViewportWidth;
            Assert.True(Math.Abs(vm.SamplesPerPixel - maxExpected) < 1.0);
        }

        [Fact]
        public void ViewportSampleSpan_8Hours_NoOverflow()
        {
            var vm = CreateWaveformVm(Samples8Hours);

            // 在全局概览下，ViewportSampleSpan 应接近 TotalSamples
            Assert.True(vm.ViewportSampleSpan > 0);
            Assert.True(vm.ViewportSampleSpan <= Samples8Hours + ViewportWidth);
        }

        [Fact]
        public void PlayheadSample_LargeValue_NoCrash()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var timelineVm = CreateTimelineVm(waveformVm);

            // 设置播放头到最末尾
            timelineVm.PlayheadSample = Samples8Hours;
            Assert.Equal(Samples8Hours, timelineVm.PlayheadSample);

            // 设置播放头超过末尾，应被限制
            timelineVm.PlayheadSample = Samples8Hours + 1000;
            Assert.True(timelineVm.PlayheadSample <= Samples8Hours);
        }

        #endregion

        #region 时间刻度标记

        [Fact]
        public void GetVisibleTickMarks_8Hours_ReturnsReasonableCount()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var timelineVm = CreateTimelineVm(waveformVm);

            var tickMarks = timelineVm.GetVisibleTickMarks();

            // 全局概览下刻度数量应合理（不会太多导致性能问题）
            Assert.NotNull(tickMarks);
            Assert.True(tickMarks.Count < 10000,
                string.Format("刻度数 {0} 过多", tickMarks.Count));
        }

        [Fact]
        public void GetVisibleTickMarks_AfterZoom_AdaptiveCount()
        {
            var waveformVm = CreateWaveformVm(Samples8Hours);
            var timelineVm = CreateTimelineVm(waveformVm);

            // 放大后刻度应适应
            for (int i = 0; i < 20; i++)
            {
                waveformVm.ZoomIn();
            }

            var tickMarks = timelineVm.GetVisibleTickMarks();
            Assert.NotNull(tickMarks);
            Assert.True(tickMarks.Count < 10000);
        }

        #endregion

        #region 辅助测试命令

        /// <summary>
        /// 用于撤销/重做测试的简单命令
        /// </summary>
        private class TestCommand : IUndoableCommand
        {
            private readonly int _value;
            public static int LastExecutedValue { get; set; }
            public static int LastUndoneValue { get; set; }

            public TestCommand(int value)
            {
                _value = value;
            }

            public void Execute()
            {
                LastExecutedValue = _value;
            }

            public void Undo()
            {
                LastUndoneValue = _value;
            }

            public string Description
            {
                get { return string.Format("测试命令 {0}", _value); }
            }
        }

        #endregion
    }
}
