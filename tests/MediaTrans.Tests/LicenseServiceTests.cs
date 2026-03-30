using System;
using System.IO;
using System.Text;
using KeyGenerator;
using LicenseIssuer;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 客户端授权校验服务单元测试
    /// </summary>
    public class LicenseServiceTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _licenseFilePath;
        private readonly RsaKeyGenerator _keyGen;
        private readonly LicenseIssuerService _issuer;
        private readonly string _privateKeyPem;
        private readonly string _publicKeyPem;
        private readonly MachineCodeService _machineCodeService;
        private readonly string _currentMachineCode;

        public LicenseServiceTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "LicenseServiceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _licenseFilePath = Path.Combine(_testDir, "license.dat");

            _keyGen = new RsaKeyGenerator();
            _issuer = new LicenseIssuerService();
            _machineCodeService = new MachineCodeService();
            _currentMachineCode = _machineCodeService.GetMachineCode();

            // 生成测试用密钥对
            string keysDir = Path.Combine(_testDir, "keys");
            _keyGen.GenerateKeyPair(keysDir);
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

        private LicenseService CreateService()
        {
            return new LicenseService(_machineCodeService, _publicKeyPem, _licenseFilePath);
        }

        [Fact]
        public void 初始状态_未激活()
        {
            var service = CreateService();
            Assert.Equal(LicenseStatus.NotActivated, service.Status);
            Assert.False(service.IsActivated);
        }

        [Fact]
        public void 启动校验_无保存文件_返回未激活()
        {
            var service = CreateService();
            bool result = service.CheckOnStartup();
            Assert.False(result);
            Assert.Equal(LicenseStatus.NotActivated, service.Status);
        }

        [Fact]
        public void 激活_正确激活码_成功()
        {
            string licenseCode = _issuer.IssueLicense(
                _privateKeyPem, _currentMachineCode, "1.0");

            var service = CreateService();
            bool result = service.Activate(licenseCode);

            Assert.True(result, "正确激活码应能激活成功");
            Assert.True(service.IsActivated);
            Assert.Equal(LicenseStatus.Activated, service.Status);
            Assert.Equal("1.0", service.ActivatedVersion);
        }

        [Fact]
        public void 激活_空激活码_失败()
        {
            var service = CreateService();
            bool result = service.Activate("");
            Assert.False(result);
            Assert.Equal(LicenseStatus.Invalid, service.Status);
        }

        [Fact]
        public void 激活_null激活码_失败()
        {
            var service = CreateService();
            bool result = service.Activate(null);
            Assert.False(result);
            Assert.Equal(LicenseStatus.Invalid, service.Status);
        }

        [Fact]
        public void 激活_错误激活码_失败()
        {
            var service = CreateService();
            bool result = service.Activate("invalid.code");
            Assert.False(result);
            Assert.Equal(LicenseStatus.Invalid, service.Status);
        }

        [Fact]
        public void 激活_篡改激活码_签名验证失败()
        {
            string licenseCode = _issuer.IssueLicense(
                _privateKeyPem, _currentMachineCode, "1.0");

            // 篡改数据部分
            string tampered = "AAAA" + licenseCode.Substring(4);

            var service = CreateService();
            bool result = service.Activate(tampered);
            Assert.False(result);
            Assert.Equal(LicenseStatus.Invalid, service.Status);
        }

        [Fact]
        public void 激活_其他机器码的激活码_失败()
        {
            string fakeMachineCode = "AAAA1111BBBB2222CCCC3333DDDD4444EEEE5555FFFF6666AAAA7777BBBB8888";
            string licenseCode = _issuer.IssueLicense(
                _privateKeyPem, fakeMachineCode, "1.0");

            var service = CreateService();
            bool result = service.Activate(licenseCode);
            Assert.False(result, "其他机器码的激活码不应能在本机激活");
            Assert.Equal(LicenseStatus.Invalid, service.Status);
        }

        [Fact]
        public void 激活后_重启校验_授权状态保持()
        {
            string licenseCode = _issuer.IssueLicense(
                _privateKeyPem, _currentMachineCode, "1.0");

            // 首次激活
            var service1 = CreateService();
            service1.Activate(licenseCode);
            Assert.True(service1.IsActivated);

            // 模拟重启：创建新的 service 实例
            var service2 = CreateService();
            bool result = service2.CheckOnStartup();
            Assert.True(result, "重启后授权状态应保持");
            Assert.True(service2.IsActivated);
            Assert.Equal("1.0", service2.ActivatedVersion);
        }

        [Fact]
        public void 激活后_授权文件存在()
        {
            string licenseCode = _issuer.IssueLicense(
                _privateKeyPem, _currentMachineCode, "1.0");

            var service = CreateService();
            service.Activate(licenseCode);

            Assert.True(File.Exists(_licenseFilePath), "激活后应生成授权文件");
        }

        [Fact]
        public void 授权文件被删除_重启后变为未激活()
        {
            string licenseCode = _issuer.IssueLicense(
                _privateKeyPem, _currentMachineCode, "1.0");

            var service1 = CreateService();
            service1.Activate(licenseCode);

            // 删除授权文件
            File.Delete(_licenseFilePath);

            var service2 = CreateService();
            bool result = service2.CheckOnStartup();
            Assert.False(result, "授权文件被删除后应变为未激活");
        }

        [Fact]
        public void 授权文件被篡改_重启后验证失败()
        {
            string licenseCode = _issuer.IssueLicense(
                _privateKeyPem, _currentMachineCode, "1.0");

            var service1 = CreateService();
            service1.Activate(licenseCode);

            // 篡改授权文件
            File.WriteAllText(_licenseFilePath, "tampered_content", Encoding.UTF8);

            var service2 = CreateService();
            bool result = service2.CheckOnStartup();
            Assert.False(result, "授权文件被篡改后验证应失败");
        }

        [Fact]
        public void 不同密钥对签发的激活码_验证失败()
        {
            // 生成另一对密钥
            string otherKeysDir = Path.Combine(_testDir, "other_keys");
            _keyGen.GenerateKeyPair(otherKeysDir);
            string otherPrivateKey = File.ReadAllText(
                Path.Combine(otherKeysDir, "private_key.pem"), Encoding.UTF8);

            // 用另一对密钥签发
            string licenseCode = _issuer.IssueLicense(
                otherPrivateKey, _currentMachineCode, "1.0");

            // 用原来的公钥验证
            var service = CreateService();
            bool result = service.Activate(licenseCode);
            Assert.False(result, "不同密钥对签发的激活码不应通过验证");
        }

        [Fact]
        public void 构造函数_机器码服务为空_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseService(null));
        }

        [Fact]
        public void 激活码含空格_自动裁剪()
        {
            string licenseCode = _issuer.IssueLicense(
                _privateKeyPem, _currentMachineCode, "1.0");

            var service = CreateService();
            bool result = service.Activate("  " + licenseCode + "  ");
            Assert.True(result, "激活码前后有空格也应能激活");
        }
    }
}
