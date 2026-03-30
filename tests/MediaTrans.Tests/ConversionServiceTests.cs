using System;
using System.Collections.Generic;
using Xunit;
using MediaTrans.Models;
using MediaTrans.Services;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 转换服务测试
    /// </summary>
    public class ConversionServiceTests
    {
        #region GetDefaultCodecs 测试

        [Fact]
        public void GetDefaultCodecs_Mp4_ReturnsH264AndAac()
        {
            var mapping = ConversionService.GetDefaultCodecs(".mp4");
            Assert.NotNull(mapping);
            Assert.Equal("libx264", mapping.VideoCodec);
            Assert.Equal("aac", mapping.AudioCodec);
        }

        [Fact]
        public void GetDefaultCodecs_Mkv_ReturnsH264AndAac()
        {
            var mapping = ConversionService.GetDefaultCodecs(".mkv");
            Assert.NotNull(mapping);
            Assert.Equal("libx264", mapping.VideoCodec);
            Assert.Equal("aac", mapping.AudioCodec);
        }

        [Fact]
        public void GetDefaultCodecs_Webm_ReturnsVpxAndVorbis()
        {
            var mapping = ConversionService.GetDefaultCodecs(".webm");
            Assert.NotNull(mapping);
            Assert.Equal("libvpx", mapping.VideoCodec);
            Assert.Equal("libvorbis", mapping.AudioCodec);
        }

        [Fact]
        public void GetDefaultCodecs_Mp3_AudioOnly()
        {
            var mapping = ConversionService.GetDefaultCodecs(".mp3");
            Assert.NotNull(mapping);
            Assert.Null(mapping.VideoCodec);
            Assert.Equal("libmp3lame", mapping.AudioCodec);
        }

        [Fact]
        public void GetDefaultCodecs_Wav_AudioOnly()
        {
            var mapping = ConversionService.GetDefaultCodecs(".wav");
            Assert.NotNull(mapping);
            Assert.Null(mapping.VideoCodec);
            Assert.Equal("pcm_s16le", mapping.AudioCodec);
        }

        [Fact]
        public void GetDefaultCodecs_Flac_AudioOnly()
        {
            var mapping = ConversionService.GetDefaultCodecs(".flac");
            Assert.NotNull(mapping);
            Assert.Null(mapping.VideoCodec);
            Assert.Equal("flac", mapping.AudioCodec);
        }

        [Fact]
        public void GetDefaultCodecs_Unknown_ReturnsNull()
        {
            var mapping = ConversionService.GetDefaultCodecs(".xyz");
            Assert.Null(mapping);
        }

        #endregion

        #region IsAudioOnlyFormat 测试

        [Fact]
        public void IsAudioOnlyFormat_Mp3_ReturnsTrue()
        {
            Assert.True(ConversionService.IsAudioOnlyFormat(".mp3"));
        }

        [Fact]
        public void IsAudioOnlyFormat_Wav_ReturnsTrue()
        {
            Assert.True(ConversionService.IsAudioOnlyFormat(".wav"));
        }

        [Fact]
        public void IsAudioOnlyFormat_Flac_ReturnsTrue()
        {
            Assert.True(ConversionService.IsAudioOnlyFormat(".flac"));
        }

        [Fact]
        public void IsAudioOnlyFormat_Mp4_ReturnsFalse()
        {
            Assert.False(ConversionService.IsAudioOnlyFormat(".mp4"));
        }

        [Fact]
        public void IsAudioOnlyFormat_Avi_ReturnsFalse()
        {
            Assert.False(ConversionService.IsAudioOnlyFormat(".avi"));
        }

        [Fact]
        public void IsAudioOnlyFormat_Null_ReturnsFalse()
        {
            Assert.False(ConversionService.IsAudioOnlyFormat(null));
        }

        [Fact]
        public void IsAudioOnlyFormat_Empty_ReturnsFalse()
        {
            Assert.False(ConversionService.IsAudioOnlyFormat(""));
        }

        #endregion

        #region BuildConversionArguments 测试

        private ConversionService CreateService()
        {
            var configService = new ConfigService();
            var config = configService.Load();
            var ffmpegService = new FFmpegService(config);
            return new ConversionService(ffmpegService, configService);
        }

        [Fact]
        public void BuildConversionArguments_Mp4Default_ContainsH264AndAac()
        {
            var service = CreateService();
            var source = new MediaFileInfo { FilePath = @"C:\test\input.avi", FileName = "input.avi" };

            string args = service.BuildConversionArguments(source, @"C:\test\output.mp4", ".mp4", null);

            Assert.Contains("-c:v libx264", args);
            Assert.Contains("-c:a aac", args);
            Assert.Contains("-threads 0", args);
            Assert.Contains("\"C:\\test\\output.mp4\"", args);
        }

        [Fact]
        public void BuildConversionArguments_Mp3Default_ContainsNoVideoAndLame()
        {
            var service = CreateService();
            var source = new MediaFileInfo { FilePath = @"C:\test\input.mp4", FileName = "input.mp4" };

            string args = service.BuildConversionArguments(source, @"C:\test\output.mp3", ".mp3", null);

            Assert.Contains("-vn", args);
            Assert.Contains("-c:a libmp3lame", args);
        }

        [Fact]
        public void BuildConversionArguments_WithPreset_UsesPresetParams()
        {
            var service = CreateService();
            var source = new MediaFileInfo { FilePath = @"C:\test\input.avi", FileName = "input.avi" };
            var preset = new ConversionPreset
            {
                Name = "测试预设",
                VideoCodec = "libx265",
                AudioCodec = "aac",
                Width = 1280,
                Height = 720,
                VideoBitrate = "3M",
                AudioBitrate = "192k",
                FrameRate = 24
            };

            string args = service.BuildConversionArguments(source, @"C:\test\output.mp4", ".mp4", preset);

            Assert.Contains("-c:v libx265", args);
            Assert.Contains("-c:a aac", args);
            Assert.Contains("-s 1280x720", args);
            Assert.Contains("-b:v 3M", args);
            Assert.Contains("-b:a 192k", args);
            Assert.Contains("-r 24", args);
        }

        [Fact]
        public void BuildConversionArguments_AudioPresetToMp3_HasNoVideo()
        {
            var service = CreateService();
            var source = new MediaFileInfo { FilePath = @"C:\test\input.mp4", FileName = "input.mp4" };
            var preset = new ConversionPreset
            {
                Name = "音频预设",
                VideoCodec = "",
                AudioCodec = "libmp3lame",
                AudioBitrate = "320k"
            };

            string args = service.BuildConversionArguments(source, @"C:\test\output.mp3", ".mp3", preset);

            Assert.Contains("-vn", args);
            Assert.Contains("-c:a libmp3lame", args);
            Assert.Contains("-b:a 320k", args);
        }

        [Fact]
        public void BuildConversionArguments_WebmDefault_ContainsVpxAndVorbis()
        {
            var service = CreateService();
            var source = new MediaFileInfo { FilePath = @"C:\test\input.mp4", FileName = "input.mp4" };

            string args = service.BuildConversionArguments(source, @"C:\test\output.webm", ".webm", null);

            Assert.Contains("-c:v libvpx", args);
            Assert.Contains("-c:a libvorbis", args);
        }

        [Fact]
        public void BuildConversionArguments_AlwaysOverwrite()
        {
            var service = CreateService();
            var source = new MediaFileInfo { FilePath = @"C:\test\input.avi", FileName = "input.avi" };

            string args = service.BuildConversionArguments(source, @"C:\test\output.mp4", ".mp4", null);

            Assert.True(args.StartsWith("-y "));
        }

        #endregion

        #region GetSupportedOutputFormats 测试

        [Fact]
        public void GetSupportedOutputFormats_ContainsVideoFormats()
        {
            var formats = ConversionService.GetSupportedOutputFormats();
            Assert.Contains(".mp4", formats);
            Assert.Contains(".avi", formats);
            Assert.Contains(".mkv", formats);
        }

        [Fact]
        public void GetSupportedOutputFormats_ContainsAudioFormats()
        {
            var formats = ConversionService.GetSupportedOutputFormats();
            Assert.Contains(".mp3", formats);
            Assert.Contains(".wav", formats);
            Assert.Contains(".flac", formats);
        }

        #endregion

        #region ConversionTask 模型测试

        [Fact]
        public void ConversionTask_DefaultStatus_IsPending()
        {
            var task = new ConversionTask();
            Assert.Equal(ConversionStatus.Pending, task.Status);
            Assert.Equal("等待中", task.StatusText);
            Assert.NotNull(task.Id);
            Assert.True(task.Id.Length > 0);
        }

        #endregion
    }
}
