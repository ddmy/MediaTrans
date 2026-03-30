using System;

namespace MediaTrans.Models
{
    /// <summary>
    /// 时间轴片段模型
    /// 表示时间轴上的一个媒体片段
    /// </summary>
    public class TimelineClip
    {
        private static int _nextId = 1;
        private static readonly object _idLock = new object();

        /// <summary>
        /// 片段唯一标识
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// 源文件路径
        /// </summary>
        public string SourceFilePath { get; set; }

        /// <summary>
        /// 源文件显示名
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 片段在源文件中的起始时间（秒）
        /// </summary>
        public double SourceStartSeconds { get; set; }

        /// <summary>
        /// 片段在源文件中的结束时间（秒）
        /// </summary>
        public double SourceEndSeconds { get; set; }

        /// <summary>
        /// 片段时长（秒）
        /// </summary>
        public double DurationSeconds
        {
            get { return SourceEndSeconds - SourceStartSeconds; }
        }

        /// <summary>
        /// 片段在时间轴上的起始位置（秒）
        /// 由 TimelineTrackViewModel 自动计算
        /// </summary>
        public double TimelineStartSeconds { get; set; }

        /// <summary>
        /// 片段在时间轴上的结束位置（秒）
        /// </summary>
        public double TimelineEndSeconds
        {
            get { return TimelineStartSeconds + DurationSeconds; }
        }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// 媒体类型（video/audio）
        /// </summary>
        public string MediaType { get; set; }

        /// <summary>
        /// 创建片段并自动分配唯一 ID
        /// </summary>
        public TimelineClip()
        {
            lock (_idLock)
            {
                Id = _nextId++;
            }
        }

        /// <summary>
        /// 使用指定 ID 创建片段（用于测试）
        /// </summary>
        /// <param name="id">指定 ID</param>
        public TimelineClip(int id)
        {
            Id = id;
        }

        /// <summary>
        /// 格式化时长文本
        /// </summary>
        public string DurationText
        {
            get
            {
                double dur = DurationSeconds;
                int totalMs = (int)(dur * 1000);
                int hours = totalMs / 3600000;
                int minutes = (totalMs % 3600000) / 60000;
                int seconds = (totalMs % 60000) / 1000;
                int ms = totalMs % 1000;
                return string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}",
                    hours, minutes, seconds, ms);
            }
        }

        /// <summary>
        /// 创建片段的深拷贝
        /// </summary>
        public TimelineClip Clone()
        {
            var clone = new TimelineClip();
            clone.SourceFilePath = SourceFilePath;
            clone.DisplayName = DisplayName;
            clone.SourceStartSeconds = SourceStartSeconds;
            clone.SourceEndSeconds = SourceEndSeconds;
            clone.TimelineStartSeconds = TimelineStartSeconds;
            clone.IsSelected = IsSelected;
            clone.MediaType = MediaType;
            return clone;
        }
    }
}
