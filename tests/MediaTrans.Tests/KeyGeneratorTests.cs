using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using KeyGenerator;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// RSA 密钥对生成器单元测试
    /// </summary>
    public class KeyGeneratorTests : IDisposable
    {
        private readonly string _testDir;
        private readonly RsaKeyGenerator _generator;

        public KeyGeneratorTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "KeyGeneratorTests_" + Guid.NewGuid().ToString("N"));
            _generator = new RsaKeyGenerator();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); }
                catch { }
            }
        }

        [Fact]
        public void 生成密钥对_输出私钥和公钥文件()
        {
            bool result = _generator.GenerateKeyPair(_testDir);

            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(_testDir, "private_key.pem")));
            Assert.True(File.Exists(Path.Combine(_testDir, "public_key.pem")));
        }

        [Fact]
        public void 生成密钥对_PEM格式正确()
        {
            _generator.GenerateKeyPair(_testDir);

            string privateKey = File.ReadAllText(
                Path.Combine(_testDir, "private_key.pem"), Encoding.UTF8);
            string publicKey = File.ReadAllText(
                Path.Combine(_testDir, "public_key.pem"), Encoding.UTF8);

            // 检查 PEM 头尾标记
            Assert.Contains("-----BEGIN RSA PRIVATE KEY-----", privateKey);
            Assert.Contains("-----END RSA PRIVATE KEY-----", privateKey);
            Assert.Contains("-----BEGIN RSA PUBLIC KEY-----", publicKey);
            Assert.Contains("-----END RSA PUBLIC KEY-----", publicKey);
        }

        [Fact]
        public void 生成密钥对_密钥为2048位()
        {
            _generator.GenerateKeyPair(_testDir);

            string privateKeyPem = File.ReadAllText(
                Path.Combine(_testDir, "private_key.pem"), Encoding.UTF8);

            byte[] blob = _generator.ParsePem(privateKeyPem);
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(blob);
                Assert.Equal(2048, rsa.KeySize);
                rsa.Clear();
            }
        }

        [Fact]
        public void 公钥可验证私钥签名()
        {
            _generator.GenerateKeyPair(_testDir);

            string privateKeyPem = File.ReadAllText(
                Path.Combine(_testDir, "private_key.pem"), Encoding.UTF8);
            string publicKeyPem = File.ReadAllText(
                Path.Combine(_testDir, "public_key.pem"), Encoding.UTF8);

            bool verified = _generator.VerifyKeyPair(privateKeyPem, publicKeyPem);
            Assert.True(verified, "公钥应能验证私钥签名");
        }

        [Fact]
        public void 不同密钥对_不能互相验证()
        {
            // 生成第一对密钥
            string dir1 = Path.Combine(_testDir, "pair1");
            _generator.GenerateKeyPair(dir1);
            string privateKey1 = File.ReadAllText(
                Path.Combine(dir1, "private_key.pem"), Encoding.UTF8);

            // 生成第二对密钥
            string dir2 = Path.Combine(_testDir, "pair2");
            _generator.GenerateKeyPair(dir2);
            string publicKey2 = File.ReadAllText(
                Path.Combine(dir2, "public_key.pem"), Encoding.UTF8);

            // 用密钥对1的私钥签名，密钥对2的公钥验签，应该失败
            bool verified = _generator.VerifyKeyPair(privateKey1, publicKey2);
            Assert.False(verified, "不同密钥对不应能互相验证");
        }

        [Fact]
        public void 私钥签名_公钥验签_数据完整性()
        {
            _generator.GenerateKeyPair(_testDir);

            string privateKeyPem = File.ReadAllText(
                Path.Combine(_testDir, "private_key.pem"), Encoding.UTF8);
            string publicKeyPem = File.ReadAllText(
                Path.Combine(_testDir, "public_key.pem"), Encoding.UTF8);

            byte[] data = Encoding.UTF8.GetBytes("机器码:ABC123-DEF456|版本:1.0");

            // 用私钥签名
            byte[] signature;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(_generator.ParsePem(privateKeyPem));
                signature = rsa.SignData(data, "SHA256");
                rsa.Clear();
            }

            // 用公钥验签
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(_generator.ParsePem(publicKeyPem));
                bool verified = rsa.VerifyData(data, "SHA256", signature);
                Assert.True(verified, "公钥应能验证私钥对原始数据的签名");
                rsa.Clear();
            }
        }

        [Fact]
        public void 篡改数据_验签失败()
        {
            _generator.GenerateKeyPair(_testDir);

            string privateKeyPem = File.ReadAllText(
                Path.Combine(_testDir, "private_key.pem"), Encoding.UTF8);
            string publicKeyPem = File.ReadAllText(
                Path.Combine(_testDir, "public_key.pem"), Encoding.UTF8);

            byte[] data = Encoding.UTF8.GetBytes("原始数据");
            byte[] tamperedData = Encoding.UTF8.GetBytes("篡改数据");

            // 用私钥签名原始数据
            byte[] signature;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(_generator.ParsePem(privateKeyPem));
                signature = rsa.SignData(data, "SHA256");
                rsa.Clear();
            }

            // 用公钥对篡改数据验签，应该失败
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(_generator.ParsePem(publicKeyPem));
                bool verified = rsa.VerifyData(tamperedData, "SHA256", signature);
                Assert.False(verified, "篡改数据后验签应失败");
                rsa.Clear();
            }
        }

        [Fact]
        public void PEM解析_正确还原字节数组()
        {
            _generator.GenerateKeyPair(_testDir);

            string publicKeyPem = File.ReadAllText(
                Path.Combine(_testDir, "public_key.pem"), Encoding.UTF8);

            byte[] blob = _generator.ParsePem(publicKeyPem);
            Assert.NotNull(blob);
            Assert.True(blob.Length > 0, "解析后的字节数组不应为空");

            // 验证可以正确导入
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportCspBlob(blob);
                Assert.Equal(2048, rsa.KeySize);
                rsa.Clear();
            }
        }

        [Fact]
        public void 输出目录不存在_自动创建()
        {
            string subDir = Path.Combine(_testDir, "sub", "deep");
            Assert.False(Directory.Exists(subDir));

            _generator.GenerateKeyPair(subDir);

            Assert.True(Directory.Exists(subDir));
            Assert.True(File.Exists(Path.Combine(subDir, "private_key.pem")));
        }

        [Fact]
        public void 参数为空_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() => _generator.GenerateKeyPair(null));
            Assert.Throws<ArgumentNullException>(() => _generator.GenerateKeyPair(""));
        }

        [Fact]
        public void 多次生成_每次密钥不同()
        {
            string dir1 = Path.Combine(_testDir, "gen1");
            string dir2 = Path.Combine(_testDir, "gen2");

            _generator.GenerateKeyPair(dir1);
            _generator.GenerateKeyPair(dir2);

            string pub1 = File.ReadAllText(
                Path.Combine(dir1, "public_key.pem"), Encoding.UTF8);
            string pub2 = File.ReadAllText(
                Path.Combine(dir2, "public_key.pem"), Encoding.UTF8);

            Assert.NotEqual(pub1, pub2);
        }

        [Fact]
        public void 私钥文件_不应出现在客户端项目中()
        {
            // 安全检查：确认私钥未被编译嵌入到客户端程序集中
            // 私钥文件可能存在于磁盘（开发者工具需要），但必须不是嵌入资源
            var assembly = typeof(MediaTrans.Services.LicenseService).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            foreach (string name in resourceNames)
            {
                Assert.DoesNotContain("private_key", name.ToLowerInvariant());
            }
        }
    }
}
