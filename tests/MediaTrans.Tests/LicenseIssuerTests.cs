using System;
using System.IO;
using System.Text;
using KeyGenerator;
using LicenseIssuer;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 激活码签发服务单元测试
    /// </summary>
    public class LicenseIssuerTests : IDisposable
    {
        private readonly string _testDir;
        private readonly RsaKeyGenerator _keyGen;
        private readonly LicenseIssuerService _issuer;
        private readonly string _privateKeyPem;
        private readonly string _publicKeyPem;

        public LicenseIssuerTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "LicenseIssuerTests_" + Guid.NewGuid().ToString("N"));
            _keyGen = new RsaKeyGenerator();
            _issuer = new LicenseIssuerService();

            // 生成测试用密钥对
            _keyGen.GenerateKeyPair(_testDir);
            _privateKeyPem = File.ReadAllText(
                Path.Combine(_testDir, "private_key.pem"), Encoding.UTF8);
            _publicKeyPem = File.ReadAllText(
                Path.Combine(_testDir, "public_key.pem"), Encoding.UTF8);
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
        public void 签发激活码_返回非空字符串()
        {
            string code = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "1.0");
            Assert.False(string.IsNullOrEmpty(code));
        }

        [Fact]
        public void 签发激活码_格式为Base64点Base64()
        {
            string code = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "1.0");

            // 格式: dataBase64.signatureBase64
            int dotIndex = code.IndexOf('.');
            Assert.True(dotIndex > 0, "激活码应含有'.'分隔符");
            Assert.True(dotIndex < code.Length - 1, "分隔符后应有签名数据");

            // 两部分都应是合法 Base64
            string dataPart = code.Substring(0, dotIndex);
            string sigPart = code.Substring(dotIndex + 1);

            byte[] dataBytes = Convert.FromBase64String(dataPart);
            byte[] sigBytes = Convert.FromBase64String(sigPart);
            Assert.True(dataBytes.Length > 0);
            Assert.True(sigBytes.Length > 0);
        }

        [Fact]
        public void 签发激活码_公钥验签通过()
        {
            string code = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "1.0");

            var result = _issuer.VerifyLicense(_publicKeyPem, code, "MACHINE001");

            Assert.True(result.IsValid, string.Format("验签应通过，错误: {0}", result.ErrorMessage));
            Assert.Equal("MACHINE001", result.MachineCode);
            Assert.Equal("1.0", result.Version);
        }

        [Fact]
        public void 验证_机器码不匹配_失败()
        {
            string code = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "1.0");

            var result = _issuer.VerifyLicense(_publicKeyPem, code, "MACHINE999");

            Assert.False(result.IsValid);
            Assert.Contains("机器码不匹配", result.ErrorMessage);
        }

        [Fact]
        public void 验证_篡改激活码_签名失败()
        {
            string code = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "1.0");

            // 篡改数据部分
            int dotIndex = code.IndexOf('.');
            string tampered = "AAAAAA" + code.Substring(6);

            var result = _issuer.VerifyLicense(_publicKeyPem, tampered, "MACHINE001");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void 验证_空激活码_返回错误()
        {
            var result = _issuer.VerifyLicense(_publicKeyPem, "", "MACHINE001");
            Assert.False(result.IsValid);
            Assert.Contains("激活码为空", result.ErrorMessage);
        }

        [Fact]
        public void 验证_无效格式_返回错误()
        {
            var result = _issuer.VerifyLicense(_publicKeyPem, "nodot", "MACHINE001");
            Assert.False(result.IsValid);
            Assert.Contains("无效", result.ErrorMessage);
        }

        [Fact]
        public void 验证_非法Base64_返回错误()
        {
            var result = _issuer.VerifyLicense(_publicKeyPem, "!!!.!!!", "MACHINE001");
            Assert.False(result.IsValid);
            Assert.Contains("Base64", result.ErrorMessage);
        }

        [Fact]
        public void 不同密钥对_签发的激活码不能被另一对公钥验证()
        {
            // 生成另一对密钥
            string otherDir = Path.Combine(_testDir, "other");
            _keyGen.GenerateKeyPair(otherDir);
            string otherPublicKey = File.ReadAllText(
                Path.Combine(otherDir, "public_key.pem"), Encoding.UTF8);

            string code = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "1.0");

            var result = _issuer.VerifyLicense(otherPublicKey, code, "MACHINE001");
            Assert.False(result.IsValid, "不同密钥对的公钥不应能验证签名");
        }

        [Fact]
        public void 买断制_无过期时间_验证数据不含时间戳()
        {
            string code = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "1.0");

            // 解析数据部分，确认不含过期时间
            int dotIndex = code.IndexOf('.');
            string dataBase64 = code.Substring(0, dotIndex);
            string data = Encoding.UTF8.GetString(Convert.FromBase64String(dataBase64));

            // 数据格式: MEDIATRANS_LICENSE|机器码|版本号
            Assert.Equal("MEDIATRANS_LICENSE|MACHINE001|1.0", data);
        }

        [Fact]
        public void 构建授权数据_格式正确()
        {
            string data = _issuer.BuildLicenseData("ABC123", "2.0");
            Assert.Equal("MEDIATRANS_LICENSE|ABC123|2.0", data);
        }

        [Fact]
        public void 构建授权数据_去除首尾空格()
        {
            string data = _issuer.BuildLicenseData("  ABC123  ", "  2.0  ");
            Assert.Equal("MEDIATRANS_LICENSE|ABC123|2.0", data);
        }

        [Fact]
        public void 签发_机器码为空_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _issuer.IssueLicense(_privateKeyPem, null, "1.0"));
            Assert.Throws<ArgumentNullException>(() =>
                _issuer.IssueLicense(_privateKeyPem, "", "1.0"));
        }

        [Fact]
        public void 签发_版本号为空_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _issuer.IssueLicense(_privateKeyPem, "MACHINE001", null));
            Assert.Throws<ArgumentNullException>(() =>
                _issuer.IssueLicense(_privateKeyPem, "MACHINE001", ""));
        }

        [Fact]
        public void 签发_私钥为空_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _issuer.IssueLicense(null, "MACHINE001", "1.0"));
            Assert.Throws<ArgumentNullException>(() =>
                _issuer.IssueLicense("", "MACHINE001", "1.0"));
        }

        [Fact]
        public void 验证_机器码不区分大小写()
        {
            string code = _issuer.IssueLicense(_privateKeyPem, "Machine001", "1.0");

            var result = _issuer.VerifyLicense(_publicKeyPem, code, "machine001");
            Assert.True(result.IsValid, "机器码比对应不区分大小写");
        }

        [Fact]
        public void 多次签发_同一机器码_激活码内容相同()
        {
            // 虽然每次签名可能因 RSA padding 不同而不同，但数据部分应相同
            string code1 = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "1.0");
            string code2 = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "1.0");

            int dot1 = code1.IndexOf('.');
            int dot2 = code2.IndexOf('.');
            string data1 = code1.Substring(0, dot1);
            string data2 = code2.Substring(0, dot2);

            // 数据部分应完全相同
            Assert.Equal(data1, data2);

            // 两个激活码都应能通过验证
            var result1 = _issuer.VerifyLicense(_publicKeyPem, code1, "MACHINE001");
            var result2 = _issuer.VerifyLicense(_publicKeyPem, code2, "MACHINE001");
            Assert.True(result1.IsValid);
            Assert.True(result2.IsValid);
        }

        [Fact]
        public void 不同版本号_生成不同激活码()
        {
            string code1 = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "1.0");
            string code2 = _issuer.IssueLicense(_privateKeyPem, "MACHINE001", "2.0");

            int dot1 = code1.IndexOf('.');
            int dot2 = code2.IndexOf('.');
            string data1 = code1.Substring(0, dot1);
            string data2 = code2.Substring(0, dot2);

            Assert.NotEqual(data1, data2);
        }
    }
}
