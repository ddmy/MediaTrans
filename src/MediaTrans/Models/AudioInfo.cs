using System;

namespace MediaTrans.Models
{
    /// <summary>
    /// 音频基本信息，用于波形渲染和 PCM 缓存管理
    /// </summary>
    public class AudioInfo
    {
        /// <summary>
        /// 源文件路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 采样率 (Hz)，例如 44100
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// 声道数（1=单声道, 2=立体声）
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// 总采样帧数（每帧包含所有声道的一个采样点）
        /// </summary>
        public long TotalSamples { get; set; }

        /// <summary>
        /// 总时长（秒）
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// 每个采样点的字节数（16bit=2, 24bit=3, 32bit float=4）
        /// </summary>
        public int BytesPerSample { get; set; }

        /// <summary>
        /// 每帧字节数 = Channels * BytesPerSample
        /// </summary>
        public int BytesPerFrame
        {
            get { return Channels * BytesPerSample; }
        }

        /// <summary>
        /// 从采样帧号转换为时间秒数
        /// </summary>
        public double SamplesToSeconds(long sampleIndex)
        {
            if (SampleRate <= 0) return 0;
            return (double)sampleIndex / SampleRate;
        }

        /// <summary>
        /// 从时间秒数转换为采样帧号
        /// </summary>
        public long SecondsToSamples(double seconds)
        {
            if (seconds < 0) return 0;
            return (long)(seconds * SampleRate);
        }
    }
}
