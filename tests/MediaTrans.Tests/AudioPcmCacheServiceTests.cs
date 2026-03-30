using System;
using System.Collections.Generic;
using MediaTrans.Models;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 音频 PCM 缓存服务单元测试
    /// </summary>
    public class AudioPcmCacheServiceTests : IDisposable
    {
        private AudioPcmCacheService _service;

        public AudioPcmCacheServiceTests()
        {
            _service = new AudioPcmCacheService("ffmpeg.exe", 44100, 10);
        }

        public void Dispose()
        {
            if (_service != null)
            {
                _service.Dispose();
            }
        }

        // ===== AudioInfo 基本测试 =====

        [Fact]
        public void AudioInfo_SamplesToSeconds_正确转换()
        {
            var info = new AudioInfo { SampleRate = 44100 };
            double seconds = info.SamplesToSeconds(44100);
            Assert.Equal(1.0, seconds, 3);
        }

        [Fact]
        public void AudioInfo_SecondsToSamples_正确转换()
        {
            var info = new AudioInfo { SampleRate = 44100 };
            long samples = info.SecondsToSamples(1.0);
            Assert.Equal(44100, samples);
        }

        [Fact]
        public void AudioInfo_SecondsToSamples_负值返回0()
        {
            var info = new AudioInfo { SampleRate = 44100 };
            Assert.Equal(0, info.SecondsToSamples(-1.0));
        }

        [Fact]
        public void AudioInfo_SamplesToSeconds_采样率为0_返回0()
        {
            var info = new AudioInfo { SampleRate = 0 };
            Assert.Equal(0, info.SamplesToSeconds(1000));
        }

        [Fact]
        public void AudioInfo_BytesPerFrame_立体声16bit_返回4()
        {
            var info = new AudioInfo { Channels = 2, BytesPerSample = 2 };
            Assert.Equal(4, info.BytesPerFrame);
        }

        [Fact]
        public void AudioInfo_BytesPerFrame_单声道16bit_返回2()
        {
            var info = new AudioInfo { Channels = 1, BytesPerSample = 2 };
            Assert.Equal(2, info.BytesPerFrame);
        }

        // ===== PcmBlock 测试 =====

        [Fact]
        public void PcmBlock_EndSample_计算正确()
        {
            var block = new PcmBlock { StartSample = 100, SampleCount = 50 };
            Assert.Equal(150, block.EndSample);
        }

        [Fact]
        public void PcmBlock_Overlaps_重叠_返回true()
        {
            var block = new PcmBlock { StartSample = 100, SampleCount = 100 };
            Assert.True(block.Overlaps(50, 150));  // 部分重叠
            Assert.True(block.Overlaps(100, 200)); // 左边对齐
            Assert.True(block.Overlaps(100, 150)); // 完全包含
            Assert.True(block.Overlaps(0, 300));   // 被完全包含
        }

        [Fact]
        public void PcmBlock_Overlaps_不重叠_返回false()
        {
            var block = new PcmBlock { StartSample = 100, SampleCount = 100 };
            Assert.False(block.Overlaps(0, 100));   // 刚好不重叠（结束=开始）
            Assert.False(block.Overlaps(200, 300)); // 在后面
            Assert.False(block.Overlaps(0, 50));    // 完全在前面
        }

        [Fact]
        public void PcmBlock_GetByteOffset_有效偏移_返回正确值()
        {
            var block = new PcmBlock { StartSample = 100, SampleCount = 50 };
            // 立体声16bit: bytesPerFrame = 4
            Assert.Equal(0, block.GetByteOffset(100, 4));   // 第一帧
            Assert.Equal(4, block.GetByteOffset(101, 4));   // 第二帧
            Assert.Equal(196, block.GetByteOffset(149, 4)); // 最后一帧
        }

        [Fact]
        public void PcmBlock_GetByteOffset_越界_返回负1()
        {
            var block = new PcmBlock { StartSample = 100, SampleCount = 50 };
            Assert.Equal(-1, block.GetByteOffset(99, 4));   // 块前
            Assert.Equal(-1, block.GetByteOffset(150, 4));  // 块后
        }

        // ===== 构造函数测试 =====

        [Fact]
        public void 构造函数_null路径_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() => new AudioPcmCacheService(null));
        }

        [Fact]
        public void 构造函数_默认参数_设置正确()
        {
            var service = new AudioPcmCacheService("ffmpeg.exe");
            Assert.Equal(44100, service.BlockSampleCount);
            Assert.Equal(10, service.BufferBlockCount);
            service.Dispose();
        }

        [Fact]
        public void 构造函数_自定义参数_设置正确()
        {
            var service = new AudioPcmCacheService("ffmpeg.exe", 22050, 5);
            Assert.Equal(22050, service.BlockSampleCount);
            Assert.Equal(5, service.BufferBlockCount);
            service.Dispose();
        }

        // ===== LoadAudioFile 测试 =====

        [Fact]
        public void LoadAudioFile_返回正确AudioInfo()
        {
            var info = _service.LoadAudioFile("test.mp3", 44100, 2, 60.0);

            Assert.NotNull(info);
            Assert.Equal("test.mp3", info.FilePath);
            Assert.Equal(44100, info.SampleRate);
            Assert.Equal(2, info.Channels);
            Assert.Equal(60.0, info.DurationSeconds);
            Assert.Equal(2646000, info.TotalSamples);
            Assert.Equal(2, info.BytesPerSample);
        }

        [Fact]
        public void LoadAudioFile_重新加载_清除旧缓存()
        {
            _service.LoadAudioFile("test1.mp3", 44100, 2, 10.0);

            // 手动添加一些模拟缓存块
            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 0,
                SampleCount = 1000,
                Data = new byte[4000]
            });

            Assert.Equal(1, _service.CachedBlockCount);

            // 重新加载新文件
            _service.LoadAudioFile("test2.mp3", 48000, 1, 30.0);

            Assert.Equal(0, _service.CachedBlockCount);
            Assert.Equal("test2.mp3", _service.AudioInfo.FilePath);
            Assert.Equal(48000, _service.AudioInfo.SampleRate);
        }

        // ===== GetSamples 测试 =====

        [Fact]
        public void GetSamples_无缓存_返回null()
        {
            _service.LoadAudioFile("test.mp3", 44100, 2, 10.0);
            var samples = _service.GetSamples(0, 1000, 0);
            Assert.Null(samples);
        }

        [Fact]
        public void GetSamples_有缓存数据_返回正确浮点值()
        {
            _service.LoadAudioFile("test.mp3", 44100, 1, 10.0);

            // 创建模拟 PCM 数据：单声道 16-bit
            // 样本值 = 16384（half of max 32768）→ float ≈ 0.5
            var data = new byte[4]; // 2 samples
            data[0] = 0x00; data[1] = 0x40; // 16384 (little-endian)
            data[2] = 0x00; data[3] = 0xC0; // -16384 (little-endian)

            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 0,
                SampleCount = 2,
                Data = data
            });

            var samples = _service.GetSamples(0, 2, 0);
            Assert.NotNull(samples);
            Assert.Equal(2, samples.Length);
            Assert.Equal(0.5, (double)samples[0], 3);    // 16384/32768
            Assert.Equal(-0.5, (double)samples[1], 3);   // -16384/32768
        }

        [Fact]
        public void GetSamples_立体声_分离声道()
        {
            _service.LoadAudioFile("test.mp3", 44100, 2, 10.0);

            // 立体声 16-bit: 每帧 4 字节 (L_low, L_high, R_low, R_high)
            var data = new byte[8]; // 2 frames
            // 帧1: 左=16384, 右=-16384
            data[0] = 0x00; data[1] = 0x40; // L = 16384
            data[2] = 0x00; data[3] = 0xC0; // R = -16384
            // 帧2: 左=-8192, 右=8192
            data[4] = 0x00; data[5] = 0xE0; // L = -8192
            data[6] = 0x00; data[7] = 0x20; // R = 8192

            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 0,
                SampleCount = 2,
                Data = data
            });

            // 左声道
            var left = _service.GetSamples(0, 2, 0);
            Assert.NotNull(left);
            Assert.Equal(0.5, (double)left[0], 3);
            Assert.Equal(-0.25, (double)left[1], 3);

            // 右声道
            var right = _service.GetSamples(0, 2, 1);
            Assert.NotNull(right);
            Assert.Equal(-0.5, (double)right[0], 3);
            Assert.Equal(0.25, (double)right[1], 3);
        }

        [Fact]
        public void GetSamples_无效声道_返回null()
        {
            _service.LoadAudioFile("test.mp3", 44100, 2, 10.0);
            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 0,
                SampleCount = 100,
                Data = new byte[400]
            });

            Assert.Null(_service.GetSamples(0, 100, 2));  // 声道2不存在
            Assert.Null(_service.GetSamples(0, 100, -1)); // 负数声道
        }

        [Fact]
        public void GetSamples_跨块获取_合并数据()
        {
            _service.LoadAudioFile("test.mp3", 44100, 1, 10.0);

            // 块1: 采样 0-99
            var data1 = new byte[200]; // 100 samples * 2 bytes
            data1[0] = 0x00; data1[1] = 0x40; // sample 0 = 16384

            // 块2: 采样 100-199
            var data2 = new byte[200];
            data2[0] = 0x00; data2[1] = 0xC0; // sample 100 = -16384

            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 0, SampleCount = 100, Data = data1
            });
            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 100, SampleCount = 100, Data = data2
            });

            // 跨块获取 50-150
            var samples = _service.GetSamples(50, 100, 0);
            Assert.NotNull(samples);
            Assert.Equal(100, samples.Length);
        }

        // ===== Evict 测试 =====

        [Fact]
        public void Evict_释放超出范围的块()
        {
            _service.LoadAudioFile("test.mp3", 44100, 1, 60.0);

            // 添加三个块
            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 0, SampleCount = 1000, Data = new byte[2000]
            });
            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 1000, SampleCount = 1000, Data = new byte[2000]
            });
            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 2000, SampleCount = 1000, Data = new byte[2000]
            });

            Assert.Equal(3, _service.CachedBlockCount);

            // 仅保留 1000-2000 范围
            _service.Evict(1000, 2000);

            Assert.Equal(1, _service.CachedBlockCount);
        }

        [Fact]
        public void Evict_释放所有块_当范围不包含任何块()
        {
            _service.LoadAudioFile("test.mp3", 44100, 1, 60.0);

            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 0, SampleCount = 100, Data = new byte[200]
            });

            _service.Evict(1000, 2000);

            Assert.Equal(0, _service.CachedBlockCount);
        }

        // ===== CachedBytes 测试 =====

        [Fact]
        public void CachedBytes_计算正确()
        {
            _service.LoadAudioFile("test.mp3", 44100, 1, 10.0);

            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 0, SampleCount = 100, Data = new byte[200]
            });
            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 100, SampleCount = 50, Data = new byte[100]
            });

            Assert.Equal(300, _service.CachedBytes);
        }

        [Fact]
        public void CachedBytes_空缓存_返回0()
        {
            Assert.Equal(0, _service.CachedBytes);
        }

        // ===== ClearCache 测试 =====

        [Fact]
        public void ClearCache_清空所有缓存块()
        {
            _service.LoadAudioFile("test.mp3", 44100, 1, 10.0);

            _service.AddBlockForTest(new PcmBlock
            {
                StartSample = 0, SampleCount = 100, Data = new byte[200]
            });

            Assert.Equal(1, _service.CachedBlockCount);

            _service.ClearCache();

            Assert.Equal(0, _service.CachedBlockCount);
            Assert.Equal(0, _service.CachedBytes);
        }

        // ===== BuildDecodeArguments 测试 =====

        [Fact]
        public void BuildDecodeArguments_包含正确参数()
        {
            string args = AudioPcmCacheService.BuildDecodeArguments(
                "C:\\测试 目录\\音频.mp3", 1.5, 2.0, 44100, 2);

            Assert.Contains("-ss", args);
            Assert.Contains("-t", args);
            Assert.Contains("-i \"C:\\测试 目录\\音频.mp3\"", args);
            Assert.Contains("-f s16le", args);
            Assert.Contains("-acodec pcm_s16le", args);
            Assert.Contains("-ar 44100", args);
            Assert.Contains("-ac 2", args);
        }

        [Fact]
        public void BuildDecodeArguments_路径含中文和空格_双引号包裹()
        {
            string args = AudioPcmCacheService.BuildDecodeArguments(
                "C:\\我的 音乐\\歌曲 (1).wav", 0, 10, 48000, 1);

            Assert.Contains("\"C:\\我的 音乐\\歌曲 (1).wav\"", args);
        }

        [Fact]
        public void BuildDecodeArguments_float精度_不丢失()
        {
            string args = AudioPcmCacheService.BuildDecodeArguments(
                "test.mp3", 1.123456, 0.500000, 44100, 2);

            Assert.Contains("1.123456", args);
            Assert.Contains("0.500000", args);
        }

        // ===== Dispose 测试 =====

        [Fact]
        public void Dispose_不抛异常()
        {
            var service = new AudioPcmCacheService("ffmpeg.exe");
            service.LoadAudioFile("test.mp3", 44100, 2, 10.0);
            service.AddBlockForTest(new PcmBlock
            {
                StartSample = 0, SampleCount = 100, Data = new byte[400]
            });

            service.Dispose();
            Assert.Equal(0, service.CachedBlockCount);
        }

        [Fact]
        public void Dispose_多次调用_不抛异常()
        {
            var service = new AudioPcmCacheService("ffmpeg.exe");
            service.Dispose();
            service.Dispose(); // 第二次调用不应抛异常
        }
    }
}
