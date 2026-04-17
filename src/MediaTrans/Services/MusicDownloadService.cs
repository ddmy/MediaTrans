using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaTrans.Models;

namespace MediaTrans.Services
{
    /// <summary>
    /// 音乐下载服务：下载音乐文件并通过 FFmpeg 转码为目标格式
    /// </summary>
    public class MusicDownloadService
    {
        private readonly FFmpegService _ffmpegService;
        private readonly ConfigService _configService;

        public MusicDownloadService(FFmpegService ffmpegService, ConfigService configService)
        {
            _ffmpegService = ffmpegService;
            _configService = configService;
        }

        /// <summary>
        /// 下载并转码音乐文件
        /// </summary>
        /// <param name="streamInfo">播放链接信息</param>
        /// <param name="songName">歌曲名</param>
        /// <param name="artist">歌手</param>
        /// <param name="targetFormat">目标格式（如 .mp3, .flac）</param>
        /// <param name="outputPath">输出文件路径</param>
        /// <param name="progress">进度回调 0-100</param>
        /// <param name="token">取消令牌</param>
        public Task DownloadAndConvertAsync(
            MusicStreamInfo streamInfo,
            string songName,
            string artist,
            string targetFormat,
            string outputPath,
            Action<int> progress,
            CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                // 1. 下载到临时文件
                string tempDir = Path.GetTempPath();
                string tempFile = Path.Combine(tempDir,
                    string.Format("mt_music_{0}.{1}", Guid.NewGuid().ToString("N").Substring(0, 8), streamInfo.Format ?? "mp3"));

                try
                {
                    if (progress != null) progress(5);
                    DownloadFile(streamInfo.Url, tempFile, token, progress);
                    if (progress != null) progress(60);

                    token.ThrowIfCancellationRequested();

                    // 2. 判断是否需要转码
                    string sourceExt = "." + (streamInfo.Format ?? "mp3");
                    if (string.Equals(sourceExt, targetFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        // 无需转码，直接复制
                        File.Copy(tempFile, outputPath, true);
                        if (progress != null) progress(100);
                    }
                    else
                    {
                        // 3. FFmpeg 转码
                        if (progress != null) progress(65);
                        ConvertWithFFmpeg(tempFile, outputPath, targetFormat, songName, artist, token);
                        if (progress != null) progress(100);
                    }
                }
                finally
                {
                    // 清理临时文件
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch { }
                }
            }, token);
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        private void DownloadFile(string url, string outputPath, CancellationToken token, Action<int> progress)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

            if (token.CanBeCanceled)
            {
                token.Register(() =>
                {
                    try { request.Abort(); }
                    catch { }
                });
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var responseStream = response.GetResponseStream())
            using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                long totalBytes = response.ContentLength;
                byte[] buffer = new byte[8192];
                long bytesRead = 0;
                int count;

                while ((count = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    fileStream.Write(buffer, 0, count);
                    bytesRead += count;

                    if (progress != null && totalBytes > 0)
                    {
                        int pct = (int)(5 + (bytesRead * 55.0 / totalBytes)); // 5% ~ 60%
                        progress(Math.Min(pct, 60));
                    }
                }
            }
        }

        /// <summary>
        /// 使用 FFmpeg 转码
        /// </summary>
        private void ConvertWithFFmpeg(string inputPath, string outputPath, string targetFormat,
            string songName, string artist, CancellationToken token)
        {
            // 确定目标编解码器
            string audioCodec;
            switch (targetFormat.ToLowerInvariant())
            {
                case ".mp3":
                    audioCodec = "libmp3lame";
                    break;
                case ".flac":
                    audioCodec = "flac";
                    break;
                case ".wav":
                    audioCodec = "pcm_s16le";
                    break;
                case ".aac":
                case ".m4a":
                    audioCodec = "aac";
                    break;
                case ".ogg":
                    audioCodec = "libvorbis";
                    break;
                case ".opus":
                    audioCodec = "libopus";
                    break;
                default:
                    audioCodec = "libmp3lame";
                    break;
            }

            // 构建 FFmpeg 命令
            string metadataArgs = "";
            if (!string.IsNullOrEmpty(songName))
            {
                metadataArgs += string.Format(" -metadata title=\"{0}\"", songName.Replace("\"", "'"));
            }
            if (!string.IsNullOrEmpty(artist))
            {
                metadataArgs += string.Format(" -metadata artist=\"{0}\"", artist.Replace("\"", "'"));
            }

            string args = string.Format(
                "-y -i \"{0}\" -vn -c:a {1}{2} \"{3}\"",
                inputPath, audioCodec, metadataArgs, outputPath);

            var config = _configService.CurrentConfig;
            string ffmpegPath = config != null ? config.FFmpegPath : @"lib\ffmpeg\ffmpeg.exe";

            // 如果是相对路径，基于应用程序目录解析
            if (!Path.IsPathRooted(ffmpegPath))
            {
                ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ffmpegPath);
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.FileName = ffmpegPath;
            startInfo.Arguments = args;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                // 绑定取消
                if (token.CanBeCanceled)
                {
                    token.Register(() =>
                    {
                        try
                        {
                            if (!process.HasExited) process.Kill();
                        }
                        catch { }
                    });
                }

                process.WaitForExit(120000); // 最多等待 2 分钟

                if (!process.HasExited)
                {
                    process.Kill();
                    throw new TimeoutException("FFmpeg 转码超时");
                }

                if (process.ExitCode != 0)
                {
                    string stderr = process.StandardError.ReadToEnd();
                    throw new InvalidOperationException(
                        string.Format("FFmpeg 转码失败 (exit {0}): {1}", process.ExitCode, stderr));
                }
            }
        }
    }
}
