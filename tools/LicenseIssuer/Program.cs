using System;
using System.IO;
using System.Text;

namespace LicenseIssuer
{
    /// <summary>
    /// MediaTrans 激活码签发工具
    /// 仅供开发者本地使用
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("========================================");
            Console.WriteLine("  MediaTrans 激活码签发工具");
            Console.WriteLine("========================================");
            Console.WriteLine();

            string privateKeyPath = null;
            string machineCode = null;
            string version = null;

            // 解析命令行参数
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "-k" || args[i] == "--key") && i + 1 < args.Length)
                {
                    privateKeyPath = args[i + 1];
                    i++;
                }
                else if ((args[i] == "-m" || args[i] == "--machine") && i + 1 < args.Length)
                {
                    machineCode = args[i + 1];
                    i++;
                }
                else if ((args[i] == "-v" || args[i] == "--version") && i + 1 < args.Length)
                {
                    version = args[i + 1];
                    i++;
                }
                else if (args[i] == "-h" || args[i] == "--help")
                {
                    PrintUsage();
                    return 0;
                }
            }

            // 交互模式：如果没有提供参数则提示输入
            if (string.IsNullOrEmpty(privateKeyPath))
            {
                Console.Write("请输入私钥文件路径: ");
                privateKeyPath = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(machineCode))
            {
                Console.Write("请输入机器码: ");
                machineCode = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(version))
            {
                Console.Write("请输入授权版本号 (如 1.0): ");
                version = Console.ReadLine();
            }

            // 校验参数
            if (string.IsNullOrEmpty(privateKeyPath) || string.IsNullOrEmpty(machineCode) || string.IsNullOrEmpty(version))
            {
                Console.WriteLine("错误: 私钥路径、机器码和版本号均为必填项。");
                PrintUsage();
                return 1;
            }

            if (!File.Exists(privateKeyPath))
            {
                Console.WriteLine(string.Format("错误: 私钥文件不存在: {0}", privateKeyPath));
                return 1;
            }

            try
            {
                string privateKeyPem = File.ReadAllText(privateKeyPath, Encoding.UTF8);

                Console.WriteLine();
                Console.WriteLine(string.Format("机器码: {0}", machineCode));
                Console.WriteLine(string.Format("版本号: {0}", version));
                Console.WriteLine("授权类型: 买断制（永久有效）");
                Console.WriteLine();

                var issuer = new LicenseIssuerService();
                string licenseCode = issuer.IssueLicense(privateKeyPem, machineCode, version);

                Console.WriteLine("========================================");
                Console.WriteLine("  激活码（请复制以下内容）:");
                Console.WriteLine("========================================");
                Console.WriteLine(licenseCode);
                Console.WriteLine("========================================");

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("错误: {0}", ex.Message));
                return 1;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("用法: LicenseIssuer [选项]");
            Console.WriteLine();
            Console.WriteLine("选项:");
            Console.WriteLine("  -k, --key <路径>       RSA 私钥文件路径");
            Console.WriteLine("  -m, --machine <机器码>  目标机器码");
            Console.WriteLine("  -v, --version <版本号>  授权版本号");
            Console.WriteLine("  -h, --help             显示帮助");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  LicenseIssuer -k keys/private_key.pem -m ABC123 -v 1.0");
        }
    }
}
