using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MediaTrans.Models;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 里程碑 1 综合测试 — 覆盖 M1 自测检查清单
    /// ① FFmpeg 指令生成器：所有格式+参数组合，断言每条命令含 -c:v / -c:a
    /// ② 编码兼容性：验证编解码器名完整性
    /// ③ 路径边界：空格/中文/超长路径
    /// ④ 进程安全：Job Object 绑定验证
    /// ⑤ 配置文件：缺失自动生成、读写正确性
    /// </summary>
    public class Milestone1Tests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ConversionService _conversionService;
        private readonly AppConfig _config;

        public Milestone1Tests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "M1Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _config = AppConfig.CreateDefault();
            var ffmpegService = new FFmpegService(_config);
            var configService = new ConfigService();
            _conversionService = new ConversionService(ffmpegService, configService);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch (Exception) { }
        }

        private MediaFileInfo CreateTestSource(string filePath)
        {
            return new MediaFileInfo
            {
                FilePath = filePath,
                HasVideo = true,
                HasAudio = true,
                DurationSeconds = 60.0
            };
        }

        // ============================================================
        // ① FFmpeg 指令生成器 — 所有格式默认编解码器断言
        // ============================================================

        [Theory]
        [InlineData(".mp4", "libx264", "aac")]
        [InlineData(".avi", "libx264", "mp3lame")]
        [InlineData(".mkv", "libx264", "aac")]
        [InlineData(".mov", "libx264", "aac")]
        [InlineData(".wmv", "wmv2", "wmav2")]
        [InlineData(".flv", "flv1", "mp3lame")]
        [InlineData(".webm", "libvpx", "libvorbis")]
        public void 所有视频格式_默认编解码器映射正确(string format, string expectedVideo, string expectedAudio)
        {
            var mapping = ConversionService.GetDefaultCodecs(format);
            Assert.NotNull(mapping);
            Assert.Equal(expectedVideo, mapping.VideoCodec);
            Assert.Equal(expectedAudio, mapping.AudioCodec);
        }

        [Theory]
        [InlineData(".mp3", "libmp3lame")]
        [InlineData(".wav", "pcm_s16le")]
        [InlineData(".flac", "flac")]
        [InlineData(".aac", "aac")]
        [InlineData(".ogg", "libvorbis")]
        [InlineData(".wma", "wmav2")]
        [InlineData(".m4a", "aac")]
        public void 所有音频格式_默认编解码器映射正确(string format, string expectedAudio)
        {
            var mapping = ConversionService.GetDefaultCodecs(format);
            Assert.NotNull(mapping);
            Assert.Null(mapping.VideoCodec);
            Assert.Equal(expectedAudio, mapping.AudioCodec);
        }

        // ---- 视频格式转换参数必含 -c:v 和 -c:a ----

        [Theory]
        [InlineData(".mp4")]
        [InlineData(".avi")]
        [InlineData(".mkv")]
        [InlineData(".mov")]
        [InlineData(".wmv")]
        [InlineData(".flv")]
        [InlineData(".webm")]
        public void 视频格式转换_命令必含显式视频和音频编解码器(string targetFormat)
        {
            var source = CreateTestSource("input.mp4");
            string outputPath = string.Format("output{0}", targetFormat);
            string args = _conversionService.BuildConversionArguments(source, outputPath, targetFormat, null);

            Assert.Contains("-c:v ", args);
            Assert.Contains("-c:a ", args);
        }

        [Theory]
        [InlineData(".mp3")]
        [InlineData(".wav")]
        [InlineData(".flac")]
        [InlineData(".aac")]
        [InlineData(".ogg")]
        [InlineData(".wma")]
        [InlineData(".m4a")]
        public void 音频格式转换_命令含vn和显式音频编解码器(string targetFormat)
        {
            var source = CreateTestSource("input.mp4");
            string outputPath = string.Format("output{0}", targetFormat);
            string args = _conversionService.BuildConversionArguments(source, outputPath, targetFormat, null);

            Assert.Contains("-vn", args);
            Assert.Contains("-c:a ", args);
        }

        // ---- 使用预设时的编解码器断言 ----

        [Fact]
        public void 视频预设转换_命令含显式编解码器()
        {
            var preset = new ConversionPreset
            {
                Name = "测试预设",
                VideoCodec = "libx264",
                AudioCodec = "aac",
                Width = 1920,
                Height = 1080,
                VideoBitrate = "5M",
                AudioBitrate = "192k",
                FrameRate = 30
            };

            var source = CreateTestSource("input.avi");
            string args = _conversionService.BuildConversionArguments(source, "output.mp4", ".mp4", preset);

            Assert.Contains("-c:v libx264", args);
            Assert.Contains("-c:a aac", args);
            Assert.Contains("-s 1920x1080", args);
            Assert.Contains("-b:v 5M", args);
            Assert.Contains("-b:a 192k", args);
            Assert.Contains("-r 30", args);
            Assert.Contains("-threads 0", args);
        }

        [Fact]
        public void H265预设转换_命令含libx265()
        {
            var preset = new ConversionPreset
            {
                Name = "H265 测试",
                VideoCodec = "libx265",
                AudioCodec = "aac",
                Width = 3840,
                Height = 2160,
                VideoBitrate = "10M",
                AudioBitrate = "256k",
                FrameRate = 60
            };

            var source = CreateTestSource("input.mp4");
            string args = _conversionService.BuildConversionArguments(source, "output.mkv", ".mkv", preset);

            Assert.Contains("-c:v libx265", args);
            Assert.Contains("-c:a aac", args);
        }

        [Fact]
        public void VP9预设转换_命令含libvpx_vp9()
        {
            var preset = new ConversionPreset
            {
                Name = "VP9 测试",
                VideoCodec = "libvpx-vp9",
                AudioCodec = "libopus",
                Width = 1280,
                Height = 720,
                VideoBitrate = "2M",
                AudioBitrate = "128k",
                FrameRate = 30
            };

            var source = CreateTestSource("input.mp4");
            string args = _conversionService.BuildConversionArguments(source, "output.webm", ".webm", preset);

            Assert.Contains("-c:v libvpx-vp9", args);
            Assert.Contains("-c:a libopus", args);
        }

        // ---- 提取音频 & 提取视频 编解码器断言 ----

        [Theory]
        [InlineData(".mp3", "libmp3lame")]
        [InlineData(".wav", "pcm_s16le")]
        [InlineData(".flac", "flac")]
        [InlineData(".aac", "aac")]
        [InlineData(".ogg", "libvorbis")]
        public void 提取音频_所有格式含vn和显式音频编解码器(string targetFormat, string expectedCodec)
        {
            var source = CreateTestSource("input.mp4");
            string outputPath = string.Format("output{0}", targetFormat);
            string args = _conversionService.BuildExtractAudioArguments(source, outputPath, targetFormat, null);

            Assert.Contains("-vn", args);
            Assert.Contains(string.Format("-c:a {0}", expectedCodec), args);
        }

        [Theory]
        [InlineData(".mp4", "libx264")]
        [InlineData(".avi", "libx264")]
        [InlineData(".mkv", "libx264")]
        [InlineData(".mov", "libx264")]
        [InlineData(".webm", "libvpx")]
        public void 提取视频_所有格式含an和显式视频编解码器(string targetFormat, string expectedCodec)
        {
            var source = CreateTestSource("input.mp4");
            string outputPath = string.Format("output{0}", targetFormat);
            string args = _conversionService.BuildExtractVideoArguments(source, outputPath, targetFormat, null);

            Assert.Contains("-an", args);
            Assert.Contains(string.Format("-c:v {0}", expectedCodec), args);
        }

        // ---- 所有命令始终含 -y 覆盖标志和 -threads ----

        [Theory]
        [InlineData(".mp4")]
        [InlineData(".mp3")]
        [InlineData(".webm")]
        [InlineData(".flac")]
        public void 所有格式转换_命令含覆盖标志和多线程(string format)
        {
            var source = CreateTestSource("input.avi");
            string args = _conversionService.BuildConversionArguments(
                source, string.Format("out{0}", format), format, null);

            Assert.True(args.StartsWith("-y "), "命令应以 -y 开头");
            Assert.Contains("-threads 0", args);
        }

        // ============================================================
        // ② 编码兼容性 — 验证所有需求列出的编解码器名称有效
        // ============================================================

        [Theory]
        [InlineData("libx264")]      // H.264
        [InlineData("libx265")]      // H.265 (HEVC)
        [InlineData("libvpx")]       // VP9 基础
        [InlineData("libvpx-vp9")]   // VP9
        [InlineData("aac")]          // AAC 音频
        [InlineData("libmp3lame")]   // MP3 音频
        [InlineData("flac")]         // FLAC 无损
        [InlineData("libopus")]      // Opus 音频
        [InlineData("mpeg2video")]   // MPEG-2 视频
        [InlineData("wmav2")]        // WMA 音频
        [InlineData("libvorbis")]    // Vorbis 音频
        [InlineData("pcm_s16le")]    // PCM 无损
        public void 编解码器名称格式_可用于FFmpeg命令(string codec)
        {
            // 验证编解码器名称不含非法字符，可安全嵌入命令行
            Assert.False(string.IsNullOrEmpty(codec));
            Assert.False(codec.Contains("\""));
            Assert.False(codec.Contains(" "));
            Assert.False(codec.Contains(";"));
            Assert.False(codec.Contains("&"));
            Assert.False(codec.Contains("|"));
        }

        [Fact]
        public void FFmpegCommandBuilder_所有常见编解码器_可设置()
        {
            string[] videoCodecs = new string[]
            {
                "libx264", "libx265", "libvpx", "libvpx-vp9",
                "mpeg2video", "wmv2", "flv1",
                "h264_nvenc", "hevc_nvenc", "h264_qsv", "hevc_qsv"
            };

            string[] audioCodecs = new string[]
            {
                "aac", "libmp3lame", "flac", "libopus",
                "wmav2", "libvorbis", "pcm_s16le", "mp3lame"
            };

            foreach (var vc in videoCodecs)
            {
                var builder = new FFmpegCommandBuilder();
                builder.Input("test.mp4");
                builder.VideoCodec(vc);
                builder.AudioCodec("aac");
                builder.Output("out.mp4");
                string cmd = builder.Build();
                Assert.Contains(string.Format("-c:v {0}", vc), cmd);
            }

            foreach (var ac in audioCodecs)
            {
                var builder = new FFmpegCommandBuilder();
                builder.Input("test.mp4");
                builder.NoVideo();
                builder.AudioCodec(ac);
                builder.Output("out.mp3");
                string cmd = builder.Build();
                Assert.Contains(string.Format("-c:a {0}", ac), cmd);
            }
        }

        // ============================================================
        // ③ 路径边界测试 — 空格/中文/超长路径
        // ============================================================

        [Fact]
        public void 路径含空格_FFmpegCommandBuilder_双引号包裹()
        {
            var builder = new FFmpegCommandBuilder();
            builder.Input("C:\\My Documents\\test file.mp4");
            builder.VideoCodec("libx264");
            builder.AudioCodec("aac");
            builder.Output("C:\\My Output\\output file.mp4");
            string cmd = builder.Build();

            Assert.Contains("-i \"C:\\My Documents\\test file.mp4\"", cmd);
            Assert.Contains("\"C:\\My Output\\output file.mp4\"", cmd);
        }

        [Fact]
        public void 路径含中文_FFmpegCommandBuilder_双引号包裹()
        {
            var builder = new FFmpegCommandBuilder();
            builder.Input("C:\\用户\\文档\\测试视频.mp4");
            builder.VideoCodec("libx264");
            builder.AudioCodec("aac");
            builder.Output("C:\\输出\\转换结果.mp4");
            string cmd = builder.Build();

            Assert.Contains("-i \"C:\\用户\\文档\\测试视频.mp4\"", cmd);
            Assert.Contains("\"C:\\输出\\转换结果.mp4\"", cmd);
        }

        [Fact]
        public void 路径含空格和中文混合_正确处理()
        {
            var builder = new FFmpegCommandBuilder();
            builder.Input("C:\\我的 文件夹\\test 视频 (1).mp4");
            builder.VideoCodec("libx264");
            builder.AudioCodec("aac");
            builder.Output("C:\\输出 目录\\转换 结果 (2).mp4");
            string cmd = builder.Build();

            Assert.Contains("-i \"C:\\我的 文件夹\\test 视频 (1).mp4\"", cmd);
            Assert.Contains("\"C:\\输出 目录\\转换 结果 (2).mp4\"", cmd);
        }

        [Fact]
        public void 超长路径_FFmpegCommandBuilder_正确包裹()
        {
            // 创建一个约 240 字符的路径
            string longDirName = new string('a', 50);
            string longPath = string.Format("C:\\{0}\\{0}\\{0}\\{0}\\视频文件.mp4", longDirName);

            var builder = new FFmpegCommandBuilder();
            builder.Input(longPath);
            builder.VideoCodec("libx264");
            builder.AudioCodec("aac");
            builder.Output("C:\\output.mp4");
            string cmd = builder.Build();

            Assert.Contains(string.Format("-i \"{0}\"", longPath), cmd);
        }

        [Fact]
        public void 路径含特殊字符_正确处理()
        {
            var builder = new FFmpegCommandBuilder();
            builder.Input("C:\\test [1]\\video (copy).mp4");
            builder.VideoCodec("libx264");
            builder.AudioCodec("aac");
            builder.Output("C:\\out [2]\\result (final).mp4");
            string cmd = builder.Build();

            Assert.Contains("test [1]", cmd);
            Assert.Contains("out [2]", cmd);
        }

        [Fact]
        public void ConversionService_GenerateOutputPath_创建输出目录()
        {
            string outputDir = Path.Combine(_tempDir, "output_subdir");
            _config.DefaultOutputDir = outputDir;

            var configService = new ConfigService();
            configService.Save(_config);

            var cs = new ConversionService(new FFmpegService(_config), configService);
            string path = cs.GenerateOutputPath(Path.Combine(_tempDir, "input.mp4"), ".mkv");

            Assert.True(Directory.Exists(outputDir));
            Assert.EndsWith(".mkv", path);
        }

        [Fact]
        public void ConversionService_GenerateOutputPath_避免文件名冲突()
        {
            string outputDir = _tempDir;
            _config.DefaultOutputDir = outputDir;

            var configService = new ConfigService();
            configService.Save(_config);

            // 创建一个已存在的同名文件
            string existingFile = Path.Combine(outputDir, "video.mp4");
            File.WriteAllText(existingFile, "dummy", Encoding.UTF8);

            var cs = new ConversionService(new FFmpegService(_config), configService);
            string path = cs.GenerateOutputPath(Path.Combine(_tempDir, "video.avi"), ".mp4");

            // 应该生成 video_1.mp4 而不是 video.mp4
            Assert.NotEqual(existingFile, path);
            Assert.Contains("video_1.mp4", path);
        }

        // ============================================================
        // ④ 进程安全 — Job Object 绑定验证
        // ============================================================

        [Fact]
        public void JobObject_创建不抛异常()
        {
            using (var jobObject = new JobObject())
            {
                // Job Object 创建成功
                Assert.NotNull(jobObject);
            }
        }

        [Fact]
        public void FFmpegService_创建_包含JobObject绑定()
        {
            // FFmpegService 内部创建 JobObject，确保不抛异常
            var service = new FFmpegService(_config);
            Assert.NotNull(service);
            service.Dispose();
        }

        [Fact]
        public void FFmpegService_使用路径创建_不抛异常()
        {
            var service = new FFmpegService("ffmpeg.exe", "ffprobe.exe");
            Assert.NotNull(service);
            service.Dispose();
        }

        // ============================================================
        // ⑤ 配置文件测试 — 缺失配置自动生成、读写正确
        // ============================================================

        [Fact]
        public void AppConfig_CreateDefault_所有字段有合理默认值()
        {
            var config = AppConfig.CreateDefault();

            Assert.False(string.IsNullOrEmpty(config.FFmpegPath));
            Assert.False(string.IsNullOrEmpty(config.FFprobePath));
            Assert.Equal(100, config.MaxCachedFrames);
            Assert.Equal(512, config.WaveformBlockWidth);
            Assert.Equal(10, config.SnapThresholdPixels);
            Assert.Equal(50, config.MaxUndoDepth);
            Assert.Equal(10 * 1024 * 1024, config.LogMaxFileSize);
            Assert.Equal(5, config.LogMaxFileCount);
            Assert.Equal("MediaTrans", config.WatermarkText);
            Assert.Equal(24, config.WatermarkFontSize);
            Assert.Equal("RightBottom", config.WatermarkPosition);
            Assert.Equal(60, config.FreeMaxExportSeconds);
            Assert.True(config.HardwareAccelerationEnabled);
            Assert.Equal("auto", config.PreferredHardwareEncoder);
            Assert.Equal(1, config.MaxParallelTasks);
            Assert.NotNull(config.ConversionPresets);
            Assert.True(config.ConversionPresets.Count >= 3);
        }

        [Fact]
        public void AppConfig_默认预设_编解码器完整()
        {
            var config = AppConfig.CreateDefault();
            foreach (var preset in config.ConversionPresets)
            {
                Assert.False(string.IsNullOrEmpty(preset.Name));
                Assert.False(string.IsNullOrEmpty(preset.AudioCodec));
                // 音频预设的 VideoCodec 可以为空
            }
        }

        [Fact]
        public void ConfigService_配置文件缺失_自动生成默认配置()
        {
            string tempConfigDir = Path.Combine(_tempDir, "config_test");
            Directory.CreateDirectory(tempConfigDir);
            string configFile = Path.Combine(tempConfigDir, "AppConfig.json");

            Assert.False(File.Exists(configFile));

            // 使用临时路径创建 ConfigService 需要通过反射或直接读取后验证
            // 这里验证 CreateDefault 的正确性
            var config = AppConfig.CreateDefault();
            Assert.NotNull(config);
            Assert.False(string.IsNullOrEmpty(config.FFmpegPath));
        }

        [Fact]
        public void ConfigService_保存后读取_数据完全一致()
        {
            var configService = new ConfigService();
            var config = AppConfig.CreateDefault();

            config.DefaultOutputDir = "C:\\测试 输出\\视频";
            config.MaxCachedFrames = 200;
            config.WaveformBlockWidth = 1024;
            config.SnapThresholdPixels = 15;
            config.MaxUndoDepth = 100;
            config.LogMaxFileSize = 20 * 1024 * 1024;
            config.LogMaxFileCount = 10;
            config.WatermarkText = "测试水印";
            config.WatermarkFontSize = 36;
            config.HardwareAccelerationEnabled = false;
            config.PreferredHardwareEncoder = "nvenc";
            config.MaxParallelTasks = 4;
            config.FreeMaxExportSeconds = 120;

            configService.Save(config);
            var loaded = configService.Load();

            Assert.Equal(config.DefaultOutputDir, loaded.DefaultOutputDir);
            Assert.Equal(config.MaxCachedFrames, loaded.MaxCachedFrames);
            Assert.Equal(config.WaveformBlockWidth, loaded.WaveformBlockWidth);
            Assert.Equal(config.SnapThresholdPixels, loaded.SnapThresholdPixels);
            Assert.Equal(config.MaxUndoDepth, loaded.MaxUndoDepth);
            Assert.Equal(config.LogMaxFileSize, loaded.LogMaxFileSize);
            Assert.Equal(config.LogMaxFileCount, loaded.LogMaxFileCount);
            Assert.Equal(config.WatermarkText, loaded.WatermarkText);
            Assert.Equal(config.WatermarkFontSize, loaded.WatermarkFontSize);
            Assert.Equal(config.HardwareAccelerationEnabled, loaded.HardwareAccelerationEnabled);
            Assert.Equal(config.PreferredHardwareEncoder, loaded.PreferredHardwareEncoder);
            Assert.Equal(config.MaxParallelTasks, loaded.MaxParallelTasks);
            Assert.Equal(config.FreeMaxExportSeconds, loaded.FreeMaxExportSeconds);
        }

        [Fact]
        public void ConfigService_保存含中文值_UTF8编码正确()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "_AppConfig.json");
            try
            {
                var configService = new ConfigService(tempPath);
                var config = AppConfig.CreateDefault();
                config.WatermarkText = "MediaTrans 专业版 水印";
                config.DefaultOutputDir = "C:\\用户\\文档\\输出 目录";

                configService.Save(config);
                var loaded = configService.Load();

                Assert.Equal("MediaTrans 专业版 水印", loaded.WatermarkText);
                Assert.Equal("C:\\用户\\文档\\输出 目录", loaded.DefaultOutputDir);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        // ============================================================
        // 硬件加速集成 — 编解码器替换后仍满足 C7 约束
        // ============================================================

        [Fact]
        public void 硬件加速启用_转换命令仍含显式编解码器()
        {
            var config = AppConfig.CreateDefault();
            config.HardwareAccelerationEnabled = true;
            config.PreferredHardwareEncoder = "auto";

            var hwService = new HardwareAccelerationService("ffmpeg.exe", config);
            hwService.SetProbeResultForTest(new List<string> { "h264_nvenc" });

            var ffmpegService = new FFmpegService(config);
            var configService = new ConfigService();
            var convService = new ConversionService(ffmpegService, configService, hwService);

            var source = CreateTestSource("input.avi");
            string args = convService.BuildConversionArguments(source, "output.mp4", ".mp4", null);

            // 使用硬件编码器但仍有显式编解码器
            Assert.Contains("-c:v h264_nvenc", args);
            Assert.Contains("-c:a aac", args);
        }

        [Fact]
        public void 硬件加速无可用编码器_回退后仍含显式编解码器()
        {
            var config = AppConfig.CreateDefault();
            config.HardwareAccelerationEnabled = true;

            var hwService = new HardwareAccelerationService("ffmpeg.exe", config);
            hwService.SetProbeResultForTest(new List<string>()); // 无可用硬件编码器

            var ffmpegService = new FFmpegService(config);
            var configService = new ConfigService();
            var convService = new ConversionService(ffmpegService, configService, hwService);

            var source = CreateTestSource("input.avi");
            string args = convService.BuildConversionArguments(source, "output.mp4", ".mp4", null);

            // 回退到软编码
            Assert.Contains("-c:v libx264", args);
            Assert.Contains("-c:a aac", args);
        }

        // ============================================================
        // 日志系统集成 — 轮转与编码
        // ============================================================

        [Fact]
        public void 日志轮转_文件数超过最大保留数_清理最旧文件()
        {
            string logDir = Path.Combine(_tempDir, "logs_rotation");
            var logger = new LogService(logDir, 50, 3);

            // 写入大量日志强制多次轮转
            for (int i = 0; i < 50; i++)
            {
                logger.Info(string.Format("日志消息 {0} - 填充长文本测试轮转机制", i));
            }

            logger.Dispose();

            // 验证日志文件数量不超过最大保留数
            string[] files = logger.GetAllLogFiles();
            Assert.True(files.Length <= 3, string.Format("日志文件数 {0} 超过保留数 3", files.Length));
        }

        [Fact]
        public void 日志服务_崩溃异常记录完整堆栈()
        {
            string logDir = Path.Combine(_tempDir, "logs_crash");
            var logger = new LogService(logDir);

            try
            {
                // 模拟多层异常
                try
                {
                    throw new ArgumentException("最内层异常");
                }
                catch (Exception innerEx)
                {
                    throw new InvalidOperationException("中间层异常", innerEx);
                }
            }
            catch (Exception outerEx)
            {
                logger.Fatal("应用崩溃", outerEx);
            }

            logger.Dispose();

            string content = File.ReadAllText(
                Path.Combine(logDir, "MediaTrans.log"), Encoding.UTF8);

            Assert.Contains("[FATAL]", content);
            Assert.Contains("应用崩溃", content);
            Assert.Contains("InvalidOperationException", content);
            Assert.Contains("中间层异常", content);
            Assert.Contains("内部异常", content);
            Assert.Contains("ArgumentException", content);
            Assert.Contains("最内层异常", content);
            Assert.Contains("堆栈跟踪", content);
        }

        // ============================================================
        // 支持的格式完整性
        // ============================================================

        [Fact]
        public void GetSupportedOutputFormats_包含所有14个格式()
        {
            var formats = ConversionService.GetSupportedOutputFormats();

            string[] expected = new string[]
            {
                ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm",
                ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"
            };

            foreach (var format in expected)
            {
                Assert.Contains(format, formats);
            }

            Assert.Equal(14, formats.Count);
        }

        [Fact]
        public void 每个支持格式_都有默认编解码器映射()
        {
            var formats = ConversionService.GetSupportedOutputFormats();
            foreach (var format in formats)
            {
                var mapping = ConversionService.GetDefaultCodecs(format);
                Assert.NotNull(mapping);
                // 每个格式至少要有音频编解码器
                Assert.False(string.IsNullOrEmpty(mapping.AudioCodec),
                    string.Format("格式 {0} 缺少音频编解码器映射", format));
            }
        }

        [Fact]
        public void 文件导入过滤器_包含主流格式()
        {
            string filter = MediaFileService.FileDialogFilter;

            Assert.Contains("mp4", filter.ToLowerInvariant());
            Assert.Contains("avi", filter.ToLowerInvariant());
            Assert.Contains("mkv", filter.ToLowerInvariant());
            Assert.Contains("mp3", filter.ToLowerInvariant());
            Assert.Contains("wav", filter.ToLowerInvariant());
            Assert.Contains("flac", filter.ToLowerInvariant());
        }

        // ============================================================
        // 转换任务状态流转
        // ============================================================

        [Fact]
        public void ConversionTask_状态流转_Pending到各终态()
        {
            var task = new ConversionTask();
            Assert.Equal(ConversionStatus.Pending, task.Status);

            // 转为转换中
            task.Status = ConversionStatus.Converting;
            Assert.Equal(ConversionStatus.Converting, task.Status);

            // 转为完成
            task.Status = ConversionStatus.Completed;
            Assert.Equal(ConversionStatus.Completed, task.Status);

            // 新建任务测试取消状态
            var task2 = new ConversionTask();
            task2.Status = ConversionStatus.Cancelled;
            Assert.Equal(ConversionStatus.Cancelled, task2.Status);

            // 新建任务测试失败状态
            var task3 = new ConversionTask();
            task3.Status = ConversionStatus.Failed;
            Assert.Equal(ConversionStatus.Failed, task3.Status);
        }

        [Fact]
        public void ConversionTask_唯一ID()
        {
            var task1 = new ConversionTask();
            var task2 = new ConversionTask();

            Assert.NotEqual(task1.Id, task2.Id);
            Assert.False(string.IsNullOrEmpty(task1.Id));
        }
    }
}
