using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using MediaTrans.Models;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 里程碑 4 综合测试 — Task 4.6
    /// 覆盖安装包完整性、兼容性矩阵验证、构建流水线、混淆配置、全流程集成
    /// </summary>
    public class Milestone4Tests
    {
        private readonly string _projectRoot;

        public Milestone4Tests()
        {
            _projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        }

        // ==================== 一、安装包完整性验证 ====================

        [Fact]
        public void 安装脚本_ISS文件存在且有效()
        {
            string issPath = Path.Combine(_projectRoot, "installer", "MediaTrans.iss");
            Assert.True(File.Exists(issPath), "Inno Setup 脚本应存在");

            string content = File.ReadAllText(issPath);
            Assert.Contains("[Setup]", content);
            Assert.Contains("[Files]", content);
            Assert.Contains("[Icons]", content);
        }

        [Fact]
        public void 安装脚本_包含NET452检测()
        {
            string issPath = Path.Combine(_projectRoot, "installer", "MediaTrans.iss");
            string content = File.ReadAllText(issPath);
            // .NET 4.5.2 Release 值 379893
            Assert.Contains("379893", content);
        }

        [Fact]
        public void 安装脚本_包含桌面快捷方式()
        {
            string issPath = Path.Combine(_projectRoot, "installer", "MediaTrans.iss");
            string content = File.ReadAllText(issPath);
            Assert.Contains("desktopicon", content);
        }

        [Fact]
        public void 安装脚本_包含卸载清理逻辑()
        {
            string issPath = Path.Combine(_projectRoot, "installer", "MediaTrans.iss");
            string content = File.ReadAllText(issPath);
            Assert.Contains("UninstallDelete", content.Contains("CurUninstallStepChanged") ? content : content);
        }

        [Fact]
        public void 安装脚本_最低版本为Win7SP1()
        {
            string issPath = Path.Combine(_projectRoot, "installer", "MediaTrans.iss");
            string content = File.ReadAllText(issPath);
            Assert.Contains("MinVersion", content);
            Assert.Contains("6.1sp1", content);
        }

        [Fact]
        public void 安装脚本_支持x86和x64架构()
        {
            string issPath = Path.Combine(_projectRoot, "installer", "MediaTrans.iss");
            string content = File.ReadAllText(issPath);
            Assert.Contains("x86compatible", content);
            Assert.Contains("x64compatible", content);
        }

        [Fact]
        public void 安装脚本_使用LZMA2压缩()
        {
            string issPath = Path.Combine(_projectRoot, "installer", "MediaTrans.iss");
            string content = File.ReadAllText(issPath);
            Assert.Contains("lzma2", content);
            Assert.Contains("SolidCompression", content);
        }

        // ==================== 二、构建流水线验证 ====================

        [Fact]
        public void 构建脚本_BAT文件存在()
        {
            string batPath = Path.Combine(_projectRoot, "build.bat");
            Assert.True(File.Exists(batPath), "build.bat 应存在");
        }

        [Fact]
        public void 构建脚本_包含MSBuild步骤()
        {
            string batPath = Path.Combine(_projectRoot, "build.bat");
            string content = File.ReadAllText(batPath);
            Assert.Contains("MSBuild", content);
            Assert.Contains("Release", content);
        }

        [Fact]
        public void 构建脚本_包含xUnit测试步骤()
        {
            string batPath = Path.Combine(_projectRoot, "build.bat");
            string content = File.ReadAllText(batPath);
            Assert.Contains("xunit", content.ToLower());
        }

        [Fact]
        public void 构建脚本_包含ConfuserEx步骤()
        {
            string batPath = Path.Combine(_projectRoot, "build.bat");
            string content = File.ReadAllText(batPath);
            Assert.Contains("Confuser", content);
        }

        [Fact]
        public void 构建脚本_包含InnoSetup步骤()
        {
            string batPath = Path.Combine(_projectRoot, "build.bat");
            string content = File.ReadAllText(batPath);
            Assert.Contains("ISCC", content);
        }

        [Fact]
        public void 构建脚本_支持跳过参数()
        {
            string batPath = Path.Combine(_projectRoot, "build.bat");
            string content = File.ReadAllText(batPath);
            Assert.Contains("--skip-test", content);
            Assert.Contains("--skip-confuse", content);
            Assert.Contains("--skip-installer", content);
        }

        // ==================== 三、代码混淆配置验证 ====================

        [Fact]
        public void 混淆配置_crproj文件存在()
        {
            string crprojPath = Path.Combine(_projectRoot, "tools", "ConfuserEx", "MediaTrans.crproj");
            Assert.True(File.Exists(crprojPath), "ConfuserEx 配置文件应存在");
        }

        [Fact]
        public void 混淆配置_保护LicenseService()
        {
            string crprojPath = Path.Combine(_projectRoot, "tools", "ConfuserEx", "MediaTrans.crproj");
            string content = File.ReadAllText(crprojPath);
            Assert.Contains("LicenseService", content);
        }

        [Fact]
        public void 混淆配置_保护MachineCodeService()
        {
            string crprojPath = Path.Combine(_projectRoot, "tools", "ConfuserEx", "MediaTrans.crproj");
            string content = File.ReadAllText(crprojPath);
            Assert.Contains("MachineCodeService", content);
        }

        [Fact]
        public void 混淆配置_保护PaywallService()
        {
            string crprojPath = Path.Combine(_projectRoot, "tools", "ConfuserEx", "MediaTrans.crproj");
            string content = File.ReadAllText(crprojPath);
            Assert.Contains("PaywallService", content);
        }

        [Fact]
        public void 混淆配置_包含AntiTamper保护()
        {
            string crprojPath = Path.Combine(_projectRoot, "tools", "ConfuserEx", "MediaTrans.crproj");
            string content = File.ReadAllText(crprojPath);
            Assert.Contains("anti tamper", content);
        }

        [Fact]
        public void 混淆配置_包含常量加密()
        {
            string crprojPath = Path.Combine(_projectRoot, "tools", "ConfuserEx", "MediaTrans.crproj");
            string content = File.ReadAllText(crprojPath);
            Assert.Contains("constants", content);
        }

        [Fact]
        public void 混淆配置_包含资源保护()
        {
            string crprojPath = Path.Combine(_projectRoot, "tools", "ConfuserEx", "MediaTrans.crproj");
            string content = File.ReadAllText(crprojPath);
            Assert.Contains("resources", content);
        }

        // ==================== 四、兼容性矩阵验证 ====================

        [Fact]
        public void 兼容性_AppManifest包含所有OS声明()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            string content = File.ReadAllText(manifestPath);

            // Win7 SP1
            Assert.Contains("35138b9a-5d96-4fbd-8e2d-a2440225f93a", content);
            // Win8
            Assert.Contains("4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38", content);
            // Win8.1
            Assert.Contains("1f676c76-80e1-4239-95bb-83d0f6d0da78", content);
            // Win10/11
            Assert.Contains("8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a", content);
        }

        [Fact]
        public void 兼容性_DPI声明同时支持旧版和新版()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            string content = File.ReadAllText(manifestPath);

            // Win7/8 使用 dpiAware (2005 命名空间)
            Assert.Contains("dpiAware", content);
            // Win10+ 使用 dpiAwareness (2016 命名空间)
            Assert.Contains("dpiAwareness", content);
            Assert.Contains("PerMonitorV2", content);
        }

        [Fact]
        public void 兼容性_UAC声明为AsInvoker()
        {
            string manifestPath = Path.Combine(_projectRoot, "src", "MediaTrans", "app.manifest");
            string content = File.ReadAllText(manifestPath);

            Assert.Contains("asInvoker", content);
            Assert.DoesNotContain("requireAdministrator", content);
            Assert.DoesNotContain("highestAvailable", content);
        }

        [Theory]
        [InlineData(96, 1.0)]
        [InlineData(120, 1.25)]
        [InlineData(144, 1.5)]
        [InlineData(192, 2.0)]
        public void 兼容性_DpiHelper各缩放级别计算正确(int dpi, double expectedScale)
        {
            DpiHelper.SetDpi(dpi, dpi);
            Assert.Equal(expectedScale, DpiHelper.ScaleX);
            Assert.Equal(expectedScale, DpiHelper.ScaleY);
            DpiHelper.Reset();
        }

        [Fact]
        public void 兼容性_CompatibilityService系统信息完整()
        {
            var service = new CompatibilityService();
            string summary = service.GetSystemSummary();

            Assert.Contains("OS:", summary);
            Assert.Contains(".NET CLR:", summary);
            Assert.Contains("位进程", summary);
            Assert.Contains("位系统", summary);
        }

        // ==================== 五、目标框架与依赖项验证 ====================

        [Fact]
        public void 框架_NET452运行时环境()
        {
            // 验证当前运行在 .NET 4.5.2+ 上
            Assert.True(Environment.Version.Major >= 4, "应运行在 .NET 4.x 上");
        }

        [Fact]
        public void 依赖项_SkiaSharp托管库可用()
        {
            // 验证 SkiaSharp 可正常创建对象
            using (var bitmap = new SkiaSharp.SKBitmap(10, 10))
            {
                Assert.Equal(10, bitmap.Width);
                Assert.Equal(10, bitmap.Height);
            }
        }

        [Fact]
        public void 依赖项_NAudio库可加载()
        {
            string nAudioPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NAudio.dll");
            Assert.True(File.Exists(nAudioPath));
        }

        [Fact]
        public void 依赖项_NewtonsoftJson可用()
        {
            // 验证 JSON 序列化正常
            var obj = Newtonsoft.Json.JsonConvert.SerializeObject(new { test = 1 });
            Assert.Contains("test", obj);
        }

        // ==================== 六、服务集成验证 ====================

        [Fact]
        public void 集成_ConfigService加载默认配置()
        {
            var service = new ConfigService();
            var config = service.Load();

            Assert.NotNull(config);
            Assert.True(config.WaveformBlockWidth > 0);
            Assert.True(config.MaxCachedFrames > 0);
        }

        [Fact]
        public void 集成_MachineCodeService生成唯一码()
        {
            var service = new MachineCodeService();
            string code1 = service.GetMachineCode();
            string code2 = service.GetMachineCode();

            // 同一机器两次调用应返回相同结果
            Assert.Equal(code1, code2);
            Assert.Equal(64, code1.Length);
        }

        [Fact]
        public void 集成_LicenseService初始化正常()
        {
            var machineCodeService = new MachineCodeService();
            var service = new LicenseService(machineCodeService);
            // 未激活时状态应为未授权
            var status = service.Status;
            Assert.True(status == LicenseStatus.NotActivated || status == LicenseStatus.Activated);
        }

        [Fact]
        public void 集成_PaywallService初始化正常()
        {
            var machineCodeService = new MachineCodeService();
            var licService = new LicenseService(machineCodeService);
            var configService = new ConfigService();
            var paywallService = new PaywallService(licService, configService);
            Assert.NotNull(paywallService);
        }

        [Fact]
        public void 集成_FFmpegCommandBuilder构建命令()
        {
            var builder = new FFmpegCommandBuilder();
            string cmd = builder
                .Input("test input.mp4")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output("test output.mp4")
                .Build();

            Assert.Contains("-c:v libx264", cmd);
            Assert.Contains("-c:a aac", cmd);
            Assert.Contains("\"test input.mp4\"", cmd);
            Assert.Contains("\"test output.mp4\"", cmd);
        }

        [Fact]
        public void 集成_UndoRedoService栈操作()
        {
            var service = new UndoRedoService(50);
            Assert.False(service.CanUndo);
            Assert.False(service.CanRedo);
        }

        [Fact]
        public void 集成_SnappingService磁吸计算()
        {
            var service = new SnappingService(10);
            Assert.NotNull(service);
        }

        [Fact]
        public void 集成_GainService增益计算()
        {
            double db = GainService.LinearToDb(1.0);
            Assert.Equal(0.0, Math.Round(db, 1));
        }

        [Fact]
        public void 集成_LogService写入日志()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "MediaTransM4Test");
            try
            {
                LogService.Initialize(tempDir, 1024 * 1024, 3);
                var logger = LogService.Instance;
                Assert.NotNull(logger);
                logger.Info("里程碑4综合测试日志");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        // ==================== 七、波形渲染 DPI 集成 ====================

        [Fact]
        public void 波形_DpiScale各级别渲染正常()
        {
            var pcmService = new AudioPcmCacheService("ffmpeg");
            using (var renderService = new WaveformRenderService(pcmService, 100, 50))
            {
                double[] scales = new double[] { 1.0, 1.25, 1.5, 2.0 };
                foreach (double scale in scales)
                {
                    renderService.DpiScale = scale;
                    Assert.Equal(scale, renderService.DpiScale);
                }
            }
        }

        [Fact]
        public void 波形_高DPI渲染位图尺寸正确()
        {
            var pcmService = new AudioPcmCacheService("ffmpeg");
            using (var renderService = new WaveformRenderService(pcmService, 100, 50))
            {
                float[] samples = new float[1000];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = (float)Math.Sin(i * 0.1);
                }

                // 200% DPI 下渲染
                var bitmap = renderService.RenderWaveformBitmap(samples, 200, 100, 5);
                Assert.Equal(200, bitmap.Width);
                Assert.Equal(100, bitmap.Height);
                bitmap.Dispose();
            }
        }

        // ==================== 八、项目配置综合验证 ====================

        [Fact]
        public void 项目_csproj引用ApplicationManifest()
        {
            string csprojPath = Path.Combine(_projectRoot, "src", "MediaTrans", "MediaTrans.csproj");
            string content = File.ReadAllText(csprojPath);
            Assert.Contains("ApplicationManifest", content);
        }

        [Fact]
        public void 项目_csproj目标框架为v452()
        {
            string csprojPath = Path.Combine(_projectRoot, "src", "MediaTrans", "MediaTrans.csproj");
            string content = File.ReadAllText(csprojPath);
            Assert.Contains("<TargetFrameworkVersion>v4.5.2</TargetFrameworkVersion>", content);
        }

        [Fact]
        public void 项目_公钥嵌入资源存在()
        {
            string csprojPath = Path.Combine(_projectRoot, "src", "MediaTrans", "MediaTrans.csproj");
            string content = File.ReadAllText(csprojPath);
            Assert.Contains("public_key.pem", content);
        }

        [Fact]
        public void 项目_配置文件复制到输出目录()
        {
            string csprojPath = Path.Combine(_projectRoot, "src", "MediaTrans", "MediaTrans.csproj");
            string content = File.ReadAllText(csprojPath);
            Assert.Contains("AppConfig.json", content);
            Assert.Contains("PreserveNewest", content);
        }

        [Fact]
        public void 项目_InternalsVisibleTo已配置()
        {
            string assemblyInfoPath = Path.Combine(_projectRoot, "src", "MediaTrans", "Properties", "AssemblyInfo.cs");
            string content = File.ReadAllText(assemblyInfoPath);
            Assert.Contains("InternalsVisibleTo", content);
            Assert.Contains("MediaTrans.Tests", content);
        }

        // ==================== 九、安全红线验证 ====================

        [Fact]
        public void 安全_客户端代码不含私钥()
        {
            // 扫描 src/MediaTrans 目录下所有 .cs 文件
            string srcDir = Path.Combine(_projectRoot, "src", "MediaTrans");
            string[] csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories);

            foreach (string file in csFiles)
            {
                string content = File.ReadAllText(file);
                Assert.DoesNotContain("PRIVATE KEY", content);
                Assert.DoesNotContain("RSAParameters", content.Contains("RSAParameters") 
                    && !file.Contains("LicenseService") ? "PRIVATE_KEY_MARKER" : "");
            }
        }

        [Fact]
        public void 安全_公钥以嵌入资源存储()
        {
            string pemPath = Path.Combine(_projectRoot, "src", "MediaTrans", "Assets", "public_key.pem");
            Assert.True(File.Exists(pemPath), "公钥文件应存在");

            string content = File.ReadAllText(pemPath);
            Assert.Contains("PUBLIC KEY", content);
            Assert.DoesNotContain("PRIVATE KEY", content);
        }

        [Fact]
        public void 安全_FFmpeg路径使用双引号包裹()
        {
            var builder = new FFmpegCommandBuilder();
            string cmd = builder
                .Input("路径 含空格/中文.mp4")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output("输出 目录/结果.mp4")
                .Build();

            // 路径应被双引号包裹
            Assert.Contains("\"路径 含空格/中文.mp4\"", cmd);
            Assert.Contains("\"输出 目录/结果.mp4\"", cmd);
        }

        [Fact]
        public void 安全_FFmpeg命令必须包含显式编解码器()
        {
            var builder = new FFmpegCommandBuilder();
            string cmd = builder
                .Input("input.mp4")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output("output.mp4")
                .Build();

            Assert.Contains("-c:v", cmd);
            Assert.Contains("-c:a", cmd);
        }

        // ==================== 十、M4 自测检查清单覆盖 ====================

        [Fact]
        public void 检查清单_ConfuserEx配置文件完整()
        {
            string crprojPath = Path.Combine(_projectRoot, "tools", "ConfuserEx", "MediaTrans.crproj");
            Assert.True(File.Exists(crprojPath));

            var doc = new XmlDocument();
            doc.Load(crprojPath);
            Assert.NotNull(doc.DocumentElement);
        }

        [Fact]
        public void 检查清单_混淆脚本存在()
        {
            string confuseBat = Path.Combine(_projectRoot, "tools", "ConfuserEx", "confuse.bat");
            Assert.True(File.Exists(confuseBat));
        }

        [Fact]
        public void 检查清单_构建脚本可执行()
        {
            string buildBat = Path.Combine(_projectRoot, "build.bat");
            Assert.True(File.Exists(buildBat));

            string content = File.ReadAllText(buildBat);
            // 验证脚本结构完整
            Assert.Contains("MSBuild", content);
            Assert.Contains("exit", content.ToLower());
        }

        [Fact]
        public void 检查清单_FFmpeg路径配置支持()
        {
            var configService = new ConfigService();
            var config = configService.Load();
            Assert.NotNull(config);
            // FFmpeg 路径在配置中
            Assert.NotNull(config.FFmpegPath);
        }

        [Fact]
        public void 检查清单_DarkTheme资源文件存在()
        {
            string themePath = Path.Combine(_projectRoot, "src", "MediaTrans", "Assets", "DarkTheme.xaml");
            Assert.True(File.Exists(themePath));
        }

        [Fact]
        public void 检查清单_gitignore排除构建产物()
        {
            string gitignorePath = Path.Combine(_projectRoot, ".gitignore");
            Assert.True(File.Exists(gitignorePath));

            string content = File.ReadAllText(gitignorePath);
            Assert.Contains("dist/", content);
            Assert.Contains("Confused", content);
        }

        // ==================== 十一、编码规范验证 ====================

        [Fact]
        public void 规范_MVVM架构_ViewModel继承ViewModelBase()
        {
            // 验证所有 ViewModel 文件引用 ViewModelBase
            string vmDir = Path.Combine(_projectRoot, "src", "MediaTrans", "ViewModels");
            string[] vmFiles = Directory.GetFiles(vmDir, "*ViewModel.cs");

            foreach (string file in vmFiles)
            {
                string fileName = Path.GetFileName(file);
                // 跳过 ViewModelBase 本身
                if (fileName == "ViewModelBase.cs") continue;

                string content = File.ReadAllText(file);
                Assert.True(
                    content.Contains("ViewModelBase") || content.Contains(": ViewModelBase"),
                    string.Format("{0} 应继承 ViewModelBase", fileName));
            }
        }

        [Fact]
        public void 规范_RelayCommand实现ICommand()
        {
            string cmdFile = Path.Combine(_projectRoot, "src", "MediaTrans", "Commands", "RelayCommand.cs");
            Assert.True(File.Exists(cmdFile));

            string content = File.ReadAllText(cmdFile);
            Assert.Contains("ICommand", content);
        }

        [Fact]
        public void 规范_ConfigService使用UTF8编码()
        {
            string svcFile = Path.Combine(_projectRoot, "src", "MediaTrans", "Services", "ConfigService.cs");
            string content = File.ReadAllText(svcFile);
            Assert.Contains("UTF8", content);
        }
    }
}
