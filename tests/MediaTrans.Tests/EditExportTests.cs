using System;
using System.Collections.Generic;
using System.Globalization;
using MediaTrans.Models;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 编辑导出服务单元测试
    /// </summary>
    public class EditExportServiceTests
    {
        private readonly EditExportService _service;

        public EditExportServiceTests()
        {
            _service = new EditExportService();
        }

        #region 参数校验

        [Fact]
        public void 校验_参数为null_返回错误()
        {
            var errors = _service.ValidateParams(null);
            Assert.NotEmpty(errors);
            Assert.Contains("导出参数不能为空", errors);
        }

        [Fact]
        public void 校验_输出路径为空_返回错误()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "test.mp4",
                TargetFormat = ".mp4"
            };
            var errors = _service.ValidateParams(p);
            Assert.Contains("输出路径不能为空", errors);
        }

        [Fact]
        public void 校验_目标格式为空_返回错误()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "test.mp4",
                OutputFilePath = "out.mp4"
            };
            var errors = _service.ValidateParams(p);
            Assert.Contains("目标格式不能为空", errors);
        }

        [Fact]
        public void 校验_单文件模式源路径为空_返回错误()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "out.mp4",
                TargetFormat = ".mp4"
            };
            var errors = _service.ValidateParams(p);
            Assert.Contains("源文件路径不能为空", errors);
        }

        [Fact]
        public void 校验_裁剪起始时间为负数_返回错误()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "test.mp4",
                OutputFilePath = "out.mp4",
                TargetFormat = ".mp4",
                TrimStartSeconds = -1
            };
            var errors = _service.ValidateParams(p);
            Assert.Contains("裁剪起始时间不能为负数", errors);
        }

        [Fact]
        public void 校验_裁剪时长为0_返回错误()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "test.mp4",
                OutputFilePath = "out.mp4",
                TargetFormat = ".mp4",
                TrimDurationSeconds = 0
            };
            var errors = _service.ValidateParams(p);
            Assert.Contains("裁剪持续时长必须大于 0", errors);
        }

        [Fact]
        public void 校验_增益超范围_返回错误()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "test.mp4",
                OutputFilePath = "out.mp4",
                TargetFormat = ".mp4",
                GainDb = 25
            };
            var errors = _service.ValidateParams(p);
            Assert.True(errors.Count > 0);
        }

        [Fact]
        public void 校验_多段片段源路径为空_返回错误()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "out.mp4",
                TargetFormat = ".mp4",
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { DurationSeconds = 10 }
                }
            };
            var errors = _service.ValidateParams(p);
            Assert.True(errors.Count > 0);
        }

        [Fact]
        public void 校验_多段片段时长为0_表示整段不设t_无错误()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "out.mp4",
                TargetFormat = ".mp4",
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "a.mp4", DurationSeconds = 0 }
                }
            };
            var errors = _service.ValidateParams(p);
            Assert.Empty(errors);
        }

        [Fact]
        public void 校验_多段片段时长为负数_返回错误()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "out.mp4",
                TargetFormat = ".mp4",
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "a.mp4", DurationSeconds = -1 }
                }
            };
            var errors = _service.ValidateParams(p);
            Assert.True(errors.Count > 0);
        }

        [Fact]
        public void 校验_合法单文件参数_无错误()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "test.mp4",
                OutputFilePath = "out.mp4",
                TargetFormat = ".mp4",
                TrimStartSeconds = 5,
                TrimDurationSeconds = 10
            };
            var errors = _service.ValidateParams(p);
            Assert.Empty(errors);
        }

        [Fact]
        public void 校验_合法多段参数_无错误()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "out.mp4",
                TargetFormat = ".mp4",
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "a.mp4", StartSeconds = 0, DurationSeconds = 10 },
                    new ClipSegment { SourceFilePath = "b.mp4", StartSeconds = 5, DurationSeconds = 15 }
                }
            };
            var errors = _service.ValidateParams(p);
            Assert.Empty(errors);
        }

        #endregion

        #region 单文件裁剪导出

        [Fact]
        public void 裁剪导出_基本MP4_包含显式编解码器()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                TrimStartSeconds = 10,
                TrimDurationSeconds = 20
            };

            string args = _service.BuildTrimExportArguments(p);

            Assert.Contains("-c:v libx264", args);
            Assert.Contains("-c:a aac", args);
            Assert.Contains("-ss", args);
            Assert.Contains("-t", args);
            Assert.Contains("\"input.mp4\"", args);
            Assert.Contains("\"output.mp4\"", args);
        }

        [Fact]
        public void 裁剪导出_纯音频MP3_包含vn()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "input.wav",
                OutputFilePath = "output.mp3",
                TargetFormat = ".mp3"
            };

            string args = _service.BuildTrimExportArguments(p);

            Assert.Contains("-vn", args);
            Assert.Contains("-c:a libmp3lame", args);
            Assert.DoesNotContain("-c:v", args);
        }

        [Fact]
        public void 裁剪导出_无裁剪参数_不包含ss和t()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4"
            };

            string args = _service.BuildTrimExportArguments(p);

            Assert.DoesNotContain("-ss", args);
            Assert.DoesNotContain("-t ", args);
        }

        [Fact]
        public void 裁剪导出_带增益_包含volume滤镜()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                GainDb = 6.0
            };

            string args = _service.BuildTrimExportArguments(p);

            Assert.Contains("-af", args);
            Assert.Contains("volume=", args);
        }

        [Fact]
        public void 裁剪导出_增益为0_不包含volume()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                GainDb = 0
            };

            string args = _service.BuildTrimExportArguments(p);

            Assert.DoesNotContain("volume=", args);
        }

        [Fact]
        public void 裁剪导出_包含覆盖标志和线程()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4"
            };

            string args = _service.BuildTrimExportArguments(p);

            Assert.True(args.StartsWith("-y "));
            Assert.Contains("-threads 0", args);
        }

        [Fact]
        public void 裁剪导出_路径含空格和中文_正确引用()
        {
            var p = new EditExportParams
            {
                SourceFilePath = @"C:\我的 视频\测试 文件.mp4",
                OutputFilePath = @"C:\输出 目录\结果 文件.mp4",
                TargetFormat = ".mp4"
            };

            string args = _service.BuildTrimExportArguments(p);

            Assert.Contains("\"C:\\我的 视频\\测试 文件.mp4\"", args);
            Assert.Contains("\"C:\\输出 目录\\结果 文件.mp4\"", args);
        }

        [Fact]
        public void 裁剪导出_使用预设参数()
        {
            var preset = new ConversionPreset
            {
                VideoCodec = "libx265",
                AudioCodec = "aac",
                Width = 1920,
                Height = 1080,
                VideoBitrate = "5M",
                AudioBitrate = "192k",
                FrameRate = 30
            };

            var p = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                Preset = preset
            };

            string args = _service.BuildTrimExportArguments(p);

            Assert.Contains("-c:v libx265", args);
            Assert.Contains("-s 1920x1080", args);
            Assert.Contains("-b:v 5M", args);
            Assert.Contains("-b:a 192k", args);
            Assert.Contains("-r 30", args);
        }

        [Theory]
        [InlineData(".mp4", "-c:v libx264", "-c:a aac")]
        [InlineData(".avi", "-c:v libx264", "-c:a mp3lame")]
        [InlineData(".mkv", "-c:v libx264", "-c:a aac")]
        [InlineData(".webm", "-c:v libvpx", "-c:a libvorbis")]
        [InlineData(".wav", "-vn", "-c:a pcm_s16le")]
        [InlineData(".flac", "-vn", "-c:a flac")]
        [InlineData(".aac", "-vn", "-c:a aac")]
        [InlineData(".ogg", "-vn", "-c:a libvorbis")]
        public void 裁剪导出_各格式正确编解码器(string format, string expected1, string expected2)
        {
            var p = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = "output" + format,
                TargetFormat = format
            };

            string args = _service.BuildTrimExportArguments(p);

            Assert.Contains(expected1, args);
            Assert.Contains(expected2, args);
        }

        [Fact]
        public void 裁剪导出_不支持的格式_抛出异常()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "input.xyz",
                OutputFilePath = "output.xyz",
                TargetFormat = ".xyz"
            };

            Assert.Throws<InvalidOperationException>(() => _service.BuildTrimExportArguments(p));
        }

        #endregion

        #region 多段拼接导出

        [Fact]
        public void 拼接导出_两段视频_包含filter_complex和concat()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "a.mp4", StartSeconds = 0, DurationSeconds = 10, HasAudio = true },
                    new ClipSegment { SourceFilePath = "b.mp4", StartSeconds = 5, DurationSeconds = 15, HasAudio = true }
                }
            };

            string args = _service.BuildConcatExportArguments(p);

            Assert.Contains("-filter_complex", args);
            Assert.Contains("concat=n=2:v=1:a=1", args);
            Assert.Contains("[0:v][0:a]", args);
            Assert.Contains("[1:v][1:a]", args);
            Assert.Contains("-c:v libx264", args);
            Assert.Contains("-c:a aac", args);
        }

        [Fact]
        public void 拼接导出_三段音频_纯音频拼接()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "output.mp3",
                TargetFormat = ".mp3",
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "a.mp3", StartSeconds = 0, DurationSeconds = 30 },
                    new ClipSegment { SourceFilePath = "b.mp3", StartSeconds = 0, DurationSeconds = 20 },
                    new ClipSegment { SourceFilePath = "c.mp3", StartSeconds = 0, DurationSeconds = 10 }
                }
            };

            string args = _service.BuildConcatExportArguments(p);

            Assert.Contains("concat=n=3:v=0:a=1", args);
            Assert.Contains("[0:a]", args);
            Assert.Contains("[1:a]", args);
            Assert.Contains("[2:a]", args);
            Assert.Contains("-vn", args);
            Assert.Contains("-c:a libmp3lame", args);
        }

        [Fact]
        public void 拼接导出_带增益_包含volume滤镜()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                GainDb = 3.0,
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "a.mp4", StartSeconds = 0, DurationSeconds = 10, HasAudio = true },
                    new ClipSegment { SourceFilePath = "b.mp4", StartSeconds = 0, DurationSeconds = 10, HasAudio = true }
                }
            };

            string args = _service.BuildConcatExportArguments(p);

            Assert.Contains("volume=", args);
            Assert.Contains("concat=n=2:v=1:a=1", args);
        }

        [Fact]
        public void 拼接导出_片段带裁剪_包含ss和t()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "a.mp4", StartSeconds = 5, DurationSeconds = 10 }
                }
            };

            string args = _service.BuildConcatExportArguments(p);

            Assert.Contains("-ss", args);
            Assert.Contains("-t", args);
        }

        [Fact]
        public void 拼接导出_空片段列表_抛出异常()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                Segments = new List<ClipSegment>()
            };

            Assert.Throws<InvalidOperationException>(() => _service.BuildConcatExportArguments(p));
        }

        [Fact]
        public void 拼接导出_无音频视频_使用纯视频concat()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "a.mp4", StartSeconds = 0, DurationSeconds = 10, HasAudio = false },
                    new ClipSegment { SourceFilePath = "b.mp4", StartSeconds = 0, DurationSeconds = 10, HasAudio = false }
                }
            };

            string args = _service.BuildConcatExportArguments(p);

            Assert.Contains("concat=n=2:v=1:a=0", args);
            Assert.Contains("[0:v]", args);
            Assert.Contains("[1:v]", args);
            Assert.DoesNotContain("[0:a]", args);
            Assert.DoesNotContain("-c:a", args);
            Assert.Contains("-an", args);
            Assert.Contains("-c:v libx264", args);
        }

        #endregion

        #region 自动选择构建方式

        [Fact]
        public void 自动构建_有片段列表_使用拼接模式()
        {
            var p = new EditExportParams
            {
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "a.mp4", StartSeconds = 0, DurationSeconds = 10 },
                    new ClipSegment { SourceFilePath = "b.mp4", StartSeconds = 0, DurationSeconds = 10 }
                }
            };

            string args = _service.BuildExportArguments(p);

            Assert.Contains("filter_complex", args);
            Assert.Contains("concat", args);
        }

        [Fact]
        public void 自动构建_无片段列表_使用裁剪模式()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                TrimStartSeconds = 5,
                TrimDurationSeconds = 10
            };

            string args = _service.BuildExportArguments(p);

            Assert.DoesNotContain("filter_complex", args);
            Assert.Contains("-ss", args);
        }

        #endregion

        #region 计算总时长

        [Fact]
        public void 计算时长_单文件裁剪模式()
        {
            var p = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                TrimDurationSeconds = 30
            };

            double duration = _service.CalculateTotalDuration(p);

            Assert.Equal(30, duration);
        }

        [Fact]
        public void 计算时长_多段拼接模式()
        {
            var p = new EditExportParams
            {
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "a.mp4", DurationSeconds = 10 },
                    new ClipSegment { SourceFilePath = "b.mp4", DurationSeconds = 20 },
                    new ClipSegment { SourceFilePath = "c.mp4", DurationSeconds = 30 }
                }
            };

            double duration = _service.CalculateTotalDuration(p);

            Assert.Equal(60, duration);
        }

        [Fact]
        public void 计算时长_参数为null_返回0()
        {
            Assert.Equal(0, _service.CalculateTotalDuration(null));
        }

        #endregion

        #region 从时间轴创建导出参数

        [Fact]
        public void 从时间轴创建_单片段_单文件模式()
        {
            var clips = new List<TimelineClip>
            {
                new TimelineClip()
                {
                    SourceFilePath = "video.mp4",
                    SourceStartSeconds = 5,
                    SourceEndSeconds = 25
                }
            };

            var result = EditExportService.CreateFromTimeline(
                clips, "out.mp4", ".mp4", 3.0, null);

            Assert.Equal("video.mp4", result.SourceFilePath);
            Assert.Equal(5, result.TrimStartSeconds);
            Assert.Equal(20, result.TrimDurationSeconds);
            Assert.Equal(3.0, result.GainDb);
            Assert.Null(result.Segments);
        }

        [Fact]
        public void 从时间轴创建_多片段_多段模式()
        {
            var clips = new List<TimelineClip>
            {
                new TimelineClip()
                {
                    SourceFilePath = "a.mp4",
                    SourceStartSeconds = 0,
                    SourceEndSeconds = 10
                },
                new TimelineClip()
                {
                    SourceFilePath = "b.mp4",
                    SourceStartSeconds = 5,
                    SourceEndSeconds = 20
                }
            };

            var result = EditExportService.CreateFromTimeline(
                clips, "out.mp4", ".mp4", 0, null);

            Assert.NotNull(result.Segments);
            Assert.Equal(2, result.Segments.Count);
            Assert.Equal("a.mp4", result.Segments[0].SourceFilePath);
            Assert.Equal(10, result.Segments[0].DurationSeconds);
            Assert.Equal("b.mp4", result.Segments[1].SourceFilePath);
            Assert.Equal(5, result.Segments[1].StartSeconds);
            Assert.Equal(15, result.Segments[1].DurationSeconds);
        }

        [Fact]
        public void 从时间轴创建_空列表_抛出异常()
        {
            Assert.Throws<ArgumentException>(() =>
                EditExportService.CreateFromTimeline(
                    new List<TimelineClip>(), "out.mp4", ".mp4", 0, null));
        }

        #endregion
    }

    /// <summary>
    /// FFmpegCommandBuilder 扩展方法测试
    /// </summary>
    public class FFmpegCommandBuilderExtendedTests
    {
        [Fact]
        public void SeekStart_添加ss参数()
        {
            string args = new FFmpegCommandBuilder()
                .SeekStart(10.5)
                .Input("input.mp4")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output("output.mp4")
                .Build();

            Assert.Contains("-ss", args);
            // -ss 应该在 -i 之前
            int ssIndex = args.IndexOf("-ss");
            int iIndex = args.IndexOf("-i");
            Assert.True(ssIndex < iIndex, "ss 应在 -i 之前");
        }

        [Fact]
        public void Duration_添加t参数()
        {
            string args = new FFmpegCommandBuilder()
                .Input("input.mp4")
                .Duration(20)
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output("output.mp4")
                .Build();

            Assert.Contains("-t", args);
        }

        [Fact]
        public void FilterComplex_正确添加()
        {
            string args = new FFmpegCommandBuilder()
                .Input("a.mp4")
                .Input("b.mp4")
                .FilterComplex("[0:v][0:a][1:v][1:a]concat=n=2:v=1:a=1[v][a]")
                .Map("[v]")
                .Map("[a]")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output("output.mp4")
                .Build();

            Assert.Contains("-filter_complex", args);
            Assert.Contains("concat=n=2:v=1:a=1", args);
            Assert.Contains("-map \"[v]\"", args);
            Assert.Contains("-map \"[a]\"", args);
        }

        [Fact]
        public void AudioFilter_添加af参数()
        {
            string args = new FFmpegCommandBuilder()
                .Input("input.mp4")
                .AudioFilter("volume=2.0")
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .Output("output.mp4")
                .Build();

            Assert.Contains("-af \"volume=2.0\"", args);
        }

        [Fact]
        public void ConcatDemuxer_添加格式参数()
        {
            string args = new FFmpegCommandBuilder()
                .ConcatDemuxer()
                .Input("filelist.txt")
                .VideoCodec("copy")
                .AudioCodec("copy")
                .Output("output.mp4")
                .Build();

            Assert.Contains("-f concat", args);
            Assert.Contains("-safe 0", args);
        }

        [Fact]
        public void 组合_裁剪加增益_命令完整()
        {
            string args = new FFmpegCommandBuilder()
                .SeekStart(5)
                .Input("input.mp4")
                .Duration(10)
                .VideoCodec("libx264")
                .AudioCodec("aac")
                .AudioFilter("volume=1.5")
                .Threads(0)
                .Output("output.mp4")
                .Build();

            // 验证所有参数都存在且顺序正确
            Assert.True(args.StartsWith("-y "));
            Assert.Contains("-ss", args);
            Assert.Contains("-i \"input.mp4\"", args);
            Assert.Contains("-c:v libx264", args);
            Assert.Contains("-c:a aac", args);
            Assert.Contains("-t", args);
            Assert.Contains("-af \"volume=1.5\"", args);
            Assert.Contains("-threads 0", args);
            Assert.True(args.EndsWith("\"output.mp4\""));
        }
    }
}
