using System;
using System.Collections.Generic;
using MediaTrans.Models;

namespace MediaTrans.Services
{
    /// <summary>
    /// 磁吸对齐服务
    /// 提供片段拖拽时的自动吸附功能（片段边缘、播放头、时间刻度线）
    /// </summary>
    public class SnappingService
    {
        private readonly int _baseSnapThresholdPixels;
        private double _samplesPerPixel;

        /// <summary>
        /// 创建磁吸对齐服务
        /// </summary>
        /// <param name="snapThresholdPixels">基础吸附阈值（像素）</param>
        public SnappingService(int snapThresholdPixels)
        {
            if (snapThresholdPixels < 0)
            {
                throw new ArgumentOutOfRangeException("snapThresholdPixels", "吸附阈值不能为负数");
            }

            _baseSnapThresholdPixels = snapThresholdPixels;
            _samplesPerPixel = 1.0;
        }

        /// <summary>
        /// 基础吸附阈值（像素）
        /// </summary>
        public int BaseSnapThresholdPixels
        {
            get { return _baseSnapThresholdPixels; }
        }

        /// <summary>
        /// 更新缩放级别（影响吸附阈值的时间转换）
        /// </summary>
        /// <param name="samplesPerPixel">每像素采样数</param>
        /// <param name="sampleRate">采样率</param>
        public void UpdateZoomLevel(double samplesPerPixel, int sampleRate)
        {
            if (samplesPerPixel > 0 && sampleRate > 0)
            {
                _samplesPerPixel = samplesPerPixel;
            }
        }

        /// <summary>
        /// 获取当前吸附阈值（秒，随缩放自适应）
        /// </summary>
        /// <param name="sampleRate">采样率</param>
        /// <returns>阈值（秒）</returns>
        public double GetSnapThresholdSeconds(int sampleRate)
        {
            if (sampleRate <= 0 || _samplesPerPixel <= 0)
            {
                return 0;
            }
            // 阈值像素 × 每像素对应的时间
            return (_baseSnapThresholdPixels * _samplesPerPixel) / sampleRate;
        }

        /// <summary>
        /// 对给定时间位置执行磁吸对齐
        /// </summary>
        /// <param name="timeSeconds">原始时间位置（秒）</param>
        /// <param name="snapTargets">吸附目标时间点列表（秒）</param>
        /// <param name="sampleRate">采样率</param>
        /// <returns>吸附结果</returns>
        public SnapResult Snap(double timeSeconds, List<double> snapTargets, int sampleRate)
        {
            var result = new SnapResult();
            result.OriginalTimeSeconds = timeSeconds;
            result.SnappedTimeSeconds = timeSeconds;
            result.IsSnapped = false;
            result.SnapTargetTimeSeconds = 0;

            if (snapTargets == null || snapTargets.Count == 0)
            {
                return result;
            }

            double threshold = GetSnapThresholdSeconds(sampleRate);
            if (threshold <= 0)
            {
                return result;
            }

            double closestDistance = double.MaxValue;
            double closestTarget = timeSeconds;

            for (int i = 0; i < snapTargets.Count; i++)
            {
                double distance = Math.Abs(timeSeconds - snapTargets[i]);
                if (distance < closestDistance && distance <= threshold)
                {
                    closestDistance = distance;
                    closestTarget = snapTargets[i];
                }
            }

            if (closestDistance <= threshold && closestDistance < double.MaxValue)
            {
                result.SnappedTimeSeconds = closestTarget;
                result.IsSnapped = true;
                result.SnapTargetTimeSeconds = closestTarget;
                result.SnapDistance = closestDistance;
            }

            return result;
        }

        /// <summary>
        /// 收集片段边缘的吸附目标点
        /// </summary>
        /// <param name="clips">片段列表</param>
        /// <param name="excludeClipId">要排除的片段 ID（拖动中的片段）</param>
        /// <returns>吸附目标时间点列表</returns>
        public List<double> CollectClipEdgeTargets(IList<TimelineClip> clips, int excludeClipId)
        {
            var targets = new List<double>();

            if (clips == null)
            {
                return targets;
            }

            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i].Id == excludeClipId)
                {
                    continue;
                }

                targets.Add(clips[i].TimelineStartSeconds);
                targets.Add(clips[i].TimelineEndSeconds);
            }

            return targets;
        }

        /// <summary>
        /// 收集所有吸附目标（片段边缘 + 播放头 + 时间刻度线）
        /// </summary>
        /// <param name="clips">片段列表</param>
        /// <param name="excludeClipId">要排除的片段 ID</param>
        /// <param name="playheadTimeSeconds">播放头位置（秒），负数表示无播放头</param>
        /// <param name="tickMarkTimes">时间刻度线位置列表（秒），可为 null</param>
        /// <returns>所有吸附目标时间点</returns>
        public List<double> CollectAllSnapTargets(IList<TimelineClip> clips, int excludeClipId,
            double playheadTimeSeconds, IList<double> tickMarkTimes)
        {
            var targets = CollectClipEdgeTargets(clips, excludeClipId);

            // 添加播放头位置
            if (playheadTimeSeconds >= 0)
            {
                targets.Add(playheadTimeSeconds);
            }

            // 添加时间刻度线
            if (tickMarkTimes != null)
            {
                for (int i = 0; i < tickMarkTimes.Count; i++)
                {
                    targets.Add(tickMarkTimes[i]);
                }
            }

            // 添加时间轴起点
            targets.Add(0);

            return targets;
        }

        /// <summary>
        /// 对片段拖拽进行磁吸对齐
        /// 同时检测片段的起始和结束边缘
        /// </summary>
        /// <param name="clipStartSeconds">片段起始时间（秒）</param>
        /// <param name="clipDurationSeconds">片段时长（秒）</param>
        /// <param name="snapTargets">吸附目标时间点列表</param>
        /// <param name="sampleRate">采样率</param>
        /// <returns>吸附结果（基于起始时间）</returns>
        public SnapResult SnapClipEdges(double clipStartSeconds, double clipDurationSeconds,
            List<double> snapTargets, int sampleRate)
        {
            if (snapTargets == null || snapTargets.Count == 0)
            {
                var noSnapResult = new SnapResult();
                noSnapResult.OriginalTimeSeconds = clipStartSeconds;
                noSnapResult.SnappedTimeSeconds = clipStartSeconds;
                noSnapResult.IsSnapped = false;
                return noSnapResult;
            }

            double clipEndSeconds = clipStartSeconds + clipDurationSeconds;

            // 检测起始边缘吸附
            SnapResult startResult = Snap(clipStartSeconds, snapTargets, sampleRate);

            // 检测结束边缘吸附
            SnapResult endResult = Snap(clipEndSeconds, snapTargets, sampleRate);

            // 选择距离更近的吸附
            if (startResult.IsSnapped && endResult.IsSnapped)
            {
                if (startResult.SnapDistance <= endResult.SnapDistance)
                {
                    return startResult;
                }
                else
                {
                    // 将末端吸附转换为起始位置
                    var result = new SnapResult();
                    result.OriginalTimeSeconds = clipStartSeconds;
                    result.SnappedTimeSeconds = endResult.SnapTargetTimeSeconds - clipDurationSeconds;
                    result.IsSnapped = true;
                    result.SnapTargetTimeSeconds = endResult.SnapTargetTimeSeconds;
                    result.SnapDistance = endResult.SnapDistance;
                    result.SnappedEdge = SnapEdge.End;
                    return result;
                }
            }
            else if (startResult.IsSnapped)
            {
                return startResult;
            }
            else if (endResult.IsSnapped)
            {
                var result = new SnapResult();
                result.OriginalTimeSeconds = clipStartSeconds;
                result.SnappedTimeSeconds = endResult.SnapTargetTimeSeconds - clipDurationSeconds;
                result.IsSnapped = true;
                result.SnapTargetTimeSeconds = endResult.SnapTargetTimeSeconds;
                result.SnapDistance = endResult.SnapDistance;
                result.SnappedEdge = SnapEdge.End;
                return result;
            }

            return startResult; // 未吸附
        }
    }

    /// <summary>
    /// 吸附结果
    /// </summary>
    public class SnapResult
    {
        /// <summary>
        /// 原始时间位置（秒）
        /// </summary>
        public double OriginalTimeSeconds { get; set; }

        /// <summary>
        /// 吸附后的时间位置（秒）
        /// </summary>
        public double SnappedTimeSeconds { get; set; }

        /// <summary>
        /// 是否发生吸附
        /// </summary>
        public bool IsSnapped { get; set; }

        /// <summary>
        /// 吸附目标的时间位置（秒）
        /// </summary>
        public double SnapTargetTimeSeconds { get; set; }

        /// <summary>
        /// 吸附距离（秒）
        /// </summary>
        public double SnapDistance { get; set; }

        /// <summary>
        /// 吸附的边缘类型
        /// </summary>
        public SnapEdge SnappedEdge { get; set; }
    }

    /// <summary>
    /// 吸附边缘类型
    /// </summary>
    public enum SnapEdge
    {
        /// <summary>
        /// 起始边缘
        /// </summary>
        Start,

        /// <summary>
        /// 结束边缘
        /// </summary>
        End
    }
}
