using System;
using System.Collections.Generic;
using System.IO;

namespace MediaTrans.Models
{
    /// <summary>
    /// 转换任务模型
    /// </summary>
    public class ConversionTask
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 源文件信息
        /// </summary>
        public MediaFileInfo SourceFile { get; set; }

        /// <summary>
        /// 输出文件路径
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// 目标格式扩展名（如 ".mp4"）
        /// </summary>
        public string TargetFormat { get; set; }

        /// <summary>
        /// 使用的预设（可为 null 表示自定义参数）
        /// </summary>
        public ConversionPreset Preset { get; set; }

        /// <summary>
        /// 转换状态
        /// </summary>
        public ConversionStatus Status { get; set; }

        /// <summary>
        /// 当前进度百分比（0-100）
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        public ConversionTask()
        {
            Id = Guid.NewGuid().ToString("N");
            Status = ConversionStatus.Pending;
            StatusText = "等待中";
        }
    }

    /// <summary>
    /// 转换状态枚举
    /// </summary>
    public enum ConversionStatus
    {
        /// <summary>
        /// 等待执行
        /// </summary>
        Pending,

        /// <summary>
        /// 正在转换
        /// </summary>
        Converting,

        /// <summary>
        /// 转换完成
        /// </summary>
        Completed,

        /// <summary>
        /// 转换失败
        /// </summary>
        Failed,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled
    }
}
