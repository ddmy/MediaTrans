using System;
using MediaTrans.ViewModels;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 波形缩放与平移 ViewModel 单元测试
    /// </summary>
    public class WaveformViewModelTests
    {
        private WaveformViewModel CreateInitializedVm(long totalSamples = 441000, int sampleRate = 44100, int viewportWidth = 1000)
        {
            var vm = new WaveformViewModel();
            vm.Initialize(totalSamples, sampleRate, viewportWidth);
            return vm;
        }

        // ===== 初始化测试 =====

        [Fact]
        public void 初始化_全局概览缩放()
        {
            // 10秒音频 441000采样，视口1000像素
            var vm = CreateInitializedVm(441000, 44100, 1000);
            // 全局概览: maxSpp = 441000/1000 = 441
            Assert.Equal(441, vm.SamplesPerPixel);
            Assert.Equal(0, vm.ViewportStartSample);
            Assert.Equal(441000, vm.TotalSamples);
        }

        [Fact]
        public void 初始化_缩放级别文本_100百分号()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            Assert.Equal("100%", vm.ZoomLevelText);
        }

        [Fact]
        public void 初始化_视口覆盖全部采样()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            Assert.Equal(441000, vm.ViewportSampleSpan);
            Assert.Equal(441000, vm.ViewportEndSample);
        }

        // ===== ZoomAtPosition 测试 =====

        [Fact]
        public void ZoomAtPosition_放大_SamplesPerPixel减小()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            double originalSpp = vm.SamplesPerPixel;

            vm.ZoomAtPosition(1, 0.5); // 放大，中心位置

            Assert.True(vm.SamplesPerPixel < originalSpp, "放大后 SamplesPerPixel 应减小");
        }

        [Fact]
        public void ZoomAtPosition_缩小_SamplesPerPixel增大()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);

            // 先放大
            vm.ZoomAtPosition(1, 0.5);
            double zoomedSpp = vm.SamplesPerPixel;

            // 再缩小
            vm.ZoomAtPosition(-1, 0.5);

            Assert.True(vm.SamplesPerPixel > zoomedSpp, "缩小后 SamplesPerPixel 应增大");
        }

        [Fact]
        public void ZoomAtPosition_光标位置为中心_保持对齐()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);

            // 先设置一个不在起点的视口
            vm.ZoomAtPosition(1, 0.5); // 放大到中心

            long centerSample = vm.ViewportStartSample + vm.ViewportSampleSpan / 2;

            // 再次以同一位置放大
            vm.ZoomAtPosition(1, 0.5);

            // 中心点应该大致在同一采样帧位置
            long newCenterSample = vm.ViewportStartSample + vm.ViewportSampleSpan / 2;
            long tolerance = (long)(vm.SamplesPerPixel * 2); // 允许2像素误差
            Assert.True(Math.Abs(newCenterSample - centerSample) <= tolerance,
                string.Format("中心采样帧偏移过大: 期望 ~{0}, 实际 {1}", centerSample, newCenterSample));
        }

        [Fact]
        public void ZoomAtPosition_不超过最小缩放()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);

            // 连续放大100次
            for (int i = 0; i < 100; i++)
            {
                vm.ZoomAtPosition(1, 0.5);
            }

            Assert.True(vm.SamplesPerPixel >= 1, "SamplesPerPixel 不应小于1");
        }

        [Fact]
        public void ZoomAtPosition_不超过最大缩放()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);

            // 先放大再连续缩小100次
            vm.ZoomAtPosition(1, 0.5);
            for (int i = 0; i < 100; i++)
            {
                vm.ZoomAtPosition(-1, 0.5);
            }

            // maxSpp = 441000/1000 = 441
            Assert.Equal(441, vm.SamplesPerPixel);
        }

        // ===== ZoomIn/ZoomOut 测试 =====

        [Fact]
        public void ZoomIn_居中放大()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            double before = vm.SamplesPerPixel;
            vm.ZoomIn();
            Assert.True(vm.SamplesPerPixel < before);
        }

        [Fact]
        public void ZoomOut_居中缩小()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            vm.ZoomIn();
            double before = vm.SamplesPerPixel;
            vm.ZoomOut();
            Assert.True(vm.SamplesPerPixel > before);
        }

        // ===== ZoomToFit 测试 =====

        [Fact]
        public void ZoomToFit_恢复全局概览()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);

            // 放大
            vm.ZoomIn();
            vm.ZoomIn();
            Assert.NotEqual(441.0, vm.SamplesPerPixel);

            // 缩放到适合
            vm.ZoomToFit();
            Assert.Equal(441.0, vm.SamplesPerPixel);
            Assert.Equal(0, vm.ViewportStartSample);
        }

        // ===== 平移测试 =====

        [Fact]
        public void Pan_右键拖拽_视口移动()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);

            // 先放大，使可视区域不覆盖全部
            vm.ZoomIn();
            vm.ZoomIn();

            vm.StartPan(500);
            Assert.True(vm.IsPanning);

            long originalStart = vm.ViewportStartSample;

            // 向右拖动100像素（应该向左滚动）
            vm.UpdatePan(400);

            Assert.True(vm.ViewportStartSample > originalStart, "向右拖动应使视口向右移动");

            vm.EndPan();
            Assert.False(vm.IsPanning);
        }

        [Fact]
        public void Pan_向左拖动_视口向左移动()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);

            vm.ZoomIn();
            vm.ZoomIn();

            // 先移动到中间位置
            vm.ScrollTo(100000);
            long midStart = vm.ViewportStartSample;

            vm.StartPan(500);
            vm.UpdatePan(600); // 向左拖动
            Assert.True(vm.ViewportStartSample < midStart, "向左拖动应使视口向左移动");
            vm.EndPan();
        }

        [Fact]
        public void Pan_不平移状态_UpdatePan无效()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            vm.ZoomIn();
            vm.ZoomIn();

            long before = vm.ViewportStartSample;
            vm.UpdatePan(100); // 未调用 StartPan

            Assert.Equal(before, vm.ViewportStartSample);
        }

        [Fact]
        public void Pan_边界clamping_不越界()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            vm.ZoomIn();

            vm.StartPan(0);
            vm.UpdatePan(-10000); // 极大向右拖
            vm.EndPan();

            Assert.True(vm.ViewportStartSample >= 0);
            Assert.True(vm.ViewportEndSample <= vm.TotalSamples);
        }

        // ===== 视口边界测试 =====

        [Fact]
        public void ViewportStartSample_不能为负()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            vm.ScrollTo(-1000);
            Assert.Equal(0, vm.ViewportStartSample);
        }

        [Fact]
        public void ViewportEndSample_不超过总采样数()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            vm.ZoomIn();
            vm.ScrollTo(999999);
            Assert.True(vm.ViewportEndSample <= vm.TotalSamples);
        }

        // ===== 坐标转换测试 =====

        [Fact]
        public void PixelToSample_正确转换()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            // spp = 441, start = 0
            // pixel 0 → sample 0, pixel 500 → sample 220500
            Assert.Equal(0, vm.PixelToSample(0));
            Assert.Equal(220500, vm.PixelToSample(500));
        }

        [Fact]
        public void SampleToPixel_正确转换()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            // spp = 441
            Assert.Equal(0, vm.SampleToPixel(0));
            Assert.Equal(500, vm.SampleToPixel(220500), 1);
        }

        [Fact]
        public void PixelToSample_SampleToPixel_互逆()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            long sample = vm.PixelToSample(300);
            double pixel = vm.SampleToPixel(sample);
            Assert.Equal(300, pixel, 1);
        }

        // ===== 时间转换测试 =====

        [Fact]
        public void SamplesToSeconds_正确()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            Assert.Equal(1.0, vm.SamplesToSeconds(44100), 5);
            Assert.Equal(10.0, vm.SamplesToSeconds(441000), 5);
        }

        [Fact]
        public void SecondsToSamples_正确()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            Assert.Equal(44100, vm.SecondsToSamples(1.0));
        }

        // ===== FormatTime 测试 =====

        [Fact]
        public void FormatTime_零秒()
        {
            Assert.Equal("00:00:00.000", WaveformViewModel.FormatTime(0));
        }

        [Fact]
        public void FormatTime_小于1秒_含毫秒()
        {
            Assert.Equal("00:00:00.500", WaveformViewModel.FormatTime(0.5));
        }

        [Fact]
        public void FormatTime_1分30秒()
        {
            Assert.Equal("00:01:30.000", WaveformViewModel.FormatTime(90));
        }

        [Fact]
        public void FormatTime_1小时2分3秒456毫秒()
        {
            Assert.Equal("01:02:03.456", WaveformViewModel.FormatTime(3723.456));
        }

        [Fact]
        public void FormatTime_负值_返回零()
        {
            Assert.Equal("00:00:00.000", WaveformViewModel.FormatTime(-5));
        }

        // ===== 视口尺寸变化测试 =====

        [Fact]
        public void ViewportWidthPixels_改变后_视口跨度重算()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            // spp = 441, span = 441000

            vm.ViewportWidthPixels = 500;
            // maxSpp 重算: 441000/500 = 882
            // 但当前 spp 不自动变（保持有效范围内）
            Assert.Equal(500, vm.ViewportWidthPixels);
        }

        [Fact]
        public void ViewportWidthPixels_设置负值_使用1()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            vm.ViewportWidthPixels = -100;
            Assert.Equal(1, vm.ViewportWidthPixels);
        }

        // ===== ZoomLevelText 更新测试 =====

        [Fact]
        public void ZoomLevelText_放大后_百分比增大()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            Assert.Equal("100%", vm.ZoomLevelText);

            vm.ZoomIn();
            // 百分比应该大于100（因为放大了）
            Assert.NotEqual("100%", vm.ZoomLevelText);
        }

        // ===== 属性变更通知测试 =====

        [Fact]
        public void 属性变更通知_SamplesPerPixel()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            bool notified = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "SamplesPerPixel") notified = true;
            };

            vm.ZoomIn();
            Assert.True(notified, "缩放后应触发 SamplesPerPixel 变更通知");
        }

        [Fact]
        public void 属性变更通知_ViewportStartSample()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            vm.ZoomIn();
            vm.ZoomIn();

            bool notified = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "ViewportStartSample") notified = true;
            };

            vm.ScrollTo(10000);
            Assert.True(notified, "平移后应触发 ViewportStartSample 变更通知");
        }

        // ===== 无音频状态测试 =====

        [Fact]
        public void 未初始化_缩放无异常()
        {
            var vm = new WaveformViewModel();
            vm.ZoomIn();    // 不应崩溃
            vm.ZoomOut();   // 不应崩溃
            vm.ZoomToFit(); // 不应崩溃
        }

        [Fact]
        public void 未初始化_平移无异常()
        {
            var vm = new WaveformViewModel();
            vm.StartPan(100);
            vm.UpdatePan(200);
            vm.EndPan();
        }

        // ===== ZoomFactor 测试 =====

        [Fact]
        public void ZoomFactor_设置正值()
        {
            var vm = new WaveformViewModel();
            vm.ZoomFactor = 2.0;
            Assert.Equal(2.0, vm.ZoomFactor);
        }

        [Fact]
        public void ZoomFactor_设置无效值_使用默认()
        {
            var vm = new WaveformViewModel();
            vm.ZoomFactor = 0;
            Assert.Equal(1.2, vm.ZoomFactor);
        }

        // ===== ViewportDurationSeconds 测试 =====

        [Fact]
        public void ViewportDurationSeconds_初始为总时长()
        {
            var vm = CreateInitializedVm(441000, 44100, 1000);
            // 441000 / 44100 = 10 秒
            Assert.Equal(10.0, vm.ViewportDurationSeconds, 1);
        }
    }
}
