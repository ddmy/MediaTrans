using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MediaTrans.Models
{
    /// <summary>
    /// 音乐平台枚举
    /// </summary>
    public enum MusicPlatform
    {
        NetEase,
        QQ,
        Kugou,
        Kuwo,
        Migu,
        Bilibili,
        Taihe
    }

    /// <summary>
    /// 单个平台的歌曲源信息
    /// </summary>
    public class MusicSource
    {
        public string Platform { get; set; }
        public string PlatformName { get; set; }
        public string SongId { get; set; }
        public List<string> Quality { get; set; }
        public bool NeedVip { get; set; }
        public int DurationSeconds { get; set; }
        public string DurationText { get; set; }

        public MusicSource()
        {
            Quality = new List<string>();
        }

        public override string ToString()
        {
            return PlatformName ?? Platform ?? "";
        }
    }

    /// <summary>
    /// 搜索结果项（跨平台合并后）
    /// </summary>
    public class MusicSearchResult : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public string SongName { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }

        private int _durationSeconds;
        public int DurationSeconds
        {
            get { return _durationSeconds; }
            set
            {
                if (_durationSeconds != value)
                {
                    _durationSeconds = value;
                    OnPropertyChanged("DurationSeconds");
                }
            }
        }

        private string _durationText;
        public string DurationText
        {
            get { return _durationText; }
            set
            {
                if (_durationText != value)
                {
                    _durationText = value;
                    OnPropertyChanged("DurationText");
                }
            }
        }

        public List<MusicSource> Sources { get; set; }

        private MusicSource _selectedSource;
        public MusicSource SelectedSource
        {
            get { return _selectedSource; }
            set
            {
                if (_selectedSource != value)
                {
                    _selectedSource = value;
                    OnPropertyChanged("SelectedSource");
                    // 切换平台时同步更新时长
                    if (value != null && value.DurationSeconds > 0)
                    {
                        DurationSeconds = value.DurationSeconds;
                        DurationText = value.DurationText;
                    }
                }
            }
        }

        /// <summary>
        /// 是否有多个平台源
        /// </summary>
        public bool HasMultipleSources
        {
            get { return Sources != null && Sources.Count > 1; }
        }

        /// <summary>
        /// 可用平台摘要文本，如 "网易云 | QQ | 酷狗"
        /// </summary>
        public string AvailablePlatformsText
        {
            get
            {
                if (Sources == null || Sources.Count == 0) return "";
                var names = new List<string>();
                foreach (var s in Sources)
                {
                    names.Add(s.PlatformName);
                }
                return string.Join(" | ", names);
            }
        }

        /// <summary>
        /// 是否有可用的免费源
        /// </summary>
        public bool HasFreeSource
        {
            get
            {
                if (Sources == null) return false;
                foreach (var s in Sources)
                {
                    if (!s.NeedVip) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 最佳可用音质
        /// </summary>
        public string BestQuality
        {
            get
            {
                if (Sources == null || Sources.Count == 0) return "128";
                bool hasFlac = false;
                bool has320 = false;
                foreach (var s in Sources)
                {
                    if (s.Quality == null) continue;
                    foreach (var q in s.Quality)
                    {
                        if (q == "flac") hasFlac = true;
                        if (q == "320") has320 = true;
                    }
                }
                if (hasFlac) return "flac";
                if (has320) return "320";
                return "128";
            }
        }

        /// <summary>
        /// 是否被选中（用于 UI 勾选）
        /// </summary>
        public bool IsSelected { get; set; }

        public MusicSearchResult()
        {
            Sources = new List<MusicSource>();
        }
    }

    /// <summary>
    /// 播放/下载链接信息
    /// </summary>
    public class MusicStreamInfo
    {
        public string Url { get; set; }
        public int Quality { get; set; }
        public string Format { get; set; }
        public long Size { get; set; }
    }

    /// <summary>
    /// 平台搜索状态
    /// </summary>
    public class PlatformSearchStatus
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Status { get; set; }
        public int Count { get; set; }
        public string Error { get; set; }

        /// <summary>
        /// 状态显示文本
        /// </summary>
        public string StatusText
        {
            get
            {
                if (Status == "success")
                {
                    return string.Format("{0}({1})", DisplayName, Count);
                }
                if (Status == "error")
                {
                    return string.Format("{0}✗", DisplayName);
                }
                return string.Format("{0}...", DisplayName);
            }
        }
    }

    /// <summary>
    /// 平台筛选项
    /// </summary>
    public class PlatformFilterItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public string Name { get; set; }
        public string DisplayName { get; set; }
        public int Count { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged("IsChecked");
                    OnPropertyChanged("DisplayText");
                    if (CheckedChanged != null)
                    {
                        CheckedChanged(this, EventArgs.Empty);
                    }
                }
            }
        }

        public string DisplayText
        {
            get
            {
                return string.Format("{0}({1})", DisplayName, Count);
            }
        }

        /// <summary>
        /// IsChecked 变化事件，供 ViewModel 监听以触发过滤
        /// </summary>
        public event EventHandler CheckedChanged;
    }
}
