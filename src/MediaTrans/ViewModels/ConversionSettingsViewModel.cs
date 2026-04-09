using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MediaTrans.Commands;
using MediaTrans.Models;
using MediaTrans.Services;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 转换参数设置 ViewModel
    /// </summary>
    public class ConversionSettingsViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;

        private string _selectedFormat;
        private ConversionPreset _selectedPreset;
        private string _customVideoCodec;
        private string _customAudioCodec;
        private string _customWidth;
        private string _customHeight;
        private string _customVideoBitrate;
        private string _customAudioBitrate;
        private string _customFrameRate;
        private string _validationMessage;
        private bool _isCustomMode;
        private bool _isAudioMode;

        /// <summary>
        /// 可选的输出格式列表
        /// </summary>
        public ObservableCollection<string> OutputFormats { get; private set; }

        /// <summary>
        /// 全部预设列表（所有模式）
        /// </summary>
        public ObservableCollection<ConversionPreset> Presets { get; private set; }

        /// <summary>
        /// 按当前工具模式过滤后的预设列表：
        /// 音频模式 → 只显示无 VideoCodec 的音频预设；
        /// 视频模式 → 只显示有 VideoCodec 的视频预设。
        /// </summary>
        public List<ConversionPreset> FilteredPresets
        {
            get
            {
                var result = new List<ConversionPreset>();
                foreach (var p in Presets)
                {
                    bool isAudioPreset = string.IsNullOrEmpty(p.VideoCodec);
                    if (_isAudioMode == isAudioPreset)
                    {
                        result.Add(p);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// 当前是否处于音频工具模式（true=音频，false=视频）
        /// 由 MainViewModel 在切换模式时设置，用于过滤预设列表。
        /// </summary>
        public bool IsAudioMode
        {
            get { return _isAudioMode; }
            set
            {
                if (SetProperty(ref _isAudioMode, value, "IsAudioMode"))
                {
                    // 切换模式时清除与新模式不匹配的预设选择
                    if (_selectedPreset != null)
                    {
                        bool presetIsAudio = string.IsNullOrEmpty(_selectedPreset.VideoCodec);
                        if (presetIsAudio != value)
                        {
                            SelectedPreset = null;
                        }
                    }
                    OnPropertyChanged("FilteredPresets");
                }
            }
        }

        /// <summary>
        /// 保存自定义预设命令
        /// </summary>
        public RelayCommand SavePresetCommand { get; private set; }

        /// <summary>
        /// 删除预设命令
        /// </summary>
        public RelayCommand DeletePresetCommand { get; private set; }

        public ConversionSettingsViewModel(ConfigService configService)
        {
            _configService = configService;

            OutputFormats = new ObservableCollection<string>(ConversionService.GetSupportedOutputFormats());
            Presets = new ObservableCollection<ConversionPreset>();

            SavePresetCommand = new RelayCommand(OnSavePreset, CanSavePreset);
            DeletePresetCommand = new RelayCommand(OnDeletePreset, CanDeletePreset);

            // 从配置加载预设
            LoadPresets();

            // 默认选择 MP4
            _selectedFormat = ".mp4";
            _isCustomMode = false;
        }

        #region 属性

        /// <summary>
        /// 选中的输出格式
        /// </summary>
        public string SelectedFormat
        {
            get { return _selectedFormat; }
            set
            {
                if (SetProperty(ref _selectedFormat, value, "SelectedFormat"))
                {
                    // 格式变更时，如果非自定义模式，加载该格式的默认编解码器
                    if (!_isCustomMode)
                    {
                        ApplyDefaultCodecs();
                    }
                    Validate();
                }
            }
        }

        /// <summary>
        /// 选中的预设
        /// </summary>
        public ConversionPreset SelectedPreset
        {
            get { return _selectedPreset; }
            set
            {
                if (SetProperty(ref _selectedPreset, value, "SelectedPreset"))
                {
                    if (_selectedPreset != null)
                    {
                        _isCustomMode = false;
                        ApplyPreset(_selectedPreset);
                    }
                }
            }
        }

        /// <summary>
        /// 是否为自定义参数模式
        /// </summary>
        public bool IsCustomMode
        {
            get { return _isCustomMode; }
            set { SetProperty(ref _isCustomMode, value, "IsCustomMode"); }
        }

        public string CustomVideoCodec
        {
            get { return _customVideoCodec; }
            set
            {
                if (SetProperty(ref _customVideoCodec, value, "CustomVideoCodec"))
                {
                    _isCustomMode = true;
                    Validate();
                }
            }
        }

        public string CustomAudioCodec
        {
            get { return _customAudioCodec; }
            set
            {
                if (SetProperty(ref _customAudioCodec, value, "CustomAudioCodec"))
                {
                    _isCustomMode = true;
                    Validate();
                }
            }
        }

        public string CustomWidth
        {
            get { return _customWidth; }
            set
            {
                if (SetProperty(ref _customWidth, value, "CustomWidth"))
                {
                    _isCustomMode = true;
                    Validate();
                }
            }
        }

        public string CustomHeight
        {
            get { return _customHeight; }
            set
            {
                if (SetProperty(ref _customHeight, value, "CustomHeight"))
                {
                    _isCustomMode = true;
                    Validate();
                }
            }
        }

        public string CustomVideoBitrate
        {
            get { return _customVideoBitrate; }
            set
            {
                if (SetProperty(ref _customVideoBitrate, value, "CustomVideoBitrate"))
                {
                    _isCustomMode = true;
                    Validate();
                }
            }
        }

        public string CustomAudioBitrate
        {
            get { return _customAudioBitrate; }
            set
            {
                if (SetProperty(ref _customAudioBitrate, value, "CustomAudioBitrate"))
                {
                    _isCustomMode = true;
                    Validate();
                }
            }
        }

        public string CustomFrameRate
        {
            get { return _customFrameRate; }
            set
            {
                if (SetProperty(ref _customFrameRate, value, "CustomFrameRate"))
                {
                    _isCustomMode = true;
                    Validate();
                }
            }
        }

        /// <summary>
        /// 参数校验结果消息
        /// </summary>
        public string ValidationMessage
        {
            get { return _validationMessage; }
            set { SetProperty(ref _validationMessage, value, "ValidationMessage"); }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 从配置文件加载预设列表
        /// </summary>
        public void LoadPresets()
        {
            Presets.Clear();
            var config = _configService.Load();
            if (config.ConversionPresets != null)
            {
                foreach (var preset in config.ConversionPresets)
                {
                    Presets.Add(preset);
                }
            }
            OnPropertyChanged("FilteredPresets");
        }

        /// <summary>
        /// 将预设参数填充到自定义字段
        /// </summary>
        private void ApplyPreset(ConversionPreset preset)
        {
            _customVideoCodec = preset.VideoCodec ?? "";
            _customAudioCodec = preset.AudioCodec ?? "";
            _customWidth = preset.Width > 0 ? preset.Width.ToString() : "";
            _customHeight = preset.Height > 0 ? preset.Height.ToString() : "";
            _customVideoBitrate = preset.VideoBitrate ?? "";
            _customAudioBitrate = preset.AudioBitrate ?? "";
            _customFrameRate = preset.FrameRate > 0 ? preset.FrameRate.ToString() : "";

            // 手动触发属性变更通知
            OnPropertyChanged("CustomVideoCodec");
            OnPropertyChanged("CustomAudioCodec");
            OnPropertyChanged("CustomWidth");
            OnPropertyChanged("CustomHeight");
            OnPropertyChanged("CustomVideoBitrate");
            OnPropertyChanged("CustomAudioBitrate");
            OnPropertyChanged("CustomFrameRate");

            Validate();
        }

        /// <summary>
        /// 应用当前格式的默认编解码器
        /// </summary>
        private void ApplyDefaultCodecs()
        {
            var mapping = ConversionService.GetDefaultCodecs(_selectedFormat);
            if (mapping != null)
            {
                _customVideoCodec = mapping.VideoCodec ?? "";
                _customAudioCodec = mapping.AudioCodec ?? "";
                OnPropertyChanged("CustomVideoCodec");
                OnPropertyChanged("CustomAudioCodec");
            }
        }

        /// <summary>
        /// 构建当前设置对应的 ConversionPreset 对象
        /// </summary>
        public ConversionPreset BuildCurrentPreset()
        {
            var preset = new ConversionPreset();
            preset.VideoCodec = _customVideoCodec ?? "";
            preset.AudioCodec = _customAudioCodec ?? "";

            int width;
            if (int.TryParse(_customWidth, out width))
            {
                preset.Width = width;
            }

            int height;
            if (int.TryParse(_customHeight, out height))
            {
                preset.Height = height;
            }

            preset.VideoBitrate = _customVideoBitrate ?? "";
            preset.AudioBitrate = _customAudioBitrate ?? "";

            int frameRate;
            if (int.TryParse(_customFrameRate, out frameRate))
            {
                preset.FrameRate = frameRate;
            }

            return preset;
        }

        /// <summary>
        /// 参数校验
        /// </summary>
        public bool Validate()
        {
            var errors = new List<string>();

            // 分辨率校验
            if (!string.IsNullOrEmpty(_customWidth) || !string.IsNullOrEmpty(_customHeight))
            {
                int width, height;
                bool widthValid = int.TryParse(_customWidth, out width) && width > 0 && width <= 7680;
                bool heightValid = int.TryParse(_customHeight, out height) && height > 0 && height <= 4320;

                if (!string.IsNullOrEmpty(_customWidth) && !widthValid)
                {
                    errors.Add("宽度无效（1-7680）");
                }
                if (!string.IsNullOrEmpty(_customHeight) && !heightValid)
                {
                    errors.Add("高度无效（1-4320）");
                }

                // 宽高必须同时指定或同时为空
                if ((!string.IsNullOrEmpty(_customWidth)) != (!string.IsNullOrEmpty(_customHeight)))
                {
                    errors.Add("宽度和高度必须同时指定");
                }

                // 宽高必须是偶数（FFmpeg 要求）
                if (widthValid && width % 2 != 0)
                {
                    errors.Add("宽度必须是偶数");
                }
                if (heightValid && height % 2 != 0)
                {
                    errors.Add("高度必须是偶数");
                }
            }

            // 帧率校验
            if (!string.IsNullOrEmpty(_customFrameRate))
            {
                int fps;
                if (!int.TryParse(_customFrameRate, out fps) || fps < 1 || fps > 120)
                {
                    errors.Add("帧率无效（1-120）");
                }
            }

            // 比特率格式校验（如 "5M", "128k", "5000000"）
            if (!string.IsNullOrEmpty(_customVideoBitrate) && !IsValidBitrate(_customVideoBitrate))
            {
                errors.Add("视频比特率格式无效（如 5M, 2500k, 5000000）");
            }
            if (!string.IsNullOrEmpty(_customAudioBitrate) && !IsValidBitrate(_customAudioBitrate))
            {
                errors.Add("音频比特率格式无效（如 320k, 128k）");
            }

            if (errors.Count > 0)
            {
                ValidationMessage = string.Join("\n", errors.ToArray());
                return false;
            }

            ValidationMessage = "";
            return true;
        }

        /// <summary>
        /// 校验比特率字符串格式
        /// </summary>
        public static bool IsValidBitrate(string bitrate)
        {
            if (string.IsNullOrEmpty(bitrate))
            {
                return false;
            }

            string trimmed = bitrate.Trim();

            // 允许纯数字
            long numericValue;
            if (long.TryParse(trimmed, out numericValue) && numericValue > 0)
            {
                return true;
            }

            // 允许带 k/K/m/M 后缀
            if (trimmed.Length > 1)
            {
                char suffix = trimmed[trimmed.Length - 1];
                if (suffix == 'k' || suffix == 'K' || suffix == 'm' || suffix == 'M')
                {
                    string numberPart = trimmed.Substring(0, trimmed.Length - 1);
                    double value;
                    if (double.TryParse(numberPart, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 保存当前参数为新预设
        /// </summary>
        private void OnSavePreset(object parameter)
        {
            string presetName = parameter as string;
            if (string.IsNullOrEmpty(presetName))
            {
                return;
            }

            var preset = BuildCurrentPreset();
            preset.Name = presetName;

            // 如果同名预设已存在，则更新
            var config = _configService.Load();
            bool found = false;
            for (int i = 0; i < config.ConversionPresets.Count; i++)
            {
                if (string.Equals(config.ConversionPresets[i].Name, presetName, StringComparison.Ordinal))
                {
                    config.ConversionPresets[i] = preset;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                config.ConversionPresets.Add(preset);
            }

            _configService.Save(config);
            LoadPresets();
        }

        private bool CanSavePreset(object parameter)
        {
            return Validate();
        }

        /// <summary>
        /// 删除选中的预设
        /// </summary>
        private void OnDeletePreset(object parameter)
        {
            if (_selectedPreset == null)
            {
                return;
            }

            var config = _configService.Load();
            for (int i = config.ConversionPresets.Count - 1; i >= 0; i--)
            {
                if (string.Equals(config.ConversionPresets[i].Name, _selectedPreset.Name, StringComparison.Ordinal))
                {
                    config.ConversionPresets.RemoveAt(i);
                    break;
                }
            }

            _configService.Save(config);
            _selectedPreset = null;
            OnPropertyChanged("SelectedPreset");
            LoadPresets();
        }

        private bool CanDeletePreset(object parameter)
        {
            return _selectedPreset != null;
        }

        #endregion
    }
}
