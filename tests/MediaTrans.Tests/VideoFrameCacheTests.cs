using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Xunit;
using MediaTrans.Services;
using MediaTrans.ViewModels;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 视频帧缓存服务测试
    /// </summary>
    public class VideoFrameCacheServiceTests
    {
        #region 构造函数测试

        [Fact]
        public void 构造函数_有效参数_创建成功()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            Assert.Equal(100, service.MaxCachedFrames);
            Assert.Equal(0, service.CachedFrameCount);
            Assert.False(service.IsVideoLoaded);
        }

        [Fact]
        public void 构造函数_空路径_应抛异常()
        {
            Assert.Throws<ArgumentNullException>(() => new VideoFrameCacheService(null, 100));
            Assert.Throws<ArgumentNullException>(() => new VideoFrameCacheService("", 100));
        }

        [Fact]
        public void 构造函数_帧数为零_应抛异常()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VideoFrameCacheService("ffmpeg.exe", 0));
        }

        [Fact]
        public void 构造函数_帧数为负_应抛异常()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VideoFrameCacheService("ffmpeg.exe", -1));
        }

        #endregion

        #region LoadVideo 测试

        [Fact]
        public void LoadVideo_有效参数_状态正确()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 30.0, 120.0, 1920, 1080);

            Assert.True(service.IsVideoLoaded);
            Assert.Equal(30.0, service.FrameRate);
            Assert.Equal(120.0, service.TotalDurationSeconds);
            Assert.Equal(1920, service.VideoWidth);
            Assert.Equal(1080, service.VideoHeight);
            Assert.Equal(3600, service.TotalFrames); // 120 * 30
        }

        [Fact]
        public void LoadVideo_空路径_应抛异常()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            Assert.Throws<ArgumentNullException>(() =>
                service.LoadVideo(null, 30.0, 120.0, 1920, 1080));
        }

        [Fact]
        public void LoadVideo_帧率为零_应抛异常()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                service.LoadVideo("test.mp4", 0, 120.0, 1920, 1080));
        }

        [Fact]
        public void LoadVideo_重复加载_清空旧缓存()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test1.mp4", 30.0, 60.0, 1920, 1080);
            // 此时缓存为空，加载新视频应正常
            service.LoadVideo("test2.mp4", 25.0, 90.0, 1280, 720);

            Assert.True(service.IsVideoLoaded);
            Assert.Equal(25.0, service.FrameRate);
            Assert.Equal(90.0, service.TotalDurationSeconds);
            Assert.Equal(1280, service.VideoWidth);
            Assert.Equal(720, service.VideoHeight);
            Assert.Equal(0, service.CachedFrameCount);
        }

        #endregion

        #region UnloadVideo 测试

        [Fact]
        public void UnloadVideo_清除状态()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 30.0, 120.0, 1920, 1080);
            service.UnloadVideo();

            Assert.False(service.IsVideoLoaded);
            Assert.Equal(0.0, service.FrameRate);
            Assert.Equal(0.0, service.TotalDurationSeconds);
            Assert.Equal(0, service.VideoWidth);
            Assert.Equal(0, service.VideoHeight);
            Assert.Equal(0, service.TotalFrames);
            Assert.Equal(0, service.CachedFrameCount);
        }

        #endregion

        #region 帧/时间戳转换测试

        [Fact]
        public void FrameToTimestamp_正常转换()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 25.0, 100.0, 1920, 1080);

            Assert.Equal(0.0, service.FrameToTimestamp(0));
            Assert.Equal(1.0, service.FrameToTimestamp(25));
            Assert.Equal(2.0, service.FrameToTimestamp(50));
            Assert.Equal(0.04, service.FrameToTimestamp(1), 6);
        }

        [Fact]
        public void FrameToTimestamp_未加载视频_返回零()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            Assert.Equal(0.0, service.FrameToTimestamp(100));
        }

        [Fact]
        public void TimestampToFrame_正常转换()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            Assert.Equal(0, service.TimestampToFrame(0.0));
            Assert.Equal(30, service.TimestampToFrame(1.0));
            Assert.Equal(150, service.TimestampToFrame(5.0));
        }

        [Fact]
        public void TimestampToFrame_负数_钳位到零()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            Assert.Equal(0, service.TimestampToFrame(-1.0));
        }

        [Fact]
        public void TimestampToFrame_超出范围_钳位到最后帧()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            long lastFrame = service.TotalFrames - 1;
            Assert.Equal(lastFrame, service.TimestampToFrame(999.0));
        }

        [Fact]
        public void TimestampToFrame_未加载视频_返回零()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            Assert.Equal(0, service.TimestampToFrame(5.0));
        }

        #endregion

        #region LRU 缓存测试

        [Fact]
        public void TryGetFrame_空缓存_返回false()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            byte[] data;
            Assert.False(service.TryGetFrame(0, out data));
            Assert.Null(data);
        }

        [Fact]
        public void IsFrameCached_空缓存_返回false()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            Assert.False(service.IsFrameCached(0));
        }

        [Fact]
        public void ClearCache_清空所有缓存()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            // 缓存本来为空，清空后仍为零
            service.ClearCache();
            Assert.Equal(0, service.CachedFrameCount);
        }

        [Fact]
        public void RequestFrame_未加载视频_返回null()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            Assert.Null(service.RequestFrame(0));
        }

        [Fact]
        public void ExtractFrameSync_未加载视频_返回null()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            Assert.Null(service.ExtractFrameSync(0));
        }

        #endregion

        #region LRU 淘汰策略测试（使用内部机制验证）

        [Fact]
        public void LRU缓存_超出限制时淘汰最久未使用帧()
        {
            // 创建一个很小的缓存（最大3帧）
            var service = new TestableVideoFrameCacheService("ffmpeg.exe", 3);
            service.LoadVideo("test.mp4", 30.0, 100.0, 1920, 1080);

            // 手动添加3帧（通过可测试接口）
            service.TestAddToCache(0, new byte[] { 0x01 });
            service.TestAddToCache(1, new byte[] { 0x02 });
            service.TestAddToCache(2, new byte[] { 0x03 });
            Assert.Equal(3, service.CachedFrameCount);

            // 添加第4帧，应淘汰帧0（最久未使用）
            service.TestAddToCache(3, new byte[] { 0x04 });
            Assert.Equal(3, service.CachedFrameCount);
            Assert.False(service.IsFrameCached(0)); // 帧0被淘汰
            Assert.True(service.IsFrameCached(1));
            Assert.True(service.IsFrameCached(2));
            Assert.True(service.IsFrameCached(3));
        }

        [Fact]
        public void LRU缓存_访问后更新顺序()
        {
            var service = new TestableVideoFrameCacheService("ffmpeg.exe", 3);
            service.LoadVideo("test.mp4", 30.0, 100.0, 1920, 1080);

            service.TestAddToCache(0, new byte[] { 0x01 });
            service.TestAddToCache(1, new byte[] { 0x02 });
            service.TestAddToCache(2, new byte[] { 0x03 });

            // 访问帧0，使其变为最近使用
            byte[] data;
            service.TryGetFrame(0, out data);
            Assert.NotNull(data);

            // 添加第4帧，应淘汰帧1（现在最久未使用）
            service.TestAddToCache(3, new byte[] { 0x04 });
            Assert.Equal(3, service.CachedFrameCount);
            Assert.True(service.IsFrameCached(0));  // 帧0仍在（被访问过）
            Assert.False(service.IsFrameCached(1)); // 帧1被淘汰
            Assert.True(service.IsFrameCached(2));
            Assert.True(service.IsFrameCached(3));
        }

        [Fact]
        public void LRU缓存_重复添加同一帧不增加计数()
        {
            var service = new TestableVideoFrameCacheService("ffmpeg.exe", 3);
            service.LoadVideo("test.mp4", 30.0, 100.0, 1920, 1080);

            service.TestAddToCache(0, new byte[] { 0x01 });
            service.TestAddToCache(0, new byte[] { 0x02 }); // 更新
            Assert.Equal(1, service.CachedFrameCount);

            byte[] data;
            Assert.True(service.TryGetFrame(0, out data));
            Assert.Equal(new byte[] { 0x02 }, data); // 数据已更新
        }

        [Fact]
        public void LRU缓存_连续淘汰多帧()
        {
            var service = new TestableVideoFrameCacheService("ffmpeg.exe", 2);
            service.LoadVideo("test.mp4", 30.0, 100.0, 1920, 1080);

            service.TestAddToCache(0, new byte[] { 0x01 });
            service.TestAddToCache(1, new byte[] { 0x02 });

            // 添加帧2，淘汰帧0
            service.TestAddToCache(2, new byte[] { 0x03 });
            Assert.False(service.IsFrameCached(0));
            Assert.True(service.IsFrameCached(1));
            Assert.True(service.IsFrameCached(2));

            // 添加帧3，淘汰帧1
            service.TestAddToCache(3, new byte[] { 0x04 });
            Assert.False(service.IsFrameCached(1));
            Assert.True(service.IsFrameCached(2));
            Assert.True(service.IsFrameCached(3));
        }

        #endregion

        #region Dispose 测试

        [Fact]
        public void Dispose_清空缓存和预取()
        {
            var service = new TestableVideoFrameCacheService("ffmpeg.exe", 10);
            service.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            service.TestAddToCache(0, new byte[] { 0x01 });

            service.Dispose();
            Assert.Equal(0, service.CachedFrameCount);
        }

        #endregion

        #region TotalFrames 计算测试

        [Fact]
        public void TotalFrames_30fps_10秒_等于300()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            Assert.Equal(300, service.TotalFrames);
        }

        [Fact]
        public void TotalFrames_24fps_非整数时长_向上取整()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 24.0, 10.5, 1920, 1080);
            // 10.5 * 24 = 252.0，Math.Ceiling(252.0) = 252
            Assert.Equal(252, service.TotalFrames);
        }

        [Fact]
        public void TotalFrames_25fps_小数时长_正确计算()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 25.0, 10.04, 1920, 1080);
            // 10.04 * 25 = 251.0，Math.Ceiling(251.0) = 251
            Assert.Equal(251, service.TotalFrames);
        }

        #endregion

        #region StartPrefetch 测试

        [Fact]
        public void StartPrefetch_未加载视频_不抛异常()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            // 不应抛异常
            service.StartPrefetch(0, 5);
        }

        [Fact]
        public void StartPrefetch_帧数为零_不抛异常()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            service.StartPrefetch(0, 0);
        }

        [Fact]
        public void CancelPrefetch_未启动_不抛异常()
        {
            var service = new VideoFrameCacheService("ffmpeg.exe", 100);
            service.CancelPrefetch();
        }

        #endregion
    }

    /// <summary>
    /// 视频帧预览 ViewModel 测试
    /// </summary>
    public class VideoPreviewViewModelTests
    {
        #region 构造函数测试

        [Fact]
        public void 构造函数_有效参数_创建成功()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            Assert.False(vm.HasVideo);
            Assert.Null(vm.CurrentFrameData);
            Assert.Equal(0, vm.CurrentFrameIndex);
            Assert.Equal("", vm.FrameInfoText);
            Assert.False(vm.IsLoading);
        }

        [Fact]
        public void 构造函数_帧缓存服务为null_应抛异常()
        {
            var wvm = CreateWaveformViewModel();
            Assert.Throws<ArgumentNullException>(() => new VideoPreviewViewModel(null, wvm));
        }

        #endregion

        #region LoadVideo 测试

        [Fact]
        public void LoadVideo_状态正确()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 120.0, 1920, 1080);

            Assert.True(vm.HasVideo);
            Assert.Equal(0, vm.CurrentFrameIndex);
            Assert.True(vm.FrameInfoText.Contains("帧 0"));
        }

        [Fact]
        public void LoadVideo_帧信息文本包含总帧数()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            Assert.True(vm.FrameInfoText.Contains("300")); // 10 * 30 = 300
        }

        #endregion

        #region UnloadVideo 测试

        [Fact]
        public void UnloadVideo_清除状态()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 120.0, 1920, 1080);
            vm.UnloadVideo();

            Assert.False(vm.HasVideo);
            Assert.Equal(0, vm.CurrentFrameIndex);
            Assert.Null(vm.CurrentFrameData);
            Assert.Equal("", vm.FrameInfoText);
        }

        #endregion

        #region SeekToFrame 测试

        [Fact]
        public void SeekToFrame_未加载视频_不抛异常()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.SeekToFrame(10);
            // 不应更新
            Assert.Equal(0, vm.CurrentFrameIndex);
        }

        [Fact]
        public void SeekToFrame_更新帧索引()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            vm.SeekToFrame(50);

            Assert.Equal(50, vm.CurrentFrameIndex);
            Assert.True(vm.FrameInfoText.Contains("帧 50"));
        }

        [Fact]
        public void SeekToFrame_负帧号_钳位到零()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            vm.SeekToFrame(-5);

            Assert.Equal(0, vm.CurrentFrameIndex);
        }

        [Fact]
        public void SeekToFrame_超出范围_钳位到最后帧()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            vm.SeekToFrame(999);

            Assert.Equal(299, vm.CurrentFrameIndex); // 300 - 1
        }

        [Fact]
        public void SeekToFrame_缓存未命中_标记加载中()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            vm.SeekToFrame(10);

            // ffmpeg.exe 不存在，提取会失败，但 IsLoading 应先为 true
            Assert.True(vm.IsLoading);
        }

        [Fact]
        public void SeekToFrame_缓存命中_立即显示()
        {
            var cacheService = new TestableVideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            // 预填充缓存
            byte[] fakeFrame = new byte[] { 0x42, 0x4D, 0x01, 0x02 };
            cacheService.TestAddToCache(10, fakeFrame);

            vm.SeekToFrame(10);

            Assert.Equal(10, vm.CurrentFrameIndex);
            Assert.False(vm.IsLoading);
            Assert.NotNull(vm.CurrentFrameData);
            Assert.Equal(fakeFrame, vm.CurrentFrameData);
        }

        #endregion

        #region SeekToTimestamp 测试

        [Fact]
        public void SeekToTimestamp_转换正确()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            vm.SeekToTimestamp(1.0); // 1秒 = 帧30

            Assert.Equal(30, vm.CurrentFrameIndex);
        }

        [Fact]
        public void SeekToTimestamp_未加载视频_不抛异常()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.SeekToTimestamp(5.0);
            Assert.Equal(0, vm.CurrentFrameIndex);
        }

        #endregion

        #region SeekToSamplePosition 测试

        [Fact]
        public void SeekToSamplePosition_正确转换()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            // 44100 采样率，44100 个采样 = 1 秒 = 帧 30
            vm.SeekToSamplePosition(44100, 44100);

            Assert.Equal(30, vm.CurrentFrameIndex);
        }

        [Fact]
        public void SeekToSamplePosition_采样率为零_不抛异常()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);
            vm.SeekToSamplePosition(44100, 0);
            // 不应更新
            Assert.Equal(0, vm.CurrentFrameIndex);
        }

        [Fact]
        public void SeekToSamplePosition_未加载视频_不抛异常()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.SeekToSamplePosition(44100, 44100);
            Assert.Equal(0, vm.CurrentFrameIndex);
        }

        #endregion

        #region 属性变更通知测试

        [Fact]
        public void 属性通知_HasVideo()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);
            var changedProps = new List<string>();
            vm.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            Assert.Contains("HasVideo", changedProps);
        }

        [Fact]
        public void 属性通知_CurrentFrameIndex()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);
            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            var changedProps = new List<string>();
            vm.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName);

            vm.SeekToFrame(50);

            Assert.Contains("CurrentFrameIndex", changedProps);
        }

        [Fact]
        public void 属性通知_FrameInfoText()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);
            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            var changedProps = new List<string>();
            vm.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName);

            vm.SeekToFrame(60);

            Assert.Contains("FrameInfoText", changedProps);
        }

        [Fact]
        public void 属性通知_IsLoading()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);
            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            var changedProps = new List<string>();
            vm.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName);

            vm.SeekToFrame(10);

            Assert.Contains("IsLoading", changedProps);
        }

        #endregion

        #region FrameInfoText 格式测试

        [Fact]
        public void FrameInfoText_格式正确()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 25.0, 10.0, 1920, 1080);
            vm.SeekToFrame(75); // 75/25 = 3秒

            Assert.Equal("帧 75 / 250 | 00:00:03.000", vm.FrameInfoText);
        }

        [Fact]
        public void FrameInfoText_首帧()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            Assert.Equal("帧 0 / 300 | 00:00:00.000", vm.FrameInfoText);
        }

        [Fact]
        public void FrameInfoText_超过1小时()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);

            vm.LoadVideo("test.mp4", 25.0, 7200.0, 1920, 1080);
            // 帧 90000 = 3600 秒 = 1小时
            vm.SeekToFrame(90000);

            Assert.Equal("帧 90000 / 180000 | 01:00:00.000", vm.FrameInfoText);
        }

        #endregion

        #region Dispose 测试

        [Fact]
        public void Dispose_取消事件订阅_不抛异常()
        {
            var cacheService = new VideoFrameCacheService("ffmpeg.exe", 100);
            var wvm = CreateWaveformViewModel();
            var vm = new VideoPreviewViewModel(cacheService, wvm);
            vm.LoadVideo("test.mp4", 30.0, 10.0, 1920, 1080);

            vm.Dispose();
            // dispose 后不应抛异常
        }

        #endregion

        #region 帮助方法

        /// <summary>
        /// 创建 WaveformViewModel 实例用于测试
        /// </summary>
        private WaveformViewModel CreateWaveformViewModel()
        {
            return new WaveformViewModel();
        }

        #endregion
    }

    /// <summary>
    /// 可测试的帧缓存服务子类，暴露内部 AddToCache 方法
    /// </summary>
    public class TestableVideoFrameCacheService : VideoFrameCacheService
    {
        public TestableVideoFrameCacheService(string ffmpegPath, int maxCachedFrames)
            : base(ffmpegPath, maxCachedFrames)
        {
        }

        /// <summary>
        /// 测试用：直接向缓存添加帧数据
        /// </summary>
        public void TestAddToCache(long frameIndex, byte[] frameData)
        {
            // 使用反射调用私有方法 AddToCache
            var method = typeof(VideoFrameCacheService).GetMethod("AddToCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(this, new object[] { frameIndex, frameData });
        }
    }
}
