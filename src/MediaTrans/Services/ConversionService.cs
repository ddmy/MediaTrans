using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaTrans.Models;

namespace MediaTrans.Services
{
    /// <summary>
    /// 格式转换服务 — 根据预设或自定义参数构建 FFmpeg 命令并执行转换
    /// </summary>
    public class ConversionService
    {
        private readonly FFmpegService _ffmpegService;
        private readonly ConfigService _configService;

        /// <summary>
        /// 格式与默认编解码器映射表
        /// </summary>
        private static readonly Dictionary<string, FormatCodecMapping> _formatMappings =
            new Dictionary<string, FormatCodecMapping>(StringComparer.OrdinalIgnoreCase)
        {
            { ".mp4",  new FormatCodecMapping("libx264", "aac") },
            { ".avi",  new FormatCodecMapping("libx264", "mp3lame") },
            { ".mkv",  new FormatCodecMapping("libx264", "aac") },
            { ".mov",  new FormatCodecMapping("libx264", "aac") },
            { ".wmv",  new FormatCodecMapping("wmv2", "wmav2") },
            { ".flv",  new FormatCodecMapping("flv1", "mp3lame") },
            { ".webm", new FormatCodecMapping("libvpx", "libvorbis") },
            { ".mp3",  new FormatCodecMapping(null, "libmp3lame") },
            { ".wav",  new FormatCodecMapping(null, "pcm_s16le") },
            { ".flac", new FormatCodecMapping(null, "flac") },
            { ".aac",  new FormatCodecMapping(null, "aac") },
            { ".ogg",  new FormatCodecMapping(null, "libvorbis") },
            { ".wma",  new FormatCodecMapping(null, "wmav2") },
            { ".m4a",  new FormatCodecMapping(null, "aac") }
        };

        /// <summary>
        /// 进度变化事件
        /// </summary>
        public event EventHandler<FFmpegProgressEventArgs> ProgressChanged;

        public ConversionService(FFmpegService ffmpegService, ConfigService configService)
        {
            _ffmpegService = ffmpegService;
            _configService = configService;
        }

        /// <summary>
        /// 获取指定格式的默认编解码器映射
        /// </summary>
        public static FormatCodecMapping GetDefaultCodecs(string targetExtension)
        {
            FormatCodecMapping mapping;
            if (_formatMappings.TryGetValue(targetExtension, out mapping))
            {
                return mapping;
            }
            return null;
        }

        /// <summary>
        /// 判断目标格式是否为纯音频格式
        /// </summary>
        public static bool IsAudioOnlyFormat(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }
            string ext = extension.ToLowerInvariant();
            return ext == ".mp3" || ext == ".wav" || ext == ".flac" ||
                   ext == ".aac" || ext == ".ogg" || ext == ".wma" || ext == ".m4a" ||
                   ext == ".opus" || ext == ".ape" || ext == ".aiff" || ext == ".ac3" || ext == ".dts";
        }

        /// <summary>
        /// 构建转换用的 FFmpeg 命令参数
        /// </summary>
        public string BuildConversionArguments(MediaFileInfo source, string outputPath, string targetFormat, ConversionPreset preset)
        {
            var builder = new FFmpegCommandBuilder();
            builder.Input(source.FilePath);

            bool isAudioOnly = IsAudioOnlyFormat(targetFormat);

            if (preset != null)
            {
                // 使用预设参数
                if (isAudioOnly)
                {
                    builder.NoVideo();
                }
                else
                {
                    if (!string.IsNullOrEmpty(preset.VideoCodec))
                    {
                        builder.VideoCodec(preset.VideoCodec);
                    }
                    if (preset.Width > 0 && preset.Height > 0)
                    {
                        builder.Resolution(preset.Width, preset.Height);
                    }
                    if (!string.IsNullOrEmpty(preset.VideoBitrate))
                    {
                        builder.VideoBitrate(preset.VideoBitrate);
                    }
                    if (preset.FrameRate > 0)
                    {
                        builder.FrameRate(preset.FrameRate);
                    }
                }

                if (!string.IsNullOrEmpty(preset.AudioCodec))
                {
                    builder.AudioCodec(preset.AudioCodec);
                }
                if (!string.IsNullOrEmpty(preset.AudioBitrate))
                {
                    builder.AudioBitrate(preset.AudioBitrate);
                }
            }
            else
            {
                // 无预设时使用格式默认编解码器
                var mapping = GetDefaultCodecs(targetFormat);
                if (mapping != null)
                {
                    if (isAudioOnly)
                    {
                        builder.NoVideo();
                    }
                    else if (!string.IsNullOrEmpty(mapping.VideoCodec))
                    {
                        builder.VideoCodec(mapping.VideoCodec);
                    }

                    if (!string.IsNullOrEmpty(mapping.AudioCodec))
                    {
                        builder.AudioCodec(mapping.AudioCodec);
                    }
                }
            }

            // 自动多线程
            builder.Threads(0);
            builder.Overwrite(true);
            builder.Output(outputPath);

            return builder.Build();
        }

        /// <summary>
        /// 生成输出文件路径（避免文件名冲突）
        /// </summary>
        public string GenerateOutputPath(string sourceFilePath, string targetFormat)
        {
            var config = _configService.Load();
            string outputDir = config.DefaultOutputDir;

            // 如果输出目录为空，使用源文件所在目录
            if (string.IsNullOrEmpty(outputDir))
            {
                outputDir = Path.GetDirectoryName(sourceFilePath);
            }

            // 确保输出目录存在
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
            string outputPath = Path.Combine(outputDir, baseName + targetFormat);

            // 避免覆盖已存在的文件（添加序号）
            int counter = 1;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(outputDir, string.Format("{0}_{1}{2}", baseName, counter, targetFormat));
                counter++;
            }

            return outputPath;
        }

        /// <summary>
        /// 执行格式转换
        /// </summary>
        public Task<FFmpegResult> ConvertAsync(ConversionTask task, CancellationToken cancellationToken)
        {
            task.Status = ConversionStatus.Converting;
            task.StatusText = "正在转换...";

            // 构建命令参数
            string arguments = BuildConversionArguments(
                task.SourceFile, task.OutputPath, task.TargetFormat, task.Preset);

            // 监听进度
            EventHandler<FFmpegProgressEventArgs> progressHandler = null;
            progressHandler = (s, e) =>
            {
                task.Progress = e.Percentage;
                task.StatusText = string.Format("转换中 {0:F1}%", e.Percentage);
                var handler = ProgressChanged;
                if (handler != null)
                {
                    handler(this, e);
                }
            };

            _ffmpegService.ProgressChanged += progressHandler;

            return _ffmpegService.ExecuteAsync(
                arguments,
                task.SourceFile.DurationSeconds,
                cancellationToken).ContinueWith(t =>
            {
                _ffmpegService.ProgressChanged -= progressHandler;

                if (t.IsFaulted)
                {
                    task.Status = ConversionStatus.Failed;
                    task.StatusText = "转换失败";
                    task.ErrorMessage = t.Exception != null ? t.Exception.InnerException.Message : "未知错误";
                    return new FFmpegResult
                    {
                        Success = false,
                        ErrorMessage = task.ErrorMessage
                    };
                }

                var result = t.Result;
                if (result.Cancelled)
                {
                    task.Status = ConversionStatus.Cancelled;
                    task.StatusText = "已取消";
                }
                else if (result.Success)
                {
                    task.Status = ConversionStatus.Completed;
                    task.Progress = 100;
                    task.StatusText = "转换完成";
                }
                else
                {
                    task.Status = ConversionStatus.Failed;
                    task.StatusText = "转换失败";
                    task.ErrorMessage = result.ErrorMessage;
                }

                return result;
            });
        }

        /// <summary>
        /// 构建仅提取音频的 FFmpeg 命令参数（-vn 去视频轨）
        /// </summary>
        public string BuildExtractAudioArguments(MediaFileInfo source, string outputPath, string targetFormat, ConversionPreset preset)
        {
            var builder = new FFmpegCommandBuilder();
            builder.Input(source.FilePath);
            builder.NoVideo();

            if (preset != null && !string.IsNullOrEmpty(preset.AudioCodec))
            {
                builder.AudioCodec(preset.AudioCodec);
                if (!string.IsNullOrEmpty(preset.AudioBitrate))
                {
                    builder.AudioBitrate(preset.AudioBitrate);
                }
            }
            else
            {
                // 使用目标格式的默认音频编解码器
                var mapping = GetDefaultCodecs(targetFormat);
                if (mapping != null && !string.IsNullOrEmpty(mapping.AudioCodec))
                {
                    builder.AudioCodec(mapping.AudioCodec);
                }
            }

            builder.Threads(0);
            builder.Overwrite(true);
            builder.Output(outputPath);

            return builder.Build();
        }

        /// <summary>
        /// 构建仅保留视频的 FFmpeg 命令参数（-an 去音频轨）
        /// </summary>
        public string BuildExtractVideoArguments(MediaFileInfo source, string outputPath, string targetFormat, ConversionPreset preset)
        {
            var builder = new FFmpegCommandBuilder();
            builder.Input(source.FilePath);
            builder.NoAudio();

            if (preset != null && !string.IsNullOrEmpty(preset.VideoCodec))
            {
                builder.VideoCodec(preset.VideoCodec);
                if (!string.IsNullOrEmpty(preset.VideoBitrate))
                {
                    builder.VideoBitrate(preset.VideoBitrate);
                }
                if (preset.Width > 0 && preset.Height > 0)
                {
                    builder.Resolution(preset.Width, preset.Height);
                }
                if (preset.FrameRate > 0)
                {
                    builder.FrameRate(preset.FrameRate);
                }
            }
            else
            {
                // 使用目标格式的默认视频编解码器
                var mapping = GetDefaultCodecs(targetFormat);
                if (mapping != null && !string.IsNullOrEmpty(mapping.VideoCodec))
                {
                    builder.VideoCodec(mapping.VideoCodec);
                }
            }

            builder.Threads(0);
            builder.Overwrite(true);
            builder.Output(outputPath);

            return builder.Build();
        }

        /// <summary>
        /// 获取所有支持的输出格式
        /// </summary>
        public static List<string> GetSupportedOutputFormats()
        {
            return new List<string>
            {
                ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm",
                ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"
            };
        }
    }

    /// <summary>
    /// 格式与编解码器映射
    /// </summary>
    public class FormatCodecMapping
    {
        /// <summary>
        /// 视频编解码器（纯音频格式为 null）
        /// </summary>
        public string VideoCodec { get; private set; }

        /// <summary>
        /// 音频编解码器
        /// </summary>
        public string AudioCodec { get; private set; }

        public FormatCodecMapping(string videoCodec, string audioCodec)
        {
            VideoCodec = videoCodec;
            AudioCodec = audioCodec;
        }
    }
}
