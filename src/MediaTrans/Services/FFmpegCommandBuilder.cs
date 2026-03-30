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
        private string _videoCodec;
        private string _audioCodec;
        private string _output;
        private bool _overwrite;

        public FFmpegCommandBuilder()
        {
            _inputs = new List<string>();
            _options = new List<string>();
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
