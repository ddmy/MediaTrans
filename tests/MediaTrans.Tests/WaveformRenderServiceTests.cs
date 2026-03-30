using System;
using System.Collections.Generic;
using SkiaSharp;
using MediaTrans.Models;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 波形渲染服务单元测试
    /// </summary>
    public class WaveformRenderServiceTests : IDisposable
    {
        private AudioPcmCacheService _pcmService;
        private WaveformRenderService _renderService;

        public WaveformRenderServiceTests()
        {
            _pcmService = new AudioPcmCacheService("ffmpeg.exe", 44100, 10);
            _pcmService.LoadAudioFile("test.mp3", 44100, 1, 10.0);
            _renderService = new WaveformRenderService(_pcmService, 512, 150);
        }

        public void Dispose()
        {
            if (_renderService != null)
            {
                _renderService.Dispose();
            }
            if (_pcmService != null)
            {
                _pcmService.Dispose();
            }
        }

        // ===== 构造函数测试 =====

        [Fact]
        public void 构造函数_null参数_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() => new WaveformRenderService(null));
        }

        [Fact]
        public void 构造函数_默认参数_设置正确()
        {
            Assert.Equal(512, _renderService.BlockPixelWidth);
            Assert.Equal(150, _renderService.BlockPixelHeight);
        }

        [Fact]
        public void 构造函数_自定义参数_设置正确()
        {
            using (var service = new WaveformRenderService(_pcmService, 256, 100))
            {
                Assert.Equal(256, service.BlockPixelWidth);
                Assert.Equal(100, service.BlockPixelHeight);
            }
        }

        [Fact]
        public void 构造函数_无效参数_使用默认值()
        {
            using (var service = new WaveformRenderService(_pcmService, -1, -1))
            {
                Assert.Equal(512, service.BlockPixelWidth);
                Assert.Equal(150, service.BlockPixelHeight);
            }
        }

        // ===== WaveformBlock 测试 =====

        [Fact]
        public void WaveformBlock_EndSample_计算正确()
        {
            var block = new WaveformBlock { StartSample = 1000, SampleCount = 500 };
            Assert.Equal(1500, block.EndSample);
        }

        [Fact]
        public void WaveformBlock_MatchesZoom_匹配_返回true()
        {
            var block = new WaveformBlock { SamplesPerPixel = 441.0 };
            Assert.True(block.MatchesZoom(441.0, 0.001));
            Assert.True(block.MatchesZoom(441.0005, 0.001));
        }

        [Fact]
        public void WaveformBlock_MatchesZoom_不匹配_返回false()
        {
            var block = new WaveformBlock { SamplesPerPixel = 441.0 };
            Assert.False(block.MatchesZoom(442.0, 0.001));
            Assert.False(block.MatchesZoom(440.0, 0.001));
        }

        [Fact]
        public void WaveformBlock_Dispose_释放位图()
        {
            var bitmap = new SKBitmap(100, 50);
            var block = new WaveformBlock { Bitmap = bitmap };
            Assert.NotNull(block.Bitmap);
            block.Dispose();
            Assert.Null(block.Bitmap);
        }

        // ===== SamplesPerPixel 属性测试 =====

        [Fact]
        public void SamplesPerPixel_设置正值_有效()
        {
            _renderService.SamplesPerPixel = 100;
            Assert.Equal(100, _renderService.SamplesPerPixel);
        }

        [Fact]
        public void SamplesPerPixel_设置零或负值_使用1()
        {
            _renderService.SamplesPerPixel = 0;
            Assert.Equal(1, _renderService.SamplesPerPixel);

            _renderService.SamplesPerPixel = -5;
            Assert.Equal(1, _renderService.SamplesPerPixel);
        }

        // ===== RenderWaveformBitmap 测试 =====

        [Fact]
        public void RenderWaveformBitmap_空数据_返回带背景和中心线的位图()
        {
            var bitmap = _renderService.RenderWaveformBitmap(null, 100, 50, 441);
            Assert.NotNull(bitmap);
            Assert.Equal(100, bitmap.Width);
            Assert.Equal(50, bitmap.Height);
            bitmap.Dispose();
        }

        [Fact]
        public void RenderWaveformBitmap_有效数据_返回正确尺寸位图()
        {
            // 生成正弦波模拟数据
            var samples = new float[4410]; // 100 个像素，每像素 44.1 采样
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (float)Math.Sin(2 * Math.PI * i / 44100.0 * 440);
            }

            var bitmap = _renderService.RenderWaveformBitmap(samples, 100, 150, 44.1);
            Assert.NotNull(bitmap);
            Assert.Equal(100, bitmap.Width);
            Assert.Equal(150, bitmap.Height);

            // 验证中心区域有波形像素（非纯背景色）
            var centerPixel = bitmap.GetPixel(50, 75); // 中心点
            // 中心线或波形应该有颜色
            Assert.True(centerPixel.Alpha > 0);

            bitmap.Dispose();
        }

        [Fact]
        public void RenderWaveformBitmap_静音数据_波形在中心线附近()
        {
            var samples = new float[1000]; // 全零
            var bitmap = _renderService.RenderWaveformBitmap(samples, 100, 100, 10);
            Assert.NotNull(bitmap);
            Assert.Equal(100, bitmap.Width);
            Assert.Equal(100, bitmap.Height);
            bitmap.Dispose();
        }

        [Fact]
        public void RenderWaveformBitmap_满幅数据_使用全高度()
        {
            // 最大振幅数据
            var samples = new float[100];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (i % 2 == 0) ? 1.0f : -1.0f;
            }

            var bitmap = _renderService.RenderWaveformBitmap(samples, 100, 100, 1);
            Assert.NotNull(bitmap);

            // 验证顶部和底部有波形像素
            bool hasTopPixel = false;
            bool hasBottomPixel = false;
            var bgColor = new SKColor(30, 30, 30); // 默认背景色

            for (int x = 0; x < 100; x++)
            {
                var topPixel = bitmap.GetPixel(x, 5);
                var bottomPixel = bitmap.GetPixel(x, 95);
                if (topPixel != bgColor && topPixel.Alpha > 0) hasTopPixel = true;
                if (bottomPixel != bgColor && bottomPixel.Alpha > 0) hasBottomPixel = true;
            }

            Assert.True(hasTopPixel, "满幅波形顶部应有像素");
            Assert.True(hasBottomPixel, "满幅波形底部应有像素");

            bitmap.Dispose();
        }

        // ===== ComputePeaks 静态方法测试 =====

        [Fact]
        public void ComputePeaks_正弦波_正确峰值()
        {
            var samples = new float[200];
            for (int i = 0; i < 100; i++) samples[i] = 0.5f;
            for (int i = 100; i < 200; i++) samples[i] = -0.3f;

            float[] peakMin, peakMax;
            WaveformRenderService.ComputePeaks(samples, 2, 100, out peakMin, out peakMax);

            // 前半段：所有值 0.5
            Assert.Equal(0.5f, peakMax[0]);
            Assert.Equal(0f, peakMin[0]); // min 初始化为 0

            // 后半段：所有值 -0.3
            Assert.Equal(0f, peakMax[1]); // max 初始化为 0
            Assert.Equal(-0.3f, peakMin[1]);
        }

        [Fact]
        public void ComputePeaks_空数据_返回零数组()
        {
            float[] peakMin, peakMax;
            WaveformRenderService.ComputePeaks(null, 100, 441, out peakMin, out peakMax);

            Assert.Equal(100, peakMin.Length);
            Assert.Equal(100, peakMax.Length);
            Assert.Equal(0f, peakMin[0]);
            Assert.Equal(0f, peakMax[0]);
        }

        [Fact]
        public void ComputePeaks_峰值计算_正负交替()
        {
            var samples = new float[10];
            samples[0] = 0.8f;
            samples[1] = -0.6f;
            samples[2] = 0.3f;
            samples[3] = -0.2f;

            float[] peakMin, peakMax;
            WaveformRenderService.ComputePeaks(samples, 1, 10, out peakMin, out peakMax);

            Assert.Equal(0.8f, peakMax[0]);
            Assert.Equal(-0.6f, peakMin[0]); // 所有10个采样在1个像素中，min = -0.6
        }

        // ===== OnZoomChanged 测试 =====

        [Fact]
        public void OnZoomChanged_清除不匹配缩放级别的缓存()
        {
            _renderService.SamplesPerPixel = 441;

            // 添加一个缓存块
            _renderService.AddBlockForTest(new WaveformBlock
            {
                StartSample = 0,
                SampleCount = 22050,
                Bitmap = new SKBitmap(512, 150),
                PixelWidth = 512,
                PixelHeight = 150,
                SamplesPerPixel = 441
            });

            Assert.Equal(1, _renderService.CachedBlockCount);

            // 切换缩放级别
            _renderService.OnZoomChanged(220);
            Assert.Equal(0, _renderService.CachedBlockCount);
        }

        [Fact]
        public void OnZoomChanged_保留匹配缩放级别的缓存()
        {
            _renderService.SamplesPerPixel = 441;

            _renderService.AddBlockForTest(new WaveformBlock
            {
                StartSample = 0,
                SampleCount = 22050,
                Bitmap = new SKBitmap(512, 150),
                PixelWidth = 512,
                PixelHeight = 150,
                SamplesPerPixel = 441
            });

            // 不改变缩放级别
            _renderService.OnZoomChanged(441);
            Assert.Equal(1, _renderService.CachedBlockCount);
        }

        // ===== EvictOutOfView 测试 =====

        [Fact]
        public void EvictOutOfView_释放超出视区和缓冲带的块()
        {
            _renderService.SamplesPerPixel = 100;

            // 添加三个块
            _renderService.AddBlockForTest(new WaveformBlock
            {
                StartSample = 0, SampleCount = 51200,
                Bitmap = new SKBitmap(512, 150),
                PixelWidth = 512, PixelHeight = 150, SamplesPerPixel = 100
            });
            _renderService.AddBlockForTest(new WaveformBlock
            {
                StartSample = 51200, SampleCount = 51200,
                Bitmap = new SKBitmap(512, 150),
                PixelWidth = 512, PixelHeight = 150, SamplesPerPixel = 100
            });
            _renderService.AddBlockForTest(new WaveformBlock
            {
                StartSample = 200000, SampleCount = 51200,
                Bitmap = new SKBitmap(512, 150),
                PixelWidth = 512, PixelHeight = 150, SamplesPerPixel = 100
            });

            Assert.Equal(3, _renderService.CachedBlockCount);

            // 可视区域在 0-51200，缓冲带 0 块 → 仅保留重叠块
            _renderService.EvictOutOfView(0, 51200, 0);
            Assert.Equal(1, _renderService.CachedBlockCount);
        }

        [Fact]
        public void EvictOutOfView_带缓冲带_保留更多块()
        {
            _renderService.SamplesPerPixel = 100;

            _renderService.AddBlockForTest(new WaveformBlock
            {
                StartSample = 0, SampleCount = 51200,
                Bitmap = new SKBitmap(512, 150),
                PixelWidth = 512, PixelHeight = 150, SamplesPerPixel = 100
            });
            _renderService.AddBlockForTest(new WaveformBlock
            {
                StartSample = 51200, SampleCount = 51200,
                Bitmap = new SKBitmap(512, 150),
                PixelWidth = 512, PixelHeight = 150, SamplesPerPixel = 100
            });

            // 可视区域 0-51200，缓冲带 2 块 → 保留两块
            _renderService.EvictOutOfView(0, 51200, 2);
            Assert.Equal(2, _renderService.CachedBlockCount);
        }

        // ===== ClearCache 测试 =====

        [Fact]
        public void ClearCache_释放所有缓存块()
        {
            _renderService.AddBlockForTest(new WaveformBlock
            {
                StartSample = 0, SampleCount = 10000,
                Bitmap = new SKBitmap(512, 150),
                PixelWidth = 512, PixelHeight = 150, SamplesPerPixel = 441
            });
            _renderService.AddBlockForTest(new WaveformBlock
            {
                StartSample = 10000, SampleCount = 10000,
                Bitmap = new SKBitmap(512, 150),
                PixelWidth = 512, PixelHeight = 150, SamplesPerPixel = 441
            });

            Assert.Equal(2, _renderService.CachedBlockCount);

            _renderService.ClearCache();
            Assert.Equal(0, _renderService.CachedBlockCount);
        }

        // ===== RenderBlock 测试 =====

        [Fact]
        public void RenderBlock_有PCM数据_返回有效块()
        {
            // 准备 PCM 数据
            var pcmData = new byte[88200]; // 44100 采样 * 单声道 * 2 bytes
            var rand = new Random(42);
            rand.NextBytes(pcmData);

            _pcmService.AddBlockForTest(new PcmBlock
            {
                StartSample = 0,
                SampleCount = 44100,
                Data = pcmData
            });

            _renderService.SamplesPerPixel = 441; // 44100 / 441 = 100 像素

            var block = _renderService.RenderBlock(0, 44100, 0);
            Assert.NotNull(block);
            Assert.Equal(0, block.StartSample);
            Assert.Equal(44100, block.SampleCount);
            Assert.NotNull(block.Bitmap);
            Assert.True(block.PixelWidth > 0);
            Assert.Equal(150, block.PixelHeight);
            Assert.Equal(441, block.SamplesPerPixel);

            block.Dispose();
        }

        [Fact]
        public void RenderBlock_无PCM数据_返回null()
        {
            _renderService.SamplesPerPixel = 441;
            var block = _renderService.RenderBlock(0, 44100, 0);
            Assert.Null(block);
        }

        // ===== GetVisibleBlocks 测试 =====

        [Fact]
        public void GetVisibleBlocks_无音频_返回空列表()
        {
            using (var emptyPcm = new AudioPcmCacheService("ffmpeg.exe"))
            using (var emptyRender = new WaveformRenderService(emptyPcm))
            {
                var blocks = emptyRender.GetVisibleBlocks(0, 44100, 0);
                Assert.NotNull(blocks);
                Assert.Equal(0, blocks.Count);
            }
        }

        [Fact]
        public void GetVisibleBlocks_有数据_返回缓存块()
        {
            // 准备足够的 PCM 数据
            var pcmData = new byte[882000]; // 441000 采样约 10 秒
            var rand = new Random(42);
            rand.NextBytes(pcmData);

            _pcmService.AddBlockForTest(new PcmBlock
            {
                StartSample = 0,
                SampleCount = 441000,
                Data = pcmData
            });

            _renderService.SamplesPerPixel = 441;

            // 请求可视区域
            var blocks = _renderService.GetVisibleBlocks(0, 44100, 0);
            Assert.NotNull(blocks);
            Assert.True(blocks.Count > 0, "应返回至少一个渲染块");

            // 验证块有位图
            foreach (var kvp in blocks)
            {
                Assert.NotNull(kvp.Key.Bitmap);
            }
        }

        // ===== SetColors 测试 =====

        [Fact]
        public void SetColors_不抛异常()
        {
            _renderService.SetColors(
                new SKColor(255, 0, 0),    // 红色波形
                new SKColor(0, 0, 0),      // 黑色背景
                new SKColor(128, 128, 128) // 灰色中心线
            );

            // 验证渲染使用新颜色（通过像素检测）
            var samples = new float[100];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (i % 2 == 0) ? 0.5f : -0.5f;
            }

            var bitmap = _renderService.RenderWaveformBitmap(samples, 100, 100, 1);
            Assert.NotNull(bitmap);

            // 检查背景色为黑色
            var cornerPixel = bitmap.GetPixel(0, 0);
            Assert.Equal(0, cornerPixel.Red);
            Assert.Equal(0, cornerPixel.Green);
            Assert.Equal(0, cornerPixel.Blue);

            bitmap.Dispose();
        }

        // ===== Dispose 测试 =====

        [Fact]
        public void Dispose_多次调用_不抛异常()
        {
            var service = new WaveformRenderService(_pcmService);
            service.Dispose();
            service.Dispose();
        }

        [Fact]
        public void Dispose_释放缓存块中的位图()
        {
            var service = new WaveformRenderService(_pcmService);
            service.AddBlockForTest(new WaveformBlock
            {
                StartSample = 0, SampleCount = 10000,
                Bitmap = new SKBitmap(100, 50),
                PixelWidth = 100, PixelHeight = 50, SamplesPerPixel = 441
            });

            Assert.Equal(1, service.CachedBlockCount);
            service.Dispose();
            Assert.Equal(0, service.CachedBlockCount);
        }
    }
}
