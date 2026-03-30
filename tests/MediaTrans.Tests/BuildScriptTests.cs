using System;
using System.IO;
using System.Text;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 构建流水线测试
    /// ① build.bat 存在性与内容验证
    /// ② 流水线步骤完整性
    /// ③ 工具路径配置
    /// </summary>
    public class BuildScriptTests
    {
        private readonly string _buildBatPath;
        private readonly string _buildBatContent;
        private readonly string _projectRoot;

        public BuildScriptTests()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            _buildBatPath = Path.Combine(_projectRoot, "build.bat");

            if (File.Exists(_buildBatPath))
            {
                _buildBatContent = File.ReadAllText(_buildBatPath, Encoding.UTF8);
            }
            else
            {
                _buildBatContent = "";
            }
        }

        // ========== 文件存在性 ==========

        [Fact]
        public void BuildBat_Exists()
        {
            Assert.True(File.Exists(_buildBatPath),
                string.Format("build.bat 不存在: {0}", _buildBatPath));
        }

        // ========== 流水线步骤 ==========

        [Fact]
        public void BuildBat_HasMSBuildStep()
        {
            // 步骤1: MSBuild Release 编译
            Assert.Contains("MSBuild", _buildBatContent);
            Assert.Contains("Release", _buildBatContent);
        }

        [Fact]
        public void BuildBat_HasXUnitStep()
        {
            // 步骤2: xUnit 测试
            Assert.Contains("xunit", _buildBatContent);
        }

        [Fact]
        public void BuildBat_HasConfuserExStep()
        {
            // 步骤3: ConfuserEx 混淆
            Assert.Contains("Confuser", _buildBatContent);
        }

        [Fact]
        public void BuildBat_HasInnoSetupStep()
        {
            // 步骤4: Inno Setup 打包
            Assert.Contains("ISCC", _buildBatContent);
        }

        // ========== 参数支持 ==========

        [Fact]
        public void BuildBat_SupportsSkipTest()
        {
            Assert.Contains("--skip-test", _buildBatContent);
        }

        [Fact]
        public void BuildBat_SupportsSkipConfuse()
        {
            Assert.Contains("--skip-confuse", _buildBatContent);
        }

        [Fact]
        public void BuildBat_SupportsSkipInstaller()
        {
            Assert.Contains("--skip-installer", _buildBatContent);
        }

        // ========== 错误处理 ==========

        [Fact]
        public void BuildBat_HasErrorHandling()
        {
            // 每步失败后应退出
            Assert.Contains("ERRORLEVEL", _buildBatContent);
            Assert.Contains("exit /b", _buildBatContent);
        }

        // ========== 混淆版本自动选择 ==========

        [Fact]
        public void BuildBat_AutoSelectsConfusedVersion()
        {
            // 自动检测混淆版本，优先使用
            Assert.Contains("UseConfused", _buildBatContent);
            Assert.Contains("Confused\\MediaTrans.exe", _buildBatContent);
        }

        // ========== 输出验证 ==========

        [Fact]
        public void BuildBat_VerifiesOutput()
        {
            // 验证安装包输出
            Assert.Contains("MediaTrans_Setup", _buildBatContent);
        }

        // ========== 工具查找逻辑 ==========

        [Fact]
        public void BuildBat_SearchesMSBuildPaths()
        {
            // 搜索多个 MSBuild 路径
            Assert.Contains("2019", _buildBatContent);
            Assert.Contains("BuildTools", _buildBatContent);
        }

        [Fact]
        public void BuildBat_SearchesInnoSetupPaths()
        {
            // 搜索 Inno Setup 路径
            Assert.Contains("Inno Setup 6", _buildBatContent);
        }

        // ========== 完整流水线输出验证 ==========

        [Fact]
        public void Pipeline_OutputExists()
        {
            // 如果已执行过 build.bat，验证输出
            string installerPath = Path.Combine(_projectRoot, "dist", "MediaTrans_Setup_1.0.0.exe");
            if (File.Exists(installerPath))
            {
                var fi = new FileInfo(installerPath);
                Assert.True(fi.Length > 1024 * 1024,
                    string.Format("安装包过小({0}字节)", fi.Length));
            }
        }

        [Fact]
        public void Pipeline_ConfusedOutputExists()
        {
            // 如果混淆步骤已执行，验证输出
            string confusedPath = Path.Combine(_projectRoot, "src", "MediaTrans", "bin", "Confused", "MediaTrans.exe");
            if (File.Exists(confusedPath))
            {
                var fi = new FileInfo(confusedPath);
                // 混淆后应大于 100KB
                Assert.True(fi.Length > 100 * 1024,
                    string.Format("混淆后文件过小({0}字节)", fi.Length));
            }
        }
    }
}
