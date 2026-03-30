using System;
using System.Collections.Generic;
using SkiaSharp;
using MediaTrans.Models;

namespace MediaTrans.Services
{
    /// <summary>
    /// 波形渲染块 — 缓存一段离屏渲染结果
    /// </summary>
    public class WaveformBlock : IDisposable
    {
        /// <summary>
        /// 块在时间轴上的起始采样帧
        /// </summary>
        public long StartSample { get; set; }

        /// <summary>
        /// 块覆盖的采样帧数量
        /// </summary>
        public long SampleCount { get; set; }

        /// <summary>
        /// 渲染后的位图（离屏 Surface 的快照）
        /// </summary>
        public SKBitmap Bitmap { get; set; }

        /// <summary>
        /// 块对应的像素宽度
        /// </summary>
        public int PixelWidth { get; set; }

        /// <summary>
        /// 块对应的像素高度
        /// </summary>
        public int PixelHeight { get; set; }

        /// <summary>
        /// 缩放级别（每像素对应的采样帧数）
        /// </summary>
        public double SamplesPerPixel { get; set; }

        /// <summary>
        /// 结束采样帧（不含）
        /// </summary>
        public long EndSample
        {
            get { return StartSample + SampleCount; }
        }

        /// <summary>
        /// 判断当前缩放级别是否匹配
        /// </summary>
        public bool MatchesZoom(double samplesPerPixel, double tolerance)
        {
            return Math.Abs(SamplesPerPixel - samplesPerPixel) < tolerance;
        }

        public void Dispose()
        {
            if (Bitmap != null)
            {
                Bitmap.Dispose();
                Bitmap = null;
            }
        }
    }

    /// <summary>
    /// 波形渲染服务 — SkiaSharp 离屏 Surface 分块缓存渲染
    /// 按固定像素宽度分块，渲染到离屏 SKSurface 并缓存
    /// 滚动/缩放时复用已缓存块，仅渲染新进入可视区的块
    /// </summary>
    public class WaveformRenderService : IDisposable
    {
        private readonly AudioPcmCacheService _pcmService;
        private readonly object _lock = new object();
        private readonly List<WaveformBlock> _cachedBlocks;

        // 配置参数
        private int _blockPixelWidth;             // 每块像素宽度（默认 512）
        private int _blockPixelHeight;            // 每块像素高度
        private double _samplesPerPixel;          // 当前缩放级别
        private double _dpiScale;                 // DPI 缩放因子（1.0=96DPI, 1.5=144DPI, 2.0=192DPI）

        // 波形颜色
        private SKColor _waveformColor;
        private SKColor _backgroundColor;
        private SKColor _centerLineColor;

        /// <summary>
        /// 当前缩放级别（每像素对应的采样帧数）
        /// </summary>
        public double SamplesPerPixel
        {
            get { return _samplesPerPixel; }
            set { _samplesPerPixel = value > 0 ? value : 1; }
        }

        /// <summary>
        /// 每块像素宽度
        /// </summary>
        public int BlockPixelWidth
        {
            get { return _blockPixelWidth; }
        }

        /// <summary>
        /// 块像素高度
        /// </summary>
        public int BlockPixelHeight
        {
            get { return _blockPixelHeight; }
            set { _blockPixelHeight = value > 0 ? value : 100; }
        }

        /// <summary>
        /// 当前缓存的块数
        /// </summary>
        public int CachedBlockCount
        {
            get
            {
                lock (_lock)
                {
                    return _cachedBlocks.Count;
                }
            }
        }

        /// <summary>
        /// DPI 缩放因子 — 高 DPI 下按此倍率增大渲染像素尺寸，确保波形清晰不模糊
        /// 1.0 = 96 DPI (100%), 1.25 = 120 DPI (125%), 1.5 = 144 DPI (150%), 2.0 = 192 DPI (200%)
        /// </summary>
        public double DpiScale
        {
            get { return _dpiScale; }
            set { _dpiScale = value > 0 ? value : 1.0; }
        }

        /// <summary>
        /// 创建波形渲染服务
        /// </summary>
        /// <param name="pcmService">PCM 缓存服务</param>
        /// <param name="blockPixelWidth">每块像素宽度，默认 512</param>
        /// <param name="blockPixelHeight">每块像素高度，默认 150</param>
        public WaveformRenderService(AudioPcmCacheService pcmService, int blockPixelWidth = 512, int blockPixelHeight = 150)
        {
            if (pcmService == null)
            {
                throw new ArgumentNullException("pcmService");
            }
            _pcmService = pcmService;
            _blockPixelWidth = blockPixelWidth > 0 ? blockPixelWidth : 512;
            _blockPixelHeight = blockPixelHeight > 0 ? blockPixelHeight : 150;
            _samplesPerPixel = 441; // 默认：44100Hz / 100pps ≈ 1秒=100像素
            _dpiScale = 1.0;      // 默认 96 DPI（无缩放）
            _cachedBlocks = new List<WaveformBlock>();

            // 默认颜色配置（暗色主题）
            _waveformColor = new SKColor(0, 200, 100);        // 绿色波形
            _backgroundColor = new SKColor(30, 30, 30);       // 深灰背景
            _centerLineColor = new SKColor(80, 80, 80);       // 灰色中心线
        }

        /// <summary>
        /// 设置波形颜色
        /// </summary>
        public void SetColors(SKColor waveformColor, SKColor backgroundColor, SKColor centerLineColor)
        {
            _waveformColor = waveformColor;
            _backgroundColor = backgroundColor;
            _centerLineColor = centerLineColor;
        }

        /// <summary>
        /// 缩放级别变化时清除不匹配的缓存块
        /// </summary>
        public void OnZoomChanged(double newSamplesPerPixel)
        {
            _samplesPerPixel = newSamplesPerPixel > 0 ? newSamplesPerPixel : 1;
            lock (_lock)
            {
                // 移除缩放级别不匹配的缓存块
                for (int i = _cachedBlocks.Count - 1; i >= 0; i--)
                {
                    if (!_cachedBlocks[i].MatchesZoom(_samplesPerPixel, 0.001))
                    {
                        _cachedBlocks[i].Dispose();
                        _cachedBlocks.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// 获取指定可视区域内需要渲染的块
        /// 返回可直接绘制的块列表以及它们在画布上的 X 偏移
        /// </summary>
        /// <param name="visibleStartSample">可视区域起始采样帧</param>
        /// <param name="visibleEndSample">可视区域结束采样帧</param>
        /// <param name="channel">声道索引（0=左声道）</param>
        /// <returns>渲染块及其 X 偏移量的列表</returns>
        public List<KeyValuePair<WaveformBlock, int>> GetVisibleBlocks(
            long visibleStartSample, long visibleEndSample, int channel)
        {
            var result = new List<KeyValuePair<WaveformBlock, int>>();

            if (_pcmService == null || _pcmService.AudioInfo == null || _samplesPerPixel <= 0)
            {
                return result;
            }

            // 计算块对齐的起始位置
            long samplesPerBlock = (long)(_blockPixelWidth * _samplesPerPixel);
            if (samplesPerBlock <= 0)
            {
                return result;
            }

            long blockAlignedStart = (visibleStartSample / samplesPerBlock) * samplesPerBlock;
            long totalSamples = _pcmService.AudioInfo.TotalSamples;

            for (long blockStart = blockAlignedStart; blockStart < visibleEndSample && blockStart < totalSamples; blockStart += samplesPerBlock)
            {
                long blockSampleCount = Math.Min(samplesPerBlock, totalSamples - blockStart);

                // 查找或渲染此块
                WaveformBlock block = FindCachedBlock(blockStart);
                if (block == null)
                {
                    block = RenderBlock(blockStart, blockSampleCount, channel);
                    if (block != null)
                    {
                        lock (_lock)
                        {
                            _cachedBlocks.Add(block);
                        }
                    }
                }

                if (block != null)
                {
                    // 计算块在画布上的 X 偏移（像素）
                    int xOffset = (int)((blockStart - visibleStartSample) / _samplesPerPixel);
                    result.Add(new KeyValuePair<WaveformBlock, int>(block, xOffset));
                }
            }

            return result;
        }

        /// <summary>
        /// 渲染单个波形块到离屏 Surface
        /// </summary>
        /// <param name="startSample">起始采样帧</param>
        /// <param name="sampleCount">采样帧数量</param>
        /// <param name="channel">声道索引</param>
        /// <returns>渲染完成的波形块，如果 PCM 数据不可用返回 null</returns>
        public WaveformBlock RenderBlock(long startSample, long sampleCount, int channel)
        {
            // 计算此块实际需要的像素宽度
            int pixelWidth = (int)(sampleCount / _samplesPerPixel);
            if (pixelWidth <= 0)
            {
                pixelWidth = 1;
            }
            if (pixelWidth > _blockPixelWidth)
            {
                pixelWidth = _blockPixelWidth;
            }

            // 获取 PCM 采样数据
            float[] samples = _pcmService.GetSamples(startSample, (int)Math.Min(sampleCount, int.MaxValue), channel);
            if (samples == null)
            {
                return null;
            }

            // 高 DPI 下按缩放因子增大渲染尺寸，确保波形清晰
            int renderWidth = (int)(pixelWidth * _dpiScale);
            int renderHeight = (int)(_blockPixelHeight * _dpiScale);
            if (renderWidth < 1) renderWidth = 1;
            if (renderHeight < 1) renderHeight = 1;

            // 按物理像素尺寸渲染，samplesPerPixel 也要对应缩放
            double renderSamplesPerPixel = _samplesPerPixel / _dpiScale;

            // 创建离屏 Surface 渲染
            var bitmap = RenderWaveformBitmap(samples, renderWidth, renderHeight, renderSamplesPerPixel);

            return new WaveformBlock
            {
                StartSample = startSample,
                SampleCount = sampleCount,
                Bitmap = bitmap,
                PixelWidth = pixelWidth,
                PixelHeight = _blockPixelHeight,
                SamplesPerPixel = _samplesPerPixel
            };
        }

        /// <summary>
        /// 使用 SkiaSharp 渲染波形到位图
        /// 采用峰值采样：每像素计算对应采样帧范围的最大/最小值
        /// </summary>
        public SKBitmap RenderWaveformBitmap(float[] samples, int width, int height, double samplesPerPixel)
        {
            var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bitmap))
            {
                // 填充背景
                canvas.Clear(_backgroundColor);

                float centerY = height / 2f;

                // 绘制中心线
                using (var centerPaint = new SKPaint())
                {
                    centerPaint.Color = _centerLineColor;
                    centerPaint.StrokeWidth = 1;
                    centerPaint.IsAntialias = false;
                    canvas.DrawLine(0, centerY, width, centerY, centerPaint);
                }

                if (samples == null || samples.Length == 0)
                {
                    return bitmap;
                }

                // 绘制波形
                using (var wavePaint = new SKPaint())
                {
                    wavePaint.Color = _waveformColor;
                    wavePaint.StrokeWidth = 1;
                    wavePaint.IsAntialias = true;

                    for (int x = 0; x < width; x++)
                    {
                        // 计算当前像素对应的采样帧范围
                        int sampleStart = (int)(x * samplesPerPixel);
                        int sampleEnd = (int)((x + 1) * samplesPerPixel);
                        sampleEnd = Math.Min(sampleEnd, samples.Length);

                        if (sampleStart >= samples.Length)
                        {
                            break;
                        }

                        // 查找范围内的最大/最小值
                        float minVal = 0f;
                        float maxVal = 0f;
                        for (int i = sampleStart; i < sampleEnd; i++)
                        {
                            float s = samples[i];
                            if (s < minVal) minVal = s;
                            if (s > maxVal) maxVal = s;
                        }

                        // 映射到像素坐标（-1.0 → 顶部, +1.0 → 底部）
                        float yTop = centerY - (maxVal * centerY);
                        float yBottom = centerY - (minVal * centerY);

                        // 确保至少绘制 1 像素
                        if (Math.Abs(yTop - yBottom) < 1)
                        {
                            yTop = centerY - 0.5f;
                            yBottom = centerY + 0.5f;
                        }

                        canvas.DrawLine(x, yTop, x, yBottom, wavePaint);
                    }
                }
            }

            return bitmap;
        }

        /// <summary>
        /// 计算指定采样帧范围内每像素的峰值数据（最大/最小）
        /// 用于外部自定义渲染
        /// </summary>
        public static void ComputePeaks(float[] samples, int pixelWidth, double samplesPerPixel,
            out float[] peakMin, out float[] peakMax)
        {
            peakMin = new float[pixelWidth];
            peakMax = new float[pixelWidth];

            if (samples == null || samples.Length == 0)
            {
                return;
            }

            for (int x = 0; x < pixelWidth; x++)
            {
                int sampleStart = (int)(x * samplesPerPixel);
                int sampleEnd = (int)((x + 1) * samplesPerPixel);
                sampleEnd = Math.Min(sampleEnd, samples.Length);

                if (sampleStart >= samples.Length)
                {
                    break;
                }

                float minV = 0f;
                float maxV = 0f;
                for (int i = sampleStart; i < sampleEnd; i++)
                {
                    float s = samples[i];
                    if (s < minV) minV = s;
                    if (s > maxV) maxV = s;
                }

                peakMin[x] = minV;
                peakMax[x] = maxV;
            }
        }

        /// <summary>
        /// 释放超出可视范围的缓存块
        /// </summary>
        public void EvictOutOfView(long visibleStartSample, long visibleEndSample, int bufferBlocks)
        {
            long samplesPerBlock = (long)(_blockPixelWidth * _samplesPerPixel);
            if (samplesPerBlock <= 0)
            {
                return;
            }

            long bufferSamples = samplesPerBlock * bufferBlocks;
            long keepStart = visibleStartSample - bufferSamples;
            long keepEnd = visibleEndSample + bufferSamples;

            lock (_lock)
            {
                for (int i = _cachedBlocks.Count - 1; i >= 0; i--)
                {
                    var block = _cachedBlocks[i];
                    if (block.EndSample <= keepStart || block.StartSample >= keepEnd)
                    {
                        block.Dispose();
                        _cachedBlocks.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// 清除所有缓存块
        /// </summary>
        public void ClearCache()
        {
            lock (_lock)
            {
                foreach (var block in _cachedBlocks)
                {
                    block.Dispose();
                }
                _cachedBlocks.Clear();
            }
        }

        /// <summary>
        /// 查找已缓存的块
        /// </summary>
        private WaveformBlock FindCachedBlock(long startSample)
        {
            lock (_lock)
            {
                foreach (var block in _cachedBlocks)
                {
                    if (block.StartSample == startSample && block.MatchesZoom(_samplesPerPixel, 0.001))
                    {
                        return block;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 添加块（仅供测试使用）
        /// </summary>
        public void AddBlockForTest(WaveformBlock block)
        {
            lock (_lock)
            {
                _cachedBlocks.Add(block);
            }
        }

        public void Dispose()
        {
            ClearCache();
        }
    }
}
