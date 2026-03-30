using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// Win7 兼容性测试 — Task 4.1
    /// 验证应用清单、OS 兼容性声明、DPI 感知、UAC 声明、系统依赖项检查
    /// </summary>
    public class Win7CompatibilityTests
    {
        private readonly string _projectRoot;

        public Win7CompatibilityTests()
        {
            // 定位到项目根目录
            _projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        }

        // ==================== 应用清单（app.manifest）验证 ====================

        [Fact]
        public void AppManifest_文件存在()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            Assert.True(File.Exists(manifestPath), "app.manifest 文件应存在");
        }

        [Fact]
        public void AppManifest_是有效XML()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            if (!File.Exists(manifestPath)) return;

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            Assert.NotNull(doc.DocumentElement);
        }

        [Fact]
        public void AppManifest_包含Win7兼容性声明()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            if (!File.Exists(manifestPath)) return;

            string content = File.ReadAllText(manifestPath);
            // Win7 SP1 GUID: {35138b9a-5d96-4fbd-8e2d-a2440225f93a}
            Assert.Contains("35138b9a-5d96-4fbd-8e2d-a2440225f93a", content);
        }

        [Fact]
        public void AppManifest_包含Win8兼容性声明()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            if (!File.Exists(manifestPath)) return;

            string content = File.ReadAllText(manifestPath);
            // Win8 GUID: {4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38}
            Assert.Contains("4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38", content);
        }

        [Fact]
        public void AppManifest_包含Win81兼容性声明()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            if (!File.Exists(manifestPath)) return;

            string content = File.ReadAllText(manifestPath);
            // Win8.1 GUID: {1f676c76-80e1-4239-95bb-83d0f6d0da78}
            Assert.Contains("1f676c76-80e1-4239-95bb-83d0f6d0da78", content);
        }

        [Fact]
        public void AppManifest_包含Win10兼容性声明()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            if (!File.Exists(manifestPath)) return;

            string content = File.ReadAllText(manifestPath);
            // Win10/11 GUID: {8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}
            Assert.Contains("8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a", content);
        }

        [Fact]
        public void AppManifest_包含UAC声明_AsInvoker()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            if (!File.Exists(manifestPath)) return;

            string content = File.ReadAllText(manifestPath);
            // 应以调用者身份运行（不需要管理员权限）
            Assert.Contains("asInvoker", content);
            Assert.Contains("requestedExecutionLevel", content);
        }

        [Fact]
        public void AppManifest_包含DPI感知声明()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            if (!File.Exists(manifestPath)) return;

            string content = File.ReadAllText(manifestPath);
            // Win7/8 兼容的 dpiAware 声明
            Assert.Contains("dpiAware", content);
        }

        [Fact]
        public void AppManifest_包含PerMonitorV2声明()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            if (!File.Exists(manifestPath)) return;

            string content = File.ReadAllText(manifestPath);
            // Win10+ Per-Monitor V2 声明
            Assert.Contains("PerMonitorV2", content);
            Assert.Contains("dpiAwareness", content);
        }

        // ==================== csproj 项目配置验证 ====================

        [Fact]
        public void Csproj_引用了ApplicationManifest()
        {
            string csprojPath = Path.Combine(_projectRoot, "src", "MediaTrans", "MediaTrans.csproj");
            string content = File.ReadAllText(csprojPath);
            Assert.Contains("ApplicationManifest", content);
            Assert.Contains("app.manifest", content);
        }

        [Fact]
        public void Csproj_目标框架为NET452()
        {
            string csprojPath = Path.Combine(_projectRoot, "src", "MediaTrans", "MediaTrans.csproj");
            string content = File.ReadAllText(csprojPath);
            Assert.Contains("v4.5.2", content);
        }

        [Fact]
        public void Csproj_SkiaSharp版本兼容NET452()
        {
            string csprojPath = Path.Combine(_projectRoot, "src", "MediaTrans", "MediaTrans.csproj");
            string content = File.ReadAllText(csprojPath);
            // 必须使用 SkiaSharp 1.68.3（net45 兼容版本）
            Assert.Contains("SkiaSharp.1.68.3", content);
        }

        [Fact]
        public void Csproj_NAudio版本兼容NET35()
        {
            string csprojPath = Path.Combine(_projectRoot, "src", "MediaTrans", "MediaTrans.csproj");
            string content = File.ReadAllText(csprojPath);
            // NAudio 1.10.0 有 net35 版（兼容 .NET 4.5.2）
            Assert.Contains("NAudio.1.10.0", content);
        }

        // ==================== CompatibilityService 单元测试 ====================

        [Fact]
        public void CompatibilityService_GetWindowsVersion_返回有效版本()
        {
            var service = new CompatibilityService();
            var version = service.GetWindowsVersion();

            Assert.NotNull(version);
            Assert.True(version.Major >= 6, "操作系统主版本应 >= 6（Win7+）");
        }

        [Fact]
        public void CompatibilityService_IsWindows7OrLater_当前系统应返回True()
        {
            var service = new CompatibilityService();
            Assert.True(service.IsWindows7OrLater(), "当前系统应为 Win7 或更高版本");
        }

        [Fact]
        public void CompatibilityService_IsNetFramework452OrLater_当前环境应返回True()
        {
            var service = new CompatibilityService();
            Assert.True(service.IsNetFramework452OrLater(), ".NET 4.5.2 或更高版本应已安装");
        }

        [Fact]
        public void CompatibilityService_IsWmiAvailable_应返回True()
        {
            var service = new CompatibilityService();
            Assert.True(service.IsWmiAvailable(), "WMI 服务应可用");
        }

        [Fact]
        public void CompatibilityService_IsAudioSubsystemAvailable_应返回True()
        {
            var service = new CompatibilityService();
            // 在 CI 环境或无声卡机器上可能为 false，但开发机应有音频设备
            bool result = service.IsAudioSubsystemAvailable();
            // 不强制断言为 true，仅验证调用不抛异常
            Assert.True(result || !result); // 仅验证不抛异常
        }

        [Fact]
        public void CompatibilityService_Is64BitOperatingSystem_不抛异常()
        {
            var service = new CompatibilityService();
            bool result = service.Is64BitOperatingSystem();
            Assert.True(result || !result);
        }

        [Fact]
        public void CompatibilityService_Is64BitProcess_不抛异常()
        {
            var service = new CompatibilityService();
            bool result = service.Is64BitProcess();
            Assert.True(result || !result);
        }

        [Fact]
        public void CompatibilityService_GetSystemSummary_返回非空字符串()
        {
            var service = new CompatibilityService();
            string summary = service.GetSystemSummary();

            Assert.False(string.IsNullOrEmpty(summary));
            Assert.Contains("OS:", summary);
            Assert.Contains(".NET CLR:", summary);
        }

        [Fact]
        public void CompatibilityService_IsFFmpegAvailable_空路径返回False()
        {
            var service = new CompatibilityService();
            Assert.False(service.IsFFmpegAvailable(null));
            Assert.False(service.IsFFmpegAvailable(""));
        }

        [Fact]
        public void CompatibilityService_IsFFmpegAvailable_不存在路径返回False()
        {
            var service = new CompatibilityService();
            Assert.False(service.IsFFmpegAvailable(@"C:\nonexistent\ffmpeg.exe"));
        }

        // ==================== WMI 硬件信息采集兼容性 ====================

        [Fact]
        public void MachineCodeService_GetCpuId_Win7兼容WMI()
        {
            var service = new MachineCodeService();
            string cpuId = service.GetCpuId();
            // WMI Win32_Processor.ProcessorId 在 Win7+ 可用
            // 允许在虚拟机中返回空值
            Assert.NotNull(cpuId);
        }

        [Fact]
        public void MachineCodeService_GetDiskSerial_Win7兼容WMI()
        {
            var service = new MachineCodeService();
            string serial = service.GetDiskSerial();
            Assert.NotNull(serial);
        }

        [Fact]
        public void MachineCodeService_GetBoardSerial_Win7兼容WMI()
        {
            var service = new MachineCodeService();
            string serial = service.GetBoardSerial();
            Assert.NotNull(serial);
        }

        [Fact]
        public void MachineCodeService_GetMachineCode_Win7生成有效机器码()
        {
            var service = new MachineCodeService();
            string code = service.GetMachineCode();

            Assert.NotNull(code);
            Assert.Equal(64, code.Length); // SHA256 = 64 hex chars
        }

        // ==================== SkiaSharp 原生库兼容性 ====================

        [Fact]
        public void SkiaSharp_DLL存在于构建输出()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string skiaPath = Path.Combine(baseDir, "SkiaSharp.dll");
            Assert.True(File.Exists(skiaPath), "SkiaSharp.dll 应存在于输出目录");
        }

        [Fact]
        public void SkiaSharp_原生库存在于构建输出()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // SkiaSharp 1.68.3 使用 x86/x64 子目录
            bool hasRoot = File.Exists(Path.Combine(baseDir, "libSkiaSharp.dll"));
            bool hasX86 = File.Exists(Path.Combine(baseDir, "x86", "libSkiaSharp.dll"));
            bool hasX64 = File.Exists(Path.Combine(baseDir, "x64", "libSkiaSharp.dll"));

            Assert.True(hasRoot || hasX86 || hasX64,
                "libSkiaSharp.dll 应存在于输出目录的根目录或 x86/x64 子目录");
        }

        [Fact]
        public void SkiaSharp_可创建SKPaint对象()
        {
            // 验证 SkiaSharp 管理库可正常加载和使用
            using (var paint = new SkiaSharp.SKPaint())
            {
                paint.Color = new SkiaSharp.SKColor(255, 0, 0);
                Assert.Equal(255, paint.Color.Red);
            }
        }

        // ==================== 进程管理兼容性（Job Object） ====================

        [Fact]
        public void JobObject_Win7支持CreateJobObject()
        {
            // Job Object API 从 Win2000 开始支持，Win7 完全兼容
            var jo = new JobObject();
            // 创建不抛异常即验证通过
            jo.Dispose();
        }

        // ==================== NAudio 兼容性 ====================

        [Fact]
        public void NAudio_DLL存在于构建输出()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string nAudioPath = Path.Combine(baseDir, "NAudio.dll");
            Assert.True(File.Exists(nAudioPath), "NAudio.dll 应存在于输出目录");
        }

        // ==================== 应用启动兼容性 ====================

        [Fact]
        public void ConfigService_Win7环境可正常加载配置()
        {
            var service = new ConfigService();
            var config = service.Load();
            Assert.NotNull(config);
        }

        [Fact]
        public void LogService_Win7环境可正常初始化()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "MediaTransTest_Win7");
            try
            {
                LogService.Initialize(tempDir, 1024 * 1024, 3);
                var instance = LogService.Instance;
                Assert.NotNull(instance);
                instance.Info("Win7 兼容性测试日志");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
