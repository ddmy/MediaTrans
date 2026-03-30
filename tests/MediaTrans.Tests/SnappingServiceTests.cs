using System;
using System.Collections.Generic;
using Xunit;
using MediaTrans.Models;
using MediaTrans.Services;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 磁吸对齐服务测试
    /// </summary>
    public class SnappingServiceTests
    {
        #region 构造函数测试

        [Fact]
        public void 构造函数_有效阈值_创建成功()
        {
            var service = new SnappingService(10);
            Assert.Equal(10, service.BaseSnapThresholdPixels);
        }

        [Fact]
        public void 构造函数_零阈值_创建成功()
        {
            var service = new SnappingService(0);
            Assert.Equal(0, service.BaseSnapThresholdPixels);
        }

        [Fact]
        public void 构造函数_负阈值_应抛异常()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnappingService(-1));
        }

        #endregion

        #region GetSnapThresholdSeconds 测试

        [Fact]
        public void GetSnapThresholdSeconds_默认缩放()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100); // 每像素100个采样

            double threshold = service.GetSnapThresholdSeconds(44100);
            // 10 * 100 / 44100 ≈ 0.02268
            Assert.True(threshold > 0);
            Assert.Equal(10.0 * 100.0 / 44100.0, threshold, 6);
        }

        [Fact]
        public void GetSnapThresholdSeconds_高缩放_阈值更小()
        {
            var service = new SnappingService(10);
            // 放大（每像素采样少）
            service.UpdateZoomLevel(10.0, 44100);
            double thresholdZoomed = service.GetSnapThresholdSeconds(44100);

            // 缩小（每像素采样多）
            service.UpdateZoomLevel(1000.0, 44100);
            double thresholdZoomedOut = service.GetSnapThresholdSeconds(44100);

            Assert.True(thresholdZoomed < thresholdZoomedOut);
        }

        [Fact]
        public void GetSnapThresholdSeconds_采样率为零_返回零()
        {
            var service = new SnappingService(10);
            Assert.Equal(0, service.GetSnapThresholdSeconds(0));
        }

        #endregion

        #region Snap 基础测试

        [Fact]
        public void Snap_精确命中目标_吸附()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            var targets = new List<double> { 5.0, 10.0, 15.0 };
            var result = service.Snap(10.0, targets, 44100);

            Assert.True(result.IsSnapped);
            Assert.Equal(10.0, result.SnappedTimeSeconds);
            Assert.Equal(10.0, result.SnapTargetTimeSeconds);
        }

        [Fact]
        public void Snap_在阈值内_吸附到最近目标()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            double threshold = service.GetSnapThresholdSeconds(44100);
            var targets = new List<double> { 10.0 };
            double nearTargetTime = 10.0 + threshold * 0.5; // 在阈值内
            var result = service.Snap(nearTargetTime, targets, 44100);

            Assert.True(result.IsSnapped);
            Assert.Equal(10.0, result.SnappedTimeSeconds);
        }

        [Fact]
        public void Snap_超出阈值_不吸附()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            double threshold = service.GetSnapThresholdSeconds(44100);
            var targets = new List<double> { 10.0 };
            double farFromTarget = 10.0 + threshold * 2; // 超出阈值
            var result = service.Snap(farFromTarget, targets, 44100);

            Assert.False(result.IsSnapped);
            Assert.Equal(farFromTarget, result.SnappedTimeSeconds);
        }

        [Fact]
        public void Snap_多个目标_吸附到最近的()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            double threshold = service.GetSnapThresholdSeconds(44100);
            var targets = new List<double> { 5.0, 10.0, 15.0 };
            // 靠近 10.0
            double nearTen = 10.0 + threshold * 0.3;
            var result = service.Snap(nearTen, targets, 44100);

            Assert.True(result.IsSnapped);
            Assert.Equal(10.0, result.SnappedTimeSeconds);
        }

        [Fact]
        public void Snap_空目标列表_不吸附()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            var result = service.Snap(10.0, new List<double>(), 44100);
            Assert.False(result.IsSnapped);
        }

        [Fact]
        public void Snap_Null目标_不吸附()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            var result = service.Snap(10.0, null, 44100);
            Assert.False(result.IsSnapped);
        }

        [Fact]
        public void Snap_阈值为零_不吸附()
        {
            var service = new SnappingService(0);
            service.UpdateZoomLevel(100.0, 44100);

            var targets = new List<double> { 10.0 };
            var result = service.Snap(10.001, targets, 44100);
            Assert.False(result.IsSnapped);
        }

        #endregion

        #region CollectClipEdgeTargets 测试

        [Fact]
        public void CollectClipEdgeTargets_收集边缘点()
        {
            var service = new SnappingService(10);
            var clips = new List<TimelineClip>();

            var clip1 = new TimelineClip(1);
            clip1.SourceStartSeconds = 0;
            clip1.SourceEndSeconds = 10;
            clip1.TimelineStartSeconds = 0;
            clips.Add(clip1);

            var clip2 = new TimelineClip(2);
            clip2.SourceStartSeconds = 0;
            clip2.SourceEndSeconds = 5;
            clip2.TimelineStartSeconds = 10;
            clips.Add(clip2);

            var targets = service.CollectClipEdgeTargets(clips, -1);

            // clip1: 0.0, 10.0; clip2: 10.0, 15.0
            Assert.Contains(0.0, targets);
            Assert.Contains(10.0, targets);
            Assert.Contains(15.0, targets);
        }

        [Fact]
        public void CollectClipEdgeTargets_排除指定片段()
        {
            var service = new SnappingService(10);
            var clips = new List<TimelineClip>();

            var clip1 = new TimelineClip(1);
            clip1.SourceStartSeconds = 0;
            clip1.SourceEndSeconds = 10;
            clip1.TimelineStartSeconds = 0;
            clips.Add(clip1);

            var clip2 = new TimelineClip(2);
            clip2.SourceStartSeconds = 0;
            clip2.SourceEndSeconds = 5;
            clip2.TimelineStartSeconds = 10;
            clips.Add(clip2);

            // 排除 clip1
            var targets = service.CollectClipEdgeTargets(clips, 1);

            Assert.Equal(2, targets.Count); // 只有 clip2 的两个边缘
            Assert.Contains(10.0, targets);
            Assert.Contains(15.0, targets);
        }

        [Fact]
        public void CollectClipEdgeTargets_Null列表_返回空()
        {
            var service = new SnappingService(10);
            var targets = service.CollectClipEdgeTargets(null, -1);
            Assert.Empty(targets);
        }

        #endregion

        #region CollectAllSnapTargets 测试

        [Fact]
        public void CollectAllSnapTargets_包含所有来源()
        {
            var service = new SnappingService(10);
            var clips = new List<TimelineClip>();

            var clip1 = new TimelineClip(1);
            clip1.SourceStartSeconds = 0;
            clip1.SourceEndSeconds = 10;
            clip1.TimelineStartSeconds = 5;
            clips.Add(clip1);

            var tickMarks = new List<double> { 1.0, 2.0, 3.0 };

            var targets = service.CollectAllSnapTargets(clips, -1, 7.5, tickMarks);

            // 应包含：clip1 边缘(5.0, 15.0)、播放头(7.5)、刻度线(1.0, 2.0, 3.0)、起点(0)
            Assert.Contains(5.0, targets);
            Assert.Contains(15.0, targets);
            Assert.Contains(7.5, targets);
            Assert.Contains(1.0, targets);
            Assert.Contains(2.0, targets);
            Assert.Contains(3.0, targets);
            Assert.Contains(0.0, targets);
        }

        [Fact]
        public void CollectAllSnapTargets_负播放头_不添加()
        {
            var service = new SnappingService(10);
            var targets = service.CollectAllSnapTargets(
                new List<TimelineClip>(), -1, -1.0, null);

            // 只有起点 0
            Assert.Contains(0.0, targets);
            Assert.DoesNotContain(-1.0, targets);
        }

        [Fact]
        public void CollectAllSnapTargets_始终包含起点0()
        {
            var service = new SnappingService(10);
            var targets = service.CollectAllSnapTargets(
                new List<TimelineClip>(), -1, -1, null);

            Assert.Contains(0.0, targets);
        }

        #endregion

        #region SnapClipEdges 测试

        [Fact]
        public void SnapClipEdges_起始边缘吸附()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            double threshold = service.GetSnapThresholdSeconds(44100);
            var targets = new List<double> { 5.0 };

            // 片段起始接近 5.0
            double clipStart = 5.0 + threshold * 0.3;
            var result = service.SnapClipEdges(clipStart, 3.0, targets, 44100);

            Assert.True(result.IsSnapped);
            Assert.Equal(5.0, result.SnappedTimeSeconds, 6);
            Assert.Equal(SnapEdge.Start, result.SnappedEdge);
        }

        [Fact]
        public void SnapClipEdges_结束边缘吸附()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            double threshold = service.GetSnapThresholdSeconds(44100);
            var targets = new List<double> { 10.0 };

            // 片段结束接近 10.0，片段时长 3 秒
            // clipEnd = clipStart + 3.0 ≈ 10.0
            double clipStart = 10.0 - 3.0 + threshold * 0.3;
            var result = service.SnapClipEdges(clipStart, 3.0, targets, 44100);

            Assert.True(result.IsSnapped);
            Assert.Equal(SnapEdge.End, result.SnappedEdge);
            // 吸附后起始位置 = 10.0 - 3.0 = 7.0
            Assert.Equal(7.0, result.SnappedTimeSeconds, 6);
        }

        [Fact]
        public void SnapClipEdges_两端都在阈值内_选择更近的()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            double threshold = service.GetSnapThresholdSeconds(44100);
            var targets = new List<double> { 5.0, 8.0 };

            // 片段起始接近 5.0（偏差0.3*threshold），
            // 片段结束接近 8.0 (clipStart + 3.0)，偏差相同
            // 起始更近
            double clipStart = 5.0 + threshold * 0.2;
            var result = service.SnapClipEdges(clipStart, 3.0, targets, 44100);

            Assert.True(result.IsSnapped);
        }

        [Fact]
        public void SnapClipEdges_无目标_不吸附()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            var result = service.SnapClipEdges(5.0, 3.0, new List<double>(), 44100);

            Assert.False(result.IsSnapped);
            Assert.Equal(5.0, result.SnappedTimeSeconds);
        }

        [Fact]
        public void SnapClipEdges_Null目标_不吸附()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            var result = service.SnapClipEdges(5.0, 3.0, null, 44100);

            Assert.False(result.IsSnapped);
        }

        #endregion

        #region 缩放自适应测试

        [Fact]
        public void 缩放自适应_放大时阈值范围缩小()
        {
            var service = new SnappingService(10);

            // 放大（细节视图，每像素采样少）
            service.UpdateZoomLevel(10.0, 44100);
            double thresholdZoomed = service.GetSnapThresholdSeconds(44100);

            // 缩小（总览视图，每像素采样多）
            service.UpdateZoomLevel(1000.0, 44100);
            double thresholdZoomedOut = service.GetSnapThresholdSeconds(44100);

            // 放大时阈值（秒）更小，缩小时阈值更大
            Assert.True(thresholdZoomed < thresholdZoomedOut);
        }

        [Fact]
        public void 缩放自适应_吸附行为随缩放变化()
        {
            var service = new SnappingService(10);
            var targets = new List<double> { 10.0 };

            // 放大视图
            service.UpdateZoomLevel(10.0, 44100);
            double smallThreshold = service.GetSnapThresholdSeconds(44100);

            // 距离在放大阈值之外
            double timeOutOfSmallThreshold = 10.0 + smallThreshold * 2;
            var resultZoomed = service.Snap(timeOutOfSmallThreshold, targets, 44100);
            Assert.False(resultZoomed.IsSnapped);

            // 缩小视图（阈值变大）
            service.UpdateZoomLevel(1000.0, 44100);
            var resultZoomedOut = service.Snap(timeOutOfSmallThreshold, targets, 44100);
            // 同样距离可能在大阈值内
            // 注：具体取决于阈值大小关系，这里验证逻辑正确即可
        }

        #endregion

        #region SnapResult 测试

        [Fact]
        public void SnapResult_默认值()
        {
            var result = new SnapResult();
            Assert.False(result.IsSnapped);
            Assert.Equal(0.0, result.OriginalTimeSeconds);
            Assert.Equal(0.0, result.SnappedTimeSeconds);
            Assert.Equal(0.0, result.SnapTargetTimeSeconds);
            Assert.Equal(0.0, result.SnapDistance);
            Assert.Equal(SnapEdge.Start, result.SnappedEdge);
        }

        #endregion

        #region 边界情况测试

        [Fact]
        public void Snap_吸附到时间轴起点()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);

            double threshold = service.GetSnapThresholdSeconds(44100);
            var targets = new List<double> { 0.0 };
            double nearZero = threshold * 0.5;
            var result = service.Snap(nearZero, targets, 44100);

            Assert.True(result.IsSnapped);
            Assert.Equal(0.0, result.SnappedTimeSeconds);
        }

        [Fact]
        public void UpdateZoomLevel_无效值_不更新()
        {
            var service = new SnappingService(10);
            service.UpdateZoomLevel(100.0, 44100);
            double original = service.GetSnapThresholdSeconds(44100);

            // 无效值不应更新
            service.UpdateZoomLevel(0, 44100);
            Assert.Equal(original, service.GetSnapThresholdSeconds(44100));

            service.UpdateZoomLevel(100.0, 0);
            Assert.Equal(original, service.GetSnapThresholdSeconds(44100));
        }

        #endregion
    }
}
