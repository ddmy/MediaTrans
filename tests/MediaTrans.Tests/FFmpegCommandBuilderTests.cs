using System;
using Xunit;
using MediaTrans.Services;

namespace MediaTrans.Tests
{
    /// <summary>
    /// FFmpegCommandBuilder 单元测试
    /// </summary>
    public class FFmpegCommandBuilderTests
    {
        [Fact]
        public void Build_基本视频转换_包含显式编解码器()
        {
            // 准备 & 执行
            string cmd = new FFmpegCommandBuilder()
                .Input(@"C:\test\input.avi")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output(@"C:\test\output.mp4")
                .Build();

            // 验证：包含 -c:v 和 -c:a
            Assert.Contains("-c:v libx264", cmd);
            Assert.Contains("-c:a aac", cmd);
            // 路径双引号包裹
            Assert.Contains("\"C:\\test\\input.avi\"", cmd);
            Assert.Contains("\"C:\\test\\output.mp4\"", cmd);
        }

        [Fact]
        public void Build_路径含空格和中文_正确双引号包裹()
        {
            // 准备 & 执行
            string cmd = new FFmpegCommandBuilder()
                .Input(@"C:\我的 视频\测试文件.avi")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output(@"C:\输出 目录\结果.mp4")
                .Build();

            // 验证
            Assert.Contains("\"C:\\我的 视频\\测试文件.avi\"", cmd);
            Assert.Contains("\"C:\\输出 目录\\结果.mp4\"", cmd);
        }

        [Fact]
        public void Build_仅提取音频_包含vn和音频编解码器()
        {
            // 准备 & 执行
            string cmd = new FFmpegCommandBuilder()
                .Input(@"C:\test\video.mp4")
                .NoVideo()
                .AudioCodec("aac")
                .Output(@"C:\test\audio.m4a")
                .Build();

            // 验证
            Assert.Contains("-vn", cmd);
            Assert.Contains("-c:a aac", cmd);
            Assert.DoesNotContain("-c:v", cmd);
        }

        [Fact]
        public void Build_仅保留视频_包含an和视频编解码器()
        {
            // 准备 & 执行
            string cmd = new FFmpegCommandBuilder()
                .Input(@"C:\test\video.mp4")
                .NoAudio()
                .VideoCodec("libx264")
                .Output(@"C:\test\video_only.mp4")
                .Build();

            // 验证
            Assert.Contains("-an", cmd);
            Assert.Contains("-c:v libx264", cmd);
            Assert.DoesNotContain("-c:a", cmd);
        }

        [Fact]
        public void Build_包含分辨率和比特率()
        {
            // 准备 & 执行
            string cmd = new FFmpegCommandBuilder()
                .Input(@"C:\test\input.avi")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Resolution(1920, 1080)
                .VideoBitrate("5M")
                .AudioBitrate("192k")
                .FrameRate(30)
                .Output(@"C:\test\output.mp4")
                .Build();

            // 验证
            Assert.Contains("-s 1920x1080", cmd);
            Assert.Contains("-b:v 5M", cmd);
            Assert.Contains("-b:a 192k", cmd);
            Assert.Contains("-r 30", cmd);
        }

        [Fact]
        public void Build_多线程设置()
        {
            // 准备 & 执行
            string cmd = new FFmpegCommandBuilder()
                .Input(@"C:\test\input.avi")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Threads(0)
                .Output(@"C:\test\output.mp4")
                .Build();

            // 验证
            Assert.Contains("-threads 0", cmd);
        }

        [Fact]
        public void Build_默认覆盖输出()
        {
            // 准备 & 执行
            string cmd = new FFmpegCommandBuilder()
                .Input(@"C:\test\input.avi")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output(@"C:\test\output.mp4")
                .Build();

            // 验证
            Assert.StartsWith("-y ", cmd);
        }

        [Fact]
        public void Build_无输入文件_抛出异常()
        {
            // 准备
            var builder = new FFmpegCommandBuilder()
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output(@"C:\test\output.mp4");

            // 验证
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Build_无输出文件_抛出异常()
        {
            // 准备
            var builder = new FFmpegCommandBuilder()
                .Input(@"C:\test\input.avi")
                .VideoCodec("libx264")
                .AudioCodec("aac");

            // 验证
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void HasExplicitCodecs_有视频和音频编解码器_返回true()
        {
            var builder = new FFmpegCommandBuilder()
                .Input("input.avi")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output("output.mp4");

            Assert.True(builder.HasExplicitCodecs);
        }

        [Fact]
        public void HasExplicitCodecs_缺少视频编解码器_返回false()
        {
            var builder = new FFmpegCommandBuilder()
                .Input("input.avi")
                .AudioCodec("aac")
                .Output("output.mp4");

            Assert.False(builder.HasExplicitCodecs);
        }

        [Fact]
        public void HasExplicitCodecs_禁用视频仅音频_返回true()
        {
            var builder = new FFmpegCommandBuilder()
                .Input("input.avi")
                .NoVideo()
                .AudioCodec("aac")
                .Output("output.mp3");

            Assert.True(builder.HasExplicitCodecs);
        }

        [Fact]
        public void HasExplicitCodecs_禁用音频仅视频_返回true()
        {
            var builder = new FFmpegCommandBuilder()
                .Input("input.avi")
                .NoAudio()
                .VideoCodec("libx264")
                .Output("output.mp4");

            Assert.True(builder.HasExplicitCodecs);
        }

        [Fact]
        public void Build_多个输入文件()
        {
            // 准备 & 执行
            string cmd = new FFmpegCommandBuilder()
                .Input(@"C:\test\input1.avi")
                .Input(@"C:\test\input2.avi")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output(@"C:\test\output.mp4")
                .Build();

            // 验证两个输入文件都存在
            Assert.Contains("\"C:\\test\\input1.avi\"", cmd);
            Assert.Contains("\"C:\\test\\input2.avi\"", cmd);
        }

        [Fact]
        public void Build_自定义选项()
        {
            string cmd = new FFmpegCommandBuilder()
                .Input("input.avi")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Option("-preset fast")
                .Option("-crf 23")
                .Output("output.mp4")
                .Build();

            Assert.Contains("-preset fast", cmd);
            Assert.Contains("-crf 23", cmd);
        }

        [Fact]
        public void Build_硬件编解码器()
        {
            string cmd = new FFmpegCommandBuilder()
                .Input("input.avi")
                .VideoCodec("h264_nvenc")
                .AudioCodec("aac")
                .Output("output.mp4")
                .Build();

            Assert.Contains("-c:v h264_nvenc", cmd);
        }
    }
}
