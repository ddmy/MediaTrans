using System;
using System.IO;
using System.Text;

namespace KeyGenerator
{
    /// <summary>
    /// MediaTrans RSA-2048 密钥对生成工具
    /// 仅供开发者使用，运行一次即可生成密钥对
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("========================================");
            Console.WriteLine("  MediaTrans RSA-2048 密钥对生成工具");
            Console.WriteLine("========================================");
            Console.WriteLine();

            // 默认输出到当前目录下的 keys 子目录
            string outputDir = "keys";
            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                outputDir = args[0];
            }

            string fullPath = Path.GetFullPath(outputDir);
            Console.WriteLine(string.Format("输出目录: {0}", fullPath));
            Console.WriteLine();

            try
            {
                var generator = new RsaKeyGenerator();

                // 生成密钥对
                Console.WriteLine("正在生成 RSA-2048 密钥对...");
                bool success = generator.GenerateKeyPair(outputDir);

                if (!success)
                {
                    Console.WriteLine("错误: 密钥对生成失败。");
                    return 1;
                }

                string privateKeyPath = Path.Combine(fullPath, "private_key.pem");
                string publicKeyPath = Path.Combine(fullPath, "public_key.pem");

                Console.WriteLine(string.Format("私钥已保存: {0}", privateKeyPath));
                Console.WriteLine(string.Format("公钥已保存: {0}", publicKeyPath));
                Console.WriteLine();

                // 读取并验证密钥对
                Console.WriteLine("正在验证密钥对...");
                string privateKeyPem = File.ReadAllText(privateKeyPath, Encoding.UTF8);
                string publicKeyPem = File.ReadAllText(publicKeyPath, Encoding.UTF8);

                bool verified = generator.VerifyKeyPair(privateKeyPem, publicKeyPem);
                if (verified)
                {
                    Console.WriteLine("验证通过: 公钥可以正确验证私钥签名。");
                }
                else
                {
                    Console.WriteLine("验证失败: 公钥无法验证私钥签名！");
                    return 1;
                }

                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("  密钥对生成完成！");
                Console.WriteLine("  重要提醒：");
                Console.WriteLine("  - 私钥 (private_key.pem) 仅由开发者保管");
                Console.WriteLine("  - 私钥不得包含在客户端代码中");
                Console.WriteLine("  - 公钥 (public_key.pem) 将嵌入客户端");
                Console.WriteLine("========================================");

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("错误: {0}", ex.Message));
                return 1;
            }
        }
    }
}
