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
        private readonly HardwareAccelerationService _hwAccelService;
        private readonly PaywallService _paywallService;

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
            { ".ts",   new FormatCodecMapping("libx264", "aac") },
            { ".mpg",  new FormatCodecMapping("mpeg2video", "mp2") },
            { ".mpeg", new FormatCodecMapping("mpeg2video", "mp2") },
            { ".mp3",  new FormatCodecMapping(null, "libmp3lame") },
            { ".wav",  new FormatCodecMapping(null, "pcm_s16le") },
            { ".flac", new FormatCodecMapping(null, "flac") },
            { ".aac",  new FormatCodecMapping(null, "aac") },
            { ".ogg",  new FormatCodecMapping(null, "libvorbis") },
            { ".wma",  new FormatCodecMapping(null, "wmav2") },
            { ".m4a",  new FormatCodecMapping(null, "aac") },
            { ".opus", new FormatCodecMapping(null, "libopus") }
        };

        /// <summary>
        /// 进度变化事件
        /// </summary>
        public event EventHandler<FFmpegProgressEventArgs> ProgressChanged;

        public ConversionService(FFmpegService ffmpegService, ConfigService configService)
        {
            _ffmpegService = ffmpegService;
            _configService = configService;
            _hwAccelService = null;
            _paywallService = null;
        }

        /// <summary>
        /// 创建带硬件加速服务的转换服务
        /// </summary>
        public ConversionService(FFmpegService ffmpegService, ConfigService configService, HardwareAccelerationService hwAccelService)
        {
            _ffmpegService = ffmpegService;
            _configService = configService;
            _hwAccelService = hwAccelService;
            _paywallService = null;
        }

        /// <summary>
        /// 创建带付费墙服务的转换服务
        /// </summary>
        public ConversionService(FFmpegService ffmpegService, ConfigService configService, PaywallService paywallService)
        {
            _ffmpegService = ffmpegService;
            _configService = configService;
            _hwAccelService = null;
            _paywallService = paywallService;
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
                        // 尝试硬件加速替换
                        string resolvedCodec = ResolveVideoCodec(preset.VideoCodec);
                        builder.VideoCodec(resolvedCodec);
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
                    // 若预设 codec 与目标容器不兼容，自动回退到格式默认 codec
                    string resolvedAudio = ResolveAudioCodec(preset.AudioCodec, targetFormat);
                    builder.AudioCodec(resolvedAudio);
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
                if (mapping == null)
                {
                    throw new ArgumentException(string.Format("不支持的目标格式：{0}，无法确定编解码器", targetFormat));
                }

                if (isAudioOnly)
                {
                    builder.NoVideo();
                }
                else if (!string.IsNullOrEmpty(mapping.VideoCodec))
                {
                    // 尝试硬件加速替换
                    string resolvedCodec = ResolveVideoCodec(mapping.VideoCodec);
                    builder.VideoCodec(resolvedCodec);
                }

                if (!string.IsNullOrEmpty(mapping.AudioCodec))
                {
                    builder.AudioCodec(mapping.AudioCodec);
                }
            }

            // 付费墙限制（时长截断 + 视频水印）
            if (_paywallService != null)
            {
                _paywallService.ApplyRestrictions(builder, source.DurationSeconds, !isAudioOnly);
            }

            // 自动多线程
            builder.Threads(0);
            builder.Overwrite(true);
            builder.Output(outputPath);

            return builder.Build();
        }

        /// <summary>
        /// 通过硬件加速服务解析视频编解码器
        /// 如果硬件加速可用且已启用，返回硬件编码器；否则返回原软件编码器
        /// </summary>
        public string ResolveVideoCodec(string softwareCodec)
        {
            if (_hwAccelService != null)
            {
                return _hwAccelService.ResolveVideoCodec(softwareCodec);
            }
            return softwareCodec;
        }

        /// <summary>
        /// 检查音频编解码器是否兼容目标容器格式。
        /// 若不兼容，应回退到该格式的默认编解码器，避免 FFmpeg 报错。
        /// </summary>
        public static bool IsAudioCodecCompatibleWithFormat(string audioCodec, string formatExtension)
        {
            if (string.IsNullOrEmpty(audioCodec) || string.IsNullOrEmpty(formatExtension))
            {
                return true;
            }
            string ext = formatExtension.ToLowerInvariant();
            string codec = audioCodec.ToLowerInvariant();

            // MP3 容器只接受 libmp3lame/mp3
            if (ext == ".mp3")
            {
                return codec == "libmp3lame" || codec == "mp3";
            }
            // WAV 只接受 PCM 系列
            if (ext == ".wav")
            {
                return codec.StartsWith("pcm_");
            }
            // FLAC 只接受 flac
            if (ext == ".flac")
            {
                return codec == "flac";
            }
            // OGG 只接受 libvorbis/libopus
            if (ext == ".ogg")
            {
                return codec == "libvorbis" || codec == "libopus";
            }
            // WMA 只接受 wmav2
            if (ext == ".wma")
            {
                return codec.Contains("wmav");
            }
            // Opus 只接受 libopus
            if (ext == ".opus")
            {
                return codec == "libopus" || codec == "opus";
            }
            // MPEG/MPG 只接受 mp2
            if (ext == ".mpg" || ext == ".mpeg")
            {
                return codec == "mp2" || codec == "mp3" || codec == "libmp3lame";
            }
            // 其他容器（mp4/mkv/mov/m4a/aac/ts 等）对 aac/libmp3lame 等均兼容，宽松处理
            return true;
        }

        /// <summary>
        /// 按需解析实际要使用的音频编解码器：
        /// 若预设 codec 与目标格式不兼容，回退到格式默认 codec。
        /// </summary>
        private static string ResolveAudioCodec(string presetAudioCodec, string targetFormat)
        {
            if (string.IsNullOrEmpty(presetAudioCodec))
            {
                return presetAudioCodec;
            }
            if (!IsAudioCodecCompatibleWithFormat(presetAudioCodec, targetFormat))
            {
                var mapping = GetDefaultCodecs(targetFormat);
                if (mapping != null && !string.IsNullOrEmpty(mapping.AudioCodec))
                {
                    return mapping.AudioCodec;
                }
            }
            return presetAudioCodec;
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

            string arguments = BuildConversionArguments(
                task.SourceFile, task.OutputPath, task.TargetFormat, task.Preset);

            var progressHandler = CreateProgressHandler(task, "转换中");
            _ffmpegService.ProgressChanged += progressHandler;

            return _ffmpegService.ExecuteAsync(arguments, task.SourceFile.DurationSeconds, cancellationToken)
                .ContinueWith(t =>
                {
                    _ffmpegService.ProgressChanged -= progressHandler;
                    return ApplyTaskResult(task, t, "转换完成", "转换失败");
                });
        }

        /// <summary>
        /// 提取音频（去除视频轨）
        /// </summary>
        public Task<FFmpegResult> ExtractAudioAsync(ConversionTask task, CancellationToken cancellationToken)
        {
            task.Status = ConversionStatus.Converting;
            task.StatusText = "正在提取音频...";

            string arguments = BuildExtractAudioArguments(
                task.SourceFile, task.OutputPath, task.TargetFormat, task.Preset);

            var progressHandler = CreateProgressHandler(task, "提取中");
            _ffmpegService.ProgressChanged += progressHandler;

            return _ffmpegService.ExecuteAsync(arguments, task.SourceFile.DurationSeconds, cancellationToken)
                .ContinueWith(t =>
                {
                    _ffmpegService.ProgressChanged -= progressHandler;
                    return ApplyTaskResult(task, t, "提取音频完成", "提取音频失败");
                });
        }

        /// <summary>
        /// 提取视频（去除音频轨）
        /// </summary>
        public Task<FFmpegResult> ExtractVideoAsync(ConversionTask task, CancellationToken cancellationToken)
        {
            task.Status = ConversionStatus.Converting;
            task.StatusText = "正在提取视频...";

            string arguments = BuildExtractVideoArguments(
                task.SourceFile, task.OutputPath, task.TargetFormat, task.Preset);

            var progressHandler = CreateProgressHandler(task, "提取中");
            _ffmpegService.ProgressChanged += progressHandler;

            return _ffmpegService.ExecuteAsync(arguments, task.SourceFile.DurationSeconds, cancellationToken)
                .ContinueWith(t =>
                {
                    _ffmpegService.ProgressChanged -= progressHandler;
                    return ApplyTaskResult(task, t, "提取视频完成", "提取视频失败");
                });
        }

        /// <summary>
        /// 创建进度事件处理器（驱动 task.Progress 和 ProgressChanged 事件）
        /// </summary>
        private EventHandler<FFmpegProgressEventArgs> CreateProgressHandler(ConversionTask task, string operationLabel)
        {
            return (s, e) =>
            {
                task.Progress = e.Percentage;
                task.StatusText = string.Format("{0} {1:F1}%", operationLabel, e.Percentage);
                var handler = ProgressChanged;
                if (handler != null)
                {
                    handler(this, e);
                }
            };
        }

        /// <summary>
        /// 将已完成任务的结果写回 ConversionTask 并返回 FFmpegResult
        /// </summary>
        private static FFmpegResult ApplyTaskResult(
            ConversionTask task, Task<FFmpegResult> completedTask,
            string successText, string failureText)
        {
            if (completedTask.IsFaulted)
            {
                task.Status = ConversionStatus.Failed;
                task.StatusText = failureText;
                task.ErrorMessage = completedTask.Exception != null
                    ? completedTask.Exception.InnerException.Message : "未知错误";
                return new FFmpegResult { Success = false, ErrorMessage = task.ErrorMessage };
            }

            var result = completedTask.Result;
            if (result.Cancelled)
            {
                task.Status = ConversionStatus.Cancelled;
                task.StatusText = "已取消";
            }
            else if (result.Success)
            {
                task.Status = ConversionStatus.Completed;
                task.Progress = 100;
                task.StatusText = successText;
            }
            else
            {
                task.Status = ConversionStatus.Failed;
                task.StatusText = failureText;
                task.ErrorMessage = result.ErrorMessage;
            }

            return result;
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
                // 若预设 codec 与目标容器不兼容，自动回退到格式默认 codec
                string resolvedAudio = ResolveAudioCodec(preset.AudioCodec, targetFormat);
                builder.AudioCodec(resolvedAudio);
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
                else
                {
                    throw new ArgumentException(string.Format("不支持的音频提取目标格式：{0}，无法确定音频编解码器", targetFormat));
                }
            }

            // 付费墙限制（音频提取仅时长截断）
            if (_paywallService != null)
            {
                _paywallService.ApplyRestrictions(builder, source.DurationSeconds, false);
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
                // 尝试硬件加速替换
                string resolvedCodec = ResolveVideoCodec(preset.VideoCodec);
                builder.VideoCodec(resolvedCodec);
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
                    // 尝试硬件加速替换
                    string resolvedCodec = ResolveVideoCodec(mapping.VideoCodec);
                    builder.VideoCodec(resolvedCodec);
                }
                else
                {
                    throw new ArgumentException(string.Format("不支持的视频提取目标格式：{0}，无法确定视频编解码器", targetFormat));
                }
            }

            // 付费墙限制（视频提取：时长截断 + 水印）
            if (_paywallService != null)
            {
                _paywallService.ApplyRestrictions(builder, source.DurationSeconds, true);
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
                ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".ts", ".mpg", ".mpeg",
                ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".opus"
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
