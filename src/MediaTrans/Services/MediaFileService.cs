using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaTrans.Models;
using Newtonsoft.Json.Linq;

namespace MediaTrans.Services
{
    /// <summary>
    /// 媒体文件服务：导入文件、通过 ffprobe 读取元信息
    /// </summary>
    public class MediaFileService
    {
        private readonly ConfigService _configService;
        private readonly JobObject _jobObject;

        // 支持的媒体文件扩展名
        private static readonly HashSet<string> _videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v",
            ".mpg", ".mpeg", ".3gp", ".ts", ".mts", ".m2ts", ".vob", ".ogv"
        };

        private static readonly HashSet<string> _audioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a",
            ".opus", ".ape", ".alac", ".aiff", ".ac3", ".dts"
        };

        /// <summary>
        /// 文件对话框过滤器字符串
        /// </summary>
        public static readonly string FileDialogFilter =
            "所有支持的媒体文件|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.m4v;*.mpg;*.mpeg;*.3gp;*.ts;*.mts;*.m2ts;*.vob;*.ogv;*.mp3;*.wav;*.flac;*.aac;*.ogg;*.wma;*.m4a;*.opus;*.ape;*.alac;*.aiff;*.ac3;*.dts|" +
            "视频文件|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.m4v;*.mpg;*.mpeg;*.3gp;*.ts;*.mts;*.m2ts;*.vob;*.ogv|" +
            "音频文件|*.mp3;*.wav;*.flac;*.aac;*.ogg;*.wma;*.m4a;*.opus;*.ape;*.alac;*.aiff;*.ac3;*.dts|" +
            "所有文件|*.*";

        /// <summary>
        /// 为 SaveFileDialog 构建保存格式过滤器字符串
        /// </summary>
        /// <param name="ext">目标扩展名（如 ".mp4"）</param>
        public static string BuildSaveFilter(string ext)
        {
            switch (ext.ToLowerInvariant())
            {
                case ".mp4":  return "MP4 视频文件|*.mp4|所有文件|*.*";
                case ".avi":  return "AVI 视频文件|*.avi|所有文件|*.*";
                case ".mkv":  return "MKV 视频文件|*.mkv|所有文件|*.*";
                case ".mov":  return "MOV 视频文件|*.mov|所有文件|*.*";
                case ".wmv":  return "WMV 视频文件|*.wmv|所有文件|*.*";
                case ".flv":  return "FLV 视频文件|*.flv|所有文件|*.*";
                case ".webm": return "WebM 视频文件|*.webm|所有文件|*.*";
                case ".ts":   return "TS 视频文件|*.ts|所有文件|*.*";
                case ".mpg":  return "MPG 视频文件|*.mpg|所有文件|*.*";
                case ".mpeg": return "MPEG 视频文件|*.mpeg|所有文件|*.*";
                case ".mp3":  return "MP3 音频文件|*.mp3|所有文件|*.*";
                case ".wav":  return "WAV 音频文件|*.wav|所有文件|*.*";
                case ".flac": return "FLAC 音频文件|*.flac|所有文件|*.*";
                case ".aac":  return "AAC 音频文件|*.aac|所有文件|*.*";
                case ".ogg":  return "OGG 音频文件|*.ogg|所有文件|*.*";
                case ".wma":  return "WMA 音频文件|*.wma|所有文件|*.*";
                case ".m4a":  return "M4A 音频文件|*.m4a|所有文件|*.*";
                case ".opus": return "Opus 音频文件|*.opus|所有文件|*.*";
                default:      return "媒体文件|*" + ext + "|所有文件|*.*";
            }
        }

        public MediaFileService(ConfigService configService)
        {
            _configService = configService;
            _jobObject = new JobObject();
        }

        /// <summary>
        /// 判断文件是否为支持的媒体格式
        /// </summary>
        public static bool IsSupportedMediaFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }
            string ext = Path.GetExtension(filePath);
            return _videoExtensions.Contains(ext) || _audioExtensions.Contains(ext);
        }

        /// <summary>
        /// 过滤并返回支持的媒体文件列表
        /// </summary>
        public static List<string> FilterSupportedFiles(IEnumerable<string> filePaths)
        {
            var result = new List<string>();
            foreach (var path in filePaths)
            {
                if (IsSupportedMediaFile(path) && File.Exists(path))
                {
                    result.Add(path);
                }
            }
            return result;
        }

        /// <summary>
        /// 使用 ffprobe 异步读取媒体文件元信息
        /// </summary>
        public Task<MediaFileInfo> GetMediaInfoAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                return GetMediaInfo(filePath, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// 使用 ffprobe 读取媒体文件元信息（同步方法，应在后台线程调用）
        /// </summary>
        public MediaFileInfo GetMediaInfo(string filePath, CancellationToken cancellationToken)
        {
            var info = new MediaFileInfo();
            info.FilePath = filePath;
            info.FileName = Path.GetFileName(filePath);

            // 获取文件大小
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists)
            {
                info.FileSize = fileInfo.Length;
            }

            // 使用 ffprobe 获取 JSON 格式的元信息
            try
            {
                var config = _configService.Load();
                string ffprobePath = ResolveFfprobePath(config.FFprobePath);

                // 构建 ffprobe 参数：输出 JSON 格式，包含流和格式信息
                string arguments = string.Format(
                    "-v quiet -print_format json -show_format -show_streams \"{0}\"",
                    filePath);

                string jsonOutput = RunFFprobe(ffprobePath, arguments, cancellationToken);

                if (!string.IsNullOrEmpty(jsonOutput))
                {
                    ParseFFprobeJson(jsonOutput, info);
                }
            }
            catch (FileNotFoundException)
            {
                // ffprobe 未找到，基于文件扩展名推断基本属性
                InferMediaTypeFromExtension(info);
            }

            info.MetadataLoaded = true;
            return info;
        }

        /// <summary>
        /// 当 ffprobe 不可用时，根据文件扩展名推断基本媒体类型
        /// </summary>
        private static void InferMediaTypeFromExtension(MediaFileInfo info)
        {
            if (string.IsNullOrEmpty(info.FilePath)) return;
            string ext = Path.GetExtension(info.FilePath);
            if (string.IsNullOrEmpty(ext)) return;

            if (_audioExtensions.Contains(ext))
            {
                info.HasAudio = true;
                info.Format = ext.TrimStart('.').ToLowerInvariant();
            }
            else if (_videoExtensions.Contains(ext))
            {
                info.HasVideo = true;
                info.HasAudio = true;
                info.Format = ext.TrimStart('.').ToLowerInvariant();
            }
        }

        /// <summary>
        /// 解析 ffprobe 可执行文件路径：
        /// 1. 支持绝对路径；
        /// 2. 相对路径默认相对于应用程序目录；
        /// 3. 缺失时抛出可读性更高的异常信息。
        /// </summary>
        private string ResolveFfprobePath(string configuredPath)
        {
            string rawPath = configuredPath;
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                rawPath = @"lib\ffmpeg\ffprobe.exe";
            }

            // 优先尝试：绝对路径、应用目录相对路径、当前工作目录相对路径
            var candidates = new List<string>();
            if (Path.IsPathRooted(rawPath))
            {
                candidates.Add(rawPath);
            }
            else
            {
                candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rawPath));
                candidates.Add(Path.Combine(Environment.CurrentDirectory, rawPath));

                // 开发态回退：从 bin 目录向上查找项目根目录的 lib\ffmpeg
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
                {
                    candidates.Add(Path.Combine(dir, "lib", "ffmpeg", "ffprobe.exe"));
                    try
                    {
                        dir = Directory.GetParent(dir) != null ? Directory.GetParent(dir).FullName : null;
                    }
                    catch
                    {
                        dir = null;
                    }
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(candidate);
                }
                catch
                {
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            // 尝试在系统 PATH 中查找 ffprobe
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string[] pathDirs = pathEnv.Split(';');
                for (int i = 0; i < pathDirs.Length; i++)
                {
                    string dir2 = pathDirs[i];
                    if (string.IsNullOrWhiteSpace(dir2)) continue;
                    try
                    {
                        string pathCandidate = Path.Combine(dir2.Trim(), "ffprobe.exe");
                        if (File.Exists(pathCandidate))
                        {
                            return Path.GetFullPath(pathCandidate);
                        }
                    }
                    catch { }
                }
            }

            // 提示用户所有尝试过的路径，便于定位问题
            string triedPaths = string.Join("; ", candidates.ToArray());
            throw new FileNotFoundException(
                string.Format("未找到 ffprobe 可执行文件。已尝试：{0}。请检查 Config/AppConfig.json 中 FFprobePath，或将 ffprobe.exe 放到 lib\\ffmpeg\\ 目录。", triedPaths));
        }

        /// <summary>
        /// 运行 ffprobe 进程并获取输出
        /// </summary>
        private string RunFFprobe(string ffprobePath, string arguments, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                // 绑定到 Job Object，主进程退出时自动终止
                _jobObject.AssignProcess(process.Handle);

                string output = "";
                string error = "";

                // 异步读取输出
                var outputTask = Task.Run(() =>
                {
                    output = process.StandardOutput.ReadToEnd();
                });
                var errorTask = Task.Run(() =>
                {
                    error = process.StandardError.ReadToEnd();
                });

                // 等待进程完成或取消
                while (!process.HasExited)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        try { process.Kill(); }
                        catch (InvalidOperationException) { }
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    process.WaitForExit(100);
                }

                outputTask.Wait();
                errorTask.Wait();

                return output;
            }
        }

        /// <summary>
        /// 解析 ffprobe 的 JSON 输出
        /// </summary>
        public static void ParseFFprobeJson(string json, MediaFileInfo info)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch
            {
                return;
            }

            // 解析 format 信息
            var format = root["format"];
            if (format != null)
            {
                info.Format = GetStringValue(format, "format_name");

                // 时长
                string durationStr = GetStringValue(format, "duration");
                double duration;
                if (double.TryParse(durationStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out duration))
                {
                    info.DurationSeconds = duration;
                }
            }

            // 解析流信息
            var streams = root["streams"] as JArray;
            if (streams != null)
            {
                foreach (var stream in streams)
                {
                    string codecType = GetStringValue(stream, "codec_type");

                    if (string.Equals(codecType, "video", StringComparison.OrdinalIgnoreCase) && !info.HasVideo)
                    {
                        info.HasVideo = true;
                        info.VideoCodec = GetStringValue(stream, "codec_name");

                        int width;
                        if (int.TryParse(GetStringValue(stream, "width"), out width))
                        {
                            info.Width = width;
                        }

                        int height;
                        if (int.TryParse(GetStringValue(stream, "height"), out height))
                        {
                            info.Height = height;
                        }

                        // 帧率：r_frame_rate 格式如 "30000/1001"
                        string frameRateStr = GetStringValue(stream, "r_frame_rate");
                        info.FrameRate = ParseFrameRate(frameRateStr);

                        // 视频比特率
                        string bitRateStr = GetStringValue(stream, "bit_rate");
                        long videoBitrate;
                        if (long.TryParse(bitRateStr, out videoBitrate))
                        {
                            info.VideoBitrate = videoBitrate;
                        }
                    }
                    else if (string.Equals(codecType, "audio", StringComparison.OrdinalIgnoreCase) && !info.HasAudio)
                    {
                        info.HasAudio = true;
                        info.AudioCodec = GetStringValue(stream, "codec_name");

                        // 采样率
                        string sampleRateStr = GetStringValue(stream, "sample_rate");
                        int sampleRate;
                        if (int.TryParse(sampleRateStr, out sampleRate))
                        {
                            info.AudioSampleRate = sampleRate;
                        }

                        // 声道数
                        string channelsStr = GetStringValue(stream, "channels");
                        int channels;
                        if (int.TryParse(channelsStr, out channels))
                        {
                            info.AudioChannels = channels;
                        }

                        // 音频比特率
                        string audioBitRateStr = GetStringValue(stream, "bit_rate");
                        long audioBitrate;
                        if (long.TryParse(audioBitRateStr, out audioBitrate))
                        {
                            info.AudioBitrate = audioBitrate;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 解析帧率字符串（如 "30000/1001" 或 "25"）
        /// </summary>
        public static double ParseFrameRate(string frameRateStr)
        {
            if (string.IsNullOrEmpty(frameRateStr))
            {
                return 0;
            }

            // 尝试 "分子/分母" 格式
            string[] parts = frameRateStr.Split('/');
            if (parts.Length == 2)
            {
                double numerator, denominator;
                if (double.TryParse(parts[0], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out numerator) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out denominator) &&
                    denominator > 0)
                {
                    return numerator / denominator;
                }
            }

            // 尝试直接解析为数字
            double result;
            if (double.TryParse(frameRateStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return 0;
        }

        /// <summary>
        /// 安全获取 JToken 的字符串值
        /// </summary>
        private static string GetStringValue(JToken token, string propertyName)
        {
            var prop = token[propertyName];
            if (prop != null)
            {
                return prop.ToString();
            }
            return "";
        }
    }
}
