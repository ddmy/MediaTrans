using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MediaTrans.Commands;
using MediaTrans.Models;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 时间轴轨道 ViewModel
    /// 管理多段素材的拼接、排序、选中和删除
    /// </summary>
    public class TimelineTrackViewModel : ViewModelBase
    {
        private readonly ObservableCollection<TimelineClip> _clips;
        private TimelineClip _selectedClip;
        private double _totalDurationSeconds;
        private int _clipCount;
        private string _trackInfoText;
        private string _allowedMediaType;

        /// <summary>
        /// 创建时间轴轨道 ViewModel
        /// </summary>
        public TimelineTrackViewModel()
        {
            _clips = new ObservableCollection<TimelineClip>();
            _trackInfoText = "空轨道";
            _allowedMediaType = null; // 由第一个添加的片段决定

            DeleteSelectedCommand = new RelayCommand(
                new Action<object>(ExecuteDeleteSelected),
                new Func<object, bool>(o => CanDeleteSelected));
        }

        /// <summary>
        /// 片段集合
        /// </summary>
        public ObservableCollection<TimelineClip> Clips
        {
            get { return _clips; }
        }

        /// <summary>
        /// 当前选中片段
        /// </summary>
        public TimelineClip SelectedClip
        {
            get { return _selectedClip; }
            set
            {
                // 取消之前选中状态
                if (_selectedClip != null)
                {
                    _selectedClip.IsSelected = false;
                }

                SetProperty(ref _selectedClip, value, "SelectedClip");

                // 设置新的选中状态
                if (_selectedClip != null)
                {
                    _selectedClip.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// 轨道总时长（秒）
        /// </summary>
        public double TotalDurationSeconds
        {
            get { return _totalDurationSeconds; }
            private set { SetProperty(ref _totalDurationSeconds, value, "TotalDurationSeconds"); }
        }

        /// <summary>
        /// 片段数量
        /// </summary>
        public int ClipCount
        {
            get { return _clipCount; }
            private set { SetProperty(ref _clipCount, value, "ClipCount"); }
        }

        /// <summary>
        /// 轨道信息文本
        /// </summary>
        public string TrackInfoText
        {
            get { return _trackInfoText; }
            private set { SetProperty(ref _trackInfoText, value, "TrackInfoText"); }
        }

        /// <summary>
        /// 允许的媒体类型（由第一个片段决定，仅同类型素材）
        /// </summary>
        public string AllowedMediaType
        {
            get { return _allowedMediaType; }
        }

        /// <summary>
        /// 删除选中片段命令
        /// </summary>
        public RelayCommand DeleteSelectedCommand { get; private set; }

        /// <summary>
        /// 是否可以删除选中片段
        /// </summary>
        private bool CanDeleteSelected
        {
            get { return _selectedClip != null; }
        }

        /// <summary>
        /// 添加片段到轨道末尾
        /// </summary>
        /// <param name="clip">要添加的片段</param>
        /// <returns>添加成功返回 true，类型不匹配返回 false</returns>
        public bool AddClip(TimelineClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException("clip");
            }

            // 检查媒体类型兼容性
            if (!IsCompatibleMediaType(clip.MediaType))
            {
                return false;
            }

            // 首个片段决定轨道媒体类型
            if (_allowedMediaType == null)
            {
                _allowedMediaType = clip.MediaType;
            }

            _clips.Add(clip);
            RecalculateTimeline();
            return true;
        }

        /// <summary>
        /// 在指定位置插入片段
        /// </summary>
        /// <param name="index">插入位置索引</param>
        /// <param name="clip">要插入的片段</param>
        /// <returns>插入成功返回 true</returns>
        public bool InsertClip(int index, TimelineClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException("clip");
            }
            if (index < 0 || index > _clips.Count)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            if (!IsCompatibleMediaType(clip.MediaType))
            {
                return false;
            }

            if (_allowedMediaType == null)
            {
                _allowedMediaType = clip.MediaType;
            }

            _clips.Insert(index, clip);
            RecalculateTimeline();
            return true;
        }

        /// <summary>
        /// 删除指定片段
        /// </summary>
        /// <param name="clip">要删除的片段</param>
        /// <returns>删除成功返回 true</returns>
        public bool RemoveClip(TimelineClip clip)
        {
            if (clip == null)
            {
                return false;
            }

            bool removed = _clips.Remove(clip);
            if (removed)
            {
                if (_selectedClip == clip)
                {
                    SelectedClip = null;
                }

                // 清空轨道时重置媒体类型
                if (_clips.Count == 0)
                {
                    _allowedMediaType = null;
                }

                RecalculateTimeline();
            }
            return removed;
        }

        /// <summary>
        /// 通过索引删除片段
        /// </summary>
        /// <param name="index">片段索引</param>
        /// <returns>删除成功返回 true</returns>
        public bool RemoveClipAt(int index)
        {
            if (index < 0 || index >= _clips.Count)
            {
                return false;
            }

            var clip = _clips[index];
            return RemoveClip(clip);
        }

        /// <summary>
        /// 移动片段到新位置（拖拽排序）
        /// </summary>
        /// <param name="oldIndex">原位置</param>
        /// <param name="newIndex">新位置</param>
        /// <returns>移动成功返回 true</returns>
        public bool MoveClip(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= _clips.Count)
            {
                return false;
            }
            if (newIndex < 0 || newIndex >= _clips.Count)
            {
                return false;
            }
            if (oldIndex == newIndex)
            {
                return true; // 位置不变视为成功
            }

            var clip = _clips[oldIndex];
            _clips.RemoveAt(oldIndex);
            _clips.Insert(newIndex, clip);
            RecalculateTimeline();
            return true;
        }

        /// <summary>
        /// 选中指定索引的片段
        /// </summary>
        /// <param name="index">片段索引</param>
        public void SelectClipAt(int index)
        {
            if (index >= 0 && index < _clips.Count)
            {
                SelectedClip = _clips[index];
            }
        }

        /// <summary>
        /// 通过 ID 选中片段
        /// </summary>
        /// <param name="clipId">片段 ID</param>
        public void SelectClipById(int clipId)
        {
            for (int i = 0; i < _clips.Count; i++)
            {
                if (_clips[i].Id == clipId)
                {
                    SelectedClip = _clips[i];
                    return;
                }
            }
        }

        /// <summary>
        /// 取消所有选中
        /// </summary>
        public void ClearSelection()
        {
            SelectedClip = null;
        }

        /// <summary>
        /// 清空所有片段
        /// </summary>
        public void ClearAll()
        {
            SelectedClip = null;
            _clips.Clear();
            _allowedMediaType = null;
            RecalculateTimeline();
        }

        /// <summary>
        /// 根据时间轴位置查找命中的片段
        /// </summary>
        /// <param name="timeSeconds">时间轴位置（秒）</param>
        /// <returns>命中的片段，未命中返回 null</returns>
        public TimelineClip HitTestAtTime(double timeSeconds)
        {
            for (int i = 0; i < _clips.Count; i++)
            {
                var clip = _clips[i];
                if (timeSeconds >= clip.TimelineStartSeconds && timeSeconds < clip.TimelineEndSeconds)
                {
                    return clip;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取片段在轨道中的索引
        /// </summary>
        /// <param name="clip">片段</param>
        /// <returns>索引值，不存在返回 -1</returns>
        public int IndexOf(TimelineClip clip)
        {
            return _clips.IndexOf(clip);
        }

        /// <summary>
        /// 获取片段列表的只读副本
        /// </summary>
        public List<TimelineClip> GetClipList()
        {
            var list = new List<TimelineClip>();
            for (int i = 0; i < _clips.Count; i++)
            {
                list.Add(_clips[i]);
            }
            return list;
        }

        /// <summary>
        /// 执行删除选中片段
        /// </summary>
        private void ExecuteDeleteSelected(object parameter)
        {
            if (_selectedClip != null)
            {
                RemoveClip(_selectedClip);
            }
        }

        /// <summary>
        /// 检查媒体类型是否兼容
        /// </summary>
        /// <param name="mediaType">待检查的媒体类型</param>
        /// <returns>兼容返回 true</returns>
        private bool IsCompatibleMediaType(string mediaType)
        {
            // 轨道为空时，任何类型都兼容
            if (_allowedMediaType == null)
            {
                return true;
            }

            // 比较媒体类型（忽略大小写）
            return string.Equals(_allowedMediaType, mediaType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 重新计算片段在时间轴上的位置（无缝衔接）
        /// </summary>
        private void RecalculateTimeline()
        {
            double currentPosition = 0;
            for (int i = 0; i < _clips.Count; i++)
            {
                _clips[i].TimelineStartSeconds = currentPosition;
                currentPosition += _clips[i].DurationSeconds;
            }

            TotalDurationSeconds = currentPosition;
            ClipCount = _clips.Count;
            UpdateTrackInfoText();
        }

        /// <summary>
        /// 更新轨道信息文本
        /// </summary>
        private void UpdateTrackInfoText()
        {
            if (_clips.Count == 0)
            {
                TrackInfoText = "空轨道";
                return;
            }

            int totalMs = (int)(TotalDurationSeconds * 1000);
            int hours = totalMs / 3600000;
            int minutes = (totalMs % 3600000) / 60000;
            int seconds = (totalMs % 60000) / 1000;
            int ms = totalMs % 1000;

            TrackInfoText = string.Format("{0} 个片段 | {1:D2}:{2:D2}:{3:D2}.{4:D3}",
                _clips.Count, hours, minutes, seconds, ms);
        }
    }
}
