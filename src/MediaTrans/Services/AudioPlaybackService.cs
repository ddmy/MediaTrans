using System;
using NAudio.Wave;

namespace MediaTrans.Services
{
    /// <summary>
    /// 播放状态枚举
    /// </summary>
    public enum PlaybackState
    {
        /// <summary>已停止</summary>
        Stopped,
        /// <summary>播放中</summary>
        Playing,
        /// <summary>已暂停</summary>
        Paused
    }

    /// <summary>
    /// 音频播放服务 — 基于 NAudio WaveOut 实现 PCM 音频播放
    /// </summary>
    public class AudioPlaybackService : IDisposable
    {
        private WaveOutEvent _waveOut;
        private WaveFileReader _waveReader;
        private RawSourceWaveStream _rawStream;
        private AudioFileReader _audioFileReader;
        private bool _disposed;

        // 播放范围（采样帧）
        private long _startSample;
        private long _endSample;
        private int _channels;
        private int _sampleRate;
        private int _bytesPerSample;

        /// <summary>
        /// 当前播放状态
        /// </summary>
        public PlaybackState State
        {
            get
            {
                if (_waveOut == null) return PlaybackState.Stopped;
                switch (_waveOut.PlaybackState)
                {
                    case NAudio.Wave.PlaybackState.Playing:
                        return PlaybackState.Playing;
                    case NAudio.Wave.PlaybackState.Paused:
                        return PlaybackState.Paused;
                    default:
                        return PlaybackState.Stopped;
                }
            }
        }

        /// <summary>
        /// 当前播放位置（采样帧）
        /// </summary>
        public long CurrentPositionSamples
        {
            get
            {
                if (_audioFileReader == null) return _startSample;
                int bytesPerFrame = _channels * _bytesPerSample;
                if (bytesPerFrame <= 0) return _startSample;
                long currentSample = _audioFileReader.Position / bytesPerFrame;
                return _startSample + currentSample;
            }
        }

        /// <summary>
        /// 当前播放位置（秒）
        /// </summary>
        public double CurrentPositionSeconds
        {
            get
            {
                if (_sampleRate <= 0) return 0;
                return CurrentPositionSamples / (double)_sampleRate;
            }
        }

        /// <summary>
        /// 播放停止事件
        /// </summary>
        public event EventHandler PlaybackStopped;

        /// <summary>
        /// 采样率
        /// </summary>
        public int SampleRate
        {
            get { return _sampleRate; }
        }

        /// <summary>
        /// 声道数
        /// </summary>
        public int Channels
        {
            get { return _channels; }
        }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying
        {
            get { return State == PlaybackState.Playing; }
        }

        /// <summary>
        /// 是否已暂停
        /// </summary>
        public bool IsPaused
        {
            get { return State == PlaybackState.Paused; }
        }

        /// <summary>
        /// 打开音频文件（全文件播放）
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        public void Open(string filePath)
        {
            CleanupPlayback();

            _audioFileReader = new AudioFileReader(filePath);
            _sampleRate = _audioFileReader.WaveFormat.SampleRate;
            _channels = _audioFileReader.WaveFormat.Channels;
            _bytesPerSample = _audioFileReader.WaveFormat.BitsPerSample / 8;
            _startSample = 0;
            _endSample = _audioFileReader.Length / (_channels * _bytesPerSample);

            InitWaveOut();
        }

        /// <summary>
        /// 播放
        /// </summary>
        public void Play()
        {
            if (_waveOut == null) return;

            if (_waveOut.PlaybackState == NAudio.Wave.PlaybackState.Stopped)
            {
                // 从头开始或回到起始位置
                if (_audioFileReader != null)
                {
                    _audioFileReader.Position = 0;
                }
            }

            _waveOut.Play();
        }

        /// <summary>
        /// 暂停
        /// </summary>
        public void Pause()
        {
            if (_waveOut == null) return;
            if (_waveOut.PlaybackState == NAudio.Wave.PlaybackState.Playing)
            {
                _waveOut.Pause();
            }
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            if (_waveOut == null) return;
            _waveOut.Stop();
            if (_audioFileReader != null)
            {
                _audioFileReader.Position = 0;
            }
        }

        /// <summary>
        /// Seek 到指定采样帧位置
        /// </summary>
        /// <param name="samplePosition">全局采样帧位置</param>
        public void SeekToSample(long samplePosition)
        {
            if (_audioFileReader == null) return;

            long localSample = samplePosition - _startSample;
            if (localSample < 0) localSample = 0;

            long bytePos = localSample * _channels * _bytesPerSample;
            if (bytePos > _audioFileReader.Length)
            {
                bytePos = _audioFileReader.Length;
            }

            _audioFileReader.Position = bytePos;
        }

        /// <summary>
        /// 设置音量
        /// </summary>
        /// <param name="volume">音量系数 0.0 ~ 1.0</param>
        public void SetVolume(float volume)
        {
            if (_audioFileReader != null)
            {
                _audioFileReader.Volume = Math.Max(0f, Math.Min(1f, volume));
            }
        }

        /// <summary>
        /// 获取当前音量
        /// </summary>
        public float GetVolume()
        {
            if (_audioFileReader != null)
            {
                return _audioFileReader.Volume;
            }
            return 1.0f;
        }

        /// <summary>
        /// 初始化 WaveOut 设备
        /// </summary>
        private void InitWaveOut()
        {
            _waveOut = new WaveOutEvent();
            _waveOut.PlaybackStopped += OnWaveOutPlaybackStopped;

            if (_audioFileReader != null)
            {
                _waveOut.Init(_audioFileReader);
            }
        }

        /// <summary>
        /// WaveOut 播放停止回调
        /// </summary>
        private void OnWaveOutPlaybackStopped(object sender, StoppedEventArgs e)
        {
            EventHandler handler = PlaybackStopped;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 清理播放资源
        /// </summary>
        private void CleanupPlayback()
        {
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnWaveOutPlaybackStopped;
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }

            if (_rawStream != null)
            {
                _rawStream.Dispose();
                _rawStream = null;
            }

            if (_waveReader != null)
            {
                _waveReader.Dispose();
                _waveReader = null;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CleanupPlayback();
        }
    }
}
