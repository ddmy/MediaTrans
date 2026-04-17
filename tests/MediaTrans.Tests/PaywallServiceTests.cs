using System;
using System.IO;
using System.Text;
using KeyGenerator;
using LicenseIssuer;
using MediaTrans.Models;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 付费墙拦截服务单元测试
    /// </summary>
    public class PaywallServiceTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _licenseFilePath;
        private readonly string _configFilePath;
        private readonly MachineCodeService _machineCodeService;
        private readonly string _currentMachineCode;
        private readonly string _privateKeyPem;
        private readonly string _publicKeyPem;

        public PaywallServiceTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "PaywallTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _licenseFilePath = Path.Combine(_testDir, "license.dat");
            _configFilePath = Path.Combine(_testDir, "AppConfig.json");

            _machineCodeService = new MachineCodeService();
            _currentMachineCode = _machineCodeService.GetMachineCode();

            // 生成测试用密钥对
            var keyGen = new RsaKeyGenerator();
            string keysDir = Path.Combine(_testDir, "keys");
            keyGen.GenerateKeyPair(keysDir);
            _privateKeyPem = File.ReadAllText(
                Path.Combine(keysDir, "private_key.pem"), Encoding.UTF8);
            _publicKeyPem = File.ReadAllText(
                Path.Combine(keysDir, "public_key.pem"), Encoding.UTF8);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); }
                catch { }
            }
        }

        /// <summary>
        /// 创建未激活的 PaywallService
        /// </summary>
        private PaywallService CreateFreeService(int freeMaxExportSeconds = 60)
        {
            var config = AppConfig.CreateDefault();
            config.FreeMaxExportSeconds = freeMaxExportSeconds;
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = new LicenseService(
                _machineCodeService, _publicKeyPem, _licenseFilePath);
            return new PaywallService(licenseService, configService);
        }

        /// <summary>
        /// 创建已激活的 PaywallService
        /// </summary>
        private PaywallService CreateProService()
        {
            var config = AppConfig.CreateDefault();
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = new LicenseService(
                _machineCodeService, _publicKeyPem, _licenseFilePath);

            // 激活
            var issuer = new LicenseIssuerService();
            string licenseCode = issuer.IssueLicense(
                _privateKeyPem, _currentMachineCode);
            licenseService.Activate(licenseCode);

            return new PaywallService(licenseService, configService);
        }

        private void SaveConfig(AppConfig config)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
            string dir = Path.GetDirectoryName(_configFilePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_configFilePath, json, Encoding.UTF8);
        }

        // =================== IsProfessional ===================

        [Fact]
        public void 免费版_IsProfessional为false()
        {
            var service = CreateFreeService();
            Assert.False(service.IsProfessional);
        }

        [Fact]
        public void 专业版_IsProfessional为true()
        {
            var service = CreateProService();
            Assert.True(service.IsProfessional);
        }

        // =================== 格式限制 ===================

        [Fact]
        public void 免费版_FLAC格式不允许()
        {
            var service = CreateFreeService();
            Assert.False(service.IsFormatAllowed(".flac"));
        }

        [Fact]
        public void 免费版_WAV格式不允许()
        {
            var service = CreateFreeService();
            Assert.False(service.IsFormatAllowed(".wav"));
        }

        [Fact]
        public void 免费版_FLAC大写不允许()
        {
            var service = CreateFreeService();
            Assert.False(service.IsFormatAllowed(".FLAC"));
        }

        [Fact]
        public void 免费版_MP4格式允许()
        {
            var service = CreateFreeService();
            Assert.True(service.IsFormatAllowed(".mp4"));
        }

        [Fact]
        public void 免费版_MP3格式允许()
        {
            var service = CreateFreeService();
            Assert.True(service.IsFormatAllowed(".mp3"));
        }

        [Fact]
        public void 免费版_AAC格式允许()
        {
            var service = CreateFreeService();
            Assert.True(service.IsFormatAllowed(".aac"));
        }

        [Fact]
        public void 专业版_FLAC格式允许()
        {
            var service = CreateProService();
            Assert.True(service.IsFormatAllowed(".flac"));
        }

        [Fact]
        public void 专业版_WAV格式允许()
        {
            var service = CreateProService();
            Assert.True(service.IsFormatAllowed(".wav"));
        }

        // =================== 导出时长限制 ===================

        [Fact]
        public void 免费版_最大导出时长为配置值()
        {
            var service = CreateFreeService(60);
            Assert.Equal(60, service.GetMaxExportSeconds());
        }

        [Fact]
        public void 免费版_自定义最大导出时长()
        {
            var service = CreateFreeService(120);
            Assert.Equal(120, service.GetMaxExportSeconds());
        }

        [Fact]
        public void 专业版_最大导出时长为MaxValue()
        {
            var service = CreateProService();
            Assert.Equal(int.MaxValue, service.GetMaxExportSeconds());
        }

        [Fact]
        public void 免费版_超过限制需要截断()
        {
            var service = CreateFreeService(60);
            Assert.True(service.NeedsTruncation(61));
        }

        [Fact]
        public void 免费版_恰好等于限制无需截断()
        {
            var service = CreateFreeService(60);
            Assert.False(service.NeedsTruncation(60));
        }

        [Fact]
        public void 免费版_低于限制无需截断()
        {
            var service = CreateFreeService(60);
            Assert.False(service.NeedsTruncation(30));
        }

        [Fact]
        public void 专业版_任意时长无需截断()
        {
            var service = CreateProService();
            Assert.False(service.NeedsTruncation(99999));
        }

        // =================== 水印 ===================

        [Fact]
        public void 免费版_视频导出需要水印()
        {
            var service = CreateFreeService();
            Assert.True(service.ShouldAddWatermark(true));
        }

        [Fact]
        public void 免费版_音频导出不需要水印()
        {
            var service = CreateFreeService();
            Assert.False(service.ShouldAddWatermark(false));
        }

        [Fact]
        public void 专业版_视频导出不需要水印()
        {
            var service = CreateProService();
            Assert.False(service.ShouldAddWatermark(true));
        }

        [Fact]
        public void 水印滤镜包含配置中的文字()
        {
            var service = CreateFreeService();
            string filter = service.BuildWatermarkFilter();
            Assert.Contains("MediaTrans", filter);
        }

        [Fact]
        public void 水印滤镜包含drawtext()
        {
            var service = CreateFreeService();
            string filter = service.BuildWatermarkFilter();
            Assert.Contains("drawtext=", filter);
        }

        [Fact]
        public void 水印滤镜包含字号()
        {
            var service = CreateFreeService();
            string filter = service.BuildWatermarkFilter();
            Assert.Contains("fontsize=24", filter);
        }

        // =================== ApplyRestrictions ===================

        [Fact]
        public void 免费版_视频导出_应用截断和水印()
        {
            var service = CreateFreeService(60);
            var builder = new FFmpegCommandBuilder();
            builder.Input("test.mp4");
            builder.VideoCodec("libx264");
            builder.AudioCodec("aac");
            builder.Output("output.mp4");

            service.ApplyRestrictions(builder, 120, true);

            string cmd = builder.Build();
            // 检查截断参数
            Assert.Contains("-t 60", cmd);
            // 检查水印滤镜
            Assert.Contains("-vf", cmd);
            Assert.Contains("drawtext=", cmd);
        }

        [Fact]
        public void 免费版_音频导出_仅截断无水印()
        {
            var service = CreateFreeService(60);
            var builder = new FFmpegCommandBuilder();
            builder.Input("test.mp3");
            builder.AudioCodec("aac");
            builder.NoVideo();
            builder.Output("output.aac");

            service.ApplyRestrictions(builder, 120, false);

            string cmd = builder.Build();
            Assert.Contains("-t 60", cmd);
            Assert.DoesNotContain("-vf", cmd);
        }

        [Fact]
        public void 免费版_短视频_不截断但有水印()
        {
            var service = CreateFreeService(60);
            var builder = new FFmpegCommandBuilder();
            builder.Input("test.mp4");
            builder.VideoCodec("libx264");
            builder.AudioCodec("aac");
            builder.Output("output.mp4");

            service.ApplyRestrictions(builder, 30, true);

            string cmd = builder.Build();
            Assert.DoesNotContain("-t ", cmd);
            Assert.Contains("-vf", cmd);
        }

        [Fact]
        public void 专业版_不应用任何限制()
        {
            var service = CreateProService();
            var builder = new FFmpegCommandBuilder();
            builder.Input("test.mp4");
            builder.VideoCodec("libx264");
            builder.AudioCodec("aac");
            builder.Output("output.mp4");

            service.ApplyRestrictions(builder, 120, true);

            string cmd = builder.Build();
            Assert.DoesNotContain("-t ", cmd);
            Assert.DoesNotContain("-vf", cmd);
        }

        // =================== GetRestrictedFormats ===================

        [Fact]
        public void 免费版_受限格式包含FLAC和WAV()
        {
            var service = CreateFreeService();
            var restricted = service.GetRestrictedFormats();
            Assert.Contains(".flac", restricted);
            Assert.Contains(".wav", restricted);
        }

        [Fact]
        public void 专业版_无受限格式()
        {
            var service = CreateProService();
            var restricted = service.GetRestrictedFormats();
            Assert.Equal(0, restricted.Count);
        }

        // =================== 构造函数校验 ===================

        [Fact]
        public void 构造函数_LicenseService为空_抛出异常()
        {
            var configService = new ConfigService(_configFilePath);
            Assert.Throws<ArgumentNullException>(() =>
                new PaywallService(null, configService));
        }

        [Fact]
        public void 构造函数_ConfigService为空_抛出异常()
        {
            var licenseService = new LicenseService(
                _machineCodeService, _publicKeyPem, _licenseFilePath);
            Assert.Throws<ArgumentNullException>(() =>
                new PaywallService(licenseService, null));
        }
    }
}
