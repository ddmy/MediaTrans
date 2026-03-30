using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace KeyGenerator
{
    /// <summary>
    /// RSA-2048 密钥对生成器
    /// 生成 PEM 格式的公钥和私钥文件
    /// </summary>
    public class RsaKeyGenerator
    {
        private const int KeySize = 2048;

        /// <summary>
        /// 生成 RSA-2048 密钥对并保存为 PEM 文件
        /// </summary>
        /// <param name="outputDir">输出目录</param>
        /// <returns>生成是否成功</returns>
        public bool GenerateKeyPair(string outputDir)
        {
            if (string.IsNullOrEmpty(outputDir))
            {
                throw new ArgumentNullException("outputDir");
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            using (var rsa = new RSACryptoServiceProvider(KeySize))
            {
                try
                {
                    // 不持久化到系统密钥容器
                    rsa.PersistKeyInCsp = false;

                    // 导出私钥（包含公钥+私钥）
                    string privateKeyPem = ExportPrivateKey(rsa);
                    string privateKeyPath = Path.Combine(outputDir, "private_key.pem");
                    File.WriteAllText(privateKeyPath, privateKeyPem, Encoding.UTF8);

                    // 导出公钥
                    string publicKeyPem = ExportPublicKey(rsa);
                    string publicKeyPath = Path.Combine(outputDir, "public_key.pem");
                    File.WriteAllText(publicKeyPath, publicKeyPem, Encoding.UTF8);

                    return true;
                }
                finally
                {
                    rsa.Clear();
                }
            }
        }

        /// <summary>
        /// 导出 RSA 私钥为 PEM 格式字符串
        /// </summary>
        public string ExportPrivateKey(RSACryptoServiceProvider rsa)
        {
            byte[] privateKeyBlob = rsa.ExportCspBlob(true);
            string base64 = Convert.ToBase64String(privateKeyBlob);
            return FormatPem(base64, "RSA PRIVATE KEY");
        }

        /// <summary>
        /// 导出 RSA 公钥为 PEM 格式字符串
        /// </summary>
        public string ExportPublicKey(RSACryptoServiceProvider rsa)
        {
            byte[] publicKeyBlob = rsa.ExportCspBlob(false);
            string base64 = Convert.ToBase64String(publicKeyBlob);
            return FormatPem(base64, "RSA PUBLIC KEY");
        }

        /// <summary>
        /// 验证密钥对是否匹配：用私钥签名，用公钥验签
        /// </summary>
        /// <param name="privateKeyPem">私钥 PEM 字符串</param>
        /// <param name="publicKeyPem">公钥 PEM 字符串</param>
        /// <returns>匹配返回 true</returns>
        public bool VerifyKeyPair(string privateKeyPem, string publicKeyPem)
        {
            byte[] testData = Encoding.UTF8.GetBytes("MediaTrans_KeyPair_Verification_Test");

            // 用私钥签名
            byte[] signature;
            using (var rsaPrivate = new RSACryptoServiceProvider())
            {
                rsaPrivate.PersistKeyInCsp = false;
                byte[] privateBlob = ParsePem(privateKeyPem);
                rsaPrivate.ImportCspBlob(privateBlob);
                signature = rsaPrivate.SignData(testData, "SHA256");
                rsaPrivate.Clear();
            }

            // 用公钥验签
            using (var rsaPublic = new RSACryptoServiceProvider())
            {
                rsaPublic.PersistKeyInCsp = false;
                byte[] publicBlob = ParsePem(publicKeyPem);
                rsaPublic.ImportCspBlob(publicBlob);
                bool result = rsaPublic.VerifyData(testData, "SHA256", signature);
                rsaPublic.Clear();
                return result;
            }
        }

        /// <summary>
        /// 将 Base64 数据格式化为 PEM 格式（每行 64 字符）
        /// </summary>
        private string FormatPem(string base64, string label)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("-----BEGIN {0}-----", label));

            // 每行 64 字符
            for (int i = 0; i < base64.Length; i += 64)
            {
                int length = Math.Min(64, base64.Length - i);
                sb.AppendLine(base64.Substring(i, length));
            }

            sb.AppendLine(string.Format("-----END {0}-----", label));
            return sb.ToString();
        }

        /// <summary>
        /// 解析 PEM 格式字符串，提取 Base64 数据并解码为字节数组
        /// </summary>
        public byte[] ParsePem(string pem)
        {
            // 移除 PEM 头尾标记和换行
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
