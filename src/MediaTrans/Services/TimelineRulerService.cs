using System;
using System.Collections.Generic;

namespace MediaTrans.Services
{
    /// <summary>
    /// 时间轴刻度标记
    /// </summary>
    public class TickMark
    {
        /// <summary>
        /// 刻度在时间轴上的像素位置
        /// </summary>
        public double PixelX { get; set; }

        /// <summary>
        /// 刻度对应的时间（秒）
        /// </summary>
        public double TimeSeconds { get; set; }

        /// <summary>
        /// 是否为主刻度（有文字标签）
        /// </summary>
        public bool IsMajor { get; set; }

        /// <summary>
        /// 刻度标签文本（仅主刻度）
        /// </summary>
        public string Label { get; set; }
    }

    /// <summary>
    /// 时间轴刻度尺服务 — 根据缩放级别自适应刻度密度
    /// </summary>
    public class TimelineRulerService
    {
        // 可选的主刻度时间间隔（秒），从小到大
        private static readonly double[] MajorIntervals = new double[]
        {
            0.001,  // 1ms
            0.005,  // 5ms
            0.01,   // 10ms
            0.02,   // 20ms
            0.05,   // 50ms
            0.1,    // 100ms
            0.2,    // 200ms
            0.5,    // 500ms
            1.0,    // 1s
            2.0,    // 2s
            5.0,    // 5s
            10.0,   // 10s
            15.0,   // 15s
            30.0,   // 30s
            60.0,   // 1min
            120.0,  // 2min
            300.0,  // 5min
            600.0,  // 10min
            900.0,  // 15min
            1800.0, // 30min
            3600.0  // 1h
        };

        /// <summary>
        /// 每个主刻度之间的次刻度数量
        /// </summary>
        public int MinorTickCount { get; set; }

        /// <summary>
        /// 主刻度之间的最小像素间距
        /// </summary>
        public double MinMajorTickSpacingPixels { get; set; }

        public TimelineRulerService()
        {
            MinorTickCount = 4;
            MinMajorTickSpacingPixels = 80;
        }

        /// <summary>
        /// 根据当前缩放级别计算最佳主刻度间隔（秒）
        /// </summary>
        /// <param name="samplesPerPixel">每像素采样帧数</param>
        /// <param name="sampleRate">采样率</param>
        /// <returns>主刻度时间间隔（秒）</returns>
        public double CalculateMajorInterval(double samplesPerPixel, int sampleRate)
        {
            if (sampleRate <= 0 || samplesPerPixel <= 0) return 1.0;

            // 每像素代表的秒数
            double secondsPerPixel = samplesPerPixel / sampleRate;

            // 主刻度间隔至少要占 MinMajorTickSpacingPixels 个像素
            double minIntervalSeconds = secondsPerPixel * MinMajorTickSpacingPixels;

            // 找到大于等于 minIntervalSeconds 的最小预设间隔
            for (int i = 0; i < MajorIntervals.Length; i++)
            {
                if (MajorIntervals[i] >= minIntervalSeconds)
                {
                    return MajorIntervals[i];
                }
            }

            // 超过最大预设间隔，使用1小时的整数倍
            double hours = Math.Ceiling(minIntervalSeconds / 3600.0);
            return hours * 3600.0;
        }

        /// <summary>
        /// 计算可见区域内的所有刻度标记
        /// </summary>
        /// <param name="viewportStartSample">视口起始采样帧</param>
        /// <param name="viewportWidthPixels">视口像素宽度</param>
        /// <param name="samplesPerPixel">每像素采样帧数</param>
        /// <param name="sampleRate">采样率</param>
        /// <returns>刻度标记列表</returns>
        public List<TickMark> CalculateTickMarks(long viewportStartSample, int viewportWidthPixels,
            double samplesPerPixel, int sampleRate)
        {
            var result = new List<TickMark>();
            if (sampleRate <= 0 || samplesPerPixel <= 0 || viewportWidthPixels <= 0)
            {
                return result;
            }

            double majorInterval = CalculateMajorInterval(samplesPerPixel, sampleRate);
            double minorInterval = majorInterval / (MinorTickCount + 1);

            // 视口对应的时间范围
            double startSeconds = viewportStartSample / (double)sampleRate;
            double endSeconds = (viewportStartSample + (long)(viewportWidthPixels * samplesPerPixel)) / (double)sampleRate;

            // 找到第一个小刻度的时间位置（对齐到 minorInterval 网格）
            double firstMinor = Math.Floor(startSeconds / minorInterval) * minorInterval;

            for (double t = firstMinor; t <= endSeconds; t += minorInterval)
            {
                if (t < startSeconds - minorInterval * 0.5)
                {
                    continue;
                }

                // 判断是否为主刻度
                bool isMajor = IsMajorTick(t, majorInterval);

                // 计算像素位置
                long sample = (long)(t * sampleRate);
                double pixelX = (sample - viewportStartSample) / samplesPerPixel;

                var tick = new TickMark
                {
                    PixelX = pixelX,
                    TimeSeconds = t,
                    IsMajor = isMajor,
                    Label = isMajor ? FormatTickLabel(t, majorInterval) : null
                };
                result.Add(tick);
            }

            return result;
        }

        /// <summary>
        /// 判断是否为主刻度
        /// </summary>
        private bool IsMajorTick(double timeSeconds, double majorInterval)
        {
            if (majorInterval <= 0) return false;
            double remainder = timeSeconds % majorInterval;
            // 浮点精度容差
            double tolerance = majorInterval * 0.001;
            return remainder < tolerance || (majorInterval - remainder) < tolerance;
        }

        /// <summary>
        /// 格式化刻度标签
        /// </summary>
        /// <param name="timeSeconds">时间（秒）</param>
        /// <param name="majorInterval">主刻度间隔，用于决定标签精度</param>
        /// <returns>格式化的时间标签</returns>
        public static string FormatTickLabel(double timeSeconds, double majorInterval)
        {
            if (timeSeconds < 0) timeSeconds = 0;

            int totalSeconds = (int)timeSeconds;
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int secs = totalSeconds % 60;
            int ms = (int)((timeSeconds - totalSeconds) * 1000);

            // 根据间隔精度决定显示格式
            if (majorInterval < 1.0)
            {
                // 亚秒级精度：显示 MM:SS.ms
                if (hours > 0)
                {
                    return string.Format("{0}:{1:D2}:{2:D2}.{3:D3}", hours, minutes, secs, ms);
                }
                return string.Format("{0}:{1:D2}.{2:D3}", minutes, secs, ms);
            }

            if (majorInterval < 60.0)
            {
                // 秒级精度：显示 MM:SS
                if (hours > 0)
                {
                    return string.Format("{0}:{1:D2}:{2:D2}", hours, minutes, secs);
                }
                return string.Format("{0}:{1:D2}", minutes, secs);
            }

            // 分钟级以上：显示 H:MM:SS
            return string.Format("{0}:{1:D2}:{2:D2}", hours, minutes, secs);
        }
    }
}
