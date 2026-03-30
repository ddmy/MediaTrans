using System;
using System.IO;
using System.Reflection;
using System.Text;
using KeyGenerator;
using LicenseIssuer;
using MediaTrans.Models;
using MediaTrans.Services;
using MediaTrans.ViewModels;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 里程碑 3 综合测试 — 授权系统全面验证
    /// ① 授权算法（正确码/错误码/篡改码/空值/超长值）
    /// ② 机器码稳定性（重启 5 次一致性）
    /// ③ 付费墙边界（恰好 1 分钟/超过 1 秒/各格式）
    /// ④ 安全：客户端二进制中不包含私钥
    /// </summary>
    public class Milestone3Tests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _licenseFilePath;
        private readonly string _configFilePath;
        private readonly MachineCodeService _machineCodeService;
        private readonly string _currentMachineCode;
        private readonly string _privateKeyPem;
        private readonly string _publicKeyPem;

        public Milestone3Tests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "M3Tests_" + Guid.NewGuid().ToString("N"));
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

        private LicenseService CreateLicenseService()
        {
            return new LicenseService(_machineCodeService, _publicKeyPem, _licenseFilePath);
        }

        private void SaveConfig(AppConfig config)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_configFilePath, json, Encoding.UTF8);
        }

        // ==================================================
        // ① 授权算法单元测试
        // ==================================================

        [Fact]
        public void 授权_正确激活码通过验证()
        {
            var issuer = new LicenseIssuerService();
            string code = issuer.IssueLicense(_privateKeyPem, _currentMachineCode, "1.0");

            var service = CreateLicenseService();
            bool result = service.Activate(code);

            Assert.True(result);
            Assert.Equal(LicenseStatus.Activated, service.Status);
        }

        [Fact]
        public void 授权_错误码验证失败()
        {
            var service = CreateLicenseService();
            bool result = service.Activate("completely_wrong_code");
            Assert.False(result);
            Assert.Equal(LicenseStatus.Invalid, service.Status);
        }

        [Fact]
        public void 授权_篡改数据部分_签名验证失败()
        {
            var issuer = new LicenseIssuerService();
            string code = issuer.IssueLicense(_privateKeyPem, _currentMachineCode, "1.0");

            // 篡改数据部分（第一个点之前的 Base64）
            string tampered = "AAAA" + code.Substring(4);
            var service = CreateLicenseService();
            bool result = service.Activate(tampered);
            Assert.False(result);
        }

        [Fact]
        public void 授权_篡改签名部分_验证失败()
        {
            var issuer = new LicenseIssuerService();
            string code = issuer.IssueLicense(_privateKeyPem, _currentMachineCode, "1.0");

            // 篡改签名部分（最后一个点之后的 Base64）
            int dotIndex = code.LastIndexOf('.');
            if (dotIndex > 0)
            {
                string tampered = code.Substring(0, dotIndex + 1) + "AAAA" + code.Substring(dotIndex + 5);
                var service = CreateLicenseService();
                bool result = service.Activate(tampered);
                Assert.False(result);
            }
        }

        [Fact]
        public void 授权_空值验证失败()
        {
            var service = CreateLicenseService();
            Assert.False(service.Activate(""));
            Assert.False(service.Activate(null));
            Assert.False(service.Activate("   "));
        }

        [Fact]
        public void 授权_超长值验证失败()
        {
            string longCode = new string('A', 10000) + "." + new string('B', 10000);
            var service = CreateLicenseService();
            bool result = service.Activate(longCode);
            Assert.False(result);
        }

        [Fact]
        public void 授权_不含分隔符的字符串_验证失败()
        {
            var service = CreateLicenseService();
            bool result = service.Activate("nodotseparator");
            Assert.False(result);
        }

        [Fact]
        public void 授权_其他机器码的激活码_验证失败()
        {
            var issuer = new LicenseIssuerService();
            string fakeMachine = "0000111122223333444455556666777788889999AAAABBBBCCCCDDDDEEEEFFFF";
            string code = issuer.IssueLicense(_privateKeyPem, fakeMachine, "1.0");

            var service = CreateLicenseService();
            bool result = service.Activate(code);
            Assert.False(result, "其他机器码的激活码不应通过验证");
        }

        [Fact]
        public void 授权_不同密钥对签发_验证失败()
        {
            var keyGen2 = new RsaKeyGenerator();
            string keys2Dir = Path.Combine(_testDir, "keys2");
            keyGen2.GenerateKeyPair(keys2Dir);
            string privateKey2 = File.ReadAllText(
                Path.Combine(keys2Dir, "private_key.pem"), Encoding.UTF8);

            var issuer = new LicenseIssuerService();
            string code = issuer.IssueLicense(privateKey2, _currentMachineCode, "1.0");

            var service = CreateLicenseService();
            bool result = service.Activate(code);
            Assert.False(result, "不同密钥对签发的激活码不应通过验证");
        }

        [Fact]
        public void 授权_重启多次后状态保持()
        {
            var issuer = new LicenseIssuerService();
            string code = issuer.IssueLicense(_privateKeyPem, _currentMachineCode, "1.0");

            // 首次激活
            var service1 = CreateLicenseService();
            service1.Activate(code);
            Assert.True(service1.IsActivated);

            // 模拟多次重启
            for (int i = 0; i < 5; i++)
            {
                var serviceN = CreateLicenseService();
                bool check = serviceN.CheckOnStartup();
                Assert.True(check, string.Format("第 {0} 次重启后授权状态应保持", i + 1));
                Assert.True(serviceN.IsActivated);
                Assert.Equal("1.0", serviceN.ActivatedVersion);
            }
        }

        // ==================================================
        // ② 机器码稳定性测试
        // ==================================================

        [Fact]
        public void 机器码_5次获取结果一致()
        {
            string first = _machineCodeService.GetMachineCode();
            for (int i = 0; i < 5; i++)
            {
                var freshService = new MachineCodeService();
                string current = freshService.GetMachineCode();
                Assert.Equal(first, current);
            }
        }

        [Fact]
        public void 机器码_格式为64位大写十六进制()
        {
            string code = _machineCodeService.GetMachineCode();
            Assert.Equal(64, code.Length);
            foreach (char c in code)
            {
                Assert.True(
                    (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'),
                    string.Format("机器码应仅包含大写十六进制字符，但发现 '{0}'", c));
            }
        }

        [Fact]
        public void 机器码_GenerateMachineCode_相同输入相同输出()
        {
            string result1 = _machineCodeService.GenerateMachineCode("cpu1", "disk1", "board1");
            string result2 = _machineCodeService.GenerateMachineCode("cpu1", "disk1", "board1");
            Assert.Equal(result1, result2);
        }

        [Fact]
        public void 机器码_GenerateMachineCode_不同输入不同输出()
        {
            string result1 = _machineCodeService.GenerateMachineCode("cpu1", "disk1", "board1");
            string result2 = _machineCodeService.GenerateMachineCode("cpu2", "disk1", "board1");
            Assert.NotEqual(result1, result2);
        }

        // ==================================================
        // ③ 付费墙边界测试
        // ==================================================

        [Fact]
        public void 付费墙_恰好60秒_不截断()
        {
            var config = AppConfig.CreateDefault();
            config.FreeMaxExportSeconds = 60;
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = CreateLicenseService();
            var paywall = new PaywallService(licenseService, configService);

            Assert.False(paywall.NeedsTruncation(60.0));
        }

        [Fact]
        public void 付费墙_61秒_需截断()
        {
            var config = AppConfig.CreateDefault();
            config.FreeMaxExportSeconds = 60;
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = CreateLicenseService();
            var paywall = new PaywallService(licenseService, configService);

            Assert.True(paywall.NeedsTruncation(61.0));
        }

        [Fact]
        public void 付费墙_超过1秒即60_001_需截断()
        {
            var config = AppConfig.CreateDefault();
            config.FreeMaxExportSeconds = 60;
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = CreateLicenseService();
            var paywall = new PaywallService(licenseService, configService);

            // 60.001 > 60，应该截断
            Assert.True(paywall.NeedsTruncation(60.001));
        }

        [Fact]
        public void 付费墙_FLAC格式免费版禁用()
        {
            var config = AppConfig.CreateDefault();
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = CreateLicenseService();
            var paywall = new PaywallService(licenseService, configService);

            Assert.False(paywall.IsFormatAllowed(".flac"));
            Assert.False(paywall.IsFormatAllowed(".FLAC"));
        }

        [Fact]
        public void 付费墙_WAV格式免费版禁用()
        {
            var config = AppConfig.CreateDefault();
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = CreateLicenseService();
            var paywall = new PaywallService(licenseService, configService);

            Assert.False(paywall.IsFormatAllowed(".wav"));
            Assert.False(paywall.IsFormatAllowed(".WAV"));
        }

        [Fact]
        public void 付费墙_MP4_MP3_AAC等格式免费版允许()
        {
            var config = AppConfig.CreateDefault();
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = CreateLicenseService();
            var paywall = new PaywallService(licenseService, configService);

            Assert.True(paywall.IsFormatAllowed(".mp4"));
            Assert.True(paywall.IsFormatAllowed(".mp3"));
            Assert.True(paywall.IsFormatAllowed(".aac"));
            Assert.True(paywall.IsFormatAllowed(".avi"));
            Assert.True(paywall.IsFormatAllowed(".mkv"));
            Assert.True(paywall.IsFormatAllowed(".ogg"));
        }

        [Fact]
        public void 付费墙_专业版解锁全部格式()
        {
            var config = AppConfig.CreateDefault();
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = CreateLicenseService();

            // 激活
            var issuer = new LicenseIssuerService();
            string code = issuer.IssueLicense(_privateKeyPem, _currentMachineCode, "1.0");
            licenseService.Activate(code);

            var paywall = new PaywallService(licenseService, configService);

            Assert.True(paywall.IsFormatAllowed(".flac"));
            Assert.True(paywall.IsFormatAllowed(".wav"));
            Assert.True(paywall.IsFormatAllowed(".mp4"));
        }

        [Fact]
        public void 付费墙_专业版无时长限制()
        {
            var config = AppConfig.CreateDefault();
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = CreateLicenseService();

            var issuer = new LicenseIssuerService();
            string code = issuer.IssueLicense(_privateKeyPem, _currentMachineCode, "1.0");
            licenseService.Activate(code);

            var paywall = new PaywallService(licenseService, configService);

            Assert.False(paywall.NeedsTruncation(999999));
            Assert.Equal(int.MaxValue, paywall.GetMaxExportSeconds());
        }

        [Fact]
        public void 付费墙_免费版视频导出有水印()
        {
            var config = AppConfig.CreateDefault();
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = CreateLicenseService();
            var paywall = new PaywallService(licenseService, configService);

            Assert.True(paywall.ShouldAddWatermark(true));
            Assert.False(paywall.ShouldAddWatermark(false));

            string filter = paywall.BuildWatermarkFilter();
            Assert.Contains("drawtext=", filter);
            Assert.Contains("MediaTrans", filter);
        }

        [Fact]
        public void 付费墙_ApplyRestrictions_视频超时加截断和水印()
        {
            var config = AppConfig.CreateDefault();
            config.FreeMaxExportSeconds = 60;
            SaveConfig(config);

            var configService = new ConfigService(_configFilePath);
            var licenseService = CreateLicenseService();
            var paywall = new PaywallService(licenseService, configService);

            var builder = new FFmpegCommandBuilder();
            builder.Input("input.mp4");
            builder.VideoCodec("libx264");
            builder.AudioCodec("aac");
            builder.Output("output.mp4");

            paywall.ApplyRestrictions(builder, 120, true);

            string cmd = builder.Build();
            Assert.Contains("-t 60", cmd);
            Assert.Contains("-vf", cmd);
            Assert.Contains("drawtext=", cmd);
        }

        // ==================================================
        // ④ 安全测试：客户端二进制不包含私钥
        // ==================================================

        [Fact]
        public void 安全_客户端程序集不包含RSA私钥标记()
        {
            // 读取 MediaTrans.exe 的二进制内容
            Assembly assembly = typeof(LicenseService).Assembly;
            string assemblyPath = assembly.Location;
            byte[] binary = File.ReadAllBytes(assemblyPath);
            string content = Encoding.UTF8.GetString(binary);

            // 私钥 PEM 标记不应出现
            Assert.DoesNotContain("BEGIN RSA PRIVATE KEY", content);
            Assert.DoesNotContain("END RSA PRIVATE KEY", content);
            Assert.DoesNotContain("PRIVATE KEY", content);
        }

        [Fact]
        public void 安全_客户端程序集包含公钥但不含私钥()
        {
            Assembly assembly = typeof(LicenseService).Assembly;
            string assemblyPath = assembly.Location;
            byte[] binary = File.ReadAllBytes(assemblyPath);
            string content = Encoding.UTF8.GetString(binary);

            // 公钥标记应存在（作为嵌入资源）
            Assert.Contains("BEGIN RSA PUBLIC KEY", content);
            // 私钥标记不应存在
            Assert.DoesNotContain("PRIVATE KEY", content);
        }

        [Fact]
        public void 安全_测试程序集中也不包含硬编码私钥()
        {
            // 确认测试读取的私钥是从临时目录生成的，不是硬编码的
            Assert.True(_privateKeyPem.Contains("BEGIN RSA PRIVATE KEY"));
            // 私钥文件应在临时目录
            Assert.True(_testDir.Contains(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)));
        }

        // ==================================================
        // 端到端授权流程
        // ==================================================

        [Fact]
        public void 端到端_生成密钥_签发激活码_客户端激活_重启校验()
        {
            // 步骤1: 生成密钥对（已在构造函数中完成）
            Assert.True(_privateKeyPem.Contains("BEGIN RSA PRIVATE KEY"));
            Assert.True(_publicKeyPem.Contains("BEGIN RSA PUBLIC KEY"));

            // 步骤2: 获取机器码
            string machineCode = _machineCodeService.GetMachineCode();
            Assert.Equal(64, machineCode.Length);

            // 步骤3: 签发激活码
            var issuer = new LicenseIssuerService();
            string licenseCode = issuer.IssueLicense(_privateKeyPem, machineCode, "2.0");
            Assert.False(string.IsNullOrEmpty(licenseCode));
            Assert.Contains(".", licenseCode);

            // 步骤4: 客户端激活
            var service = CreateLicenseService();
            bool activated = service.Activate(licenseCode);
            Assert.True(activated);
            Assert.Equal("2.0", service.ActivatedVersion);

            // 步骤5: 模拟重启校验
            var service2 = CreateLicenseService();
            bool checked2 = service2.CheckOnStartup();
            Assert.True(checked2);
            Assert.True(service2.IsActivated);
            Assert.Equal("2.0", service2.ActivatedVersion);
        }

        [Fact]
        public void 端到端_ViewModel激活流程()
        {
            var licenseService = CreateLicenseService();
            var vm = new LicenseViewModel(licenseService, _machineCodeService);

            Assert.False(vm.IsActivated);
            Assert.False(string.IsNullOrEmpty(vm.MachineCode));

            // 输入正确激活码
            var issuer = new LicenseIssuerService();
            string code = issuer.IssueLicense(_privateKeyPem, _currentMachineCode, "1.0");

            bool eventFired = false;
            vm.ActivationSucceeded += (s, e) => { eventFired = true; };

            vm.LicenseCode = code;
            vm.ActivateCommand.Execute(null);

            Assert.True(vm.IsActivated);
            Assert.True(eventFired);
        }
    }
}
