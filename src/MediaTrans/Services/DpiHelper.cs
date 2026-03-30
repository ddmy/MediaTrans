using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MediaTrans.Services
{
    /// <summary>
    /// DPI 缩放辅助服务 — 提供系统 DPI 检测与缩放计算
    /// 兼容 Win7 SP1（System DPI）和 Win10+（Per-Monitor DPI）
    /// </summary>
    public static class DpiHelper
    {
        // 标准 DPI 基准值
        private const double StandardDpi = 96.0;

        // 缓存系统 DPI
        private static double _systemDpiX;
        private static double _systemDpiY;
        private static bool _initialized;

        /// <summary>
        /// 系统水平 DPI 缩放因子（1.0 = 100%，1.25 = 125%，1.5 = 150%，2.0 = 200%）
        /// </summary>
        public static double ScaleX
        {
            get
            {
                EnsureInitialized();
                return _systemDpiX / StandardDpi;
            }
        }

        /// <summary>
        /// 系统垂直 DPI 缩放因子
        /// </summary>
        public static double ScaleY
        {
            get
            {
                EnsureInitialized();
                return _systemDpiY / StandardDpi;
            }
        }

        /// <summary>
        /// 系统水平 DPI 值
        /// </summary>
        public static double DpiX
        {
            get
            {
                EnsureInitialized();
                return _systemDpiX;
            }
        }

        /// <summary>
        /// 系统垂直 DPI 值
        /// </summary>
        public static double DpiY
        {
            get
            {
                EnsureInitialized();
                return _systemDpiY;
            }
        }

        /// <summary>
        /// 将设备无关像素（WPF 逻辑像素）转换为物理像素
        /// </summary>
        /// <param name="logicalPixels">WPF 逻辑像素值</param>
        /// <returns>物理像素值</returns>
        public static int LogicalToPhysicalX(double logicalPixels)
        {
            return (int)Math.Round(logicalPixels * ScaleX);
        }

        /// <summary>
        /// 将设备无关像素（WPF 逻辑像素）转换为物理像素
        /// </summary>
        /// <param name="logicalPixels">WPF 逻辑像素值</param>
        /// <returns>物理像素值</returns>
        public static int LogicalToPhysicalY(double logicalPixels)
        {
            return (int)Math.Round(logicalPixels * ScaleY);
        }

        /// <summary>
        /// 将物理像素转换为 WPF 逻辑像素
        /// </summary>
        /// <param name="physicalPixels">物理像素值</param>
        /// <returns>WPF 逻辑像素值</returns>
        public static double PhysicalToLogicalX(int physicalPixels)
        {
            return physicalPixels / ScaleX;
        }

        /// <summary>
        /// 将物理像素转换为 WPF 逻辑像素
        /// </summary>
        /// <param name="physicalPixels">物理像素值</param>
        /// <returns>WPF 逻辑像素值</returns>
        public static double PhysicalToLogicalY(int physicalPixels)
        {
            return physicalPixels / ScaleY;
        }

        /// <summary>
        /// 获取指定窗口所在屏幕的 DPI 缩放因子（Per-Monitor DPI 感知）
        /// 在 Win7 上回退到系统 DPI
        /// </summary>
        /// <param name="window">目标窗口</param>
        /// <returns>缩放因子（1.0 = 96 DPI）</returns>
        public static double GetWindowScaleFactor(Window window)
        {
            if (window == null)
            {
                return ScaleX;
            }

            try
            {
                // 尝试使用 WPF 内置方式获取窗口 DPI（.NET 4.6.2+）
                // 在 .NET 4.5.2 上使用 PresentationSource 方式
                var source = PresentationSource.FromVisual(window);
                if (source != null && source.CompositionTarget != null)
                {
                    return source.CompositionTarget.TransformToDevice.M11;
                }
            }
            catch
            {
                // 忽略异常，使用系统 DPI 回退
            }

            return ScaleX;
        }

        /// <summary>
        /// 获取用于 SkiaSharp 渲染的物理像素尺寸
        /// 确保在高 DPI 下波形/视频预览不模糊
        /// </summary>
        /// <param name="logicalWidth">WPF 逻辑宽度</param>
        /// <param name="logicalHeight">WPF 逻辑高度</param>
        /// <param name="physicalWidth">输出：物理像素宽度</param>
        /// <param name="physicalHeight">输出：物理像素高度</param>
        public static void GetPhysicalSize(double logicalWidth, double logicalHeight,
            out int physicalWidth, out int physicalHeight)
        {
            physicalWidth = LogicalToPhysicalX(logicalWidth);
            physicalHeight = LogicalToPhysicalY(logicalHeight);

            // 确保最小为 1 像素
            if (physicalWidth < 1) physicalWidth = 1;
            if (physicalHeight < 1) physicalHeight = 1;
        }

        /// <summary>
        /// 重置缓存（供测试使用）
        /// </summary>
        internal static void Reset()
        {
            _initialized = false;
            _systemDpiX = 0;
            _systemDpiY = 0;
        }

        /// <summary>
        /// 设置自定义 DPI 值（供测试使用）
        /// </summary>
        /// <param name="dpiX">水平 DPI</param>
        /// <param name="dpiY">垂直 DPI</param>
        internal static void SetDpi(double dpiX, double dpiY)
        {
            _systemDpiX = dpiX;
            _systemDpiY = dpiY;
            _initialized = true;
        }

        /// <summary>
        /// 初始化系统 DPI 值
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                // 使用 Win32 API 获取系统 DPI（兼容 Win7+）
                IntPtr hdc = GetDC(IntPtr.Zero);
                if (hdc != IntPtr.Zero)
                {
                    try
                    {
                        _systemDpiX = GetDeviceCaps(hdc, LOGPIXELSX);
                        _systemDpiY = GetDeviceCaps(hdc, LOGPIXELSY);
                    }
                    finally
                    {
                        ReleaseDC(IntPtr.Zero, hdc);
                    }
                }
            }
            catch
            {
                // P/Invoke 失败时使用默认值
            }

            // 确保有效值
            if (_systemDpiX <= 0) _systemDpiX = StandardDpi;
            if (_systemDpiY <= 0) _systemDpiY = StandardDpi;

            _initialized = true;
        }

        // Win32 常量
        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;

        // Win7+ 兼容的 P/Invoke 声明
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
    }
}
