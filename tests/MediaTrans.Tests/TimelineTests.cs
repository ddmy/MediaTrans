using System;
using System.Collections.Generic;
using System.Linq;
using MediaTrans.Services;
using MediaTrans.ViewModels;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 时间轴控件单元测试 — 覆盖刻度尺服务和时间轴 ViewModel
    /// </summary>
    public class TimelineRulerServiceTests
    {
        // ===== CalculateMajorInterval 测试 =====

        [Fact]
        public void CalculateMajorInterval_全局概览_返回合理间隔()
        {
            var service = new TimelineRulerService();
            // 10秒音频，1000像素，spp = 441/441 = 441
            // secondsPerPixel = 441/44100 = 0.01
            // minInterval = 0.01 * 80 = 0.8
            // 最近的预设间隔 >= 0.8 是 1.0
            double interval = service.CalculateMajorInterval(441, 44100);
            Assert.Equal(1.0, interval);
        }

        [Fact]
        public void CalculateMajorInterval_高倍缩放_返回毫秒级间隔()
        {
            var service = new TimelineRulerService();
            // spp = 1, 最大缩放。secondsPerPixel = 1/44100 ≈ 0.0000227
            // minInterval = 0.0000227 * 80 ≈ 0.00181
            // 最近 >= 0.00181 的间隔是 0.005
            double interval = service.CalculateMajorInterval(1, 44100);
            Assert.Equal(0.005, interval);
        }

        [Fact]
        public void CalculateMajorInterval_低缩放_返回分钟级间隔()
        {
            var service = new TimelineRulerService();
            // 1小时文件，1000像素。spp = 44100*3600/1000 = 158760
            // secondsPerPixel = 158760/44100 = 3.6
            // minInterval = 3.6 * 80 = 288
            // 最近 >= 288 的间隔是 300 (5min)
            double interval = service.CalculateMajorInterval(158760, 44100);
            Assert.Equal(300.0, interval);
        }

        [Fact]
        public void CalculateMajorInterval_无效参数_返回默认()
        {
            var service = new TimelineRulerService();
            Assert.Equal(1.0, service.CalculateMajorInterval(0, 44100));
            Assert.Equal(1.0, service.CalculateMajorInterval(100, 0));
        }

        [Fact]
        public void CalculateMajorInterval_自定义最小间距()
        {
            var service = new TimelineRulerService();
            service.MinMajorTickSpacingPixels = 40; // 更密的刻度
            // secondsPerPixel = 441/44100 = 0.01
            // minInterval = 0.01 * 40 = 0.4
            // 最近 >= 0.4 的间隔是 0.5
            double interval = service.CalculateMajorInterval(441, 44100);
            Assert.Equal(0.5, interval);
        }

        // ===== CalculateTickMarks 测试 =====

        [Fact]
        public void CalculateTickMarks_返回刻度列表()
        {
            var service = new TimelineRulerService();
            var ticks = service.CalculateTickMarks(0, 1000, 441, 44100);
            Assert.True(ticks.Count > 0, "应返回刻度标记");
        }

        [Fact]
        public void CalculateTickMarks_包含主刻度和次刻度()
        {
            var service = new TimelineRulerService();
            // spp = 44.1, 比较高的缩放。10秒范围
            var ticks = service.CalculateTickMarks(0, 1000, 44.1, 44100);

            bool hasMajor = false;
            bool hasMinor = false;
            foreach (var tick in ticks)
            {
                if (tick.IsMajor) hasMajor = true;
                else hasMinor = true;
            }
            Assert.True(hasMajor, "应包含主刻度");
            Assert.True(hasMinor, "应包含次刻度");
        }

        [Fact]
        public void CalculateTickMarks_主刻度有标签()
        {
            var service = new TimelineRulerService();
            var ticks = service.CalculateTickMarks(0, 1000, 441, 44100);

            foreach (var tick in ticks)
            {
                if (tick.IsMajor)
                {
                    Assert.False(string.IsNullOrEmpty(tick.Label),
                        string.Format("主刻度在 {0}s 处应有标签", tick.TimeSeconds));
                }
            }
        }

        [Fact]
        public void CalculateTickMarks_次刻度无标签()
        {
            var service = new TimelineRulerService();
            var ticks = service.CalculateTickMarks(0, 1000, 441, 44100);

            foreach (var tick in ticks)
            {
                if (!tick.IsMajor)
                {
                    Assert.Null(tick.Label);
                }
            }
        }

        [Fact]
        public void CalculateTickMarks_无效参数_返回空列表()
        {
            var service = new TimelineRulerService();
            Assert.Empty(service.CalculateTickMarks(0, 0, 441, 44100));
            Assert.Empty(service.CalculateTickMarks(0, 1000, 0, 44100));
            Assert.Empty(service.CalculateTickMarks(0, 1000, 441, 0));
        }

        [Fact]
        public void CalculateTickMarks_偏移视口_刻度在有效范围()
        {
            var service = new TimelineRulerService();
            // 视口从第5秒开始（220500采样帧），宽1000像素
            var ticks = service.CalculateTickMarks(220500, 1000, 441, 44100);

            Assert.True(ticks.Count > 0);
            // 所有刻度的 pixelX 应在合理范围内（可能略超出左边界）
            foreach (var tick in ticks)
            {
                Assert.True(tick.PixelX >= -100 && tick.PixelX <= 1100,
                    string.Format("刻度像素位置 {0} 超出合理范围", tick.PixelX));
            }
        }

        [Fact]
        public void CalculateTickMarks_主刻度间距均匀()
        {
            var service = new TimelineRulerService();
            var ticks = service.CalculateTickMarks(0, 2000, 441, 44100);

            var majorTicks = new List<TickMark>();
            foreach (var tick in ticks)
            {
                if (tick.IsMajor) majorTicks.Add(tick);
            }

            if (majorTicks.Count >= 3)
            {
                double interval = majorTicks[1].TimeSeconds - majorTicks[0].TimeSeconds;
                for (int i = 2; i < majorTicks.Count; i++)
                {
                    double gap = majorTicks[i].TimeSeconds - majorTicks[i - 1].TimeSeconds;
                    Assert.True(Math.Abs(gap - interval) < 0.001,
                        string.Format("主刻度间距应均匀: 期望 {0}, 实际 {1}", interval, gap));
                }
            }
        }

        // ===== FormatTickLabel 测试 =====

        [Fact]
        public void FormatTickLabel_亚秒级_含毫秒()
        {
            string label = TimelineRulerService.FormatTickLabel(1.5, 0.5);
            Assert.Equal("0:01.500", label);
        }

        [Fact]
        public void FormatTickLabel_秒级_不含毫秒()
        {
            string label = TimelineRulerService.FormatTickLabel(65.0, 5.0);
            Assert.Equal("1:05", label);
        }

        [Fact]
        public void FormatTickLabel_分钟级_含小时()
        {
            string label = TimelineRulerService.FormatTickLabel(3723.0, 60.0);
            Assert.Equal("1:02:03", label);
        }

        [Fact]
        public void FormatTickLabel_零时间()
        {
            string label = TimelineRulerService.FormatTickLabel(0.0, 1.0);
            Assert.Equal("0:00", label);
        }

        [Fact]
        public void FormatTickLabel_负值_返回零()
        {
            string label = TimelineRulerService.FormatTickLabel(-5.0, 1.0);
            Assert.Equal("0:00", label);
        }

        [Fact]
        public void FormatTickLabel_亚秒级_含小时()
        {
            string label = TimelineRulerService.FormatTickLabel(3661.5, 0.5);
            Assert.Equal("1:01:01.500", label);
        }
    }

    /// <summary>
    /// 时间轴 ViewModel 单元测试
    /// </summary>
    public class TimelineViewModelTests
    {
        private WaveformViewModel CreateWaveformVm(long totalSamples = 441000, int sampleRate = 44100, int width = 1000)
        {
            var vm = new WaveformViewModel();
            vm.Initialize(totalSamples, sampleRate, width);
            return vm;
        }

        // ===== 构造函数测试 =====

        [Fact]
        public void 构造函数_正常创建()
        {
            var wfVm = CreateWaveformVm();
            var tlVm = new TimelineViewModel(wfVm);

            Assert.Equal(0, tlVm.PlayheadSample);
            Assert.Equal("00:00:00.000", tlVm.PlayheadTimeText);
            Assert.NotNull(tlVm.WaveformVm);
            Assert.NotNull(tlVm.RulerService);
        }

        [Fact]
        public void 构造函数_参数为null_抛异常()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TimelineViewModel(null));
        }

        [Fact]
        public void 构造函数_注入刻度尺_使用注入的实例()
        {
            var wfVm = CreateWaveformVm();
            var ruler = new TimelineRulerService();
            ruler.MinMajorTickSpacingPixels = 120;

            var tlVm = new TimelineViewModel(wfVm, ruler);
            Assert.Same(ruler, tlVm.RulerService);
        }

        // ===== 播放头测试 =====

        [Fact]
        public void PlayheadSample_设置_更新时间码()
        {
            var wfVm = CreateWaveformVm();
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.PlayheadSample = 44100; // 1秒
            Assert.Equal("00:00:01.000", tlVm.PlayheadTimeText);
        }

        [Fact]
        public void PlayheadSample_clamp_不为负()
        {
            var wfVm = CreateWaveformVm();
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.PlayheadSample = -100;
            Assert.Equal(0, tlVm.PlayheadSample);
        }

        [Fact]
        public void PlayheadSample_clamp_不超过总采样()
        {
            var wfVm = CreateWaveformVm(441000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.PlayheadSample = 999999;
            Assert.Equal(441000, tlVm.PlayheadSample);
        }

        [Fact]
        public void PlayheadPixelX_在全局概览下_正确计算()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.PlayheadSample = 220500; // 中间位置
            // spp = 441, pixelX = 220500 / 441 = 500
            Assert.Equal(500.0, tlVm.PlayheadPixelX, 1);
        }

        // ===== 点击定位测试 =====

        [Fact]
        public void ClickToPosition_定位播放头()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.ClickToPosition(500); // 点击中间
            // spp = 441, sample = 0 + 500*441 = 220500
            Assert.Equal(220500, tlVm.PlayheadSample);
        }

        [Fact]
        public void ClickToPosition_超出右边界_clamp()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.ClickToPosition(2000); // 超出右边界
            Assert.True(tlVm.PlayheadSample <= 441000);
        }

        [Fact]
        public void ClickToPosition_负值_clamp到0()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.ClickToPosition(-100);
            Assert.Equal(0, tlVm.PlayheadSample);
        }

        // ===== 拖动播放头测试 =====

        [Fact]
        public void StartDragPlayhead_开始拖动()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.StartDragPlayhead(100);
            Assert.True(tlVm.IsDraggingPlayhead);
            // spp=441, sample = 441*100 = 44100
            Assert.Equal(44100, tlVm.PlayheadSample);
        }

        [Fact]
        public void UpdateDragPlayhead_拖动中_更新位置()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.StartDragPlayhead(100);
            tlVm.UpdateDragPlayhead(200);
            Assert.Equal(88200, tlVm.PlayheadSample); // 200*441 = 88200
        }

        [Fact]
        public void UpdateDragPlayhead_未拖动_不更新()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.UpdateDragPlayhead(200);
            Assert.Equal(0, tlVm.PlayheadSample);
        }

        [Fact]
        public void EndDragPlayhead_结束拖动()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.StartDragPlayhead(100);
            tlVm.EndDragPlayhead();
            Assert.False(tlVm.IsDraggingPlayhead);
        }

        // ===== GetVisibleTickMarks 测试 =====

        [Fact]
        public void GetVisibleTickMarks_返回非空列表()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            var ticks = tlVm.GetVisibleTickMarks();
            Assert.True(ticks.Count > 0);
        }

        [Fact]
        public void GetCurrentMajorInterval_返回正值()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            double interval = tlVm.GetCurrentMajorInterval();
            Assert.True(interval > 0);
        }

        // ===== 时间转换测试 =====

        [Fact]
        public void PlayheadTimeSeconds_正确()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.PlayheadSample = 88200; // 2秒
            Assert.Equal(2.0, tlVm.PlayheadTimeSeconds, 3);
        }

        [Fact]
        public void SetPlayheadTime_秒转采样()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.SetPlayheadTime(3.0); // 3秒
            Assert.Equal(132300, tlVm.PlayheadSample);
        }

        // ===== IsPlayheadVisible 测试 =====

        [Fact]
        public void IsPlayheadVisible_在可见区域内_返回true()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            tlVm.PlayheadSample = 100000;
            Assert.True(tlVm.IsPlayheadVisible);
        }

        [Fact]
        public void IsPlayheadVisible_缩放后超出视口_返回false()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            // 放大多次使视口很窄
            for (int i = 0; i < 10; i++) wfVm.ZoomIn();

            // 设置播放头到远端
            tlVm.PlayheadSample = 400000;
            Assert.False(tlVm.IsPlayheadVisible);
        }

        // ===== ScrollToPlayhead 测试 =====

        [Fact]
        public void ScrollToPlayhead_视口居中到播放头()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            // 放大
            for (int i = 0; i < 5; i++) wfVm.ZoomIn();

            tlVm.PlayheadSample = 220000;
            tlVm.ScrollToPlayhead();

            // 播放头应该在视口中间附近
            Assert.True(tlVm.IsPlayheadVisible, "ScrollToPlayhead 后播放头应可见");
        }

        // ===== 属性变更通知测试 =====

        [Fact]
        public void 属性变更通知_PlayheadSample()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            bool notified = false;
            tlVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "PlayheadSample") notified = true;
            };

            tlVm.PlayheadSample = 44100;
            Assert.True(notified);
        }

        [Fact]
        public void 属性变更通知_PlayheadTimeText()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            bool notified = false;
            tlVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "PlayheadTimeText") notified = true;
            };

            tlVm.PlayheadSample = 44100;
            Assert.True(notified);
        }

        [Fact]
        public void 属性变更通知_IsDraggingPlayhead()
        {
            var wfVm = CreateWaveformVm(441000, 44100, 1000);
            var tlVm = new TimelineViewModel(wfVm);

            bool notified = false;
            tlVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "IsDraggingPlayhead") notified = true;
            };

            tlVm.StartDragPlayhead(100);
            Assert.True(notified);
        }
    }
}
