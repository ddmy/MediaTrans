using System;

namespace MediaTrans.Services
{
    /// <summary>
    /// FFmpeg 执行结果
    /// </summary>
    public class FFmpegResult
    {
        /// <summary>
        /// 是否执行成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 退出码
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// 标准输出内容
        /// </summary>
        public string StandardOutput { get; set; }

        /// <summary>
        /// 标准错误输出内容
        /// </summary>
        public string StandardError { get; set; }

        /// <summary>
        /// 是否被用户取消
        /// </summary>
        public bool Cancelled { get; set; }

        /// <summary>
        /// 是否超时
        /// </summary>
        public bool TimedOut { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 执行耗时
        /// </summary>
        public TimeSpan Duration { get; set; }
    }
}
