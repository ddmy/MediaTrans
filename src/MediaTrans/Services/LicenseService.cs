using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MediaTrans.Services
{
    /// <summary>
    /// 授权验证状态
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>未激活</summary>
        NotActivated,
        /// <summary>已激活</summary>
        Activated,
        /// <summary>激活码无效</summary>
        Invalid
    }

    /// <summary>
    /// 客户端授权校验服务
    /// 公钥从嵌入资源加载，验证激活码（RSA验签+机器码比对），授权状态持久化到 AppData
    /// </summary>
    public class LicenseService
    {
        private const string LicenseFileName = "license.dat";
        private const string PublicKeyResourceName = "MediaTrans.Assets.public_key.pem";
        private const string LicenseDataPrefix = "MEDIATRANS_LICENSE";

        private readonly MachineCodeService _machineCodeService;
        private readonly string _licenseFilePath;
        private string _publicKeyPem;

        private LicenseStatus _status;
        private string _activatedVersion;

        /// <summary>
        /// 当前授权状态
        /// </summary>
        public LicenseStatus Status
        {
            get { return _status; }
        }

        /// <summary>
        /// 是否已激活
        /// </summary>
        public bool IsActivated
        {
            get { return _status == LicenseStatus.Activated; }
        }

        /// <summary>
        /// 授权版本号
        /// </summary>
        public string ActivatedVersion
        {
            get { return _activatedVersion; }
        }

        /// <summary>
        /// 创建授权服务实例
        /// </summary>
        /// <param name="machineCodeService">机器码服务</param>
        public LicenseService(MachineCodeService machineCodeService)
        {
            if (machineCodeService == null)
            {
                throw new ArgumentNullException("machineCodeService");
            }

            _machineCodeService = machineCodeService;
            _status = LicenseStatus.NotActivated;

            // 授权文件路径：AppData\Local\MediaTrans\license.dat
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MediaTrans");
            _licenseFilePath = Path.Combine(appDataDir, LicenseFileName);

            // 加载嵌入的公钥
            _publicKeyPem = LoadEmbeddedPublicKey();
        }

        /// <summary>
        /// 创建授权服务实例（测试用，允许指定公钥和授权文件路径）
        /// </summary>
        /// <param name="machineCodeService">机器码服务</param>
        /// <param name="publicKeyPem">公钥 PEM 字符串</param>
        /// <param name="licenseFilePath">授权文件路径</param>
        public LicenseService(MachineCodeService machineCodeService, string publicKeyPem, string licenseFilePath)
        {
            if (machineCodeService == null)
            {
                throw new ArgumentNullException("machineCodeService");
            }

            _machineCodeService = machineCodeService;
            _publicKeyPem = publicKeyPem;
            _licenseFilePath = licenseFilePath;
            _status = LicenseStatus.NotActivated;
        }

        /// <summary>
        /// 启动时自动校验：检查持久化的授权状态
        /// </summary>
        /// <returns>是否已激活</returns>
        public bool CheckOnStartup()
        {
            string savedLicenseCode = LoadSavedLicense();
            if (string.IsNullOrEmpty(savedLicenseCode))
            {
                _status = LicenseStatus.NotActivated;
                return false;
            }

            return VerifyLicenseCode(savedLicenseCode);
        }

        /// <summary>
        /// 激活：输入激活码进行验证
        /// </summary>
        /// <param name="licenseCode">激活码</param>
        /// <returns>激活是否成功</returns>
        public bool Activate(string licenseCode)
        {
            if (string.IsNullOrEmpty(licenseCode))
            {
                _status = LicenseStatus.Invalid;
                return false;
            }

            licenseCode = licenseCode.Trim();

            if (!VerifyLicenseCode(licenseCode))
            {
                return false;
            }

            // 验证通过，持久化激活码
            SaveLicense(licenseCode);
            return true;
        }

        /// <summary>
        /// 验证激活码
        /// </summary>
        /// <param name="licenseCode">激活码</param>
        /// <returns>是否有效</returns>
        private bool VerifyLicenseCode(string licenseCode)
        {
            if (string.IsNullOrEmpty(_publicKeyPem))
            {
                _status = LicenseStatus.Invalid;
                return false;
            }

            // 分割激活码：数据部分.签名部分
            int dotIndex = licenseCode.IndexOf('.');
            if (dotIndex < 0 || dotIndex >= licenseCode.Length - 1)
            {
                _status = LicenseStatus.Invalid;
                return false;
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
                _status = LicenseStatus.Invalid;
                return false;
            }

            // 用公钥验签
            bool verified;
            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    byte[] publicBlob = ParsePem(_publicKeyPem);
                    rsa.ImportCspBlob(publicBlob);
                    verified = rsa.VerifyData(dataBytes, "SHA256", signatureBytes);
                    rsa.Clear();
                }
            }
            catch
            {
                _status = LicenseStatus.Invalid;
                return false;
            }

            if (!verified)
            {
                _status = LicenseStatus.Invalid;
                return false;
            }

            // 解析授权数据
            string licenseData = Encoding.UTF8.GetString(dataBytes);
            string[] parts = licenseData.Split('|');
            if (parts.Length != 3 || parts[0] != LicenseDataPrefix)
            {
                _status = LicenseStatus.Invalid;
                return false;
            }

            string licenseMachineCode = parts[1];
            string version = parts[2];

            // 校验机器码
            string currentMachineCode = _machineCodeService.GetMachineCode();
            if (!string.Equals(licenseMachineCode, currentMachineCode, StringComparison.OrdinalIgnoreCase))
            {
                _status = LicenseStatus.Invalid;
                return false;
            }

            _status = LicenseStatus.Activated;
            _activatedVersion = version;
            return true;
        }

        /// <summary>
        /// 从嵌入资源加载公钥
        /// </summary>
        private string LoadEmbeddedPublicKey()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(PublicKeyResourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从 AppData 加载已保存的激活码
        /// </summary>
        private string LoadSavedLicense()
        {
            try
            {
                if (File.Exists(_licenseFilePath))
                {
                    return File.ReadAllText(_licenseFilePath, Encoding.UTF8).Trim();
                }
            }
            catch
            {
                // 读取失败不崩溃
            }
            return null;
        }

        /// <summary>
        /// 持久化激活码到 AppData
        /// </summary>
        private void SaveLicense(string licenseCode)
        {
            try
            {
                string dir = Path.GetDirectoryName(_licenseFilePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(_licenseFilePath, licenseCode, Encoding.UTF8);
            }
            catch
            {
                // 保存失败不崩溃，下次启动需要重新激活
            }
        }

        /// <summary>
        /// 解析 PEM 格式字符串
        /// </summary>
        private byte[] ParsePem(string pem)
        {
            var lines = pem.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (line.StartsWith("-----"))
                {
                    continue;
                }
                sb.Append(line.Trim());
            }
            return Convert.FromBase64String(sb.ToString());
        }
    }
}
