using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MediaTrans.Models;

namespace MediaTrans.Services
{
    /// <summary>
    /// 编辑导出参数
    /// </summary>
    public class EditExportParams
    {
        /// <summary>
        /// 源文件路径
        /// </summary>
        public string SourceFilePath { get; set; }

        /// <summary>
        /// 输出文件路径
        /// </summary>
        public string OutputFilePath { get; set; }

        /// <summary>
        /// 目标格式扩展名（如 ".mp4"）
        /// </summary>
        public string TargetFormat { get; set; }

        /// <summary>
        /// 裁剪起始时间（秒），null 表示不裁剪
        /// </summary>
        public double? TrimStartSeconds { get; set; }

        /// <summary>
        /// 裁剪持续时长（秒），null 表示到文件末尾
        /// </summary>
        public double? TrimDurationSeconds { get; set; }

        /// <summary>
        /// 增益值（dB），0 表示不调整
        /// </summary>
        public double GainDb { get; set; }

        /// <summary>
        /// 多段拼接的片段列表，null 表示单文件模式
        /// </summary>
        public List<ClipSegment> Segments { get; set; }

        /// <summary>
        /// 转换预设参数（可为 null 使用默认值）
        /// </summary>
        public ConversionPreset Preset { get; set; }
    }

    /// <summary>
    /// 拼接片段信息
    /// </summary>
    public class ClipSegment
    {
        /// <summary>
        /// 源文件路径
        /// </summary>
        public string SourceFilePath { get; set; }

        /// <summary>
        /// 片段在源文件中的起始时间（秒）
        /// </summary>
        public double StartSeconds { get; set; }

        /// <summary>
        /// 片段持续时长（秒）
        /// </summary>
        public double DurationSeconds { get; set; }
    }

    /// <summary>
    /// 编辑导出服务 — 裁剪/拼接/增益结果通过 FFmpeg 导出
    /// </summary>
    public class EditExportService
    {
        private readonly PaywallService _paywallService;

        public EditExportService()
        {
            _paywallService = null;
        }

        public EditExportService(PaywallService paywallService)
        {
            _paywallService = paywallService;
        }

        /// <summary>
        /// 校验导出参数合法性
        /// </summary>
        /// <param name="exportParams">导出参数</param>
        /// <returns>校验错误信息列表，空列表表示合法</returns>
        public List<string> ValidateParams(EditExportParams exportParams)
        {
            var errors = new List<string>();

            if (exportParams == null)
            {
                errors.Add("导出参数不能为空");
                return errors;
            }

            if (string.IsNullOrEmpty(exportParams.OutputFilePath))
            {
                errors.Add("输出路径不能为空");
            }

            if (string.IsNullOrEmpty(exportParams.TargetFormat))
            {
                errors.Add("目标格式不能为空");
            }

            // 单文件模式校验
            if (exportParams.Segments == null || exportParams.Segments.Count == 0)
            {
                if (string.IsNullOrEmpty(exportParams.SourceFilePath))
                {
                    errors.Add("源文件路径不能为空");
                }

                if (exportParams.TrimStartSeconds.HasValue && exportParams.TrimStartSeconds.Value < 0)
                {
                    errors.Add("裁剪起始时间不能为负数");
                }

                if (exportParams.TrimDurationSeconds.HasValue && exportParams.TrimDurationSeconds.Value <= 0)
                {
                    errors.Add("裁剪持续时长必须大于 0");
                }
            }
            else
            {
                // 多段模式校验
                for (int i = 0; i < exportParams.Segments.Count; i++)
                {
                    var seg = exportParams.Segments[i];
                    if (seg == null)
                    {
                        errors.Add(string.Format("第 {0} 段片段不能为空", i + 1));
                        continue;
                    }
                    if (string.IsNullOrEmpty(seg.SourceFilePath))
                    {
                        errors.Add(string.Format("第 {0} 段源文件路径不能为空", i + 1));
                    }
                    if (seg.DurationSeconds <= 0)
                    {
                        errors.Add(string.Format("第 {0} 段持续时长必须大于 0", i + 1));
                    }
                    if (seg.StartSeconds < 0)
                    {
                        errors.Add(string.Format("第 {0} 段起始时间不能为负数", i + 1));
                    }
                }
            }

            if (exportParams.GainDb < GainService.MinGainDb || exportParams.GainDb > GainService.MaxGainDb)
            {
                errors.Add(string.Format("增益值必须在 {0} ~ {1} dB 范围内",
                    GainService.MinGainDb, GainService.MaxGainDb));
            }

            return errors;
        }

        /// <summary>
        /// 构建单文件裁剪+增益导出的 FFmpeg 命令参数
        /// </summary>
        public string BuildTrimExportArguments(EditExportParams exportParams)
        {
            if (exportParams == null)
            {
                throw new ArgumentNullException("exportParams");
            }

            string targetFormat = exportParams.TargetFormat;
            bool isAudioOnly = ConversionService.IsAudioOnlyFormat(targetFormat);
            var codecs = ConversionService.GetDefaultCodecs(targetFormat);
            if (codecs == null)
            {
                throw new InvalidOperationException(
                    string.Format("不支持的目标格式: {0}", targetFormat));
            }

            var builder = new FFmpegCommandBuilder();

            // 裁剪参数（-ss 放在输入前实现快速 seek）
            if (exportParams.TrimStartSeconds.HasValue)
            {
                builder.SeekStart(exportParams.TrimStartSeconds.Value);
            }

            builder.Input(exportParams.SourceFilePath);

            // 持续时长（付费墙截断）
            if (exportParams.TrimDurationSeconds.HasValue)
            {
                double effectiveDuration = exportParams.TrimDurationSeconds.Value;
                if (_paywallService != null && _paywallService.NeedsTruncation(effectiveDuration))
                {
                    effectiveDuration = _paywallService.GetMaxExportSeconds();
                }
                builder.Duration(effectiveDuration);
            }

            // 编解码器
            if (isAudioOnly)
            {
                builder.NoVideo();
            }
            else
            {
                string videoCodec = codecs.VideoCodec;
                if (exportParams.Preset != null && !string.IsNullOrEmpty(exportParams.Preset.VideoCodec))
                {
                    videoCodec = exportParams.Preset.VideoCodec;
                }
                builder.VideoCodec(videoCodec);
            }

            string audioCodec = codecs.AudioCodec;
            if (exportParams.Preset != null && !string.IsNullOrEmpty(exportParams.Preset.AudioCodec))
            {
                audioCodec = exportParams.Preset.AudioCodec;
            }
            builder.AudioCodec(audioCodec);

            // 预设参数
            if (exportParams.Preset != null)
            {
                if (exportParams.Preset.Width > 0 && exportParams.Preset.Height > 0)
                {
                    builder.Resolution(exportParams.Preset.Width, exportParams.Preset.Height);
                }
                if (!string.IsNullOrEmpty(exportParams.Preset.VideoBitrate))
                {
                    builder.VideoBitrate(exportParams.Preset.VideoBitrate);
                }
                if (!string.IsNullOrEmpty(exportParams.Preset.AudioBitrate))
                {
                    builder.AudioBitrate(exportParams.Preset.AudioBitrate);
                }
                if (exportParams.Preset.FrameRate > 0)
                {
                    builder.FrameRate(exportParams.Preset.FrameRate);
                }
            }

            // 增益滤镜
            if (Math.Abs(exportParams.GainDb) > 0.01)
            {
                double linear = GainService.DbToLinear(exportParams.GainDb);
                builder.AudioFilter(string.Format(CultureInfo.InvariantCulture,
                    "volume={0:F6}", linear));
            }

            // 付费墙水印（仅视频）
            if (_paywallService != null && _paywallService.ShouldAddWatermark(!isAudioOnly))
            {
                builder.VideoFilter(_paywallService.BuildWatermarkFilter());
            }

            builder.Threads(0);
            builder.Output(exportParams.OutputFilePath);

            return builder.Build();
        }

        /// <summary>
        /// 构建多段拼接导出的 FFmpeg 命令参数（使用 filter_complex concat）
        /// </summary>
        public string BuildConcatExportArguments(EditExportParams exportParams)
        {
            if (exportParams == null)
            {
                throw new ArgumentNullException("exportParams");
            }
            if (exportParams.Segments == null || exportParams.Segments.Count == 0)
            {
                throw new InvalidOperationException("多段拼接需要至少一个片段");
            }

            string targetFormat = exportParams.TargetFormat;
            bool isAudioOnly = ConversionService.IsAudioOnlyFormat(targetFormat);
            var codecs = ConversionService.GetDefaultCodecs(targetFormat);
            if (codecs == null)
            {
                throw new InvalidOperationException(
                    string.Format("不支持的目标格式: {0}", targetFormat));
            }

            var builder = new FFmpegCommandBuilder();

            // 添加所有输入片段，每段带 -ss 和 -t 裁剪
            var filterParts = new StringBuilder();
            int segCount = exportParams.Segments.Count;

            for (int i = 0; i < segCount; i++)
            {
                var seg = exportParams.Segments[i];
                // 每段独立 -ss -t -i 参数
                if (seg.StartSeconds > 0)
                {
                    builder.Option(string.Format(CultureInfo.InvariantCulture,
                        "-ss {0:F6}", seg.StartSeconds));
                }
                if (seg.DurationSeconds > 0)
                {
                    builder.Option(string.Format(CultureInfo.InvariantCulture,
                        "-t {0:F6}", seg.DurationSeconds));
                }
                builder.Input(seg.SourceFilePath);
            }

            // 构建 filter_complex
            if (isAudioOnly)
            {
                // 纯音频: [0:a][1:a]...[n:a]concat=n=N:v=0:a=1[a_out]
                for (int i = 0; i < segCount; i++)
                {
                    filterParts.Append(string.Format("[{0}:a]", i));
                }
                filterParts.Append(string.Format("concat=n={0}:v=0:a=1", segCount));

                // 增益
                if (Math.Abs(exportParams.GainDb) > 0.01)
                {
                    double linear = GainService.DbToLinear(exportParams.GainDb);
                    filterParts.Append(string.Format(CultureInfo.InvariantCulture,
                        "[a_tmp];[a_tmp]volume={0:F6}[a_out]", linear));
                }
                else
                {
                    filterParts.Append("[a_out]");
                }

                builder.FilterComplex(filterParts.ToString());
                builder.Map("[a_out]");
                builder.NoVideo();
            }
            else
            {
                // 视频+音频: [0:v][0:a][1:v][1:a]...concat=n=N:v=1:a=1[v_out][a_out]
                for (int i = 0; i < segCount; i++)
                {
                    filterParts.Append(string.Format("[{0}:v][{0}:a]", i));
                }
                filterParts.Append(string.Format("concat=n={0}:v=1:a=1", segCount));

                bool hasGain = Math.Abs(exportParams.GainDb) > 0.01;
                bool needWatermark = _paywallService != null && _paywallService.ShouldAddWatermark(true);

                if (hasGain && needWatermark)
                {
                    double linear = GainService.DbToLinear(exportParams.GainDb);
                    string wm = _paywallService.BuildWatermarkFilter();
                    filterParts.Append(string.Format(CultureInfo.InvariantCulture,
                        "[v_tmp][a_tmp];[a_tmp]volume={0:F6}[a_out];[v_tmp]{1}[v_out]", linear, wm));
                }
                else if (hasGain)
                {
                    double linear = GainService.DbToLinear(exportParams.GainDb);
                    filterParts.Append(string.Format(CultureInfo.InvariantCulture,
                        "[v_out][a_tmp];[a_tmp]volume={0:F6}[a_out]", linear));
                }
                else if (needWatermark)
                {
                    string wm = _paywallService.BuildWatermarkFilter();
                    filterParts.Append(string.Format("[v_tmp][a_out];[v_tmp]{0}[v_out]", wm));
                }
                else
                {
                    filterParts.Append("[v_out][a_out]");
                }

                builder.FilterComplex(filterParts.ToString());
                builder.Map("[v_out]");
                builder.Map("[a_out]");

                string videoCodec = codecs.VideoCodec;
                if (exportParams.Preset != null && !string.IsNullOrEmpty(exportParams.Preset.VideoCodec))
                {
                    videoCodec = exportParams.Preset.VideoCodec;
                }
                builder.VideoCodec(videoCodec);
            }

            string audioCodec = codecs.AudioCodec;
            if (exportParams.Preset != null && !string.IsNullOrEmpty(exportParams.Preset.AudioCodec))
            {
                audioCodec = exportParams.Preset.AudioCodec;
            }
            builder.AudioCodec(audioCodec);

            // 预设参数
            if (exportParams.Preset != null)
            {
                if (!string.IsNullOrEmpty(exportParams.Preset.AudioBitrate))
                {
                    builder.AudioBitrate(exportParams.Preset.AudioBitrate);
                }
            }

            // 付费墙时长截断
            if (_paywallService != null)
            {
                double totalDuration = 0;
                foreach (var seg in exportParams.Segments)
                {
                    totalDuration += seg.DurationSeconds;
                }
                if (_paywallService.NeedsTruncation(totalDuration))
                {
                    builder.Duration(_paywallService.GetMaxExportSeconds());
                }
            }

            builder.Threads(0);
            builder.Output(exportParams.OutputFilePath);

            return builder.Build();
        }

        /// <summary>
        /// 根据参数自动选择构建方式（单文件裁剪或多段拼接）
        /// </summary>
        public string BuildExportArguments(EditExportParams exportParams)
        {
            if (exportParams == null)
            {
                throw new ArgumentNullException("exportParams");
            }

            if (exportParams.Segments != null && exportParams.Segments.Count > 0)
            {
                return BuildConcatExportArguments(exportParams);
            }

            return BuildTrimExportArguments(exportParams);
        }

        /// <summary>
        /// 计算导出总时长（秒），用于进度计算
        /// </summary>
        public double CalculateTotalDuration(EditExportParams exportParams)
        {
            if (exportParams == null)
            {
                return 0;
            }

            if (exportParams.Segments != null && exportParams.Segments.Count > 0)
            {
                double total = 0;
                foreach (var seg in exportParams.Segments)
                {
                    if (seg != null)
                    {
                        total += seg.DurationSeconds;
                    }
                }
                return total;
            }

            if (exportParams.TrimDurationSeconds.HasValue)
            {
                return exportParams.TrimDurationSeconds.Value;
            }

            return 0;
        }

        /// <summary>
        /// 从时间轴片段列表创建导出参数
        /// </summary>
        public static EditExportParams CreateFromTimeline(
            IList<TimelineClip> clips,
            string outputPath,
            string targetFormat,
            double gainDb,
            ConversionPreset preset)
        {
            if (clips == null || clips.Count == 0)
            {
                throw new ArgumentException("至少需要一个片段");
            }

            var exportParams = new EditExportParams
            {
                OutputFilePath = outputPath,
                TargetFormat = targetFormat,
                GainDb = gainDb,
                Preset = preset
            };

            if (clips.Count == 1)
            {
                // 单文件模式
                var clip = clips[0];
                exportParams.SourceFilePath = clip.SourceFilePath;
                if (clip.SourceStartSeconds > 0)
                {
                    exportParams.TrimStartSeconds = clip.SourceStartSeconds;
                }
                if (clip.DurationSeconds > 0)
                {
                    exportParams.TrimDurationSeconds = clip.DurationSeconds;
                }
            }
            else
            {
                // 多段模式
                exportParams.Segments = new List<ClipSegment>();
                foreach (var clip in clips)
                {
                    exportParams.Segments.Add(new ClipSegment
                    {
                        SourceFilePath = clip.SourceFilePath,
                        StartSeconds = clip.SourceStartSeconds,
                        DurationSeconds = clip.DurationSeconds
                    });
                }
            }

            return exportParams;
        }
    }
}
