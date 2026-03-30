using System;

namespace MediaTrans.Models
{
    /// <summary>
    /// 媒体文件信息模型
    /// </summary>
    public class MediaFileInfo
    {
        /// <summary>
        /// 文件完整路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 文件名（含扩展名）
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 时长（秒）
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// 格式化时长显示 (HH:MM:SS)
        /// </summary>
        public string DurationText
        {
            get
            {
                var ts = TimeSpan.FromSeconds(DurationSeconds);
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            }
        }

        /// <summary>
        /// 容器格式（如 mp4, avi, mkv）
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// 视频编解码器名称
        /// </summary>
        public string VideoCodec { get; set; }

        /// <summary>
        /// 音频编解码器名称
        /// </summary>
        public string AudioCodec { get; set; }

        /// <summary>
        /// 视频宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 视频高度
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 分辨率文本（如 "1920x1080"）
        /// </summary>
        public string ResolutionText
        {
            get
            {
                if (Width > 0 && Height > 0)
                {
                    return string.Format("{0}x{1}", Width, Height);
                }
                return "";
            }
        }

        /// <summary>
        /// 帧率
        /// </summary>
        public double FrameRate { get; set; }

        /// <summary>
        /// 视频比特率（bps）
        /// </summary>
        public long VideoBitrate { get; set; }

        /// <summary>
        /// 音频采样率
        /// </summary>
        public int AudioSampleRate { get; set; }

        /// <summary>
        /// 音频声道数
        /// </summary>
        public int AudioChannels { get; set; }

        /// <summary>
        /// 音频比特率（bps）
        /// </summary>
        public long AudioBitrate { get; set; }

        /// <summary>
        /// 是否包含视频流
        /// </summary>
        public bool HasVideo { get; set; }

        /// <summary>
        /// 是否包含音频流
        /// </summary>
        public bool HasAudio { get; set; }

        /// <summary>
        /// 元信息是否已加载完成
        /// </summary>
        public bool MetadataLoaded { get; set; }

        /// <summary>
        /// 文件大小格式化显示
        /// </summary>
        public string FileSizeText
        {
            get
            {
                if (FileSize < 1024)
                {
                    return string.Format("{0} B", FileSize);
                }
                if (FileSize < 1024 * 1024)
                {
                    return string.Format("{0:F1} KB", FileSize / 1024.0);
                }
                if (FileSize < 1024L * 1024 * 1024)
                {
                    return string.Format("{0:F1} MB", FileSize / (1024.0 * 1024));
                }
                return string.Format("{0:F2} GB", FileSize / (1024.0 * 1024 * 1024));
            }
        }
    }
}
