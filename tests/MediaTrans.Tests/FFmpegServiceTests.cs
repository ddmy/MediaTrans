using System;
using Xunit;
using MediaTrans.Services;

namespace MediaTrans.Tests
{
    /// <summary>
    /// FFmpegService 进度解析与时长解析测试
    /// </summary>
    public class FFmpegServiceTests
    {
        [Theory]
        [InlineData("  Duration: 01:23:45.67, start:", 5025.67)]
        [InlineData("Duration: 00:05:30.00, bitrate:", 330.0)]
        [InlineData("Duration: 00:00:10.50,", 10.5)]
        [InlineData("Duration: 00:00:00.00,", 0.0)]
        public void ParseDuration_各种格式_正确解析(string input, double expected)
        {
            double result = FFmpegService.ParseDuration(input);
            Assert.Equal(expected, result, 2);
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("no duration here", 0)]
        public void ParseDuration_无效输入_返回0(string input, double expected)
        {
            double result = FFmpegService.ParseDuration(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void 构造函数_config为null_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FFmpegService((Models.AppConfig)null));
        }

        [Fact]
        public void JobObject_可以创建和释放()
        {
            // 验证 JobObject 创建和释放不会抛异常
            using (var job = new JobObject())
            {
                // 正常创建
            }
        }

        [Fact]
        public void FFmpegProgressEventArgs_百分比计算_正确()
        {
            var args = new FFmpegProgressEventArgs
            {
                ProcessedSeconds = 50,
                TotalSeconds = 100
            };

            Assert.Equal(50.0, args.Percentage, 1);
        }

        [Fact]
        public void FFmpegProgressEventArgs_总时长未知_百分比为负1()
        {
            var args = new FFmpegProgressEventArgs
            {
                ProcessedSeconds = 50,
                TotalSeconds = 0
            };

            Assert.Equal(-1, args.Percentage);
        }

        [Fact]
        public void FFmpegProgressEventArgs_超过100_限制为100()
        {
            var args = new FFmpegProgressEventArgs
            {
                ProcessedSeconds = 150,
                TotalSeconds = 100
            };

            Assert.Equal(100.0, args.Percentage, 1);
        }

        [Fact]
        public void FFmpegResult_默认值_正确()
        {
            var result = new FFmpegResult();
            Assert.False(result.Success);
            Assert.False(result.Cancelled);
            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
        }
    }
}
