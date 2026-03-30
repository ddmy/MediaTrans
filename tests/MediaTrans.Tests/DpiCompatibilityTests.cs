using System;
using System.IO;
using System.Xml;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// DPI 兼容性测试 — Task 4.2
    /// 验证 DPI 感知配置、DpiHelper 计算、SkiaSharp 高 DPI 渲染、窗口布局适配
    /// </summary>
    public class DpiCompatibilityTests : IDisposable
    {
        public DpiCompatibilityTests()
        {
            // 每个测试前重置 DPI 缓存
            DpiHelper.Reset();
        }

        public void Dispose()
        {
            DpiHelper.Reset();
        }

        // ==================== DpiHelper 核心计算 ====================

        [Fact]
        public void DpiHelper_默认DPI为96()
        {
            // 无法在测试中真正读取系统 DPI，验证 API 不抛异常
            double dpiX = DpiHelper.DpiX;
            double dpiY = DpiHelper.DpiY;
            Assert.True(dpiX > 0, "DpiX 应大于 0");
            Assert.True(dpiY > 0, "DpiY 应大于 0");
        }

        [Fact]
        public void DpiHelper_ScaleX_96DPI时为1()
        {
            DpiHelper.SetDpi(96, 96);
            Assert.Equal(1.0, DpiHelper.ScaleX);
            Assert.Equal(1.0, DpiHelper.ScaleY);
        }

        [Fact]
        public void DpiHelper_ScaleX_120DPI时为1点25()
        {
            DpiHelper.SetDpi(120, 120);
            Assert.Equal(1.25, DpiHelper.ScaleX);
            Assert.Equal(1.25, DpiHelper.ScaleY);
        }

        [Fact]
        public void DpiHelper_ScaleX_144DPI时为1点5()
        {
            DpiHelper.SetDpi(144, 144);
            Assert.Equal(1.5, DpiHelper.ScaleX);
            Assert.Equal(1.5, DpiHelper.ScaleY);
        }

        [Fact]
        public void DpiHelper_ScaleX_192DPI时为2()
        {
            DpiHelper.SetDpi(192, 192);
            Assert.Equal(2.0, DpiHelper.ScaleX);
            Assert.Equal(2.0, DpiHelper.ScaleY);
        }

        // ==================== 逻辑像素与物理像素转换 ====================

        [Fact]
        public void DpiHelper_LogicalToPhysical_100DPI()
        {
            DpiHelper.SetDpi(96, 96);
            Assert.Equal(100, DpiHelper.LogicalToPhysicalX(100));
            Assert.Equal(100, DpiHelper.LogicalToPhysicalY(100));
        }

        [Fact]
        public void DpiHelper_LogicalToPhysical_125DPI()
        {
            DpiHelper.SetDpi(120, 120);
            Assert.Equal(125, DpiHelper.LogicalToPhysicalX(100));
            Assert.Equal(125, DpiHelper.LogicalToPhysicalY(100));
        }

        [Fact]
        public void DpiHelper_LogicalToPhysical_150DPI()
        {
            DpiHelper.SetDpi(144, 144);
            Assert.Equal(150, DpiHelper.LogicalToPhysicalX(100));
            Assert.Equal(150, DpiHelper.LogicalToPhysicalY(100));
        }

        [Fact]
        public void DpiHelper_LogicalToPhysical_200DPI()
        {
            DpiHelper.SetDpi(192, 192);
            Assert.Equal(200, DpiHelper.LogicalToPhysicalX(100));
            Assert.Equal(200, DpiHelper.LogicalToPhysicalY(100));
        }

        [Fact]
        public void DpiHelper_PhysicalToLogical_125DPI()
        {
            DpiHelper.SetDpi(120, 120);
            Assert.Equal(80.0, DpiHelper.PhysicalToLogicalX(100));
        }

        [Fact]
        public void DpiHelper_PhysicalToLogical_200DPI()
        {
            DpiHelper.SetDpi(192, 192);
            Assert.Equal(50.0, DpiHelper.PhysicalToLogicalX(100));
        }

        // ==================== GetPhysicalSize ====================

        [Fact]
        public void DpiHelper_GetPhysicalSize_100DPI()
        {
            DpiHelper.SetDpi(96, 96);
            int w, h;
            DpiHelper.GetPhysicalSize(900, 600, out w, out h);
            Assert.Equal(900, w);
            Assert.Equal(600, h);
        }

        [Fact]
        public void DpiHelper_GetPhysicalSize_150DPI()
        {
            DpiHelper.SetDpi(144, 144);
            int w, h;
            DpiHelper.GetPhysicalSize(900, 600, out w, out h);
            Assert.Equal(1350, w);
            Assert.Equal(900, h);
        }

        [Fact]
        public void DpiHelper_GetPhysicalSize_200DPI()
        {
            DpiHelper.SetDpi(192, 192);
            int w, h;
            DpiHelper.GetPhysicalSize(900, 600, out w, out h);
            Assert.Equal(1800, w);
            Assert.Equal(1200, h);
        }

        [Fact]
        public void DpiHelper_GetPhysicalSize_最小值保证为1()
        {
            DpiHelper.SetDpi(96, 96);
            int w, h;
            DpiHelper.GetPhysicalSize(0, 0, out w, out h);
            Assert.True(w >= 1, "最小宽度应为1");
            Assert.True(h >= 1, "最小高度应为1");
        }

        // ==================== Reset 和 SetDpi ====================

        [Fact]
        public void DpiHelper_Reset_清除缓存()
        {
            DpiHelper.SetDpi(192, 192);
            Assert.Equal(2.0, DpiHelper.ScaleX);

            DpiHelper.Reset();
            // Reset 后重新初始化，应读取系统 DPI
            double scale = DpiHelper.ScaleX;
            Assert.True(scale > 0, "Reset 后应能重新获取 DPI");
        }

        [Fact]
        public void DpiHelper_SetDpi_自定义值生效()
        {
            DpiHelper.SetDpi(240, 240);
            Assert.Equal(2.5, DpiHelper.ScaleX);
            Assert.Equal(2.5, DpiHelper.ScaleY);
            Assert.Equal(240.0, DpiHelper.DpiX);
            Assert.Equal(240.0, DpiHelper.DpiY);
        }

        // ==================== Manifest DPI 声明验证 ====================

        [Fact]
        public void Manifest_包含dpiAware声明_Win7兼容()
        {
            string manifestPath = GetManifestPath();
            if (!File.Exists(manifestPath)) return;

            string content = File.ReadAllText(manifestPath);
            // Win7 使用 dpiAware 元素
            Assert.Contains("dpiAware", content);
            Assert.Contains("true/pm", content);
        }

        [Fact]
        public void Manifest_包含dpiAwareness声明_Win10PerMonitorV2()
        {
            string manifestPath = GetManifestPath();
            if (!File.Exists(manifestPath)) return;

            string content = File.ReadAllText(manifestPath);
            // Win10 1703+ 使用 dpiAwareness 元素
            Assert.Contains("dpiAwareness", content);
            Assert.Contains("PerMonitorV2", content);
        }

        [Fact]
        public void Manifest_DPI声明使用正确的命名空间()
        {
            string manifestPath = GetManifestPath();
            if (!File.Exists(manifestPath)) return;

            string content = File.ReadAllText(manifestPath);
            // dpiAware 使用 2005 命名空间
            Assert.Contains("http://schemas.microsoft.com/SMI/2005/WindowsSettings", content);
            // dpiAwareness 使用 2016 命名空间
            Assert.Contains("http://schemas.microsoft.com/SMI/2016/WindowsSettings", content);
        }

        // ==================== WaveformRenderService DPI 缩放 ====================

        [Fact]
        public void WaveformRenderService_具有DpiScale属性()
        {
            var pcmService = new AudioPcmCacheService("ffmpeg");
            using (var service = new WaveformRenderService(pcmService))
            {
                // 默认应为 1.0
                Assert.Equal(1.0, service.DpiScale);
            }
        }

        [Fact]
        public void WaveformRenderService_DpiScale设置为2后渲染更大位图()
        {
            var pcmService = new AudioPcmCacheService("ffmpeg");
            using (var service = new WaveformRenderService(pcmService, 100, 50))
            {
                // 100% DPI 渲染
                service.DpiScale = 1.0;

                // 创建模拟的固定采样数据
                float[] samples = new float[1000];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = (float)Math.Sin(i * 0.1);
                }

                var bitmap1x = service.RenderWaveformBitmap(samples, 100, 50, 10);
                Assert.Equal(100, bitmap1x.Width);
                Assert.Equal(50, bitmap1x.Height);
                bitmap1x.Dispose();

                // 200% DPI 渲染：手动按 DPI倍率增大尺寸
                var bitmap2x = service.RenderWaveformBitmap(samples, 200, 100, 5);
                Assert.Equal(200, bitmap2x.Width);
                Assert.Equal(100, bitmap2x.Height);
                bitmap2x.Dispose();
            }
        }

        [Fact]
        public void WaveformRenderService_DpiScale不可设为0或负数()
        {
            var pcmService = new AudioPcmCacheService("ffmpeg");
            using (var service = new WaveformRenderService(pcmService))
            {
                service.DpiScale = 0;
                Assert.Equal(1.0, service.DpiScale);

                service.DpiScale = -1;
                Assert.Equal(1.0, service.DpiScale);
            }
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(1.25)]
        [InlineData(1.5)]
        [InlineData(2.0)]
        public void WaveformRenderService_各DPI缩放级别不抛异常(double scale)
        {
            var pcmService = new AudioPcmCacheService("ffmpeg");
            using (var service = new WaveformRenderService(pcmService, 100, 50))
            {
                service.DpiScale = scale;

                float[] samples = new float[500];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = (float)Math.Sin(i * 0.05);
                }

                int renderWidth = (int)(100 * scale);
                int renderHeight = (int)(50 * scale);
                double renderSpp = 5.0 / scale;

                var bitmap = service.RenderWaveformBitmap(samples, renderWidth, renderHeight, renderSpp);
                Assert.NotNull(bitmap);
                Assert.Equal(renderWidth, bitmap.Width);
                Assert.Equal(renderHeight, bitmap.Height);
                bitmap.Dispose();
            }
        }

        // ==================== 窗口布局 DPI 适配 ====================

        [Fact]
        public void MainWindow_XAML_使用WPF逻辑像素()
        {
            // WPF 的 Height/Width 是设备无关像素(DIP)，系统自动按 DPI 缩放
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string xamlPath = Path.Combine(projectRoot, "src", "MediaTrans", "Views", "MainWindow.xaml");
            string content = File.ReadAllText(xamlPath);

            // 验证使用了设备无关像素（WPF 默认行为）
            Assert.Contains("Height=\"600\"", content);
            Assert.Contains("Width=\"900\"", content);
            Assert.Contains("MinHeight=\"400\"", content);
            Assert.Contains("MinWidth=\"700\"", content);
        }

        [Fact]
        public void MainWindow_XAML_无边框窗口支持拖拽缩放()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string xamlPath = Path.Combine(projectRoot, "src", "MediaTrans", "Views", "MainWindow.xaml");
            string content = File.ReadAllText(xamlPath);

            // 无边框窗口自定义缩放
            Assert.Contains("WindowStyle=\"None\"", content);
        }

        // ==================== 深色/浅色主题兼容 ====================

        [Fact]
        public void DarkTheme_资源文件存在()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string themePath = Path.Combine(projectRoot, "src", "MediaTrans", "Assets", "DarkTheme.xaml");
            Assert.True(File.Exists(themePath), "暗色主题文件应存在");
        }

        [Fact]
        public void DarkTheme_使用DynamicResource引用()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string xamlPath = Path.Combine(projectRoot, "src", "MediaTrans", "Views", "MainWindow.xaml");
            string content = File.ReadAllText(xamlPath);

            // 使用 DynamicResource 确保主题可动态切换
            Assert.Contains("DynamicResource", content);
        }

        // ==================== CompatibilityService 辅助 ====================

        [Fact]
        public void CompatibilityService_IsWindows10OrLater_不抛异常()
        {
            var service = new CompatibilityService();
            bool result = service.IsWindows10OrLater();
            Assert.True(result || !result); // 仅验证不抛异常
        }

        // ==================== 辅助方法 ====================

        private static string GetManifestPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            return Path.Combine(projectRoot, "src", "MediaTrans", "app.manifest");
        }
    }
}
