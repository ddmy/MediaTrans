using System;
using System.IO;
using System.Text;
using KeyGenerator;
using LicenseIssuer;
using MediaTrans.Services;
using MediaTrans.ViewModels;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 授权 ViewModel 单元测试
    /// </summary>
    public class LicenseViewModelTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _licenseFilePath;
        private readonly MachineCodeService _machineCodeService;
        private readonly string _currentMachineCode;
        private readonly string _privateKeyPem;
        private readonly string _publicKeyPem;

        public LicenseViewModelTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "LicenseVMTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _licenseFilePath = Path.Combine(_testDir, "license.dat");

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

        private LicenseViewModel CreateViewModel()
        {
            var licenseService = new LicenseService(
                _machineCodeService, _publicKeyPem, _licenseFilePath);
            return new LicenseViewModel(licenseService, _machineCodeService);
        }

        private LicenseViewModel CreateActivatedViewModel()
        {
            var licenseService = new LicenseService(
                _machineCodeService, _publicKeyPem, _licenseFilePath);
            var issuer = new LicenseIssuerService();
            string code = issuer.IssueLicense(_privateKeyPem, _currentMachineCode);
            licenseService.Activate(code);
            return new LicenseViewModel(licenseService, _machineCodeService);
        }

        [Fact]
        public void 初始状态_未激活()
        {
            var vm = CreateViewModel();
            Assert.False(vm.IsActivated);
        }

        [Fact]
        public void 初始状态_机器码非空()
        {
            var vm = CreateViewModel();
            Assert.False(string.IsNullOrEmpty(vm.MachineCode));
            Assert.Equal(64, vm.MachineCode.Length);
        }

        [Fact]
        public void 初始状态_激活码为空()
        {
            var vm = CreateViewModel();
            Assert.Equal("", vm.LicenseCode);
        }

        [Fact]
        public void 输入正确激活码_激活成功()
        {
            var vm = CreateViewModel();
            var issuer = new LicenseIssuerService();
            string code = issuer.IssueLicense(_privateKeyPem, _currentMachineCode);

            bool eventFired = false;
            vm.ActivationSucceeded += (s, e) => { eventFired = true; };

            vm.LicenseCode = code;
            vm.ActivateCommand.Execute(null);

            Assert.True(vm.IsActivated);
            Assert.True(eventFired, "激活成功事件应被触发");
            Assert.Contains("成功", vm.StatusMessage);
        }

        [Fact]
        public void 输入错误激活码_激活失败()
        {
            var vm = CreateViewModel();
            vm.LicenseCode = "invalid.code";
            vm.ActivateCommand.Execute(null);

            Assert.False(vm.IsActivated);
            Assert.Contains("失败", vm.StatusMessage);
        }

        [Fact]
        public void 空激活码_不可执行()
        {
            var vm = CreateViewModel();
            vm.LicenseCode = "";
            Assert.False(vm.ActivateCommand.CanExecute(null));
        }

        [Fact]
        public void 非空激活码_可执行()
        {
            var vm = CreateViewModel();
            vm.LicenseCode = "test";
            Assert.True(vm.ActivateCommand.CanExecute(null));
        }

        [Fact]
        public void 已激活状态_属性正确()
        {
            var vm = CreateActivatedViewModel();
            Assert.True(vm.IsActivated);
        }

        [Fact]
        public void 构造函数_LicenseService为空_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseViewModel(null, _machineCodeService));
        }

        [Fact]
        public void 构造函数_MachineCodeService为空_抛出异常()
        {
            var licenseService = new LicenseService(
                _machineCodeService, _publicKeyPem, _licenseFilePath);
            Assert.Throws<ArgumentNullException>(() =>
                new LicenseViewModel(licenseService, null));
        }
    }
}
