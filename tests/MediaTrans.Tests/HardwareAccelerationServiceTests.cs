using System;
using System.Collections.Generic;
using MediaTrans.Models;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 硬件加速服务单元测试
    /// </summary>
    public class HardwareAccelerationServiceTests
    {
        private AppConfig CreateConfig(bool enabled, string preferred)
        {
            var config = AppConfig.CreateDefault();
            config.HardwareAccelerationEnabled = enabled;
            config.PreferredHardwareEncoder = preferred;
            return config;
        }

        private HardwareAccelerationService CreateService(bool enabled, string preferred, List<string> availableEncoders)
        {
            var config = CreateConfig(enabled, preferred);
            var service = new HardwareAccelerationService("ffmpeg.exe", config);
            service.SetProbeResultForTest(availableEncoders);
            return service;
        }

        // ===== ParseEncoderList 测试 =====

        [Fact]
        public void ParseEncoderList_空输出_返回空列表()
        {
            var result = HardwareAccelerationService.ParseEncoderList("", new string[] { "h264_nvenc" });
            Assert.Empty(result);
        }

        [Fact]
        public void ParseEncoderList_null输出_返回空列表()
        {
            var result = HardwareAccelerationService.ParseEncoderList(null, new string[] { "h264_nvenc" });
            Assert.Empty(result);
        }

        [Fact]
        public void ParseEncoderList_包含NVENC编码器_正确识别()
        {
            string output =
                "Encoders:\n" +
                " V..... libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codec h264)\n" +
                " V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)\n" +
                " V..... hevc_nvenc           NVIDIA NVENC hevc encoder (codec hevc)\n" +
                " A..... aac                  AAC (Advanced Audio Coding)\n";

            var result = HardwareAccelerationService.ParseEncoderList(output,
                new string[] { "h264_nvenc", "hevc_nvenc", "h264_qsv", "hevc_qsv" });

            Assert.Contains("h264_nvenc", result);
            Assert.Contains("hevc_nvenc", result);
            Assert.DoesNotContain("h264_qsv", result);
            Assert.DoesNotContain("hevc_qsv", result);
        }

        [Fact]
        public void ParseEncoderList_包含QSV编码器_正确识别()
        {
            string output =
                "Encoders:\n" +
                " V..... libx264              libx264 H.264\n" +
                " V..... h264_qsv             H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (Intel Quick Sync Video acceleration) (codec h264)\n" +
                " V..... hevc_qsv             HEVC (Intel Quick Sync Video acceleration) (codec hevc)\n";

            var result = HardwareAccelerationService.ParseEncoderList(output,
                new string[] { "h264_nvenc", "hevc_nvenc", "h264_qsv", "hevc_qsv" });

            Assert.DoesNotContain("h264_nvenc", result);
            Assert.Contains("h264_qsv", result);
            Assert.Contains("hevc_qsv", result);
        }

        [Fact]
        public void ParseEncoderList_无硬件编码器_返回空列表()
        {
            string output =
                "Encoders:\n" +
                " V..... libx264              libx264 H.264\n" +
                " V..... libx265              libx265 H.265 / HEVC\n" +
                " A..... aac                  AAC\n";

            var result = HardwareAccelerationService.ParseEncoderList(output,
                new string[] { "h264_nvenc", "hevc_nvenc", "h264_qsv", "hevc_qsv" });

            Assert.Empty(result);
        }

        [Fact]
        public void ParseEncoderList_不重复添加()
        {
            string output =
                " V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)\n" +
                " V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)\n";

            var result = HardwareAccelerationService.ParseEncoderList(output,
                new string[] { "h264_nvenc" });

            Assert.Equal(1, result.Count);
        }

        // ===== 探测结果属性测试 =====

        [Fact]
        public void SetProbeResult_有NVENC_IsNvencAvailable为true()
        {
            var service = CreateService(true, "auto",
                new List<string> { "h264_nvenc", "hevc_nvenc" });

            Assert.True(service.IsNvencAvailable);
            Assert.False(service.IsQsvAvailable);
            Assert.True(service.IsProbed);
        }

        [Fact]
        public void SetProbeResult_有QSV_IsQsvAvailable为true()
        {
            var service = CreateService(true, "auto",
                new List<string> { "h264_qsv", "hevc_qsv" });

            Assert.False(service.IsNvencAvailable);
            Assert.True(service.IsQsvAvailable);
        }

        [Fact]
        public void SetProbeResult_都有_两者均为true()
        {
            var service = CreateService(true, "auto",
                new List<string> { "h264_nvenc", "hevc_nvenc", "h264_qsv", "hevc_qsv" });

            Assert.True(service.IsNvencAvailable);
            Assert.True(service.IsQsvAvailable);
        }

        [Fact]
        public void SetProbeResult_无硬件编码器_两者均为false()
        {
            var service = CreateService(true, "auto", new List<string>());

            Assert.False(service.IsNvencAvailable);
            Assert.False(service.IsQsvAvailable);
        }

        // ===== ResolveVideoCodec 测试 =====

        [Fact]
        public void ResolveVideoCodec_硬件加速禁用_返回原编码器()
        {
            var service = CreateService(false, "auto",
                new List<string> { "h264_nvenc", "hevc_nvenc" });

            Assert.Equal("libx264", service.ResolveVideoCodec("libx264"));
        }

        [Fact]
        public void ResolveVideoCodec_自动模式_有NVENC_优先使用NVENC()
        {
            var service = CreateService(true, "auto",
                new List<string> { "h264_nvenc", "h264_qsv" });

            Assert.Equal("h264_nvenc", service.ResolveVideoCodec("libx264"));
        }

        [Fact]
        public void ResolveVideoCodec_自动模式_仅有QSV_使用QSV()
        {
            var service = CreateService(true, "auto",
                new List<string> { "h264_qsv" });

            Assert.Equal("h264_qsv", service.ResolveVideoCodec("libx264"));
        }

        [Fact]
        public void ResolveVideoCodec_自动模式_无硬件编码器_回退软编码()
        {
            var service = CreateService(true, "auto", new List<string>());

            Assert.Equal("libx264", service.ResolveVideoCodec("libx264"));
        }

        [Fact]
        public void ResolveVideoCodec_指定NVENC_有NVENC_使用NVENC()
        {
            var service = CreateService(true, "nvenc",
                new List<string> { "h264_nvenc", "h264_qsv" });

            Assert.Equal("h264_nvenc", service.ResolveVideoCodec("libx264"));
        }

        [Fact]
        public void ResolveVideoCodec_指定NVENC_无NVENC_回退软编码()
        {
            var service = CreateService(true, "nvenc",
                new List<string> { "h264_qsv" });

            Assert.Equal("libx264", service.ResolveVideoCodec("libx264"));
        }

        [Fact]
        public void ResolveVideoCodec_指定QSV_有QSV_使用QSV()
        {
            var service = CreateService(true, "qsv",
                new List<string> { "h264_nvenc", "h264_qsv" });

            Assert.Equal("h264_qsv", service.ResolveVideoCodec("libx264"));
        }

        [Fact]
        public void ResolveVideoCodec_指定QSV_无QSV_回退软编码()
        {
            var service = CreateService(true, "qsv",
                new List<string> { "h264_nvenc" });

            Assert.Equal("libx264", service.ResolveVideoCodec("libx264"));
        }

        [Fact]
        public void ResolveVideoCodec_libx265映射NVENC_返回hevc_nvenc()
        {
            var service = CreateService(true, "auto",
                new List<string> { "h264_nvenc", "hevc_nvenc" });

            Assert.Equal("hevc_nvenc", service.ResolveVideoCodec("libx265"));
        }

        [Fact]
        public void ResolveVideoCodec_libx265映射QSV_返回hevc_qsv()
        {
            var service = CreateService(true, "qsv",
                new List<string> { "hevc_qsv" });

            Assert.Equal("hevc_qsv", service.ResolveVideoCodec("libx265"));
        }

        [Fact]
        public void ResolveVideoCodec_无映射关系的编码器_返回原编码器()
        {
            var service = CreateService(true, "auto",
                new List<string> { "h264_nvenc" });

            // libvpx 没有对应的硬件编码器映射
            Assert.Equal("libvpx", service.ResolveVideoCodec("libvpx"));
        }

        [Fact]
        public void ResolveVideoCodec_null输入_返回null()
        {
            var service = CreateService(true, "auto",
                new List<string> { "h264_nvenc" });

            Assert.Null(service.ResolveVideoCodec(null));
        }

        [Fact]
        public void ResolveVideoCodec_空字符串输入_返回空字符串()
        {
            var service = CreateService(true, "auto",
                new List<string> { "h264_nvenc" });

            Assert.Equal("", service.ResolveVideoCodec(""));
        }

        [Fact]
        public void ResolveVideoCodec_未探测_返回原编码器()
        {
            var config = CreateConfig(true, "auto");
            var service = new HardwareAccelerationService("ffmpeg.exe", config);
            // 没有调用 Probe 或 SetProbeResultForTest

            Assert.Equal("libx264", service.ResolveVideoCodec("libx264"));
        }

        // ===== AvailableHardwareEncoders 属性测试 =====

        [Fact]
        public void AvailableHardwareEncoders_返回可用编码器列表()
        {
            var service = CreateService(true, "auto",
                new List<string> { "h264_nvenc", "hevc_nvenc" });

            var encoders = service.AvailableHardwareEncoders;
            Assert.Equal(2, encoders.Count);
            Assert.Contains("h264_nvenc", encoders);
            Assert.Contains("hevc_nvenc", encoders);
        }

        [Fact]
        public void AvailableHardwareEncoders_未探测时_返回空列表()
        {
            var config = CreateConfig(true, "auto");
            var service = new HardwareAccelerationService("ffmpeg.exe", config);

            Assert.Empty(service.AvailableHardwareEncoders);
        }

        // ===== ConversionService 集成测试 =====

        [Fact]
        public void ConversionService_有硬件加速_构建参数使用硬件编码器()
        {
            var config = CreateConfig(true, "auto");
            var ffmpegService = new FFmpegService(config);
            var configService = new ConfigService();
            var hwService = CreateService(true, "auto",
                new List<string> { "h264_nvenc" });
            var convService = new ConversionService(ffmpegService, configService, hwService);

            var source = new MediaFileInfo { FilePath = "test.avi" };
            string args = convService.BuildConversionArguments(source, "output.mp4", ".mp4", null);

            // 应该使用 h264_nvenc 而不是 libx264
            Assert.Contains("-c:v h264_nvenc", args);
        }

        [Fact]
        public void ConversionService_无硬件加速服务_使用软件编码器()
        {
            var config = CreateConfig(true, "auto");
            var ffmpegService = new FFmpegService(config);
            var configService = new ConfigService();
            var convService = new ConversionService(ffmpegService, configService);

            var source = new MediaFileInfo { FilePath = "test.avi" };
            string args = convService.BuildConversionArguments(source, "output.mp4", ".mp4", null);

            // 没有硬件加速服务，使用默认 libx264
            Assert.Contains("-c:v libx264", args);
        }

        [Fact]
        public void ConversionService_硬件加速禁用_使用软件编码器()
        {
            var config = CreateConfig(false, "auto");
            var ffmpegService = new FFmpegService(config);
            var configService = new ConfigService();
            var hwService = CreateService(false, "auto",
                new List<string> { "h264_nvenc" });
            var convService = new ConversionService(ffmpegService, configService, hwService);

            var source = new MediaFileInfo { FilePath = "test.avi" };
            string args = convService.BuildConversionArguments(source, "output.mp4", ".mp4", null);

            // 硬件加速禁用，使用 libx264
            Assert.Contains("-c:v libx264", args);
        }

        [Fact]
        public void ConversionService_预设使用硬件编码器()
        {
            var config = CreateConfig(true, "auto");
            var ffmpegService = new FFmpegService(config);
            var configService = new ConfigService();
            var hwService = CreateService(true, "auto",
                new List<string> { "h264_nvenc" });
            var convService = new ConversionService(ffmpegService, configService, hwService);

            var preset = new ConversionPreset
            {
                Name = "测试",
                VideoCodec = "libx264",
                AudioCodec = "aac",
                Width = 1920,
                Height = 1080,
                VideoBitrate = "5M",
                AudioBitrate = "192k",
                FrameRate = 30
            };

            var source = new MediaFileInfo { FilePath = "test.avi" };
            string args = convService.BuildConversionArguments(source, "output.mp4", ".mp4", preset);

            // 预设中的 libx264 应被替换为 h264_nvenc
            Assert.Contains("-c:v h264_nvenc", args);
            Assert.Contains("-c:a aac", args);
        }

        [Fact]
        public void ConversionService_提取视频_使用硬件编码器()
        {
            var config = CreateConfig(true, "auto");
            var ffmpegService = new FFmpegService(config);
            var configService = new ConfigService();
            var hwService = CreateService(true, "nvenc",
                new List<string> { "h264_nvenc" });
            var convService = new ConversionService(ffmpegService, configService, hwService);

            var source = new MediaFileInfo { FilePath = "test.avi" };
            string args = convService.BuildExtractVideoArguments(source, "output.mp4", ".mp4", null);

            Assert.Contains("-c:v h264_nvenc", args);
            Assert.Contains("-an", args);
        }

        [Fact]
        public void ConversionService_ResolveVideoCodec_委托给硬件加速服务()
        {
            var config = CreateConfig(true, "auto");
            var ffmpegService = new FFmpegService(config);
            var configService = new ConfigService();
            var hwService = CreateService(true, "auto",
                new List<string> { "h264_nvenc" });
            var convService = new ConversionService(ffmpegService, configService, hwService);

            Assert.Equal("h264_nvenc", convService.ResolveVideoCodec("libx264"));
        }

        [Fact]
        public void ConversionService_ResolveVideoCodec_无硬件服务_返回原编码器()
        {
            var config = CreateConfig(true, "auto");
            var ffmpegService = new FFmpegService(config);
            var configService = new ConfigService();
            var convService = new ConversionService(ffmpegService, configService);

            Assert.Equal("libx264", convService.ResolveVideoCodec("libx264"));
        }
    }
}
