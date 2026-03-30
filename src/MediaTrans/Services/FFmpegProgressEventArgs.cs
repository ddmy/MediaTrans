using System;

namespace MediaTrans.Services
{
    /// <summary>
    /// FFmpeg 进度信息
    /// </summary>
    public class FFmpegProgressEventArgs : EventArgs
    {
        /// <summary>
        /// 已处理时长（秒）
        /// </summary>
        public double ProcessedSeconds { get; set; }

        /// <summary>
        /// 总时长（秒），未知时为 0
        /// </summary>
        public double TotalSeconds { get; set; }

        /// <summary>
        /// 进度百分比 (0-100)，未知时为 -1
        /// </summary>
        public double Percentage
        {
            get
            {
                if (TotalSeconds > 0)
                {
                    return Math.Min(100.0, (ProcessedSeconds / TotalSeconds) * 100.0);
                }
                return -1;
            }
        }

        /// <summary>
        /// 当前处理速度
        /// </summary>
        public string Speed { get; set; }

        /// <summary>
        /// 当前帧号
        /// </summary>
        public long Frame { get; set; }

        /// <summary>
        /// 当前比特率
        /// </summary>
        public string Bitrate { get; set; }
    }
}
