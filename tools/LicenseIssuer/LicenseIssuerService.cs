using System;
using System.Security.Cryptography;
using System.Text;
using KeyGenerator;

namespace LicenseIssuer
{
    /// <summary>
    /// 激活码签发服务
    /// 使用 RSA 私钥对机器码 + 版本号进行签名，生成 Base64 编码的激活码
    /// 买断制，无过期时间
    /// </summary>
    public class LicenseIssuerService
    {
        private readonly RsaKeyGenerator _keyHelper;

        public LicenseIssuerService()
        {
            _keyHelper = new RsaKeyGenerator();
        }

        /// <summary>
        /// 构建待签名的授权数据
        /// 格式: "MEDIATRANS_LICENSE|机器码|版本号"
        /// </summary>
        /// <param name="machineCode">机器码</param>
        /// <param name="version">授权版本号</param>
        /// <returns>待签名的数据字符串</returns>
        public string BuildLicenseData(string machineCode, string version)
        {
            if (string.IsNullOrEmpty(machineCode))
            {
                throw new ArgumentNullException("machineCode", "机器码不能为空");
            }
            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentNullException("version", "版本号不能为空");
            }

            return string.Format("MEDIATRANS_LICENSE|{0}|{1}", machineCode.Trim(), version.Trim());
        }

        /// <summary>
        /// 签发激活码
        /// </summary>
        /// <param name="privateKeyPem">RSA 私钥 PEM 字符串</param>
        /// <param name="machineCode">机器码</param>
        /// <param name="version">授权版本号</param>
        /// <returns>Base64 编码的激活码</returns>
        public string IssueLicense(string privateKeyPem, string machineCode, string version)
        {
            if (string.IsNullOrEmpty(privateKeyPem))
            {
                throw new ArgumentNullException("privateKeyPem", "私钥不能为空");
            }

            // 构建待签名数据
            string licenseData = BuildLicenseData(machineCode, version);
            byte[] dataBytes = Encoding.UTF8.GetBytes(licenseData);

            // 用私钥签名
            byte[] signature;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                byte[] privateBlob = _keyHelper.ParsePem(privateKeyPem);
                rsa.ImportCspBlob(privateBlob);
                signature = rsa.SignData(dataBytes, "SHA256");
                rsa.Clear();
            }

            // 组合授权数据和签名，用分隔符连接，Base64 编码
            // 格式: Base64(授权数据) + "." + Base64(签名)
            string dataBase64 = Convert.ToBase64String(dataBytes);
            string signatureBase64 = Convert.ToBase64String(signature);

            return string.Format("{0}.{1}", dataBase64, signatureBase64);
        }

        /// <summary>
        /// 验证激活码（使用公钥）
        /// </summary>
        /// <param name="publicKeyPem">RSA 公钥 PEM 字符串</param>
        /// <param name="licenseCode">激活码</param>
        /// <param name="expectedMachineCode">期望的机器码</param>
        /// <returns>验证结果</returns>
        public LicenseVerifyResult VerifyLicense(string publicKeyPem, string licenseCode, string expectedMachineCode)
        {
            var result = new LicenseVerifyResult();

            if (string.IsNullOrEmpty(licenseCode))
            {
                result.ErrorMessage = "激活码为空";
                return result;
            }

            // 分割激活码：数据部分.签名部分
            int dotIndex = licenseCode.IndexOf('.');
            if (dotIndex < 0 || dotIndex >= licenseCode.Length - 1)
            {
                result.ErrorMessage = "激活码格式无效";
                return result;
            }

            string dataBase64 = licenseCode.Substring(0, dotIndex);
            string signatureBase64 = licenseCode.Substring(dotIndex + 1);

            byte[] dataBytes;
            byte[] signatureBytes;
            try
            {
                dataBytes = Convert.FromBase64String(dataBase64);
                signatureBytes = Convert.FromBase64String(signatureBase64);
            }
            catch (FormatException)
            {
                result.ErrorMessage = "激活码 Base64 解码失败";
                return result;
            }

            // 用公钥验签
            bool verified;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                byte[] publicBlob = _keyHelper.ParsePem(publicKeyPem);
                rsa.ImportCspBlob(publicBlob);
                verified = rsa.VerifyData(dataBytes, "SHA256", signatureBytes);
                rsa.Clear();
            }

            if (!verified)
            {
                result.ErrorMessage = "签名验证失败";
                return result;
            }

            // 解析授权数据
            string licenseData = Encoding.UTF8.GetString(dataBytes);
            string[] parts = licenseData.Split('|');
            if (parts.Length != 3 || parts[0] != "MEDIATRANS_LICENSE")
            {
                result.ErrorMessage = "授权数据格式无效";
                return result;
            }

            string machineCode = parts[1];
            string version = parts[2];

            // 校验机器码
            if (!string.IsNullOrEmpty(expectedMachineCode) &&
                !string.Equals(machineCode, expectedMachineCode, StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = "机器码不匹配";
                return result;
            }

            result.IsValid = true;
            result.MachineCode = machineCode;
            result.Version = version;
            return result;
        }
    }

    /// <summary>
    /// 授权验证结果
    /// </summary>
    public class LicenseVerifyResult
    {
        public bool IsValid { get; set; }
        public string MachineCode { get; set; }
        public string Version { get; set; }
        public string ErrorMessage { get; set; }
    }
}
