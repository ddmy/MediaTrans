using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MediaTrans.Services
{
    /// <summary>
    /// 系统兼容性检测服务 — 检查 OS 版本、.NET 版本、依赖项可用性
    /// 兼容 Win7 SP1 及以上系统
    /// </summary>
    public class CompatibilityService
    {
        /// <summary>
        /// 获取当前 Windows 版本信息
        /// </summary>
        /// <returns>操作系统版本</returns>
        public Version GetWindowsVersion()
        {
            return Environment.OSVersion.Version;
        }

        /// <summary>
        /// 检查是否为 Windows 7 SP1 或更高版本
        /// Win7 SP1 = NT 6.1，Build 7601
        /// </summary>
        public bool IsWindows7OrLater()
        {
            var version = Environment.OSVersion.Version;
            // NT 6.1 = Win7
            return version.Major > 6 || (version.Major == 6 && version.Minor >= 1);
        }

        /// <summary>
        /// 检查是否为 Windows 10 或更高版本
        /// Win10 = NT 10.0
        /// </summary>
        public bool IsWindows10OrLater()
        {
            return Environment.OSVersion.Version.Major >= 10;
        }

        /// <summary>
        /// 检查 .NET Framework 4.5.2 或更高版本是否已安装
        /// Release 值 >= 379893 表示 .NET 4.5.2+
        /// </summary>
        public bool IsNetFramework452OrLater()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    if (key != null)
                    {
                        object releaseValue = key.GetValue("Release");
                        if (releaseValue != null)
                        {
                            int release = Convert.ToInt32(releaseValue);
                            return release >= 379893; // .NET 4.5.2
                        }
                    }
                }
            }
            catch
            {
                // 注册表访问失败
            }
            return false;
        }

        /// <summary>
        /// 检查 SkiaSharp 原生库是否可加载
        /// </summary>
        public bool IsSkiaSharpAvailable()
        {
            try
            {
                // 检查 libSkiaSharp.dll 是否存在于应用目录
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string libPath = Path.Combine(baseDir, "libSkiaSharp.dll");

                // 也检查 x86/x64 子目录
                string x86Path = Path.Combine(baseDir, "x86", "libSkiaSharp.dll");
                string x64Path = Path.Combine(baseDir, "x64", "libSkiaSharp.dll");

                return File.Exists(libPath) || File.Exists(x86Path) || File.Exists(x64Path);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查 FFmpeg 可执行文件是否可用
        /// </summary>
        /// <param name="ffmpegPath">FFmpeg 路径</param>
        public bool IsFFmpegAvailable(string ffmpegPath)
        {
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                return false;
            }

            try
            {
                return File.Exists(ffmpegPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查 WMI 服务是否可用（机器码采集依赖）
        /// </summary>
        public bool IsWmiAvailable()
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        // 能查到数据说明 WMI 可用
                        return true;
                    }
                }
            }
            catch
            {
                // WMI 服务不可用
            }
            return false;
        }

        /// <summary>
        /// 检查 NAudio 所需的音频子系统是否可用
        /// Win7+ 支持 WASAPI 和 WaveOut
        /// </summary>
        public bool IsAudioSubsystemAvailable()
        {
            try
            {
                // WaveOut 在所有 Windows 版本上都可用
                int deviceCount = waveOutGetNumDevs();
                return deviceCount > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查当前系统是否为 64 位
        /// </summary>
        public bool Is64BitOperatingSystem()
        {
            return Environment.Is64BitOperatingSystem;
        }

        /// <summary>
        /// 获取当前进程是否以 64 位运行
        /// </summary>
        public bool Is64BitProcess()
        {
            return Environment.Is64BitProcess;
        }

        /// <summary>
        /// 获取系统简要信息（用于日志记录）
        /// </summary>
        public string GetSystemSummary()
        {
            var version = Environment.OSVersion;
            return string.Format("OS: {0} {1} | .NET CLR: {2} | {3}位进程 | {4}位系统",
                version.Platform,
                version.VersionString,
                Environment.Version,
                Is64BitProcess() ? "64" : "32",
                Is64BitOperatingSystem() ? "64" : "32");
        }

        // Win32 API — 检查波形音频设备数量（兼容所有 Windows 版本）
        [DllImport("winmm.dll")]
        private static extern int waveOutGetNumDevs();
    }
}
