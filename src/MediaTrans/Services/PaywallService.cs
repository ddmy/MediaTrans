using System;
using System.Collections.Generic;
using MediaTrans.Models;

namespace MediaTrans.Services
{
    /// <summary>
    /// 付费墙拦截服务 — 控制免费版与专业版的功能差异
    /// 免费版：导出时长截断 + 视频水印 + 禁用无损格式
    /// 专业版：全功能解锁
    /// </summary>
    public class PaywallService
    {
        private readonly LicenseService _licenseService;
        private readonly ConfigService _configService;

        /// <summary>
        /// 无损格式列表（免费版禁用）
        /// </summary>
        private static readonly HashSet<string> _losslessFormats =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".flac", ".wav"
        };

        public PaywallService(LicenseService licenseService, ConfigService configService)
        {
            if (licenseService == null)
            {
                throw new ArgumentNullException("licenseService");
            }
            if (configService == null)
            {
                throw new ArgumentNullException("configService");
            }
            _licenseService = licenseService;
            _configService = configService;
        }

        /// <summary>
        /// 是否为专业版（已激活）
        /// </summary>
        public bool IsProfessional
        {
            get { return _licenseService.IsActivated; }
        }

        /// <summary>
        /// 检查指定格式是否允许使用
        /// 免费版禁用无损格式（FLAC/WAV）
        /// </summary>
        public bool IsFormatAllowed(string extension)
        {
            if (IsProfessional)
            {
                return true;
            }
            if (string.IsNullOrEmpty(extension))
            {
                return true;
            }
            return !_losslessFormats.Contains(extension);
        }

        /// <summary>
        /// 获取最大导出时长（秒）
        /// 专业版无限制，免费版从配置文件读取
        /// </summary>
        public int GetMaxExportSeconds()
        {
            if (IsProfessional)
            {
                return int.MaxValue;
            }
            var config = _configService.Load();
            int maxSeconds = config.FreeMaxExportSeconds;
            if (maxSeconds <= 0)
            {
                maxSeconds = 60;
            }
            return maxSeconds;
        }

        /// <summary>
        /// 判断导出是否需要截断时长
        /// </summary>
        public bool NeedsTruncation(double sourceDurationSeconds)
        {
            if (IsProfessional)
            {
                return false;
            }
            int maxSeconds = GetMaxExportSeconds();
            return sourceDurationSeconds > maxSeconds;
        }

        /// <summary>
        /// 判断是否需要添加水印（仅视频导出 + 免费版）
        /// </summary>
        public bool ShouldAddWatermark(bool isVideoExport)
        {
            if (IsProfessional)
            {
                return false;
            }
            return isVideoExport;
        }

        /// <summary>
        /// 构建水印 drawtext 滤镜字符串
        /// 位置从配置读取（当前仅支持右下角）
        /// </summary>
        public string BuildWatermarkFilter()
        {
            var config = _configService.Load();
            string text = config.WatermarkText;
            int fontSize = config.WatermarkFontSize;

            if (string.IsNullOrEmpty(text))
            {
                text = "MediaTrans";
            }
            if (fontSize <= 0)
            {
                fontSize = 24;
            }

            // 右下角位置，留 10 像素边距，半透明白色
            return string.Format(
                "drawtext=text='{0}':fontsize={1}:fontcolor=white@0.5:x=w-tw-10:y=h-th-10",
                EscapeDrawTextValue(text), fontSize);
        }

        /// <summary>
        /// 获取免费版被限制的格式列表（用于 UI 灰化）
        /// </summary>
        public List<string> GetRestrictedFormats()
        {
            if (IsProfessional)
            {
                return new List<string>();
            }
            return new List<string>(new string[] { ".flac", ".wav" });
        }

        /// <summary>
        /// 将付费墙限制应用到 FFmpeg 命令构建器
        /// 包含：时长截断 + 视频水印
        /// </summary>
        /// <param name="builder">命令构建器</param>
        /// <param name="sourceDurationSeconds">源文件时长（秒）</param>
        /// <param name="isVideoExport">是否为视频导出</param>
        public void ApplyRestrictions(FFmpegCommandBuilder builder, double sourceDurationSeconds, bool isVideoExport)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (IsProfessional)
            {
                return;
            }

            // 时长截断
            int maxSeconds = GetMaxExportSeconds();
            if (sourceDurationSeconds > maxSeconds)
            {
                builder.Duration(maxSeconds);
            }

            // 视频水印
            if (isVideoExport)
            {
                string watermarkFilter = BuildWatermarkFilter();
                builder.VideoFilter(watermarkFilter);
            }
        }

        /// <summary>
        /// 转义 drawtext 滤镜中的特殊字符
        /// </summary>
        private static string EscapeDrawTextValue(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            // drawtext 中需要转义的特殊字符：' : \
            return text
                .Replace("\\", "\\\\")
                .Replace(":", "\\:")
                .Replace("'", "'\\''");
        }
    }
}
