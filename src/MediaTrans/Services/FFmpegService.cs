using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaTrans.Models;

namespace MediaTrans.Services
{
    /// <summary>
    /// FFmpeg/FFprobe 进程封装服务
    /// 负责启动、取消、超时管理；解析进度；Job Object 绑定防进程残留
    /// </summary>
    public class FFmpegService : IDisposable
    {
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;
        private readonly JobObject _jobObject;
        private bool _disposed;

        // 进度解析正则：time=HH:MM:SS.xx 或 time=SS.xx
        private static readonly Regex TimeRegex = new Regex(
            @"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2})",
            RegexOptions.Compiled);

        // 速度解析
        private static readonly Regex SpeedRegex = new Regex(
            @"speed=\s*([\d.]+)x",
            RegexOptions.Compiled);

        // 帧数解析
        private static readonly Regex FrameRegex = new Regex(
            @"frame=\s*(\d+)",
            RegexOptions.Compiled);

        // 比特率解析
        private static readonly Regex BitrateRegex = new Regex(
            @"bitrate=\s*([\d.]+\s*\w+/s)",
            RegexOptions.Compiled);

        // Duration 解析（从 FFprobe/FFmpeg 输出中提取总时长）
        private static readonly Regex DurationRegex = new Regex(
            @"Duration:\s*(\d{2}):(\d{2}):(\d{2})\.(\d{2})",
            RegexOptions.Compiled);

        /// <summary>
        /// 进度更新事件
        /// </summary>
        public event EventHandler<FFmpegProgressEventArgs> ProgressChanged;

        /// <summary>
        /// 创建 FFmpegService 实例
        /// </summary>
        /// <param name="config">应用配置</param>
        public FFmpegService(AppConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            _ffmpegPath = config.FFmpegPath;
            _ffprobePath = config.FFprobePath;
            _jobObject = new JobObject();
        }

        /// <summary>
        /// 使用指定路径创建实例（测试用）
        /// </summary>
        public FFmpegService(string ffmpegPath, string ffprobePath)
        {
            _ffmpegPath = ffmpegPath;
            _ffprobePath = ffprobePath;
            _jobObject = new JobObject();
        }

        /// <summary>
        /// 执行 FFmpeg 命令
        /// </summary>
        /// <param name="arguments">命令行参数</param>
        /// <param name="totalDurationSeconds">总时长（秒），用于计算进度百分比</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeoutMs">超时毫秒数，0 表示不超时</param>
        /// <returns>执行结果</returns>
        public Task<FFmpegResult> ExecuteAsync(string arguments, double totalDurationSeconds = 0,
            CancellationToken cancellationToken = default(CancellationToken), int timeoutMs = 0)
        {
            return RunProcessAsync(_ffmpegPath, arguments, totalDurationSeconds, cancellationToken, timeoutMs);
        }

        /// <summary>
        /// 执行 FFprobe 命令
        /// </summary>
        /// <param name="arguments">命令行参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeoutMs">超时毫秒数</param>
        /// <returns>执行结果</returns>
        public Task<FFmpegResult> ProbeAsync(string arguments,
            CancellationToken cancellationToken = default(CancellationToken), int timeoutMs = 30000)
        {
            return RunProcessAsync(_ffprobePath, arguments, 0, cancellationToken, timeoutMs);
        }

        /// <summary>
        /// 执行进程并收集输出
        /// </summary>
        private Task<FFmpegResult> RunProcessAsync(string executablePath, string arguments,
            double totalDurationSeconds, CancellationToken cancellationToken, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<FFmpegResult>();

            Task.Run(() =>
            {
                var result = new FFmpegResult();
                var stdoutBuilder = new StringBuilder();
                var stderrBuilder = new StringBuilder();
                var stopwatch = Stopwatch.StartNew();
                Process process = null;

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    process = new Process();
                    process.StartInfo = startInfo;
                    process.EnableRaisingEvents = true;

                    // 异步读取标准输出
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            stdoutBuilder.AppendLine(e.Data);
                        }
                    };

                    // 异步读取标准错误（FFmpeg 进度信息在 stderr）
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            stderrBuilder.AppendLine(e.Data);
                            ParseProgress(e.Data, totalDurationSeconds);
                        }
                    };

                    process.Start();

                    // 绑定到 Job Object，确保主进程退出时子进程也被终止
                    try
                    {
                        _jobObject.AssignProcess(process.Handle);
                    }
                    catch (Exception)
                    {
                        // Job Object 分配失败不阻止执行
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // 等待进程完成、超时或取消
                    bool completed = false;
                    while (!completed)
                    {
                        // 检查取消
                        if (cancellationToken.IsCancellationRequested)
                        {
                            KillProcess(process);
                            result.Cancelled = true;
                            result.Success = false;
                            result.ErrorMessage = "操作已取消";
                            break;
                        }

                        // 检查超时
                        if (timeoutMs > 0 && stopwatch.ElapsedMilliseconds > timeoutMs)
                        {
                            KillProcess(process);
                            result.TimedOut = true;
                            result.Success = false;
                            result.ErrorMessage = string.Format("操作超时（{0}ms）", timeoutMs);
                            break;
                        }

                        completed = process.WaitForExit(100);
                    }

                    if (completed)
                    {
                        // 确保所有输出都已读取
                        process.WaitForExit();
                        result.ExitCode = process.ExitCode;
                        result.Success = process.ExitCode == 0;
                        if (!result.Success)
                        {
                            result.ErrorMessage = string.Format("FFmpeg 退出码: {0}", process.ExitCode);
                        }
                    }

                    stopwatch.Stop();
                    result.Duration = stopwatch.Elapsed;
                    result.StandardOutput = stdoutBuilder.ToString();
                    result.StandardError = stderrBuilder.ToString();

                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    result.Duration = stopwatch.Elapsed;
                    result.StandardOutput = stdoutBuilder.ToString();
                    result.StandardError = stderrBuilder.ToString();
                    tcs.TrySetResult(result);
                }
                finally
                {
                    if (process != null)
                    {
                        process.Dispose();
                    }
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// 解析 FFmpeg stderr 输出中的进度信息
        /// </summary>
        private void ParseProgress(string line, double totalDurationSeconds)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            var handler = ProgressChanged;
            if (handler == null)
            {
                return;
            }

            var timeMatch = TimeRegex.Match(line);
            if (timeMatch.Success)
            {
                int hours = int.Parse(timeMatch.Groups[1].Value);
                int minutes = int.Parse(timeMatch.Groups[2].Value);
                int seconds = int.Parse(timeMatch.Groups[3].Value);
                int centiseconds = int.Parse(timeMatch.Groups[4].Value);

                double processedSeconds = hours * 3600.0 + minutes * 60.0 + seconds + centiseconds / 100.0;

                var args = new FFmpegProgressEventArgs
                {
                    ProcessedSeconds = processedSeconds,
                    TotalSeconds = totalDurationSeconds
                };

                // 解析速度
                var speedMatch = SpeedRegex.Match(line);
                if (speedMatch.Success)
                {
                    args.Speed = speedMatch.Groups[1].Value + "x";
                }

                // 解析帧数
                var frameMatch = FrameRegex.Match(line);
                if (frameMatch.Success)
                {
                    args.Frame = long.Parse(frameMatch.Groups[1].Value);
                }

                // 解析比特率
                var bitrateMatch = BitrateRegex.Match(line);
                if (bitrateMatch.Success)
                {
                    args.Bitrate = bitrateMatch.Groups[1].Value;
                }

                handler(this, args);
            }
        }

        /// <summary>
        /// 从 FFprobe/FFmpeg 输出解析总时长
        /// </summary>
        /// <param name="output">FFprobe 标准错误输出</param>
        /// <returns>时长（秒），解析失败返回 0</returns>
        public static double ParseDuration(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return 0;
            }

            var match = DurationRegex.Match(output);
            if (match.Success)
            {
                int hours = int.Parse(match.Groups[1].Value);
                int minutes = int.Parse(match.Groups[2].Value);
                int seconds = int.Parse(match.Groups[3].Value);
                int centiseconds = int.Parse(match.Groups[4].Value);
                return hours * 3600.0 + minutes * 60.0 + seconds + centiseconds / 100.0;
            }
            return 0;
        }

        /// <summary>
        /// 安全终止进程及其子进程
        /// </summary>
        private static void KillProcess(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception)
            {
                // 进程可能已退出
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _jobObject.Dispose();
                _disposed = true;
            }
        }
    }
}
