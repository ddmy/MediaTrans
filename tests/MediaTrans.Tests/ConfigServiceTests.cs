using System;
using System.IO;
using Xunit;
using MediaTrans.Models;
using MediaTrans.Services;

namespace MediaTrans.Tests
{
    /// <summary>
    /// ConfigService 单元测试
    /// </summary>
    public class ConfigServiceTests : IDisposable
    {
        private readonly string _testDir;

        public ConfigServiceTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "MediaTransTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Fact]
        public void Load_配置文件不存在时_生成默认配置()
        {
            // 准备
            string configPath = Path.Combine(_testDir, "Config", "AppConfig.json");
            var service = new ConfigService(configPath);

            // 执行
            AppConfig config = service.Load();

            // 验证
            Assert.NotNull(config);
            Assert.True(File.Exists(configPath), "配置文件应自动生成");
            Assert.Equal(@"lib\ffmpeg\ffmpeg.exe", config.FFmpegPath);
            Assert.Equal(100, config.MaxCachedFrames);
            Assert.Equal(512, config.WaveformBlockWidth);
            Assert.Equal(50, config.MaxUndoDepth);
            Assert.Equal(10 * 1024 * 1024, config.LogMaxFileSize);
            Assert.Equal(5, config.LogMaxFileCount);
        }

        [Fact]
        public void Save_保存后重新加载_数据一致()
        {
            // 准备
            string configPath = Path.Combine(_testDir, "AppConfig.json");
            var service = new ConfigService(configPath);
            var config = AppConfig.CreateDefault();
            config.FFmpegPath = @"C:\自定义路径\ffmpeg.exe";
            config.MaxCachedFrames = 200;
            config.WatermarkText = "测试水印";

            // 执行
            service.Save(config);

            // 重新加载验证
            var service2 = new ConfigService(configPath);
            AppConfig loaded = service2.Load();

            Assert.Equal(@"C:\自定义路径\ffmpeg.exe", loaded.FFmpegPath);
            Assert.Equal(200, loaded.MaxCachedFrames);
            Assert.Equal("测试水印", loaded.WatermarkText);
        }

        [Fact]
        public void Load_配置文件损坏时_恢复默认配置()
        {
            // 准备：写入无效 JSON
            string configPath = Path.Combine(_testDir, "AppConfig.json");
            File.WriteAllText(configPath, "这不是有效的JSON{{{");
            var service = new ConfigService(configPath);

            // 执行
            AppConfig config = service.Load();

            // 验证：应回退到默认配置
            Assert.NotNull(config);
            Assert.Equal(@"lib\ffmpeg\ffmpeg.exe", config.FFmpegPath);
        }

        [Fact]
        public void CreateDefault_包含默认预设()
        {
            // 执行
            var config = AppConfig.CreateDefault();

            // 验证
            Assert.NotNull(config.ConversionPresets);
            Assert.True(config.ConversionPresets.Count >= 2, "应包含至少2个默认预设");
            Assert.Equal("高质量 1080p", config.ConversionPresets[0].Name);
            Assert.Equal("libx264", config.ConversionPresets[0].VideoCodec);
            Assert.Equal("aac", config.ConversionPresets[0].AudioCodec);
        }

        [Fact]
        public void Save_传入null_抛出异常()
        {
            // 准备
            string configPath = Path.Combine(_testDir, "AppConfig.json");
            var service = new ConfigService(configPath);

            // 执行 & 验证
            Assert.Throws<ArgumentNullException>(() => service.Save(null));
        }

        [Fact]
        public void ConfigPath_返回正确路径()
        {
            // 准备
            string configPath = Path.Combine(_testDir, "test.json");
            var service = new ConfigService(configPath);

            // 验证
            Assert.Equal(configPath, service.ConfigPath);
        }

        [Fact]
        public void Load_使用UTF8编码_中文路径正确()
        {
            // 准备：包含中文的目录
            string chineseDir = Path.Combine(_testDir, "中文目录");
            Directory.CreateDirectory(chineseDir);
            string configPath = Path.Combine(chineseDir, "AppConfig.json");
            var service = new ConfigService(configPath);
            var config = AppConfig.CreateDefault();
            config.DefaultOutputDir = @"D:\输出 目录\视频";

            // 执行
            service.Save(config);
            var service2 = new ConfigService(configPath);
            AppConfig loaded = service2.Load();

            // 验证
            Assert.Equal(@"D:\输出 目录\视频", loaded.DefaultOutputDir);
        }
    }
}
