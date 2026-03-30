using System;
using System.IO;
using System.Text;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// Inno Setup 安装包脚本测试
    /// ① 脚本文件存在性与格式
    /// ② 关键配置项验证
    /// ③ .NET 4.5.2 检测逻辑
    /// ④ 安装包输出验证
    /// </summary>
    public class InnoSetupTests
    {
        private readonly string _issPath;
        private readonly string _issContent;
        private readonly string _projectRoot;

        public InnoSetupTests()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            _issPath = Path.Combine(_projectRoot, "installer", "MediaTrans.iss");

            if (File.Exists(_issPath))
            {
                _issContent = File.ReadAllText(_issPath, Encoding.UTF8);
            }
            else
            {
                _issContent = "";
            }
        }

        // ========== 文件存在性 ==========

        [Fact]
        public void IssFile_Exists()
        {
            Assert.True(File.Exists(_issPath),
                string.Format("Inno Setup 脚本不存在: {0}", _issPath));
        }

        // ========== 基本配置 ==========

        [Fact]
        public void IssFile_HasAppName()
        {
            Assert.Contains("MyAppName", _issContent);
            Assert.Contains("MediaTrans", _issContent);
        }

        [Fact]
        public void IssFile_HasAppVersion()
        {
            Assert.Contains("MyAppVersion", _issContent);
        }

        [Fact]
        public void IssFile_HasSetupSection()
        {
            Assert.Contains("[Setup]", _issContent);
        }

        [Fact]
        public void IssFile_HasFilesSection()
        {
            Assert.Contains("[Files]", _issContent);
        }

        [Fact]
        public void IssFile_HasIconsSection()
        {
            Assert.Contains("[Icons]", _issContent);
        }

        [Fact]
        public void IssFile_HasRunSection()
        {
            Assert.Contains("[Run]", _issContent);
        }

        [Fact]
        public void IssFile_HasCodeSection()
        {
            Assert.Contains("[Code]", _issContent);
        }

        // ========== .NET Framework 检测 ==========

        [Fact]
        public void IssFile_HasDotNetDetection()
        {
            // 必须包含 .NET 4.5.2 检测逻辑
            Assert.Contains("IsDotNetInstalled", _issContent);
            Assert.Contains("379893", _issContent); // .NET 4.5.2 的 Release 值
        }

        [Fact]
        public void IssFile_HasDotNetRegistryCheck()
        {
            // 必须检查注册表
            Assert.Contains("NET Framework Setup", _issContent);
        }

        [Fact]
        public void IssFile_HasDotNetDownloadPrompt()
        {
            // 缺少 .NET 时提示下载
            Assert.Contains("InitializeSetup", _issContent);
        }

        // ========== 文件打包 ==========

        [Fact]
        public void IssFile_PackagesMainExe()
        {
            // 必须打包主程序
            Assert.Contains("MediaTrans.exe", _issContent);
        }

        [Fact]
        public void IssFile_PackagesSkiaSharp()
        {
            // 必须打包 SkiaSharp
            Assert.Contains("SkiaSharp.dll", _issContent);
            Assert.Contains("libSkiaSharp.dll", _issContent);
        }

        [Fact]
        public void IssFile_PackagesNAudio()
        {
            Assert.Contains("NAudio.dll", _issContent);
        }

        [Fact]
        public void IssFile_PackagesNewtonsoftJson()
        {
            Assert.Contains("Newtonsoft.Json.dll", _issContent);
        }

        [Fact]
        public void IssFile_PackagesConfig()
        {
            // 必须打包配置文件
            Assert.Contains("AppConfig.json", _issContent);
        }

        [Fact]
        public void IssFile_ConfigOnlyIfDoesntExist()
        {
            // 配置文件使用 onlyifdoesntexist 防止覆盖用户配置
            Assert.Contains("onlyifdoesntexist", _issContent);
        }

        [Fact]
        public void IssFile_PackagesFFmpeg()
        {
            // 必须包含 FFmpeg 打包逻辑（条件编译）
            Assert.Contains("ffmpeg.exe", _issContent);
            Assert.Contains("ffprobe.exe", _issContent);
        }

        [Fact]
        public void IssFile_FFmpegInSubDir()
        {
            // FFmpeg 安装到子目录
            Assert.Contains("lib\\ffmpeg", _issContent);
        }

        [Fact]
        public void IssFile_PackagesNativeDlls()
        {
            // x86 和 x64 原生库
            Assert.Contains("x86\\libSkiaSharp.dll", _issContent);
            Assert.Contains("x64\\libSkiaSharp.dll", _issContent);
        }

        // ========== 快捷方式 ==========

        [Fact]
        public void IssFile_HasDesktopShortcut()
        {
            Assert.Contains("desktopicon", _issContent);
            Assert.Contains("{autodesktop}", _issContent);
        }

        [Fact]
        public void IssFile_HasStartMenuShortcut()
        {
            Assert.Contains("{group}", _issContent);
        }

        // ========== 卸载 ==========

        [Fact]
        public void IssFile_HasUninstallCleanup()
        {
            // 卸载时清理残留
            Assert.Contains("[UninstallDelete]", _issContent);
            Assert.Contains("{localappdata}", _issContent);
        }

        [Fact]
        public void IssFile_HasUninstallConfirmation()
        {
            // 卸载时询问是否删除应用数据
            Assert.Contains("CurUninstallStepChanged", _issContent);
        }

        // ========== Windows 版本 ==========

        [Fact]
        public void IssFile_MinVersionWin7SP1()
        {
            // 最低版本为 Win7 SP1
            Assert.Contains("MinVersion=6.1sp1", _issContent);
        }

        // ========== 路径安全 ==========

        [Fact]
        public void IssFile_SupportsSpacesInPath()
        {
            // 默认安装路径使用 {autopf} 自动处理空格
            Assert.Contains("{autopf}", _issContent);
        }

        // ========== 混淆版本支持 ==========

        [Fact]
        public void IssFile_SupportsConfusedBuild()
        {
            // 支持混淆版本编译
            Assert.Contains("UseConfused", _issContent);
            Assert.Contains("Confused", _issContent);
        }

        // ========== 安装包输出 ==========

        [Fact]
        public void InstallerOutput_ExistsAfterBuild()
        {
            // 验证安装包输出（如果已构建）
            string installerPath = Path.Combine(_projectRoot, "dist", "MediaTrans_Setup_1.0.0.exe");
            if (File.Exists(installerPath))
            {
                var fi = new FileInfo(installerPath);
                // 安装包应大于 1MB（包含 SkiaSharp 等大型 DLL）
                Assert.True(fi.Length > 1024 * 1024,
                    string.Format("安装包过小({0}字节)，可能打包不完整", fi.Length));
            }
        }

        [Fact]
        public void InstallerOutput_NameMatchesVersion()
        {
            // 输出文件名包含版本号
            Assert.Contains("OutputBaseFilename=MediaTrans_Setup_", _issContent);
        }

        // ========== 压缩设置 ==========

        [Fact]
        public void IssFile_UsesLzma2Compression()
        {
            Assert.Contains("lzma2", _issContent);
        }

        [Fact]
        public void IssFile_UsesSolidCompression()
        {
            Assert.Contains("SolidCompression=yes", _issContent);
        }
    }
}
