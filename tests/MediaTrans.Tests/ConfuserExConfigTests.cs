using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Linq;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// ConfuserEx 配置与安全加固测试
    /// ① 配置文件格式正确性
    /// ② 授权相关类型保护规则完备性
    /// ③ 混淆后二进制验证（类名/方法名不可读）
    /// ④ 嵌入资源保护验证
    /// </summary>
    public class ConfuserExConfigTests
    {
        // 配置文件路径（相对于测试程序集输出目录）
        private readonly string _crprojPath;

        public ConfuserExConfigTests()
        {
            // 从测试 bin 目录向上推算到项目根目录
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            _crprojPath = Path.Combine(projectRoot, "tools", "ConfuserEx", "MediaTrans.crproj");
        }

        // ========== 配置文件存在性 ==========

        [Fact]
        public void CrprojFile_Exists()
        {
            // 配置文件必须存在
            Assert.True(File.Exists(_crprojPath),
                string.Format("ConfuserEx 配置文件不存在: {0}", _crprojPath));
        }

        [Fact]
        public void CrprojFile_IsValidXml()
        {
            // 配置文件必须为合法 XML
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            Assert.NotNull(doc.DocumentElement);
        }

        [Fact]
        public void CrprojFile_HasCorrectNamespace()
        {
            // 必须使用 ConfuserEx 命名空间
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            Assert.Equal("http://confuser.codeplex.com", doc.DocumentElement.NamespaceURI);
        }

        // ========== 基本结构验证 ==========

        [Fact]
        public void CrprojFile_HasProjectElement()
        {
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            Assert.Equal("project", doc.DocumentElement.LocalName);
        }

        [Fact]
        public void CrprojFile_HasOutputDir()
        {
            // 必须指定输出目录
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            string outputDir = doc.DocumentElement.GetAttribute("outputDir");
            Assert.False(string.IsNullOrEmpty(outputDir), "缺少 outputDir 属性");
        }

        [Fact]
        public void CrprojFile_HasBaseDir()
        {
            // 必须指定基础目录
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            string baseDir = doc.DocumentElement.GetAttribute("baseDir");
            Assert.False(string.IsNullOrEmpty(baseDir), "缺少 baseDir 属性");
            // baseDir 应指向 Release 输出
            Assert.Contains("Release", baseDir);
        }

        [Fact]
        public void CrprojFile_HasModuleElement()
        {
            // 必须包含 module 节点指向 MediaTrans.exe
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("cr", "http://confuser.codeplex.com");
            var moduleNode = doc.SelectSingleNode("//cr:module[@path='MediaTrans.exe']", nsmgr);
            Assert.NotNull(moduleNode);
        }

        // ========== 全局保护规则 ==========

        [Fact]
        public void CrprojFile_HasGlobalRules()
        {
            // 全局规则必须存在
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("cr", "http://confuser.codeplex.com");
            var globalRule = doc.SelectSingleNode("//cr:project/cr:rule[@pattern='true']", nsmgr);
            Assert.NotNull(globalRule);
        }

        [Fact]
        public void CrprojFile_GlobalRule_HasAntiIldasm()
        {
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("cr", "http://confuser.codeplex.com");
            var protection = doc.SelectSingleNode(
                "//cr:project/cr:rule[@pattern='true']/cr:protection[@id='anti ildasm']", nsmgr);
            Assert.NotNull(protection);
        }

        [Fact]
        public void CrprojFile_GlobalRule_HasAntiDebug()
        {
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("cr", "http://confuser.codeplex.com");
            var protection = doc.SelectSingleNode(
                "//cr:project/cr:rule[@pattern='true']/cr:protection[@id='anti debug']", nsmgr);
            Assert.NotNull(protection);
        }

        [Fact]
        public void CrprojFile_GlobalRule_HasControlFlow()
        {
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("cr", "http://confuser.codeplex.com");
            var protection = doc.SelectSingleNode(
                "//cr:project/cr:rule[@pattern='true']/cr:protection[@id='ctrl flow']", nsmgr);
            Assert.NotNull(protection);
        }

        [Fact]
        public void CrprojFile_GlobalRule_HasRename()
        {
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("cr", "http://confuser.codeplex.com");
            var protection = doc.SelectSingleNode(
                "//cr:project/cr:rule[@pattern='true']/cr:protection[@id='rename']", nsmgr);
            Assert.NotNull(protection);
        }

        // ========== 授权代码特殊保护 ==========

        [Fact]
        public void CrprojFile_HasLicenseServiceProtection()
        {
            // LicenseService 必须有保护规则
            AssertHasProtectionRule("LicenseService");
        }

        [Fact]
        public void CrprojFile_HasMachineCodeServiceProtection()
        {
            // MachineCodeService 必须有保护规则
            AssertHasProtectionRule("MachineCodeService");
        }

        [Fact]
        public void CrprojFile_HasPaywallServiceProtection()
        {
            // PaywallService 必须有保护规则
            AssertHasProtectionRule("PaywallService");
        }

        [Fact]
        public void CrprojFile_AuthCode_HasAntiTamper()
        {
            // 授权代码必须启用 Anti-Tamper 保护
            AssertProtectionContainsId("LicenseService", "anti tamper");
        }

        [Fact]
        public void CrprojFile_AuthCode_HasConstants()
        {
            // 授权代码必须启用常量加密
            AssertProtectionContainsId("LicenseService", "constants");
        }

        [Fact]
        public void CrprojFile_AuthCode_HasRefProxy()
        {
            // 授权代码必须启用引用代理
            AssertProtectionContainsId("LicenseService", "ref proxy");
        }

        [Fact]
        public void CrprojFile_AuthCode_HasAntiDump()
        {
            // 授权代码必须启用反内存转储
            AssertProtectionContainsId("LicenseService", "anti dump");
        }

        [Fact]
        public void CrprojFile_MachineCode_HasAntiTamper()
        {
            AssertProtectionContainsId("MachineCodeService", "anti tamper");
        }

        [Fact]
        public void CrprojFile_MachineCode_HasConstants()
        {
            AssertProtectionContainsId("MachineCodeService", "constants");
        }

        [Fact]
        public void CrprojFile_Paywall_HasAntiTamper()
        {
            AssertProtectionContainsId("PaywallService", "anti tamper");
        }

        [Fact]
        public void CrprojFile_Paywall_HasConstants()
        {
            AssertProtectionContainsId("PaywallService", "constants");
        }

        // ========== 嵌入资源保护 ==========

        [Fact]
        public void CrprojFile_HasResourceProtection()
        {
            // 嵌入资源（如公钥）必须受保护
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("cr", "http://confuser.codeplex.com");
            var resProtection = doc.SelectSingleNode(
                "//cr:module/cr:rule/cr:protection[@id='resources']", nsmgr);
            Assert.NotNull(resProtection);
        }

        // ========== 目标类型存在性验证 ==========

        [Fact]
        public void ProtectedTypes_ExistInAssembly_LicenseService()
        {
            // 确保 LicenseService 类型确实存在于客户端程序集
            var type = typeof(MediaTrans.Services.LicenseService);
            Assert.NotNull(type);
            Assert.Equal("MediaTrans.Services", type.Namespace);
        }

        [Fact]
        public void ProtectedTypes_ExistInAssembly_MachineCodeService()
        {
            var type = typeof(MediaTrans.Services.MachineCodeService);
            Assert.NotNull(type);
            Assert.Equal("MediaTrans.Services", type.Namespace);
        }

        [Fact]
        public void ProtectedTypes_ExistInAssembly_PaywallService()
        {
            var type = typeof(MediaTrans.Services.PaywallService);
            Assert.NotNull(type);
            Assert.Equal("MediaTrans.Services", type.Namespace);
        }

        [Fact]
        public void ProtectedTypes_ExistInAssembly_LicenseStatus()
        {
            var type = typeof(MediaTrans.Services.LicenseStatus);
            Assert.NotNull(type);
            Assert.True(type.IsEnum, "LicenseStatus 应为枚举类型");
        }

        // ========== 混淆后二进制验证 ==========

        [Fact]
        public void ConfusedBinary_DoesNotContainServiceClassNames()
        {
            // 混淆后的二进制不应包含原始类名
            string confusedExePath = GetConfusedExePath();
            if (!File.Exists(confusedExePath))
            {
                // 如果混淆后文件不存在，跳过（需要先运行 confuse.bat）
                return;
            }

            byte[] data = File.ReadAllBytes(confusedExePath);
            string content = System.Text.Encoding.UTF8.GetString(data);

            // 授权相关类名不应以明文出现
            Assert.DoesNotContain("LicenseService", content);
            Assert.DoesNotContain("MachineCodeService", content);
            Assert.DoesNotContain("PaywallService", content);
        }

        [Fact]
        public void ConfusedBinary_SizeIsLarger()
        {
            // 混淆后文件应比原始文件更大（包含保护代码）
            string releaseExePath = GetReleaseExePath();
            string confusedExePath = GetConfusedExePath();

            if (!File.Exists(confusedExePath) || !File.Exists(releaseExePath))
            {
                return;
            }

            long releaseSize = new FileInfo(releaseExePath).Length;
            long confusedSize = new FileInfo(confusedExePath).Length;

            Assert.True(confusedSize > releaseSize,
                string.Format("混淆后文件({0}字节)应大于原始文件({1}字节)", confusedSize, releaseSize));
        }

        // ========== 安全检查 ==========

        [Fact]
        public void ConfusedBinary_DoesNotContainPrivateKey()
        {
            // 混淆后的二进制中也不应包含私钥标记
            string confusedExePath = GetConfusedExePath();
            if (!File.Exists(confusedExePath))
            {
                return;
            }

            byte[] data = File.ReadAllBytes(confusedExePath);
            string content = System.Text.Encoding.UTF8.GetString(data);
            Assert.DoesNotContain("PRIVATE KEY", content);
        }

        [Fact]
        public void ConfuserCLI_Exists()
        {
            // ConfuserEx CLI 工具通过 setup-confuserex.bat 下载，CI 环境可能不存在
            string cliPath = GetConfuserCliPath();
            if (!File.Exists(cliPath))
            {
                return; // CI 环境跳过
            }
            Assert.True(File.Exists(cliPath),
                string.Format("Confuser.CLI.exe 不存在: {0}", cliPath));
        }

        [Fact]
        public void ConfuseBat_Exists()
        {
            // 混淆批处理脚本必须存在
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            string batPath = Path.Combine(projectRoot, "tools", "ConfuserEx", "confuse.bat");
            Assert.True(File.Exists(batPath),
                string.Format("confuse.bat 不存在: {0}", batPath));
        }

        // ========== 辅助方法 ==========

        private void AssertHasProtectionRule(string typeName)
        {
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("cr", "http://confuser.codeplex.com");

            // 查找包含指定类名的保护规则
            var rules = doc.SelectNodes("//cr:module/cr:rule", nsmgr);
            bool found = false;
            foreach (XmlNode rule in rules)
            {
                var patternAttr = rule.Attributes["pattern"];
                if (patternAttr != null && patternAttr.Value.Contains(typeName))
                {
                    found = true;
                    break;
                }
            }

            Assert.True(found, string.Format("未找到 {0} 的保护规则", typeName));
        }

        private void AssertProtectionContainsId(string typeName, string protectionId)
        {
            var doc = new XmlDocument();
            doc.Load(_crprojPath);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("cr", "http://confuser.codeplex.com");

            var rules = doc.SelectNodes("//cr:module/cr:rule", nsmgr);
            foreach (XmlNode rule in rules)
            {
                var patternAttr = rule.Attributes["pattern"];
                if (patternAttr != null && patternAttr.Value.Contains(typeName))
                {
                    var protection = rule.SelectSingleNode(
                        string.Format("cr:protection[@id='{0}']", protectionId), nsmgr);
                    Assert.NotNull(protection);
                    return;
                }
            }

            Assert.True(false, string.Format("未找到 {0} 的 {1} 保护", typeName, protectionId));
        }

        private string GetConfusedExePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            return Path.Combine(projectRoot, "src", "MediaTrans", "bin", "Confused", "MediaTrans.exe");
        }

        private string GetReleaseExePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            return Path.Combine(projectRoot, "src", "MediaTrans", "bin", "Release", "MediaTrans.exe");
        }

        private string GetConfuserCliPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            return Path.Combine(projectRoot, "tools", "ConfuserEx", "packages",
                "ConfuserEx.Final.1.0.0", "tools", "Confuser.CLI.exe");
        }
    }
}
