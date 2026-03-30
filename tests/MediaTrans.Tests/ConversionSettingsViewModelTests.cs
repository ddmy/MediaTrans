using System;
using Xunit;
using MediaTrans.Models;
using MediaTrans.Services;
using MediaTrans.ViewModels;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 转换参数设置 ViewModel 测试
    /// </summary>
    public class ConversionSettingsViewModelTests
    {
        private ConversionSettingsViewModel CreateViewModel()
        {
            var configService = new ConfigService();
            return new ConversionSettingsViewModel(configService);
        }

        #region 初始化测试

        [Fact]
        public void Constructor_LoadsDefaultPresets()
        {
            var vm = CreateViewModel();
            // 默认配置包含 3 个预设
            Assert.True(vm.Presets.Count >= 3);
        }

        [Fact]
        public void Constructor_OutputFormatsNotEmpty()
        {
            var vm = CreateViewModel();
            Assert.True(vm.OutputFormats.Count > 0);
        }

        [Fact]
        public void Constructor_DefaultFormatIsMp4()
        {
            var vm = CreateViewModel();
            Assert.Equal(".mp4", vm.SelectedFormat);
        }

        #endregion

        #region 参数校验测试

        [Fact]
        public void Validate_ValidResolution_ReturnsTrue()
        {
            var vm = CreateViewModel();
            vm.CustomWidth = "1920";
            vm.CustomHeight = "1080";
            bool result = vm.Validate();
            Assert.True(result);
            Assert.Equal("", vm.ValidationMessage);
        }

        [Fact]
        public void Validate_OddWidth_ReturnsFalse()
        {
            var vm = CreateViewModel();
            vm.CustomWidth = "1921";
            vm.CustomHeight = "1080";
            bool result = vm.Validate();
            Assert.False(result);
            Assert.Contains("偶数", vm.ValidationMessage);
        }

        [Fact]
        public void Validate_NegativeWidth_ReturnsFalse()
        {
            var vm = CreateViewModel();
            vm.CustomWidth = "-100";
            vm.CustomHeight = "1080";
            bool result = vm.Validate();
            Assert.False(result);
        }

        [Fact]
        public void Validate_WidthOnly_ReturnsFalse()
        {
            var vm = CreateViewModel();
            vm.CustomWidth = "1920";
            vm.CustomHeight = "";
            bool result = vm.Validate();
            Assert.False(result);
            Assert.Contains("同时指定", vm.ValidationMessage);
        }

        [Fact]
        public void Validate_ExcessiveWidth_ReturnsFalse()
        {
            var vm = CreateViewModel();
            vm.CustomWidth = "10000";
            vm.CustomHeight = "1080";
            bool result = vm.Validate();
            Assert.False(result);
        }

        [Fact]
        public void Validate_ValidFrameRate_ReturnsTrue()
        {
            var vm = CreateViewModel();
            vm.CustomFrameRate = "30";
            bool result = vm.Validate();
            Assert.True(result);
        }

        [Fact]
        public void Validate_InvalidFrameRate_ReturnsFalse()
        {
            var vm = CreateViewModel();
            vm.CustomFrameRate = "200";
            bool result = vm.Validate();
            Assert.False(result);
            Assert.Contains("帧率", vm.ValidationMessage);
        }

        [Fact]
        public void Validate_ZeroFrameRate_ReturnsFalse()
        {
            var vm = CreateViewModel();
            vm.CustomFrameRate = "0";
            bool result = vm.Validate();
            Assert.False(result);
        }

        [Fact]
        public void Validate_NonNumericFrameRate_ReturnsFalse()
        {
            var vm = CreateViewModel();
            vm.CustomFrameRate = "abc";
            bool result = vm.Validate();
            Assert.False(result);
        }

        #endregion

        #region 比特率校验测试

        [Fact]
        public void IsValidBitrate_NumericValue_ReturnsTrue()
        {
            Assert.True(ConversionSettingsViewModel.IsValidBitrate("5000000"));
        }

        [Fact]
        public void IsValidBitrate_WithKSuffix_ReturnsTrue()
        {
            Assert.True(ConversionSettingsViewModel.IsValidBitrate("128k"));
            Assert.True(ConversionSettingsViewModel.IsValidBitrate("128K"));
        }

        [Fact]
        public void IsValidBitrate_WithMSuffix_ReturnsTrue()
        {
            Assert.True(ConversionSettingsViewModel.IsValidBitrate("5M"));
            Assert.True(ConversionSettingsViewModel.IsValidBitrate("2.5M"));
        }

        [Fact]
        public void IsValidBitrate_Empty_ReturnsFalse()
        {
            Assert.False(ConversionSettingsViewModel.IsValidBitrate(""));
        }

        [Fact]
        public void IsValidBitrate_Null_ReturnsFalse()
        {
            Assert.False(ConversionSettingsViewModel.IsValidBitrate(null));
        }

        [Fact]
        public void IsValidBitrate_InvalidSuffix_ReturnsFalse()
        {
            Assert.False(ConversionSettingsViewModel.IsValidBitrate("128x"));
        }

        [Fact]
        public void IsValidBitrate_NegativeValue_ReturnsFalse()
        {
            Assert.False(ConversionSettingsViewModel.IsValidBitrate("-100k"));
        }

        [Fact]
        public void IsValidBitrate_ZeroValue_ReturnsFalse()
        {
            Assert.False(ConversionSettingsViewModel.IsValidBitrate("0"));
        }

        #endregion

        #region 预设选择测试

        [Fact]
        public void SelectPreset_SetsCustomFields()
        {
            var vm = CreateViewModel();
            // 选择"高质量 1080p"预设
            ConversionPreset preset = null;
            foreach (var p in vm.Presets)
            {
                if (p.Name == "高质量 1080p")
                {
                    preset = p;
                    break;
                }
            }
            Assert.NotNull(preset);

            vm.SelectedPreset = preset;

            Assert.Equal("libx264", vm.CustomVideoCodec);
            Assert.Equal("aac", vm.CustomAudioCodec);
            Assert.Equal("1920", vm.CustomWidth);
            Assert.Equal("1080", vm.CustomHeight);
        }

        #endregion

        #region BuildCurrentPreset 测试

        [Fact]
        public void BuildCurrentPreset_ReturnsPresetWithCurrentValues()
        {
            var vm = CreateViewModel();
            vm.CustomVideoCodec = "libx265";
            vm.CustomAudioCodec = "aac";
            vm.CustomWidth = "1280";
            vm.CustomHeight = "720";
            vm.CustomVideoBitrate = "3M";
            vm.CustomAudioBitrate = "192k";
            vm.CustomFrameRate = "24";

            var preset = vm.BuildCurrentPreset();

            Assert.Equal("libx265", preset.VideoCodec);
            Assert.Equal("aac", preset.AudioCodec);
            Assert.Equal(1280, preset.Width);
            Assert.Equal(720, preset.Height);
            Assert.Equal("3M", preset.VideoBitrate);
            Assert.Equal("192k", preset.AudioBitrate);
            Assert.Equal(24, preset.FrameRate);
        }

        [Fact]
        public void BuildCurrentPreset_EmptyValues_ReturnsDefaults()
        {
            var vm = CreateViewModel();
            vm.CustomVideoCodec = "";
            vm.CustomWidth = "";
            vm.CustomHeight = "";
            vm.CustomFrameRate = "";

            var preset = vm.BuildCurrentPreset();

            Assert.Equal(0, preset.Width);
            Assert.Equal(0, preset.Height);
            Assert.Equal(0, preset.FrameRate);
        }

        #endregion
    }
}
