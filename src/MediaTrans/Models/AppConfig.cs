using System;
using System.Collections.Generic;

namespace MediaTrans.Models
{
    /// <summary>
    /// 应用程序配置模型，对应 Config/AppConfig.json
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// FFmpeg 可执行文件路径
        /// </summary>
        public string FFmpegPath { get; set; }

        /// <summary>
        /// FFprobe 可执行文件路径
        /// </summary>
        public string FFprobePath { get; set; }

        /// <summary>
        /// 默认输出目录
        /// </summary>
        public string DefaultOutputDir { get; set; }

        /// <summary>
        /// 帧缓存池最大帧数（LRU）
        /// </summary>
        public int MaxCachedFrames { get; set; }

        /// <summary>
        /// 波形分块宽度（像素）
        /// </summary>
        public int WaveformBlockWidth { get; set; }

        /// <summary>
        /// 磁吸对齐阈值（像素）
        /// </summary>
        public int SnapThresholdPixels { get; set; }

        /// <summary>
        /// 撤销栈最大深度
        /// </summary>
        public int MaxUndoDepth { get; set; }

        /// <summary>
        /// 日志单文件最大大小（字节）
        /// </summary>
        public long LogMaxFileSize { get; set; }

        /// <summary>
        /// 日志保留份数
        /// </summary>
        public int LogMaxFileCount { get; set; }

        /// <summary>
        /// 水印文字内容
        /// </summary>
        public string WatermarkText { get; set; }

        /// <summary>
        /// 水印字号
        /// </summary>
        public int WatermarkFontSize { get; set; }

        /// <summary>
        /// 水印位置（如 "RightBottom"）
        /// </summary>
        public string WatermarkPosition { get; set; }

        /// <summary>
        /// 免费版最大导出时长（秒）
        /// </summary>
        public int FreeMaxExportSeconds { get; set; }

        /// <summary>
        /// 硬件加速是否启用
        /// </summary>
        public bool HardwareAccelerationEnabled { get; set; }

        /// <summary>
        /// 首选硬件编码器类型：auto（自动检测，优先 NVENC）、nvenc、qsv
        /// </summary>
        public string PreferredHardwareEncoder { get; set; }

        /// <summary>
        /// 最大并行转换任务数
        /// </summary>
        public int MaxParallelTasks { get; set; }

        /// <summary>
        /// 方向键移动播放头的步进像素数（缩放自适应）
        /// </summary>
        public int PlayheadStepPixels { get; set; }

        /// <summary>
        /// 转换预设列表
        /// </summary>
        public List<ConversionPreset> ConversionPresets { get; set; }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static AppConfig CreateDefault()
        {
            var config = new AppConfig
            {
                FFmpegPath = @"lib\ffmpeg\ffmpeg.exe",
                FFprobePath = @"lib\ffmpeg\ffprobe.exe",
                DefaultOutputDir = "",
                MaxCachedFrames = 100,
                WaveformBlockWidth = 512,
                SnapThresholdPixels = 10,
                MaxUndoDepth = 50,
                LogMaxFileSize = 10 * 1024 * 1024,
                LogMaxFileCount = 5,
                WatermarkText = "MediaTrans",
                WatermarkFontSize = 24,
                WatermarkPosition = "RightBottom",
                FreeMaxExportSeconds = 60,
                HardwareAccelerationEnabled = true,
                PreferredHardwareEncoder = "auto",
                MaxParallelTasks = 1,
                PlayheadStepPixels = 10,
                ConversionPresets = new List<ConversionPreset>()
            };

            // 添加默认预设
            config.ConversionPresets.Add(new ConversionPreset
            {
                Name = "高质量 1080p",
                VideoCodec = "libx264",
                AudioCodec = "aac",
                Width = 1920,
                Height = 1080,
                VideoBitrate = "5M",
                AudioBitrate = "192k",
                FrameRate = 30
            });

            config.ConversionPresets.Add(new ConversionPreset
            {
                Name = "标准 720p",
                VideoCodec = "libx264",
                AudioCodec = "aac",
                Width = 1280,
                Height = 720,
                VideoBitrate = "2.5M",
                AudioBitrate = "128k",
                FrameRate = 30
            });

            config.ConversionPresets.Add(new ConversionPreset
            {
                Name = "高质量音频",
                VideoCodec = "",
                AudioCodec = "aac",
                Width = 0,
                Height = 0,
                VideoBitrate = "",
                AudioBitrate = "320k",
                FrameRate = 0
            });

            return config;
        }
    }

    /// <summary>
    /// 转换预设参数
    /// </summary>
    public class ConversionPreset
    {
        public string Name { get; set; }
        public string VideoCodec { get; set; }
        public string AudioCodec { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string VideoBitrate { get; set; }
        public string AudioBitrate { get; set; }
        public int FrameRate { get; set; }
    }
}
