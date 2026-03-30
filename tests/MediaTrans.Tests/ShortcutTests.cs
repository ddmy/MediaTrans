using System;
using System.Windows.Input;
using MediaTrans.Models;
using MediaTrans.Services;
using MediaTrans.ViewModels;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 快捷键服务单元测试
    /// </summary>
    public class ShortcutServiceTests
    {
        // 辅助方法：创建初始化好的 WaveformViewModel
        private WaveformViewModel CreateWaveformVm(long totalSamples, int sampleRate, int viewportWidth)
        {
            var vm = new WaveformViewModel();
            vm.Initialize(totalSamples, sampleRate, viewportWidth);
            return vm;
        }

        // 辅助方法：创建 SelectionViewModel
        private SelectionViewModel CreateSelectionVm(WaveformViewModel waveformVm)
        {
            return new SelectionViewModel(waveformVm);
        }

        // 辅助方法：创建 TimelineViewModel
        private TimelineViewModel CreateTimelineVm(WaveformViewModel waveformVm)
        {
            var rulerService = new TimelineRulerService();
            return new TimelineViewModel(waveformVm, rulerService);
        }

        // 辅助方法：创建 UndoRedoViewModel
        private UndoRedoViewModel CreateUndoRedoVm()
        {
            var service = new UndoRedoService(50);
            return new UndoRedoViewModel(service);
        }

        // 辅助方法：创建带基本VM的 ShortcutService
        private ShortcutService CreateService(
            WaveformViewModel waveformVm = null,
            TimelineViewModel timelineVm = null,
            SelectionViewModel selectionVm = null,
            UndoRedoViewModel undoRedoVm = null,
            TimelineTrackViewModel trackVm = null,
            int stepPixels = 10)
        {
            return new ShortcutService(
                null,  // playbackVm（需要 AudioPlaybackService，测试中跳过）
                undoRedoVm,
                trackVm,
                selectionVm,
                timelineVm,
                waveformVm,
                stepPixels);
        }

        #region Space 播放/暂停

        [Fact]
        public void Space_无PlaybackVm_返回false()
        {
            var service = CreateService();
            bool handled = service.ProcessKeyDown(Key.Space, ModifierKeys.None);
            Assert.False(handled);
        }

        #endregion

        #region Ctrl+Z 撤销

        [Fact]
        public void CtrlZ_有可撤销操作_执行撤销()
        {
            var undoRedoVm = CreateUndoRedoVm();
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var selVm = CreateSelectionVm(waveformVm);
            var cmd = new SelectionChangeCommand(selVm, 0, 0, 1000, 5000);
            undoRedoVm.Service.ExecuteCommand(cmd);

            var service = CreateService(undoRedoVm: undoRedoVm);
            bool handled = service.ProcessKeyDown(Key.Z, ModifierKeys.Control);

            Assert.True(handled);
            Assert.False(undoRedoVm.CanUndo);
            Assert.True(undoRedoVm.CanRedo);
        }

        [Fact]
        public void CtrlZ_无可撤销操作_返回false()
        {
            var undoRedoVm = CreateUndoRedoVm();
            var service = CreateService(undoRedoVm: undoRedoVm);
            bool handled = service.ProcessKeyDown(Key.Z, ModifierKeys.Control);
            Assert.False(handled);
        }

        [Fact]
        public void CtrlZ_无UndoRedoVm_返回false()
        {
            var service = CreateService();
            bool handled = service.ProcessKeyDown(Key.Z, ModifierKeys.Control);
            Assert.False(handled);
        }

        #endregion

        #region Ctrl+Y 重做

        [Fact]
        public void CtrlY_有可重做操作_执行重做()
        {
            var undoRedoVm = CreateUndoRedoVm();
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var selVm = CreateSelectionVm(waveformVm);
            var cmd = new SelectionChangeCommand(selVm, 0, 0, 1000, 5000);
            undoRedoVm.Service.ExecuteCommand(cmd);
            undoRedoVm.Service.Undo();

            var service = CreateService(undoRedoVm: undoRedoVm);
            bool handled = service.ProcessKeyDown(Key.Y, ModifierKeys.Control);

            Assert.True(handled);
            Assert.True(undoRedoVm.CanUndo);
            Assert.False(undoRedoVm.CanRedo);
        }

        [Fact]
        public void CtrlY_无可重做操作_返回false()
        {
            var undoRedoVm = CreateUndoRedoVm();
            var service = CreateService(undoRedoVm: undoRedoVm);
            bool handled = service.ProcessKeyDown(Key.Y, ModifierKeys.Control);
            Assert.False(handled);
        }

        #endregion

        #region Ctrl+Shift+Z 重做

        [Fact]
        public void CtrlShiftZ_有可重做操作_执行重做()
        {
            var undoRedoVm = CreateUndoRedoVm();
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var selVm = CreateSelectionVm(waveformVm);
            var cmd = new SelectionChangeCommand(selVm, 0, 0, 1000, 5000);
            undoRedoVm.Service.ExecuteCommand(cmd);
            undoRedoVm.Service.Undo();

            var service = CreateService(undoRedoVm: undoRedoVm);
            bool handled = service.ProcessKeyDown(Key.Z, ModifierKeys.Control | ModifierKeys.Shift);

            Assert.True(handled);
            Assert.True(undoRedoVm.CanUndo);
            Assert.False(undoRedoVm.CanRedo);
        }

        #endregion

        #region Delete 删除选中片段

        [Fact]
        public void Delete_有选中片段_执行删除()
        {
            var trackVm = new TimelineTrackViewModel();
            var clip = new TimelineClip() { SourceFilePath = "test.mp4", MediaType = "video" };
            trackVm.AddClip(clip);
            trackVm.SelectedClip = clip;

            var service = CreateService(trackVm: trackVm);
            bool handled = service.ProcessKeyDown(Key.Delete, ModifierKeys.None);

            Assert.True(handled);
            Assert.Equal(0, trackVm.ClipCount);
        }

        [Fact]
        public void Delete_无选中片段_返回false()
        {
            var trackVm = new TimelineTrackViewModel();
            var service = CreateService(trackVm: trackVm);
            bool handled = service.ProcessKeyDown(Key.Delete, ModifierKeys.None);
            Assert.False(handled);
        }

        [Fact]
        public void Delete_无TrackVm_返回false()
        {
            var service = CreateService();
            bool handled = service.ProcessKeyDown(Key.Delete, ModifierKeys.None);
            Assert.False(handled);
        }

        #endregion

        #region Left/Right 方向键微移播放头

        [Fact]
        public void Right_从起始位置向右移动()
        {
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var timelineVm = CreateTimelineVm(waveformVm);
            timelineVm.PlayheadSample = 0;

            var service = CreateService(waveformVm: waveformVm, timelineVm: timelineVm, stepPixels: 10);
            bool handled = service.ProcessKeyDown(Key.Right, ModifierKeys.None);

            Assert.True(handled);
            // 步进 = 10 * SamplesPerPixel
            long expectedStep = (long)(10 * waveformVm.SamplesPerPixel);
            Assert.Equal(expectedStep, timelineVm.PlayheadSample);
        }

        [Fact]
        public void Left_从中间位置向左移动()
        {
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var timelineVm = CreateTimelineVm(waveformVm);
            long startPos = 50000;
            timelineVm.PlayheadSample = startPos;

            var service = CreateService(waveformVm: waveformVm, timelineVm: timelineVm, stepPixels: 10);
            bool handled = service.ProcessKeyDown(Key.Left, ModifierKeys.None);

            Assert.True(handled);
            long expectedStep = (long)(10 * waveformVm.SamplesPerPixel);
            Assert.Equal(startPos - expectedStep, timelineVm.PlayheadSample);
        }

        [Fact]
        public void Left_在起始位置不会变为负值()
        {
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var timelineVm = CreateTimelineVm(waveformVm);
            timelineVm.PlayheadSample = 0;

            var service = CreateService(waveformVm: waveformVm, timelineVm: timelineVm, stepPixels: 10);
            service.ProcessKeyDown(Key.Left, ModifierKeys.None);

            // PlayheadSample setter 会 clamp 到 0
            Assert.Equal(0, timelineVm.PlayheadSample);
        }

        [Fact]
        public void Right_超过总长度会被截断()
        {
            long totalSamples = 1000;
            var waveformVm = CreateWaveformVm(totalSamples, 44100, 1000);
            var timelineVm = CreateTimelineVm(waveformVm);
            timelineVm.PlayheadSample = totalSamples - 1;

            var service = CreateService(waveformVm: waveformVm, timelineVm: timelineVm, stepPixels: 10);
            service.ProcessKeyDown(Key.Right, ModifierKeys.None);

            // PlayheadSample setter 会 clamp 到 TotalSamples
            Assert.True(timelineVm.PlayheadSample <= totalSamples);
        }

        [Fact]
        public void 方向键_无TimelineVm_返回false()
        {
            var service = CreateService();
            Assert.False(service.ProcessKeyDown(Key.Left, ModifierKeys.None));
            Assert.False(service.ProcessKeyDown(Key.Right, ModifierKeys.None));
        }

        [Fact]
        public void 方向键_步进随缩放自适应()
        {
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var timelineVm = CreateTimelineVm(waveformVm);

            // 放大：减小 SamplesPerPixel
            waveformVm.ZoomIn();
            double zoomedInSpp = waveformVm.SamplesPerPixel;
            timelineVm.PlayheadSample = 0;

            var service = CreateService(waveformVm: waveformVm, timelineVm: timelineVm, stepPixels: 10);
            service.ProcessKeyDown(Key.Right, ModifierKeys.None);
            long zoomedInStep = timelineVm.PlayheadSample;

            // 缩小：连续缩小多次使得 SamplesPerPixel 变大
            waveformVm.ZoomOut();
            waveformVm.ZoomOut();
            waveformVm.ZoomOut();
            timelineVm.PlayheadSample = 0;
            service.ProcessKeyDown(Key.Right, ModifierKeys.None);
            long zoomedOutStep = timelineVm.PlayheadSample;

            // 缩小时步进应更大
            Assert.True(zoomedOutStep > zoomedInStep,
                string.Format("缩小时步进({0})应大于放大时步进({1})", zoomedOutStep, zoomedInStep));
        }

        #endregion

        #region Home/End 跳转

        [Fact]
        public void Home_跳转到起始位置()
        {
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var timelineVm = CreateTimelineVm(waveformVm);
            timelineVm.PlayheadSample = 500000;

            var service = CreateService(waveformVm: waveformVm, timelineVm: timelineVm);
            bool handled = service.ProcessKeyDown(Key.Home, ModifierKeys.None);

            Assert.True(handled);
            Assert.Equal(0, timelineVm.PlayheadSample);
        }

        [Fact]
        public void End_跳转到结束位置()
        {
            long totalSamples = 1000000;
            var waveformVm = CreateWaveformVm(totalSamples, 44100, 1000);
            var timelineVm = CreateTimelineVm(waveformVm);
            timelineVm.PlayheadSample = 0;

            var service = CreateService(waveformVm: waveformVm, timelineVm: timelineVm);
            bool handled = service.ProcessKeyDown(Key.End, ModifierKeys.None);

            Assert.True(handled);
            Assert.Equal(totalSamples, timelineVm.PlayheadSample);
        }

        [Fact]
        public void Home_无TimelineVm_返回false()
        {
            var service = CreateService();
            Assert.False(service.ProcessKeyDown(Key.Home, ModifierKeys.None));
        }

        [Fact]
        public void End_无TimelineVm_返回false()
        {
            var service = CreateService();
            Assert.False(service.ProcessKeyDown(Key.End, ModifierKeys.None));
        }

        #endregion

        #region Ctrl+A 全选

        [Fact]
        public void CtrlA_执行全选()
        {
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var selVm = CreateSelectionVm(waveformVm);

            var service = CreateService(waveformVm: waveformVm, selectionVm: selVm);
            bool handled = service.ProcessKeyDown(Key.A, ModifierKeys.Control);

            Assert.True(handled);
            Assert.Equal(0, selVm.SelectionStartSample);
            Assert.Equal(1000000, selVm.SelectionEndSample);
            Assert.True(selVm.HasSelection);
        }

        [Fact]
        public void CtrlA_无SelectionVm_返回false()
        {
            var service = CreateService();
            bool handled = service.ProcessKeyDown(Key.A, ModifierKeys.Control);
            Assert.False(handled);
        }

        #endregion

        #region 未映射按键

        [Fact]
        public void 未映射按键_返回false()
        {
            var service = CreateService();
            Assert.False(service.ProcessKeyDown(Key.F1, ModifierKeys.None));
            Assert.False(service.ProcessKeyDown(Key.Escape, ModifierKeys.None));
            Assert.False(service.ProcessKeyDown(Key.Tab, ModifierKeys.None));
        }

        [Fact]
        public void 未映射Ctrl组合_返回false()
        {
            var service = CreateService();
            Assert.False(service.ProcessKeyDown(Key.B, ModifierKeys.Control));
            Assert.False(service.ProcessKeyDown(Key.X, ModifierKeys.Control));
        }

        #endregion

        #region 配置：PlayheadStepPixels

        [Fact]
        public void 步进像素为0时使用默认值()
        {
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var timelineVm = CreateTimelineVm(waveformVm);
            timelineVm.PlayheadSample = 0;

            // 传入 0 应该被纠正为默认 10
            var service = CreateService(waveformVm: waveformVm, timelineVm: timelineVm, stepPixels: 0);
            service.ProcessKeyDown(Key.Right, ModifierKeys.None);

            // 步进 = 10 * SamplesPerPixel（默认回退到 10）
            long expectedStep = (long)(10 * waveformVm.SamplesPerPixel);
            Assert.Equal(expectedStep, timelineVm.PlayheadSample);
        }

        [Fact]
        public void 自定义步进像素生效()
        {
            var waveformVm = CreateWaveformVm(1000000, 44100, 1000);
            var timelineVm = CreateTimelineVm(waveformVm);
            timelineVm.PlayheadSample = 0;

            var service = CreateService(waveformVm: waveformVm, timelineVm: timelineVm, stepPixels: 20);
            service.ProcessKeyDown(Key.Right, ModifierKeys.None);

            long expectedStep = (long)(20 * waveformVm.SamplesPerPixel);
            Assert.Equal(expectedStep, timelineVm.PlayheadSample);
        }

        #endregion

        #region AppConfig PlayheadStepPixels

        [Fact]
        public void AppConfig默认包含PlayheadStepPixels字段()
        {
            var config = AppConfig.CreateDefault();
            Assert.Equal(10, config.PlayheadStepPixels);
        }

        #endregion
    }
}
