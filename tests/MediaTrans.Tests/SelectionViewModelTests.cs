using System;
using MediaTrans.ViewModels;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 选区裁剪 ViewModel 单元测试
    /// </summary>
    public class SelectionViewModelTests
    {
        private WaveformViewModel CreateWaveformVm(long totalSamples = 441000, int sampleRate = 44100, int width = 1000)
        {
            var vm = new WaveformViewModel();
            vm.Initialize(totalSamples, sampleRate, width);
            return vm;
        }

        // ===== 构造函数测试 =====

        [Fact]
        public void 构造函数_初始无选区()
        {
            var wfVm = CreateWaveformVm();
            var selVm = new SelectionViewModel(wfVm);

            Assert.Equal(0, selVm.SelectionStartSample);
            Assert.Equal(0, selVm.SelectionEndSample);
            Assert.False(selVm.HasSelection);
            Assert.Equal(0, selVm.SelectionDurationSamples);
        }

        [Fact]
        public void 构造函数_null参数_抛异常()
        {
            Assert.Throws<ArgumentNullException>(() => new SelectionViewModel(null));
        }

        // ===== CreateSelection 测试 =====

        [Fact]
        public void CreateSelection_正向范围()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(100, 500);
            // spp = 441, start = 100*441 = 44100, end = 500*441 = 220500
            Assert.Equal(44100, selVm.SelectionStartSample);
            Assert.Equal(220500, selVm.SelectionEndSample);
            Assert.True(selVm.HasSelection);
        }

        [Fact]
        public void CreateSelection_反向范围_自动排序()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(500, 100); // 反向
            Assert.Equal(44100, selVm.SelectionStartSample);
            Assert.Equal(220500, selVm.SelectionEndSample);
        }

        [Fact]
        public void CreateSelection_更新时长文本()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(0, 500);
            Assert.False(string.IsNullOrEmpty(selVm.SelectionDurationText));
            Assert.False(string.IsNullOrEmpty(selVm.SelectionStartTimeText));
            Assert.False(string.IsNullOrEmpty(selVm.SelectionEndTimeText));
        }

        // ===== 选区属性测试 =====

        [Fact]
        public void SelectionDurationSamples_正确计算()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(0, 1000); // 全选
            Assert.Equal(441000, selVm.SelectionDurationSamples);
        }

        [Fact]
        public void SelectionDurationSeconds_正确计算()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(0, 1000); // 全选 = 10秒
            Assert.Equal(10.0, selVm.SelectionDurationSeconds, 1);
        }

        [Fact]
        public void SelectionStartPixelX_在视口中()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(200, 800);
            Assert.Equal(200.0, selVm.SelectionStartPixelX, 1);
            Assert.Equal(800.0, selVm.SelectionEndPixelX, 1);
        }

        [Fact]
        public void SelectionWidthPixels_正确()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(200, 600);
            Assert.Equal(400.0, selVm.SelectionWidthPixels, 1);
        }

        // ===== 拖拽左 Handle 测试 =====

        [Fact]
        public void DragLeftHandle_完整流程()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            // 先创建选区
            selVm.CreateSelection(100, 500);

            // 拖拽左 Handle
            selVm.StartDragLeftHandle();
            Assert.True(selVm.IsDraggingLeftHandle);
            Assert.True(selVm.IsDragging);

            selVm.UpdateDragLeftHandle(200);
            // start = 200*441 = 88200
            Assert.Equal(88200, selVm.SelectionStartSample);

            selVm.EndDragLeftHandle();
            Assert.False(selVm.IsDraggingLeftHandle);
        }

        [Fact]
        public void UpdateDragLeftHandle_未拖拽_无效()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);
            selVm.CreateSelection(100, 500);

            long before = selVm.SelectionStartSample;
            selVm.UpdateDragLeftHandle(300); // 未 StartDrag
            Assert.Equal(before, selVm.SelectionStartSample);
        }

        // ===== 拖拽右 Handle 测试 =====

        [Fact]
        public void DragRightHandle_完整流程()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(100, 500);

            selVm.StartDragRightHandle();
            Assert.True(selVm.IsDraggingRightHandle);

            selVm.UpdateDragRightHandle(800);
            // end = 800*441 = 352800
            Assert.Equal(352800, selVm.SelectionEndSample);

            selVm.EndDragRightHandle();
            Assert.False(selVm.IsDraggingRightHandle);
        }

        [Fact]
        public void UpdateDragRightHandle_未拖拽_无效()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);
            selVm.CreateSelection(100, 500);

            long before = selVm.SelectionEndSample;
            selVm.UpdateDragRightHandle(800);
            Assert.Equal(before, selVm.SelectionEndSample);
        }

        // ===== 边界 Clamping 测试 =====

        [Fact]
        public void SelectionStartSample_不为负()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.SelectionStartSample = -100;
            Assert.Equal(0, selVm.SelectionStartSample);
        }

        [Fact]
        public void SelectionEndSample_不超过总采样()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.SelectionEndSample = 999999;
            Assert.Equal(441000, selVm.SelectionEndSample);
        }

        // ===== 精确时间微调测试 =====

        [Fact]
        public void SetSelectionStartTime_秒转采样()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);
            selVm.SelectionEndSample = 441000;

            selVm.SetSelectionStartTime(2.0); // 2秒
            Assert.Equal(88200, selVm.SelectionStartSample);
            Assert.True(selVm.HasSelection);
        }

        [Fact]
        public void SetSelectionEndTime_秒转采样()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.SetSelectionEndTime(5.0); // 5秒
            Assert.Equal(220500, selVm.SelectionEndSample);
        }

        // ===== ClearSelection 测试 =====

        [Fact]
        public void ClearSelection_清除选区()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(100, 500);
            Assert.True(selVm.HasSelection);

            selVm.ClearSelection();
            Assert.False(selVm.HasSelection);
            Assert.Equal(0, selVm.SelectionStartSample);
            Assert.Equal(0, selVm.SelectionEndSample);
        }

        // ===== SelectAll 测试 =====

        [Fact]
        public void SelectAll_全选()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.SelectAll();
            Assert.True(selVm.HasSelection);
            Assert.Equal(0, selVm.SelectionStartSample);
            Assert.Equal(441000, selVm.SelectionEndSample);
        }

        // ===== HitTest 测试 =====

        [Fact]
        public void HitTestLeftHandle_命中()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(200, 800);
            // left handle 在 200px
            Assert.True(selVm.HitTestLeftHandle(202, 5));
            Assert.True(selVm.HitTestLeftHandle(198, 5));
        }

        [Fact]
        public void HitTestLeftHandle_未命中()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(200, 800);
            Assert.False(selVm.HitTestLeftHandle(300, 5));
        }

        [Fact]
        public void HitTestRightHandle_命中()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(200, 800);
            Assert.True(selVm.HitTestRightHandle(802, 5));
        }

        [Fact]
        public void HitTestLeftHandle_无选区_返回false()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            Assert.False(selVm.HitTestLeftHandle(0, 5));
        }

        [Fact]
        public void HitTestRightHandle_无选区_返回false()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            Assert.False(selVm.HitTestRightHandle(0, 5));
        }

        // ===== 属性变更通知测试 =====

        [Fact]
        public void 属性通知_SelectionStartSample()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            bool notified = false;
            selVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "SelectionStartSample") notified = true;
            };

            selVm.SelectionStartSample = 44100;
            Assert.True(notified);
        }

        [Fact]
        public void 属性通知_HasSelection()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            bool notified = false;
            selVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "HasSelection") notified = true;
            };

            selVm.CreateSelection(100, 500);
            Assert.True(notified);
        }

        [Fact]
        public void 属性通知_SelectionDurationText()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            bool notified = false;
            selVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "SelectionDurationText") notified = true;
            };

            selVm.CreateSelection(100, 500);
            Assert.True(notified);
        }

        // ===== 时间文本格式测试 =====

        [Fact]
        public void 选区时间文本_格式正确()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var selVm = new SelectionViewModel(wfVm);

            selVm.CreateSelection(0, 500); // 0~5秒
            Assert.Equal("00:00:00.000", selVm.SelectionStartTimeText);
            Assert.Equal("00:00:05.000", selVm.SelectionEndTimeText);
            Assert.Equal("00:00:05.000", selVm.SelectionDurationText);
        }

        // ===== WaveformVm 引用测试 =====

        [Fact]
        public void WaveformVm_引用正确()
        {
            var wfVm = CreateWaveformVm();
            var selVm = new SelectionViewModel(wfVm);
            Assert.Same(wfVm, selVm.WaveformVm);
        }
    }
}
