using System;
using System.Collections.Generic;
using System.Text;

namespace MediaTrans.Services
{
    /// <summary>
    /// FFmpeg 命令行构建器
    /// 确保路径双引号包裹、显式指定编解码器
    /// </summary>
    public class FFmpegCommandBuilder
    {
        private readonly List<string> _inputs;
        private readonly List<string> _options;
        private readonly List<string> _preInputOptions;
        private string _videoCodec;
        private string _audioCodec;
        private string _output;
        private bool _overwrite;
        private string _filterComplex;
        private readonly List<string> _maps;

        public FFmpegCommandBuilder()
        {
            _inputs = new List<string>();
            _options = new List<string>();
            _preInputOptions = new List<string>();
            _maps = new List<string>();
            _overwrite = true; // 默认覆盖输出文件
        }

        /// <summary>
        /// 添加输入文件（路径自动加双引号）
        /// </summary>
        public FFmpegCommandBuilder Input(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new ArgumentNullException("inputPath");
            }
            _inputs.Add(string.Format("-i \"{0}\"", inputPath));
            return this;
        }

        /// <summary>
        /// 添加带前置选项的输入文件（如 -ss / -t 放在 -i 之前，实现 per-input seek/duration）
        /// </summary>
        public FFmpegCommandBuilder InputWithOptions(string inputPath, string preOptions)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new ArgumentNullException("inputPath");
            }
            if (string.IsNullOrEmpty(preOptions))
            {
                _inputs.Add(string.Format("-i \"{0}\"", inputPath));
            }
            else
            {
                _inputs.Add(string.Format("{0} -i \"{1}\"", preOptions, inputPath));
            }
            return this;
        }

        /// <summary>
        /// 设置视频编解码器 (-c:v)
        /// </summary>
        public FFmpegCommandBuilder VideoCodec(string codec)
        {
            _videoCodec = codec;
            return this;
        }

        /// <summary>
        /// 设置音频编解码器 (-c:a)
        /// </summary>
        public FFmpegCommandBuilder AudioCodec(string codec)
        {
            _audioCodec = codec;
            return this;
        }

        /// <summary>
        /// 禁用视频流 (-vn)
        /// </summary>
        public FFmpegCommandBuilder NoVideo()
        {
            _options.Add("-vn");
            return this;
        }

        /// <summary>
        /// 禁用音频流 (-an)
        /// </summary>
        public FFmpegCommandBuilder NoAudio()
        {
            _options.Add("-an");
            return this;
        }

        /// <summary>
        /// 设置视频比特率
        /// </summary>
        public FFmpegCommandBuilder VideoBitrate(string bitrate)
        {
            if (!string.IsNullOrEmpty(bitrate))
            {
                _options.Add(string.Format("-b:v {0}", bitrate));
            }
            return this;
        }

        /// <summary>
        /// 设置音频比特率
        /// </summary>
        public FFmpegCommandBuilder AudioBitrate(string bitrate)
        {
            if (!string.IsNullOrEmpty(bitrate))
            {
                _options.Add(string.Format("-b:a {0}", bitrate));
            }
            return this;
        }

        /// <summary>
        /// 设置分辨率
        /// </summary>
        public FFmpegCommandBuilder Resolution(int width, int height)
        {
            if (width > 0 && height > 0)
            {
                _options.Add(string.Format("-s {0}x{1}", width, height));
            }
            return this;
        }

        /// <summary>
        /// 设置帧率
        /// </summary>
        public FFmpegCommandBuilder FrameRate(int fps)
        {
            if (fps > 0)
            {
                _options.Add(string.Format("-r {0}", fps));
            }
            return this;
        }

        /// <summary>
        /// 设置多线程模式
        /// </summary>
        public FFmpegCommandBuilder Threads(int count)
        {
            _options.Add(string.Format("-threads {0}", count));
            return this;
        }

        /// <summary>
        /// 添加自定义参数
        /// </summary>
        public FFmpegCommandBuilder Option(string option)
        {
            if (!string.IsNullOrEmpty(option))
            {
                _options.Add(option);
            }
            return this;
        }

        /// <summary>
        /// 设置输出文件（路径自动加双引号）
        /// </summary>
        public FFmpegCommandBuilder Output(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentNullException("outputPath");
            }
            _output = outputPath;
            return this;
        }

        /// <summary>
        /// 是否覆盖已有输出文件（默认 true）
        /// </summary>
        public FFmpegCommandBuilder Overwrite(bool overwrite)
        {
            _overwrite = overwrite;
            return this;
        }

        /// <summary>
        /// 设置起始时间偏移（-ss 参数，放在输入前做快速 seek）
        /// </summary>
        public FFmpegCommandBuilder SeekStart(double seconds)
        {
            if (seconds > 0)
            {
                _preInputOptions.Add(string.Format("-ss {0:F6}", seconds));
            }
            return this;
        }

        /// <summary>
        /// 设置持续时长（-t 参数）
        /// </summary>
        public FFmpegCommandBuilder Duration(double seconds)
        {
            if (seconds > 0)
            {
                _options.Add(string.Format("-t {0:F6}", seconds));
            }
            return this;
        }

        /// <summary>
        /// 设置 filter_complex 滤镜图
        /// </summary>
        public FFmpegCommandBuilder FilterComplex(string filterGraph)
        {
            if (!string.IsNullOrEmpty(filterGraph))
            {
                _filterComplex = filterGraph;
            }
            return this;
        }

        /// <summary>
        /// 添加视频滤镜（-vf 参数）
        /// </summary>
        public FFmpegCommandBuilder VideoFilter(string filter)
        {
            if (!string.IsNullOrEmpty(filter))
            {
                _options.Add(string.Format("-vf \"{0}\"", filter));
            }
            return this;
        }

        /// <summary>
        /// 添加音频滤镜（-af 参数）
        /// </summary>
        public FFmpegCommandBuilder AudioFilter(string filter)
        {
            if (!string.IsNullOrEmpty(filter))
            {
                _options.Add(string.Format("-af \"{0}\"", filter));
            }
            return this;
        }

        /// <summary>
        /// 添加流映射（-map 参数）
        /// </summary>
        public FFmpegCommandBuilder Map(string streamSpec)
        {
            if (!string.IsNullOrEmpty(streamSpec))
            {
                _maps.Add(streamSpec);
            }
            return this;
        }

        /// <summary>
        /// 添加 concat demuxer 格式输入（-f concat -safe 0）
        /// </summary>
        public FFmpegCommandBuilder ConcatDemuxer()
        {
            _preInputOptions.Add("-f concat -safe 0");
            return this;
        }

        /// <summary>
        /// 构建最终命令行参数字符串
        /// </summary>
        /// <returns>命令行参数</returns>
        public string Build()
        {
            if (_inputs.Count == 0)
            {
                throw new InvalidOperationException("必须指定至少一个输入文件");
            }
            if (string.IsNullOrEmpty(_output))
            {
                throw new InvalidOperationException("必须指定输出文件");
            }

            var sb = new StringBuilder();

            // 覆盖标志
            if (_overwrite)
            {
                sb.Append("-y ");
            }

            // 输入前选项（-ss, -f concat 等）
            foreach (var opt in _preInputOptions)
            {
                sb.Append(opt);
                sb.Append(" ");
            }

            // 输入文件
            foreach (var input in _inputs)
            {
                sb.Append(input);
                sb.Append(" ");
            }

            // 视频编解码器
            if (!string.IsNullOrEmpty(_videoCodec))
            {
                sb.Append(string.Format("-c:v {0} ", _videoCodec));
            }

            // 音频编解码器
            if (!string.IsNullOrEmpty(_audioCodec))
            {
                sb.Append(string.Format("-c:a {0} ", _audioCodec));
            }

            // 其他选项
            foreach (var option in _options)
            {
                sb.Append(option);
                sb.Append(" ");
            }

            // filter_complex
            if (!string.IsNullOrEmpty(_filterComplex))
            {
                sb.Append(string.Format("-filter_complex \"{0}\" ", _filterComplex));
            }

            // 流映射
            foreach (var map in _maps)
            {
                sb.Append(string.Format("-map \"{0}\" ", map));
            }

            // 输出文件（双引号包裹）
            sb.Append(string.Format("\"{0}\"", _output));

            return sb.ToString();
        }

        /// <summary>
        /// 验证命令是否包含显式编解码器（约束 C7）
        /// 当有视频流时必须指定 -c:v，有音频流时必须指定 -c:a
        /// </summary>
        public bool HasExplicitCodecs
        {
            get
            {
                bool hasNoVideo = _options.Contains("-vn");
                bool hasNoAudio = _options.Contains("-an");
                // concat demuxer 使用 -c copy 时不需要显式编解码器
                bool hasCopy = _videoCodec == "copy" || _audioCodec == "copy";

                // 如果没有禁用视频，必须有视频编解码器
                if (!hasNoVideo && string.IsNullOrEmpty(_videoCodec))
                {
                    return false;
                }
                // 如果没有禁用音频，必须有音频编解码器
                if (!hasNoAudio && string.IsNullOrEmpty(_audioCodec))
                {
                    return false;
                }
                return true;
            }
        }
    }
}
