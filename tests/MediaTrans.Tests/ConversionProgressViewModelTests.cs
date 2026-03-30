using System;
using Xunit;
using MediaTrans.Services;
using MediaTrans.ViewModels;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 转换进度 ViewModel 测试
    /// </summary>
    public class ConversionProgressViewModelTests
    {
        #region FormatTimeSpan 测试

        [Fact]
        public void FormatTimeSpan_Hours_ReturnsHHMMSS()
        {
            var ts = new TimeSpan(2, 15, 30);
            string result = ConversionProgressViewModel.FormatTimeSpan(ts);
            Assert.Equal("02:15:30", result);
        }

        [Fact]
        public void FormatTimeSpan_Minutes_ReturnsMMSS()
        {
            var ts = new TimeSpan(0, 5, 30);
            string result = ConversionProgressViewModel.FormatTimeSpan(ts);
            Assert.Equal("05:30", result);
        }

        [Fact]
        public void FormatTimeSpan_Seconds_ReturnsSeconds()
        {
            var ts = new TimeSpan(0, 0, 45);
            string result = ConversionProgressViewModel.FormatTimeSpan(ts);
            Assert.Equal("45秒", result);
        }

        [Fact]
        public void FormatTimeSpan_Zero_ReturnsZeroSeconds()
        {
            var ts = TimeSpan.Zero;
            string result = ConversionProgressViewModel.FormatTimeSpan(ts);
            Assert.Equal("0秒", result);
        }

        #endregion

        #region EstimateRemainingSeconds 测试

        [Fact]
        public void EstimateRemainingSeconds_HalfDone_ReturnsElapsed()
        {
            // 已过 30 秒，进度 50%，预计还要 30 秒
            double remaining = ConversionProgressViewModel.EstimateRemainingSeconds(30, 50);
            Assert.Equal(30, remaining, 1);
        }

        [Fact]
        public void EstimateRemainingSeconds_QuarterDone_ReturnsTripleElapsed()
        {
            // 已过 10 秒，进度 25%，预计还要 30 秒
            double remaining = ConversionProgressViewModel.EstimateRemainingSeconds(10, 25);
            Assert.Equal(30, remaining, 1);
        }

        [Fact]
        public void EstimateRemainingSeconds_ZeroPercent_ReturnsZero()
        {
            double remaining = ConversionProgressViewModel.EstimateRemainingSeconds(10, 0);
            Assert.Equal(0, remaining);
        }

        [Fact]
        public void EstimateRemainingSeconds_FullPercent_ReturnsZero()
        {
            double remaining = ConversionProgressViewModel.EstimateRemainingSeconds(10, 100);
            Assert.Equal(0, remaining);
        }

        #endregion

        #region StartConversion 测试

        [Fact]
        public void StartConversion_SetsState()
        {
            var vm = new ConversionProgressViewModel();
            vm.StartConversion("test.mp4");

            Assert.True(vm.IsConverting);
            Assert.Equal("test.mp4", vm.CurrentFileName);
            Assert.Equal(0, vm.ProgressPercentage);
            Assert.Equal("0%", vm.ProgressText);
            Assert.True(vm.LogEntries.Count > 0);
        }

        #endregion

        #region UpdateProgress 测试

        [Fact]
        public void UpdateProgress_WithPercentage_UpdatesProgressText()
        {
            var vm = new ConversionProgressViewModel();
            vm.StartConversion("test.mp4");

            var e = new FFmpegProgressEventArgs();
            e.ProcessedSeconds = 30;
            e.TotalSeconds = 60;
            // Percentage 是计算属性

            vm.UpdateProgress(e);

            Assert.Equal(string.Format("{0:F1}%", e.Percentage), vm.ProgressText);
        }

        #endregion

        #region CompleteConversion 测试

        [Fact]
        public void CompleteConversion_Success_SetsComplete()
        {
            var vm = new ConversionProgressViewModel();
            vm.StartConversion("test.mp4");
            vm.CompleteConversion(true, "");

            Assert.False(vm.IsConverting);
            Assert.Equal(100, vm.ProgressPercentage);
            Assert.Equal("完成", vm.ProgressText);
        }

        [Fact]
        public void CompleteConversion_Failure_SetsFailed()
        {
            var vm = new ConversionProgressViewModel();
            vm.StartConversion("test.mp4");
            vm.CompleteConversion(false, "编码错误");

            Assert.False(vm.IsConverting);
            Assert.Equal("失败", vm.ProgressText);
        }

        #endregion

        #region CancelConversion 测试

        [Fact]
        public void CancelConversion_SetsCancelled()
        {
            var vm = new ConversionProgressViewModel();
            vm.StartConversion("test.mp4");
            vm.CancelConversion();

            Assert.False(vm.IsConverting);
            Assert.Equal("已取消", vm.ProgressText);
        }

        #endregion

        #region 日志管理测试

        [Fact]
        public void AddLogEntry_AddsToCollection()
        {
            var vm = new ConversionProgressViewModel();
            vm.AddLogEntry("测试日志");

            Assert.Equal(1, vm.LogEntries.Count);
            Assert.Equal("测试日志", vm.LogEntries[0]);
        }

        [Fact]
        public void AddLogEntry_ExceedsMaxLines_TruncatesOldEntries()
        {
            var vm = new ConversionProgressViewModel();
            vm.MaxLogLines = 5;

            for (int i = 0; i < 8; i++)
            {
                vm.AddLogEntry(string.Format("日志 {0}", i));
            }

            Assert.Equal(5, vm.LogEntries.Count);
            // 最早的 3 条应被移除
            Assert.Equal("日志 3", vm.LogEntries[0]);
            Assert.Equal("日志 7", vm.LogEntries[4]);
        }

        [Fact]
        public void ClearLog_RemovesAllEntries()
        {
            var vm = new ConversionProgressViewModel();
            vm.AddLogEntry("日志1");
            vm.AddLogEntry("日志2");

            vm.ClearLogCommand.Execute(null);

            Assert.Equal(0, vm.LogEntries.Count);
        }

        #endregion
    }
}
