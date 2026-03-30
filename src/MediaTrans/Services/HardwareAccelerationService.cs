using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaTrans.Models;

namespace MediaTrans.Services
{
    /// <summary>
    /// 硬件加速服务 — 探测 NVENC/QSV 可用性，提供硬件编码器映射
    /// 启动时自动探测，不可用时静默回退软编码
    /// </summary>
    public class HardwareAccelerationService
    {
        private readonly string _ffmpegPath;
        private readonly AppConfig _config;
        private readonly JobObject _jobObject;
        private readonly object _lock = new object();

        private bool _probed;
        private List<string> _availableEncoders;

        /// <summary>
        /// NVENC 是否可用
        /// </summary>
        public bool IsNvencAvailable { get; private set; }

        /// <summary>
        /// QSV 是否可用
        /// </summary>
        public bool IsQsvAvailable { get; private set; }

        /// <summary>
        /// 是否已完成探测
        /// </summary>
        public bool IsProbed
        {
            get { return _probed; }
        }

        /// <summary>
        /// 获取所有检测到的硬件编码器名称列表
        /// </summary>
        public List<string> AvailableHardwareEncoders
        {
            get
            {
                lock (_lock)
                {
                    if (_availableEncoders == null)
                    {
                        return new List<string>();
                    }
                    return new List<string>(_availableEncoders);
                }
            }
        }

        /// <summary>
        /// 软编码器到 NVENC 硬件编码器的映射
        /// </summary>
        private static readonly Dictionary<string, string> NvencMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "libx264", "h264_nvenc" },
            { "libx265", "hevc_nvenc" }
        };

        /// <summary>
        /// 软编码器到 QSV 硬件编码器的映射
        /// </summary>
        private static readonly Dictionary<string, string> QsvMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "libx264", "h264_qsv" },
            { "libx265", "hevc_qsv" }
        };

        /// <summary>
        /// 需要探测的硬件编码器列表
        /// </summary>
        private static readonly string[] HwEncodersToProbe = new string[]
        {
            "h264_nvenc", "hevc_nvenc",  // NVIDIA NVENC
            "h264_qsv", "hevc_qsv"      // Intel QSV
        };

        public HardwareAccelerationService(AppConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            _config = config;
            _ffmpegPath = config.FFmpegPath;
            _jobObject = new JobObject();
            _availableEncoders = new List<string>();
        }

        /// <summary>
        /// 用于测试的构造函数
        /// </summary>
        public HardwareAccelerationService(string ffmpegPath, AppConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            _ffmpegPath = ffmpegPath;
            _config = config;
            _jobObject = new JobObject();
            _availableEncoders = new List<string>();
        }

        /// <summary>
        /// 异步探测硬件编码器可用性
        /// 运行 ffmpeg -encoders 并解析输出
        /// </summary>
        public Task ProbeAsync()
        {
            return Task.Run(() => Probe());
        }

        /// <summary>
        /// 同步探测硬件编码器可用性
        /// </summary>
        public void Probe()
        {
            lock (_lock)
            {
                if (_probed)
                {
                    return;
                }

                var detectedEncoders = new List<string>();

                try
                {
                    string output = RunFFmpegEncoders();
                    if (!string.IsNullOrEmpty(output))
                    {
                        detectedEncoders = ParseEncoderList(output, HwEncodersToProbe);
                    }
                }
                catch (Exception)
                {
                    // 探测失败时静默忽略，所有硬件编码器标记为不可用
                }

                _availableEncoders = detectedEncoders;

                // 判断 NVENC 可用性（至少有一个 NVENC 编码器）
                IsNvencAvailable = _availableEncoders.Contains("h264_nvenc")
                    || _availableEncoders.Contains("hevc_nvenc");

                // 判断 QSV 可用性（至少有一个 QSV 编码器）
                IsQsvAvailable = _availableEncoders.Contains("h264_qsv")
                    || _availableEncoders.Contains("hevc_qsv");

                _probed = true;
            }
        }

        /// <summary>
        /// 解析 ffmpeg -encoders 输出，提取指定编码器
        /// </summary>
        public static List<string> ParseEncoderList(string ffmpegOutput, string[] encodersToFind)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(ffmpegOutput) || encodersToFind == null)
            {
                return found;
            }

            string[] lines = ffmpegOutput.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                foreach (string encoder in encodersToFind)
                {
                    // ffmpeg -encoders 输出格式: " V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)"
                    // 编码器名称在第二列（空格分隔），前面有标志位
                    if (trimmedLine.Contains(encoder))
                    {
                        if (!found.Contains(encoder))
                        {
                            found.Add(encoder);
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// 根据软件编码器获取对应的硬件编码器
        /// 如果硬件加速未启用或不可用，返回原编码器
        /// </summary>
        public string ResolveVideoCodec(string softwareCodec)
        {
            if (string.IsNullOrEmpty(softwareCodec))
            {
                return softwareCodec;
            }

            // 如果配置中未启用硬件加速，直接返回原编码器
            if (!_config.HardwareAccelerationEnabled)
            {
                return softwareCodec;
            }

            // 如果尚未探测，无法做硬件替换
            if (!_probed)
            {
                return softwareCodec;
            }

            string preferredType = _config.PreferredHardwareEncoder;

            // 根据偏好选择硬件编码器
            if (string.IsNullOrEmpty(preferredType)
                || string.Equals(preferredType, "auto", StringComparison.OrdinalIgnoreCase))
            {
                // 自动模式：优先 NVENC，其次 QSV
                string nvencCodec = TryGetHardwareCodec(softwareCodec, NvencMapping);
                if (nvencCodec != null)
                {
                    return nvencCodec;
                }

                string qsvCodec = TryGetHardwareCodec(softwareCodec, QsvMapping);
                if (qsvCodec != null)
                {
                    return qsvCodec;
                }
            }
            else if (string.Equals(preferredType, "nvenc", StringComparison.OrdinalIgnoreCase))
            {
                string nvencCodec = TryGetHardwareCodec(softwareCodec, NvencMapping);
                if (nvencCodec != null)
                {
                    return nvencCodec;
                }
            }
            else if (string.Equals(preferredType, "qsv", StringComparison.OrdinalIgnoreCase))
            {
                string qsvCodec = TryGetHardwareCodec(softwareCodec, QsvMapping);
                if (qsvCodec != null)
                {
                    return qsvCodec;
                }
            }

            // 没有可用的硬件编码器，回退软编码
            return softwareCodec;
        }

        /// <summary>
        /// 尝试从映射表中获取硬件编码器（仅在该编码器实际可用时返回）
        /// </summary>
        private string TryGetHardwareCodec(string softwareCodec, Dictionary<string, string> mapping)
        {
            string hwCodec;
            if (mapping.TryGetValue(softwareCodec, out hwCodec))
            {
                if (_availableEncoders.Contains(hwCodec))
                {
                    return hwCodec;
                }
            }
            return null;
        }

        /// <summary>
        /// 运行 ffmpeg -encoders 获取编码器列表
        /// </summary>
        private string RunFFmpegEncoders()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = "-encoders",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                _jobObject.AssignProcess(process.Handle);

                string output = process.StandardOutput.ReadToEnd();

                // 等待进程退出（设置超时防止挂死）
                if (!process.WaitForExit(10000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    {
                        // 忽略
                    }
                }

                return output;
            }
        }

        /// <summary>
        /// 手动设置探测结果（仅用于测试）
        /// </summary>
        public void SetProbeResultForTest(List<string> availableEncoders)
        {
            lock (_lock)
            {
                _availableEncoders = availableEncoders != null
                    ? new List<string>(availableEncoders)
                    : new List<string>();

                IsNvencAvailable = _availableEncoders.Contains("h264_nvenc")
                    || _availableEncoders.Contains("hevc_nvenc");

                IsQsvAvailable = _availableEncoders.Contains("h264_qsv")
                    || _availableEncoders.Contains("hevc_qsv");

                _probed = true;
            }
        }
    }
}
