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
            var config = _configService.Load();
            string ffprobePath = config.FFprobePath;

            // 构建 ffprobe 参数：输出 JSON 格式，包含流和格式信息
            string arguments = string.Format(
                "-v quiet -print_format json -show_format -show_streams \"{0}\"",
                filePath);

            string jsonOutput = RunFFprobe(ffprobePath, arguments, cancellationToken);

            if (!string.IsNullOrEmpty(jsonOutput))
            {
                ParseFFprobeJson(jsonOutput, info);
            }

            info.MetadataLoaded = true;
            return info;
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
