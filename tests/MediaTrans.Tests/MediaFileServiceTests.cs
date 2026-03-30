using System;
using Xunit;
using MediaTrans.Models;
using MediaTrans.Services;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 媒体文件服务测试
    /// </summary>
    public class MediaFileServiceTests
    {
        #region IsSupportedMediaFile 测试

        [Fact]
        public void IsSupportedMediaFile_Mp4_ReturnsTrue()
        {
            Assert.True(MediaFileService.IsSupportedMediaFile("test.mp4"));
        }

        [Fact]
        public void IsSupportedMediaFile_Avi_ReturnsTrue()
        {
            Assert.True(MediaFileService.IsSupportedMediaFile("test.avi"));
        }

        [Fact]
        public void IsSupportedMediaFile_Mkv_ReturnsTrue()
        {
            Assert.True(MediaFileService.IsSupportedMediaFile("test.mkv"));
        }

        [Fact]
        public void IsSupportedMediaFile_Mp3_ReturnsTrue()
        {
            Assert.True(MediaFileService.IsSupportedMediaFile("test.mp3"));
        }

        [Fact]
        public void IsSupportedMediaFile_Wav_ReturnsTrue()
        {
            Assert.True(MediaFileService.IsSupportedMediaFile("test.wav"));
        }

        [Fact]
        public void IsSupportedMediaFile_Flac_ReturnsTrue()
        {
            Assert.True(MediaFileService.IsSupportedMediaFile("test.flac"));
        }

        [Fact]
        public void IsSupportedMediaFile_Txt_ReturnsFalse()
        {
            Assert.False(MediaFileService.IsSupportedMediaFile("test.txt"));
        }

        [Fact]
        public void IsSupportedMediaFile_Exe_ReturnsFalse()
        {
            Assert.False(MediaFileService.IsSupportedMediaFile("test.exe"));
        }

        [Fact]
        public void IsSupportedMediaFile_Null_ReturnsFalse()
        {
            Assert.False(MediaFileService.IsSupportedMediaFile(null));
        }

        [Fact]
        public void IsSupportedMediaFile_Empty_ReturnsFalse()
        {
            Assert.False(MediaFileService.IsSupportedMediaFile(""));
        }

        [Fact]
        public void IsSupportedMediaFile_CaseInsensitive_ReturnsTrue()
        {
            Assert.True(MediaFileService.IsSupportedMediaFile("test.MP4"));
            Assert.True(MediaFileService.IsSupportedMediaFile("test.Mp3"));
        }

        #endregion

        #region FilterSupportedFiles 测试

        [Fact]
        public void FilterSupportedFiles_MixedFiles_ReturnsOnlySupported()
        {
            // 注意：FilterSupportedFiles 要求文件实际存在（File.Exists），
            // 这里仅验证返回空列表（因为假路径不存在）
            var files = new[] { "nonexistent.mp4", "nonexistent.txt" };
            var result = MediaFileService.FilterSupportedFiles(files);
            Assert.Equal(0, result.Count);
        }

        #endregion

        #region ParseFFprobeJson 测试

        [Fact]
        public void ParseFFprobeJson_VideoFile_ParsesCorrectly()
        {
            // 模拟 ffprobe JSON 输出
            string json = @"{
                ""format"": {
                    ""format_name"": ""mov,mp4,m4a,3gp,3g2,mj2"",
                    ""duration"": ""120.500000""
                },
                ""streams"": [
                    {
                        ""codec_type"": ""video"",
                        ""codec_name"": ""h264"",
                        ""width"": 1920,
                        ""height"": 1080,
                        ""r_frame_rate"": ""30000/1001"",
                        ""bit_rate"": ""5000000""
                    },
                    {
                        ""codec_type"": ""audio"",
                        ""codec_name"": ""aac"",
                        ""sample_rate"": ""44100"",
                        ""channels"": 2,
                        ""bit_rate"": ""128000""
                    }
                ]
            }";

            var info = new MediaFileInfo();
            MediaFileService.ParseFFprobeJson(json, info);

            Assert.True(info.HasVideo);
            Assert.True(info.HasAudio);
            Assert.Equal("h264", info.VideoCodec);
            Assert.Equal("aac", info.AudioCodec);
            Assert.Equal(1920, info.Width);
            Assert.Equal(1080, info.Height);
            Assert.Equal(120.5, info.DurationSeconds, 1);
            Assert.Equal(44100, info.AudioSampleRate);
            Assert.Equal(2, info.AudioChannels);
            Assert.Equal(5000000, info.VideoBitrate);
            Assert.Equal(128000, info.AudioBitrate);
        }

        [Fact]
        public void ParseFFprobeJson_AudioOnly_ParsesCorrectly()
        {
            string json = @"{
                ""format"": {
                    ""format_name"": ""mp3"",
                    ""duration"": ""245.300000""
                },
                ""streams"": [
                    {
                        ""codec_type"": ""audio"",
                        ""codec_name"": ""mp3"",
                        ""sample_rate"": ""48000"",
                        ""channels"": 2,
                        ""bit_rate"": ""320000""
                    }
                ]
            }";

            var info = new MediaFileInfo();
            MediaFileService.ParseFFprobeJson(json, info);

            Assert.False(info.HasVideo);
            Assert.True(info.HasAudio);
            Assert.Equal("mp3", info.AudioCodec);
            Assert.Equal(48000, info.AudioSampleRate);
            Assert.Equal(2, info.AudioChannels);
            Assert.Equal(320000, info.AudioBitrate);
            Assert.Equal(245.3, info.DurationSeconds, 1);
        }

        [Fact]
        public void ParseFFprobeJson_InvalidJson_DoesNotThrow()
        {
            var info = new MediaFileInfo();
            MediaFileService.ParseFFprobeJson("not valid json", info);

            Assert.False(info.HasVideo);
            Assert.False(info.HasAudio);
        }

        [Fact]
        public void ParseFFprobeJson_EmptyStreams_NoMediaInfo()
        {
            string json = @"{
                ""format"": {
                    ""format_name"": ""unknown"",
                    ""duration"": ""0""
                },
                ""streams"": []
            }";

            var info = new MediaFileInfo();
            MediaFileService.ParseFFprobeJson(json, info);

            Assert.False(info.HasVideo);
            Assert.False(info.HasAudio);
        }

        #endregion

        #region ParseFrameRate 测试

        [Fact]
        public void ParseFrameRate_FractionFormat_ParsesCorrectly()
        {
            double result = MediaFileService.ParseFrameRate("30000/1001");
            Assert.Equal(29.97, result, 1);
        }

        [Fact]
        public void ParseFrameRate_IntegerFormat_ParsesCorrectly()
        {
            double result = MediaFileService.ParseFrameRate("25");
            Assert.Equal(25.0, result, 1);
        }

        [Fact]
        public void ParseFrameRate_Null_ReturnsZero()
        {
            double result = MediaFileService.ParseFrameRate(null);
            Assert.Equal(0, result);
        }

        [Fact]
        public void ParseFrameRate_Empty_ReturnsZero()
        {
            double result = MediaFileService.ParseFrameRate("");
            Assert.Equal(0, result);
        }

        [Fact]
        public void ParseFrameRate_ZeroDenominator_ReturnsZero()
        {
            double result = MediaFileService.ParseFrameRate("30/0");
            Assert.Equal(0, result);
        }

        #endregion

        #region MediaFileInfo 属性测试

        [Fact]
        public void MediaFileInfo_DurationText_FormatsCorrectly()
        {
            var info = new MediaFileInfo();
            info.DurationSeconds = 3723.5;
            Assert.Equal("01:02:03", info.DurationText);
        }

        [Fact]
        public void MediaFileInfo_ResolutionText_FormatsCorrectly()
        {
            var info = new MediaFileInfo();
            info.Width = 1920;
            info.Height = 1080;
            Assert.Equal("1920x1080", info.ResolutionText);
        }

        [Fact]
        public void MediaFileInfo_ResolutionText_ZeroSize_ReturnsEmpty()
        {
            var info = new MediaFileInfo();
            Assert.Equal("", info.ResolutionText);
        }

        [Fact]
        public void MediaFileInfo_FileSizeText_Bytes()
        {
            var info = new MediaFileInfo();
            info.FileSize = 500;
            Assert.Equal("500 B", info.FileSizeText);
        }

        [Fact]
        public void MediaFileInfo_FileSizeText_KB()
        {
            var info = new MediaFileInfo();
            info.FileSize = 2048;
            Assert.Equal("2.0 KB", info.FileSizeText);
        }

        [Fact]
        public void MediaFileInfo_FileSizeText_MB()
        {
            var info = new MediaFileInfo();
            info.FileSize = 10 * 1024 * 1024;
            Assert.Equal("10.0 MB", info.FileSizeText);
        }

        [Fact]
        public void MediaFileInfo_FileSizeText_GB()
        {
            var info = new MediaFileInfo();
            info.FileSize = 2L * 1024 * 1024 * 1024;
            Assert.Equal("2.00 GB", info.FileSizeText);
        }

        #endregion

        #region FileDialogFilter 测试

        [Fact]
        public void FileDialogFilter_ContainsAllCategories()
        {
            string filter = MediaFileService.FileDialogFilter;
            Assert.Contains("所有支持的媒体文件", filter);
            Assert.Contains("视频文件", filter);
            Assert.Contains("音频文件", filter);
            Assert.Contains("所有文件", filter);
        }

        #endregion
    }
}
