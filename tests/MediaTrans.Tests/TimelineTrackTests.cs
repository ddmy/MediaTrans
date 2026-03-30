using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Xunit;
using MediaTrans.Models;
using MediaTrans.ViewModels;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 时间轴片段模型测试
    /// </summary>
    public class TimelineClipTests
    {
        [Fact]
        public void 构造函数_自动分配唯一ID()
        {
            var clip1 = new TimelineClip();
            var clip2 = new TimelineClip();
            Assert.NotEqual(clip1.Id, clip2.Id);
        }

        [Fact]
        public void 构造函数_指定ID()
        {
            var clip = new TimelineClip(42);
            Assert.Equal(42, clip.Id);
        }

        [Fact]
        public void DurationSeconds_计算正确()
        {
            var clip = new TimelineClip(1);
            clip.SourceStartSeconds = 10.0;
            clip.SourceEndSeconds = 25.5;
            Assert.Equal(15.5, clip.DurationSeconds, 6);
        }

        [Fact]
        public void TimelineEndSeconds_计算正确()
        {
            var clip = new TimelineClip(1);
            clip.SourceStartSeconds = 0;
            clip.SourceEndSeconds = 5.0;
            clip.TimelineStartSeconds = 10.0;
            Assert.Equal(15.0, clip.TimelineEndSeconds, 6);
        }

        [Fact]
        public void DurationText_格式正确()
        {
            var clip = new TimelineClip(1);
            clip.SourceStartSeconds = 0;
            clip.SourceEndSeconds = 3661.234;
            // 3661.234 秒 = 1小时1分1秒234毫秒
            Assert.Equal("01:01:01.234", clip.DurationText);
        }

        [Fact]
        public void Clone_深拷贝()
        {
            var clip = new TimelineClip(1);
            clip.SourceFilePath = "test.mp4";
            clip.DisplayName = "测试";
            clip.SourceStartSeconds = 0;
            clip.SourceEndSeconds = 10;
            clip.TimelineStartSeconds = 5;
            clip.MediaType = "video";
            clip.IsSelected = true;

            var clone = clip.Clone();
            Assert.NotEqual(clip.Id, clone.Id); // 新 ID
            Assert.Equal("test.mp4", clone.SourceFilePath);
            Assert.Equal("测试", clone.DisplayName);
            Assert.Equal(0, clone.SourceStartSeconds);
            Assert.Equal(10, clone.SourceEndSeconds);
            Assert.Equal(5, clone.TimelineStartSeconds);
            Assert.Equal("video", clone.MediaType);
            Assert.True(clone.IsSelected);
        }
    }

    /// <summary>
    /// 时间轴轨道 ViewModel 测试
    /// </summary>
    public class TimelineTrackViewModelTests
    {
        #region 构造函数测试

        [Fact]
        public void 构造函数_初始状态正确()
        {
            var vm = new TimelineTrackViewModel();
            Assert.Equal(0, vm.ClipCount);
            Assert.Equal(0.0, vm.TotalDurationSeconds);
            Assert.Null(vm.SelectedClip);
            Assert.NotNull(vm.Clips);
            Assert.Empty(vm.Clips);
            Assert.Null(vm.AllowedMediaType);
            Assert.Equal("空轨道", vm.TrackInfoText);
        }

        #endregion

        #region AddClip 测试

        [Fact]
        public void AddClip_添加成功()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("test.mp4", 0, 10, "video");

            bool result = vm.AddClip(clip);

            Assert.True(result);
            Assert.Equal(1, vm.ClipCount);
            Assert.Equal(10.0, vm.TotalDurationSeconds);
        }

        [Fact]
        public void AddClip_Null参数_抛异常()
        {
            var vm = new TimelineTrackViewModel();
            Assert.Throws<ArgumentNullException>(() => vm.AddClip(null));
        }

        [Fact]
        public void AddClip_多个片段_无缝衔接()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            var clip3 = CreateClip("c.mp4", 0, 8, "video");

            vm.AddClip(clip1);
            vm.AddClip(clip2);
            vm.AddClip(clip3);

            Assert.Equal(3, vm.ClipCount);
            Assert.Equal(23.0, vm.TotalDurationSeconds);

            // 验证时间轴位置无缝衔接
            Assert.Equal(0.0, clip1.TimelineStartSeconds);
            Assert.Equal(10.0, clip1.TimelineEndSeconds);
            Assert.Equal(10.0, clip2.TimelineStartSeconds);
            Assert.Equal(15.0, clip2.TimelineEndSeconds);
            Assert.Equal(15.0, clip3.TimelineStartSeconds);
            Assert.Equal(23.0, clip3.TimelineEndSeconds);
        }

        [Fact]
        public void AddClip_首个片段决定媒体类型()
        {
            var vm = new TimelineTrackViewModel();
            var videoClip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(videoClip);

            Assert.Equal("video", vm.AllowedMediaType);
        }

        [Fact]
        public void AddClip_不同类型_拒绝添加()
        {
            var vm = new TimelineTrackViewModel();
            var videoClip = CreateClip("a.mp4", 0, 10, "video");
            var audioClip = CreateClip("b.mp3", 0, 5, "audio");

            vm.AddClip(videoClip);
            bool result = vm.AddClip(audioClip);

            Assert.False(result);
            Assert.Equal(1, vm.ClipCount);
        }

        [Fact]
        public void AddClip_同类型_允许添加()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp3", 0, 10, "audio");
            var clip2 = CreateClip("b.mp3", 0, 5, "audio");

            vm.AddClip(clip1);
            bool result = vm.AddClip(clip2);

            Assert.True(result);
            Assert.Equal(2, vm.ClipCount);
        }

        [Fact]
        public void AddClip_类型比较忽略大小写()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "Video");
            var clip2 = CreateClip("b.mp4", 0, 5, "VIDEO");

            vm.AddClip(clip1);
            bool result = vm.AddClip(clip2);

            Assert.True(result);
        }

        #endregion

        #region InsertClip 测试

        [Fact]
        public void InsertClip_开头插入()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");

            vm.AddClip(clip1);
            vm.InsertClip(0, clip2);

            Assert.Equal(2, vm.ClipCount);
            Assert.Equal(0.0, clip2.TimelineStartSeconds);
            Assert.Equal(5.0, clip1.TimelineStartSeconds);
        }

        [Fact]
        public void InsertClip_中间插入()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            var clip3 = CreateClip("c.mp4", 0, 8, "video");

            vm.AddClip(clip1);
            vm.AddClip(clip2);
            vm.InsertClip(1, clip3);

            Assert.Equal(3, vm.ClipCount);
            Assert.Equal(0.0, clip1.TimelineStartSeconds);
            Assert.Equal(10.0, clip3.TimelineStartSeconds);
            Assert.Equal(18.0, clip2.TimelineStartSeconds);
        }

        [Fact]
        public void InsertClip_Null参数_抛异常()
        {
            var vm = new TimelineTrackViewModel();
            Assert.Throws<ArgumentNullException>(() => vm.InsertClip(0, null));
        }

        [Fact]
        public void InsertClip_非法索引_抛异常()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            Assert.Throws<ArgumentOutOfRangeException>(() => vm.InsertClip(-1, clip));
            Assert.Throws<ArgumentOutOfRangeException>(() => vm.InsertClip(1, clip));
        }

        [Fact]
        public void InsertClip_类型不匹配_拒绝()
        {
            var vm = new TimelineTrackViewModel();
            var videoClip = CreateClip("a.mp4", 0, 10, "video");
            var audioClip = CreateClip("b.mp3", 0, 5, "audio");

            vm.AddClip(videoClip);
            bool result = vm.InsertClip(0, audioClip);

            Assert.False(result);
            Assert.Equal(1, vm.ClipCount);
        }

        #endregion

        #region RemoveClip 测试

        [Fact]
        public void RemoveClip_删除成功()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");

            vm.AddClip(clip1);
            vm.AddClip(clip2);

            bool result = vm.RemoveClip(clip1);

            Assert.True(result);
            Assert.Equal(1, vm.ClipCount);
            Assert.Equal(0.0, clip2.TimelineStartSeconds); // 重新计算位置
            Assert.Equal(5.0, vm.TotalDurationSeconds);
        }

        [Fact]
        public void RemoveClip_删除选中片段_清除选中状态()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(clip);
            vm.SelectedClip = clip;

            vm.RemoveClip(clip);

            Assert.Null(vm.SelectedClip);
        }

        [Fact]
        public void RemoveClip_Null_返回false()
        {
            var vm = new TimelineTrackViewModel();
            Assert.False(vm.RemoveClip(null));
        }

        [Fact]
        public void RemoveClip_不存在片段_返回false()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            Assert.False(vm.RemoveClip(clip));
        }

        [Fact]
        public void RemoveClip_清空后重置媒体类型()
        {
            var vm = new TimelineTrackViewModel();
            var videoClip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(videoClip);

            Assert.Equal("video", vm.AllowedMediaType);

            vm.RemoveClip(videoClip);
            Assert.Null(vm.AllowedMediaType);

            // 现在可以添加音频类型
            var audioClip = CreateClip("b.mp3", 0, 5, "audio");
            bool result = vm.AddClip(audioClip);
            Assert.True(result);
            Assert.Equal("audio", vm.AllowedMediaType);
        }

        #endregion

        #region RemoveClipAt 测试

        [Fact]
        public void RemoveClipAt_有效索引()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(clip);

            Assert.True(vm.RemoveClipAt(0));
            Assert.Equal(0, vm.ClipCount);
        }

        [Fact]
        public void RemoveClipAt_无效索引_返回false()
        {
            var vm = new TimelineTrackViewModel();
            Assert.False(vm.RemoveClipAt(-1));
            Assert.False(vm.RemoveClipAt(0));
        }

        #endregion

        #region MoveClip 测试

        [Fact]
        public void MoveClip_前移()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            var clip3 = CreateClip("c.mp4", 0, 8, "video");

            vm.AddClip(clip1);
            vm.AddClip(clip2);
            vm.AddClip(clip3);

            // 将 clip3 (index=2) 移到 index=0
            vm.MoveClip(2, 0);

            var clips = vm.GetClipList();
            Assert.Equal(clip3, clips[0]);
            Assert.Equal(clip1, clips[1]);
            Assert.Equal(clip2, clips[2]);

            // 验证无缝衔接
            Assert.Equal(0.0, clip3.TimelineStartSeconds);
            Assert.Equal(8.0, clip1.TimelineStartSeconds);
            Assert.Equal(18.0, clip2.TimelineStartSeconds);
        }

        [Fact]
        public void MoveClip_后移()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            var clip3 = CreateClip("c.mp4", 0, 8, "video");

            vm.AddClip(clip1);
            vm.AddClip(clip2);
            vm.AddClip(clip3);

            // 将 clip1 (index=0) 移到 index=2
            vm.MoveClip(0, 2);

            var clips = vm.GetClipList();
            Assert.Equal(clip2, clips[0]);
            Assert.Equal(clip3, clips[1]);
            Assert.Equal(clip1, clips[2]);
        }

        [Fact]
        public void MoveClip_相同位置_返回true()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(clip);

            Assert.True(vm.MoveClip(0, 0));
        }

        [Fact]
        public void MoveClip_无效索引_返回false()
        {
            var vm = new TimelineTrackViewModel();
            Assert.False(vm.MoveClip(-1, 0));
            Assert.False(vm.MoveClip(0, 0)); // 无片段
        }

        [Fact]
        public void MoveClip_新位置无效_返回false()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(clip);

            Assert.False(vm.MoveClip(0, 5)); // 超出范围
        }

        #endregion

        #region 选中测试

        [Fact]
        public void SelectClipAt_选中片段()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            vm.AddClip(clip1);
            vm.AddClip(clip2);

            vm.SelectClipAt(1);

            Assert.Equal(clip2, vm.SelectedClip);
            Assert.True(clip2.IsSelected);
            Assert.False(clip1.IsSelected);
        }

        [Fact]
        public void SelectClipAt_切换选中_取消前一个()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            vm.AddClip(clip1);
            vm.AddClip(clip2);

            vm.SelectClipAt(0);
            Assert.True(clip1.IsSelected);

            vm.SelectClipAt(1);
            Assert.False(clip1.IsSelected);
            Assert.True(clip2.IsSelected);
        }

        [Fact]
        public void SelectClipAt_无效索引_不抛异常()
        {
            var vm = new TimelineTrackViewModel();
            vm.SelectClipAt(-1); // 不应抛异常
            vm.SelectClipAt(0);  // 不应抛异常
        }

        [Fact]
        public void SelectClipById_查找并选中()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            vm.AddClip(clip1);
            vm.AddClip(clip2);

            vm.SelectClipById(clip2.Id);
            Assert.Equal(clip2, vm.SelectedClip);
        }

        [Fact]
        public void ClearSelection_清除选中()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(clip);
            vm.SelectedClip = clip;

            vm.ClearSelection();

            Assert.Null(vm.SelectedClip);
            Assert.False(clip.IsSelected);
        }

        #endregion

        #region ClearAll 测试

        [Fact]
        public void ClearAll_清空所有()
        {
            var vm = new TimelineTrackViewModel();
            vm.AddClip(CreateClip("a.mp4", 0, 10, "video"));
            vm.AddClip(CreateClip("b.mp4", 0, 5, "video"));
            vm.SelectClipAt(0);

            vm.ClearAll();

            Assert.Equal(0, vm.ClipCount);
            Assert.Equal(0.0, vm.TotalDurationSeconds);
            Assert.Null(vm.SelectedClip);
            Assert.Null(vm.AllowedMediaType);
            Assert.Equal("空轨道", vm.TrackInfoText);
        }

        #endregion

        #region HitTestAtTime 测试

        [Fact]
        public void HitTestAtTime_命中片段()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            vm.AddClip(clip1);
            vm.AddClip(clip2);

            Assert.Equal(clip1, vm.HitTestAtTime(5.0));
            Assert.Equal(clip2, vm.HitTestAtTime(12.0));
        }

        [Fact]
        public void HitTestAtTime_边界测试()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            vm.AddClip(clip1);
            vm.AddClip(clip2);

            // 精确起始位置
            Assert.Equal(clip1, vm.HitTestAtTime(0.0));
            Assert.Equal(clip2, vm.HitTestAtTime(10.0));

            // 精确结束位置（不包含）
            Assert.Equal(clip2, vm.HitTestAtTime(10.0)); // clip2 起始
            Assert.Null(vm.HitTestAtTime(15.0)); // clip2 结束后
        }

        [Fact]
        public void HitTestAtTime_未命中()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(clip);

            Assert.Null(vm.HitTestAtTime(-1.0));
            Assert.Null(vm.HitTestAtTime(10.0)); // 精确结束位置不包含
            Assert.Null(vm.HitTestAtTime(20.0));
        }

        [Fact]
        public void HitTestAtTime_空轨道()
        {
            var vm = new TimelineTrackViewModel();
            Assert.Null(vm.HitTestAtTime(0.0));
        }

        #endregion

        #region IndexOf 测试

        [Fact]
        public void IndexOf_存在片段()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            vm.AddClip(clip1);
            vm.AddClip(clip2);

            Assert.Equal(0, vm.IndexOf(clip1));
            Assert.Equal(1, vm.IndexOf(clip2));
        }

        [Fact]
        public void IndexOf_不存在片段_返回负一()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            Assert.Equal(-1, vm.IndexOf(clip));
        }

        #endregion

        #region DeleteSelectedCommand 测试

        [Fact]
        public void DeleteSelectedCommand_删除选中片段()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(clip);
            vm.SelectedClip = clip;

            vm.DeleteSelectedCommand.Execute(null);

            Assert.Equal(0, vm.ClipCount);
            Assert.Null(vm.SelectedClip);
        }

        [Fact]
        public void DeleteSelectedCommand_无选中_不执行()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(clip);

            // CanExecute 应返回 false
            Assert.False(vm.DeleteSelectedCommand.CanExecute(null));
        }

        #endregion

        #region TrackInfoText 测试

        [Fact]
        public void TrackInfoText_空轨道()
        {
            var vm = new TimelineTrackViewModel();
            Assert.Equal("空轨道", vm.TrackInfoText);
        }

        [Fact]
        public void TrackInfoText_有片段()
        {
            var vm = new TimelineTrackViewModel();
            vm.AddClip(CreateClip("a.mp4", 0, 10, "video"));
            vm.AddClip(CreateClip("b.mp4", 0, 5.5, "video"));

            // 2 个片段 | 15.5秒
            Assert.True(vm.TrackInfoText.Contains("2 个片段"));
            Assert.True(vm.TrackInfoText.Contains("00:00:15.500"));
        }

        #endregion

        #region 属性变更通知测试

        [Fact]
        public void 属性通知_ClipCount()
        {
            var vm = new TimelineTrackViewModel();
            var changedProps = new List<string>();
            vm.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName);

            vm.AddClip(CreateClip("a.mp4", 0, 10, "video"));

            Assert.Contains("ClipCount", changedProps);
        }

        [Fact]
        public void 属性通知_TotalDurationSeconds()
        {
            var vm = new TimelineTrackViewModel();
            var changedProps = new List<string>();
            vm.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName);

            vm.AddClip(CreateClip("a.mp4", 0, 10, "video"));

            Assert.Contains("TotalDurationSeconds", changedProps);
        }

        [Fact]
        public void 属性通知_SelectedClip()
        {
            var vm = new TimelineTrackViewModel();
            var clip = CreateClip("a.mp4", 0, 10, "video");
            vm.AddClip(clip);

            var changedProps = new List<string>();
            vm.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName);

            vm.SelectedClip = clip;

            Assert.Contains("SelectedClip", changedProps);
        }

        [Fact]
        public void 属性通知_TrackInfoText()
        {
            var vm = new TimelineTrackViewModel();
            var changedProps = new List<string>();
            vm.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName);

            vm.AddClip(CreateClip("a.mp4", 0, 10, "video"));

            Assert.Contains("TrackInfoText", changedProps);
        }

        #endregion

        #region GetClipList 测试

        [Fact]
        public void GetClipList_返回副本()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            vm.AddClip(clip1);
            vm.AddClip(clip2);

            var list = vm.GetClipList();
            Assert.Equal(2, list.Count);
            Assert.Equal(clip1, list[0]);
            Assert.Equal(clip2, list[1]);

            // 修改副本不影响原集合
            list.Clear();
            Assert.Equal(2, vm.ClipCount);
        }

        #endregion

        #region 复杂场景测试

        [Fact]
        public void 三个片段_删除中间_重新计算位置()
        {
            var vm = new TimelineTrackViewModel();
            var clip1 = CreateClip("a.mp4", 0, 10, "video");
            var clip2 = CreateClip("b.mp4", 0, 5, "video");
            var clip3 = CreateClip("c.mp4", 0, 8, "video");

            vm.AddClip(clip1);
            vm.AddClip(clip2);
            vm.AddClip(clip3);

            vm.RemoveClip(clip2);

            Assert.Equal(2, vm.ClipCount);
            Assert.Equal(0.0, clip1.TimelineStartSeconds);
            Assert.Equal(10.0, clip3.TimelineStartSeconds);
            Assert.Equal(18.0, vm.TotalDurationSeconds);
        }

        [Fact]
        public void 移动后_总时长不变()
        {
            var vm = new TimelineTrackViewModel();
            vm.AddClip(CreateClip("a.mp4", 0, 10, "video"));
            vm.AddClip(CreateClip("b.mp4", 0, 5, "video"));
            vm.AddClip(CreateClip("c.mp4", 0, 8, "video"));

            double totalBefore = vm.TotalDurationSeconds;
            vm.MoveClip(0, 2);
            Assert.Equal(totalBefore, vm.TotalDurationSeconds);
        }

        [Fact]
        public void 带裁剪信息的片段_正确计算时长()
        {
            var vm = new TimelineTrackViewModel();
            // 片段只使用源文件的一部分
            var clip1 = CreateClip("a.mp4", 5.0, 15.0, "video"); // 10秒
            var clip2 = CreateClip("b.mp4", 10.0, 20.0, "video"); // 10秒

            vm.AddClip(clip1);
            vm.AddClip(clip2);

            Assert.Equal(20.0, vm.TotalDurationSeconds);
            Assert.Equal(0.0, clip1.TimelineStartSeconds);
            Assert.Equal(10.0, clip2.TimelineStartSeconds);
        }

        #endregion

        #region 帮助方法

        private TimelineClip CreateClip(string filePath, double sourceStart, double sourceEnd, string mediaType)
        {
            var clip = new TimelineClip();
            clip.SourceFilePath = filePath;
            clip.DisplayName = System.IO.Path.GetFileName(filePath);
            clip.SourceStartSeconds = sourceStart;
            clip.SourceEndSeconds = sourceEnd;
            clip.MediaType = mediaType;
            return clip;
        }

        #endregion
    }
}
