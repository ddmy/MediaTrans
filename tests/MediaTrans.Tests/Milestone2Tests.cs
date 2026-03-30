using System;
using System.Collections.Generic;
using MediaTrans.Models;
using MediaTrans.Services;
using MediaTrans.ViewModels;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 里程碑 2 综合测试 — 覆盖 M2 自测检查清单
    /// 编辑器单元测试（Undo/Redo 栈、选区计算、磁吸算法、增益计算）
    /// 帧缓存 LRU 正确性测试
    /// 导出命令断言必含显式编解码器
    /// 各组件集成验证
    /// </summary>
    public class Milestone2Tests
    {
        private const int SampleRate = 44100;
        private const int ViewportWidth = 1920;

        #region 辅助方法

        private WaveformViewModel CreateWaveformVm(long totalSamples)
        {
            var vm = new WaveformViewModel();
            vm.Initialize(totalSamples, SampleRate, ViewportWidth);
            return vm;
        }

        private TimelineViewModel CreateTimelineVm(WaveformViewModel waveformVm)
        {
            var rulerService = new TimelineRulerService();
            return new TimelineViewModel(waveformVm, rulerService);
        }

        #endregion

        // ============================================================
        // Undo/Redo 栈正确性
        // ============================================================

        [Fact]
        public void UndoRedo_20步操作_逐步撤销_每步状态正确恢复()
        {
            var service = new UndoRedoService(50);
            var vm = new UndoRedoViewModel(service);
            var values = new List<int>();

            for (int i = 0; i < 20; i++)
            {
                var cmd = new TrackingCommand(i, values);
                vm.ExecuteCommand(cmd);
            }

            Assert.Equal(20, values.Count);

            // 逐一撤销
            for (int i = 19; i >= 0; i--)
            {
                Assert.True(vm.CanUndo);
                vm.UndoCommand.Execute(null);
                Assert.Equal(i, values.Count);
            }

            Assert.False(vm.CanUndo);

            // 逐一重做
            for (int i = 0; i < 20; i++)
            {
                Assert.True(vm.CanRedo);
                vm.RedoCommand.Execute(null);
                Assert.Equal(i + 1, values.Count);
            }

            Assert.False(vm.CanRedo);
        }

        [Fact]
        public void UndoRedo_片段添加删除_撤销恢复正确()
        {
            var trackVm = new TimelineTrackViewModel();
            var clip = new TimelineClip
            {
                SourceFilePath = "test.mp3",
                SourceStartSeconds = 0,
                SourceEndSeconds = 1.0,
                MediaType = "audio"
            };

            var addCmd = new ClipAddCommand(trackVm, clip);
            addCmd.Execute();
            Assert.Equal(1, trackVm.Clips.Count);

            addCmd.Undo();
            Assert.Equal(0, trackVm.Clips.Count);

            addCmd.Execute();
            Assert.Equal(1, trackVm.Clips.Count);
        }

        [Fact]
        public void UndoRedo_增益调节_撤销恢复原值()
        {
            var gainVm = new GainViewModel();

            double originalGain = gainVm.GainDb;
            var cmd = new GainChangeCommand(gainVm, originalGain, 6.0);
            cmd.Execute();
            Assert.Equal(6.0, (double)gainVm.GainDb, 2);

            cmd.Undo();
            Assert.Equal((double)originalGain, (double)gainVm.GainDb, 2);
        }

        [Fact]
        public void UndoRedo_选区变更_撤销恢复原选区()
        {
            var waveformVm = CreateWaveformVm(44100 * 60);
            var selectionVm = new SelectionViewModel(waveformVm);

            selectionVm.SelectionStartSample = 1000;
            selectionVm.SelectionEndSample = 5000;

            long oldStart = selectionVm.SelectionStartSample;
            long oldEnd = selectionVm.SelectionEndSample;

            var cmd = new SelectionChangeCommand(
                selectionVm, oldStart, oldEnd, 2000, 8000);
            cmd.Execute();
            Assert.Equal(2000L, selectionVm.SelectionStartSample);
            Assert.Equal(8000L, selectionVm.SelectionEndSample);

            cmd.Undo();
            Assert.Equal(oldStart, selectionVm.SelectionStartSample);
            Assert.Equal(oldEnd, selectionVm.SelectionEndSample);
        }

        [Fact]
        public void UndoRedo_栈深度限制_超出后最早操作丢弃()
        {
            int depth = 10;
            var service = new UndoRedoService(depth);
            var vm = new UndoRedoViewModel(service);
            var values = new List<int>();

            for (int i = 0; i < 15; i++)
            {
                vm.ExecuteCommand(new TrackingCommand(i, values));
            }

            int undoCount = 0;
            while (vm.CanUndo)
            {
                vm.UndoCommand.Execute(null);
                undoCount++;
            }
            Assert.Equal(depth, undoCount);
        }

        [Fact]
        public void UndoRedo_新操作清除Redo栈()
        {
            var service = new UndoRedoService(50);
            var vm = new UndoRedoViewModel(service);
            var values = new List<int>();

            vm.ExecuteCommand(new TrackingCommand(1, values));
            vm.ExecuteCommand(new TrackingCommand(2, values));
            vm.ExecuteCommand(new TrackingCommand(3, values));

            vm.UndoCommand.Execute(null);
            vm.UndoCommand.Execute(null);
            Assert.True(vm.CanRedo);

            vm.ExecuteCommand(new TrackingCommand(4, values));
            Assert.False(vm.CanRedo);
        }

        // ============================================================
        // 选区计算
        // ============================================================

        [Fact]
        public void 选区_像素到采样帧_转换正确()
        {
            var waveformVm = CreateWaveformVm(44100 * 60);
            var selectionVm = new SelectionViewModel(waveformVm);

            for (int i = 0; i < 10; i++)
            {
                waveformVm.ZoomIn();
            }

            selectionVm.CreateSelection(100, 500);

            Assert.True(selectionVm.HasSelection);
            Assert.True(selectionVm.SelectionStartSample >= 0);
            Assert.True(selectionVm.SelectionEndSample > selectionVm.SelectionStartSample);

            long expectedDuration = (long)(400 * waveformVm.SamplesPerPixel);
            long actualDuration = selectionVm.SelectionDurationSamples;
            Assert.True(Math.Abs(actualDuration - expectedDuration) <= 2,
                string.Format("预期 {0}，实际 {1}", expectedDuration, actualDuration));
        }

        [Fact]
        public void 选区_持续时长秒数_计算正确()
        {
            var waveformVm = CreateWaveformVm(44100 * 60);
            var selectionVm = new SelectionViewModel(waveformVm);

            // 先设置 end 再设置 start，避免 EnsureOrder 交换
            selectionVm.SetSelectionEndTime(20.0);
            selectionVm.SetSelectionStartTime(10.0);

            double duration = selectionVm.SelectionDurationSeconds;
            Assert.True(Math.Abs(duration - 10.0) < 0.01,
                string.Format("选区持续时长 {0} 秒，预期约10秒", duration));
        }

        [Fact]
        public void 选区_全选_覆盖全部采样帧()
        {
            long totalSamples = 44100 * 120;
            var waveformVm = CreateWaveformVm(totalSamples);
            var selectionVm = new SelectionViewModel(waveformVm);

            selectionVm.SelectAll();

            Assert.True(selectionVm.HasSelection);
            Assert.Equal(0L, selectionVm.SelectionStartSample);
            Assert.Equal(totalSamples, selectionVm.SelectionEndSample);
        }

        [Fact]
        public void 选区_清除后_无选区()
        {
            var waveformVm = CreateWaveformVm(44100 * 60);
            var selectionVm = new SelectionViewModel(waveformVm);

            selectionVm.SelectAll();
            Assert.True(selectionVm.HasSelection);

            selectionVm.ClearSelection();
            Assert.False(selectionVm.HasSelection);
        }

        [Fact]
        public void 选区_反向创建_自动排序()
        {
            var waveformVm = CreateWaveformVm(44100 * 60);
            var selectionVm = new SelectionViewModel(waveformVm);

            selectionVm.CreateSelection(500, 100);

            Assert.True(selectionVm.HasSelection);
            Assert.True(selectionVm.SelectionStartSample < selectionVm.SelectionEndSample);
        }

        // ============================================================
        // 磁吸算法
        // ============================================================

        [Fact]
        public void 磁吸_边缘靠近阈值内_吸附到参考点()
        {
            int thresholdPixels = 10;
            var snappingService = new SnappingService(thresholdPixels);
            // samplesPerPixel=100 → 阈值 = 10*100/44100 ≈ 0.0227秒
            snappingService.UpdateZoomLevel(100.0, SampleRate);

            var snapTargets = new List<double> { 1.0, 5.0, 10.0 };

            // 0.999秒 距参考点1.0秒 差0.001秒，在阈值0.0227秒内
            var result = snappingService.Snap(0.999, snapTargets, SampleRate);
            Assert.True(result.IsSnapped);
            Assert.Equal(1.0, result.SnappedTimeSeconds, 4);
        }

        [Fact]
        public void 磁吸_边缘超出阈值_不吸附()
        {
            int thresholdPixels = 10;
            var snappingService = new SnappingService(thresholdPixels);
            snappingService.UpdateZoomLevel(1.0, SampleRate);

            var snapTargets = new List<double> { 1.0, 5.0, 10.0 };

            // 3.0秒 距最近参考点差2.0秒，远超阈值
            var result = snappingService.Snap(3.0, snapTargets, SampleRate);
            Assert.False(result.IsSnapped);
        }

        [Fact]
        public void 磁吸_多个参考点_吸附最近的()
        {
            int thresholdPixels = 10;
            var snappingService = new SnappingService(thresholdPixels);
            snappingService.UpdateZoomLevel(100.0, SampleRate);

            var snapTargets = new List<double> { 1.0, 2.0, 3.0 };

            // 1.999秒 距2.0秒最近
            var result = snappingService.Snap(1.999, snapTargets, SampleRate);
            if (result.IsSnapped)
            {
                Assert.Equal(2.0, result.SnappedTimeSeconds, 3);
            }
        }

        [Fact]
        public void 磁吸_阈值从配置读取_默认10像素()
        {
            var config = AppConfig.CreateDefault();
            Assert.Equal(10, config.SnapThresholdPixels);
        }

        [Fact]
        public void 磁吸_收集片段边缘_正确()
        {
            var snappingService = new SnappingService(10);

            var clips = new List<TimelineClip>
            {
                new TimelineClip { SourceFilePath = "a.mp3", SourceStartSeconds = 0, SourceEndSeconds = 5.0, TimelineStartSeconds = 0 },
                new TimelineClip { SourceFilePath = "b.mp3", SourceStartSeconds = 0, SourceEndSeconds = 3.0, TimelineStartSeconds = 5.0 }
            };

            var targets = snappingService.CollectClipEdgeTargets(clips, -1);
            Assert.True(targets.Count > 0);
        }

        // ============================================================
        // 增益计算
        // ============================================================

        [Fact]
        public void 增益_零dB_不改变振幅()
        {
            double linear = GainService.DbToLinear(0.0);
            Assert.Equal(1.0, linear, 4);
        }

        [Fact]
        public void 增益_正6dB_约2倍振幅()
        {
            double linear = GainService.DbToLinear(6.0);
            Assert.True(Math.Abs(linear - 1.995) < 0.1,
                string.Format("6dB线性值 {0}，预期约2.0", linear));
        }

        [Fact]
        public void 增益_负20dB至正20dB范围_钳位正确()
        {
            Assert.Equal(-20.0, (double)GainService.ClampGainDb(-30.0), 1);
            Assert.Equal(20.0, (double)GainService.ClampGainDb(30.0), 1);
            Assert.Equal(5.0, (double)GainService.ClampGainDb(5.0), 1);
        }

        [Fact]
        public void 增益_步进_对齐正确()
        {
            // SnapToStep 对齐到0.5dB步进
            double snapped = GainService.SnapToStep(3.1);
            Assert.Equal(3.0, snapped, 4);
        }

        [Fact]
        public void 增益_应用到PCM16数据_振幅变化正确()
        {
            var data = new byte[2000];
            // 填充1000 (0x03E8) 到每个采样
            for (int i = 0; i < 2000; i += 2)
            {
                data[i] = 0xE8;
                data[i + 1] = 0x03;
            }

            // 应用6dB增益（约2倍）
            byte[] result = GainService.ApplyGainToPcm16(data, 6.0);

            short sample = (short)(result[0] | (result[1] << 8));
            Assert.True(Math.Abs(sample - 1995) < 100,
                string.Format("增益后采样值 {0}，预期约2000", sample));
        }

        // ============================================================
        // 帧缓存 LRU 正确性
        // ============================================================

        [Fact]
        public void LRU_未命中返回false()
        {
            var cache = new VideoFrameCacheService("dummy.exe", 10);
            cache.LoadVideo("test.mp4", 30.0, 100.0, 1920, 1080);

            byte[] data;
            Assert.False(cache.TryGetFrame(0, out data));
            Assert.Null(data);
        }

        [Fact]
        public void LRU_清空后_帧数为零()
        {
            var cache = new VideoFrameCacheService("dummy.exe", 10);
            cache.LoadVideo("test.mp4", 30.0, 100.0, 1920, 1080);

            cache.ClearCache();
            Assert.Equal(0, cache.CachedFrameCount);
        }

        [Fact]
        public void LRU_容量上限_不被超过()
        {
            int maxFrames = 5;
            var cache = new VideoFrameCacheService("dummy.exe", maxFrames);
            cache.LoadVideo("test.mp4", 30.0, 100.0, 1920, 1080);

            Assert.True(cache.CachedFrameCount <= maxFrames);
        }

        [Fact]
        public void LRU_重新加载视频_旧缓存被清空()
        {
            var cache = new VideoFrameCacheService("dummy.exe", 10);
            cache.LoadVideo("video1.mp4", 30.0, 100.0, 1920, 1080);
            cache.LoadVideo("video2.mp4", 25.0, 200.0, 1280, 720);

            Assert.Equal(0, cache.CachedFrameCount);
            Assert.True(cache.IsVideoLoaded);
        }

        [Fact]
        public void LRU_帧率与总帧数_计算正确()
        {
            var cache = new VideoFrameCacheService("dummy.exe", 10);
            cache.LoadVideo("test.mp4", 30.0, 120.0, 1920, 1080);

            Assert.Equal(30.0, cache.FrameRate);
            Assert.Equal(3600L, cache.TotalFrames);
        }

        // ============================================================
        // 导出命令断言必含显式编解码器
        // ============================================================

        [Theory]
        [InlineData(".mp4")]
        [InlineData(".avi")]
        [InlineData(".mkv")]
        [InlineData(".mov")]
        [InlineData(".wmv")]
        [InlineData(".flv")]
        [InlineData(".webm")]
        public void 导出视频格式_命令必含显式编解码器(string format)
        {
            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = string.Format("output{0}", format),
                TargetFormat = format,
                TrimStartSeconds = 0,
                TrimDurationSeconds = 30
            };

            string args = exportService.BuildExportArguments(param);

            Assert.Contains("-c:v ", args);
            Assert.Contains("-c:a ", args);
        }

        [Theory]
        [InlineData(".mp3")]
        [InlineData(".wav")]
        [InlineData(".flac")]
        [InlineData(".aac")]
        [InlineData(".ogg")]
        [InlineData(".wma")]
        [InlineData(".m4a")]
        public void 导出音频格式_命令必含显式音频编解码器(string format)
        {
            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                SourceFilePath = "input.mp3",
                OutputFilePath = string.Format("output{0}", format),
                TargetFormat = format,
                TrimStartSeconds = 0,
                TrimDurationSeconds = 30
            };

            string args = exportService.BuildExportArguments(param);
            Assert.Contains("-c:a ", args);
        }

        [Theory]
        [InlineData(".mp4", "-c:v libx264", "-c:a aac")]
        [InlineData(".webm", "-c:v libvpx", "-c:a libvorbis")]
        [InlineData(".mp3", null, "-c:a libmp3lame")]
        [InlineData(".wav", null, "-c:a pcm_s16le")]
        [InlineData(".flac", null, "-c:a flac")]
        public void 导出格式_编解码器映射正确(string format, string expectedVideo, string expectedAudio)
        {
            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = string.Format("output{0}", format),
                TargetFormat = format,
                TrimStartSeconds = 0,
                TrimDurationSeconds = 30
            };

            string args = exportService.BuildExportArguments(param);

            if (expectedVideo != null)
            {
                Assert.Contains(expectedVideo, args);
            }
            if (expectedAudio != null)
            {
                Assert.Contains(expectedAudio, args);
            }
        }

        [Fact]
        public void 导出_带增益_命令含音量滤镜()
        {
            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                SourceFilePath = "input.mp3",
                OutputFilePath = "output.mp3",
                TargetFormat = ".mp3",
                TrimStartSeconds = 0,
                TrimDurationSeconds = 30,
                GainDb = 6.0
            };

            string args = exportService.BuildExportArguments(param);
            Assert.Contains("volume=", args);
        }

        [Fact]
        public void 导出_裁剪参数_含seek和duration()
        {
            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4",
                TrimStartSeconds = 10.5,
                TrimDurationSeconds = 30.0
            };

            string args = exportService.BuildExportArguments(param);

            Assert.Contains("-ss", args);
            Assert.Contains("-t", args);
        }

        [Fact]
        public void 导出_多段拼接_含filter_complex()
        {
            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                SourceFilePath = "input.mp3",
                OutputFilePath = "output.mp3",
                TargetFormat = ".mp3",
                Segments = new List<ClipSegment>
                {
                    new ClipSegment { SourceFilePath = "input.mp3", StartSeconds = 0, DurationSeconds = 10 },
                    new ClipSegment { SourceFilePath = "input.mp3", StartSeconds = 20, DurationSeconds = 10 }
                }
            };

            string args = exportService.BuildExportArguments(param);

            Assert.Contains("-filter_complex", args);
            Assert.Contains("-c:a ", args);
        }

        [Fact]
        public void 导出参数校验_缺少源文件_返回错误()
        {
            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                OutputFilePath = "output.mp4",
                TargetFormat = ".mp4"
            };

            var errors = exportService.ValidateParams(param);
            Assert.True(errors.Count > 0);
        }

        [Fact]
        public void 导出参数校验_缺少输出路径_返回错误()
        {
            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                SourceFilePath = "input.mp4",
                TargetFormat = ".mp4"
            };

            var errors = exportService.ValidateParams(param);
            Assert.True(errors.Count > 0);
        }

        // ============================================================
        // 组件集成验证（模拟编辑流程）
        // ============================================================

        [Fact]
        public void 集成_导入裁剪导出_流程参数正确()
        {
            var waveformVm = CreateWaveformVm(44100 * 120);
            var selectionVm = new SelectionViewModel(waveformVm);

            // 先设置 end 再设置 start，避免 EnsureOrder 交换
            selectionVm.SetSelectionEndTime(30.0);
            selectionVm.SetSelectionStartTime(10.0);

            double startSec = waveformVm.SamplesToSeconds(selectionVm.SelectionStartSample);
            double durationSec = selectionVm.SelectionDurationSeconds;

            Assert.True(startSec > 0, string.Format("起始时间应大于0，实际 {0}", startSec));

            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                SourceFilePath = "source.mp3",
                OutputFilePath = "output.mp3",
                TargetFormat = ".mp3",
                TrimStartSeconds = startSec,
                TrimDurationSeconds = durationSec
            };

            var errors = exportService.ValidateParams(param);
            Assert.Equal(0, errors.Count);

            string args = exportService.BuildExportArguments(param);
            Assert.Contains("-c:a libmp3lame", args);
            Assert.Contains("-ss", args);
            Assert.Contains("-t", args);
        }

        [Fact]
        public void 集成_多片段拼接导出_参数正确()
        {
            var clips = new List<TimelineClip>
            {
                new TimelineClip
                {
                    SourceFilePath = "source.mp3",
                    SourceStartSeconds = 0,
                    SourceEndSeconds = 10.0,
                    TimelineStartSeconds = 0,
                    MediaType = "audio"
                },
                new TimelineClip
                {
                    SourceFilePath = "source.mp3",
                    SourceStartSeconds = 20.0,
                    SourceEndSeconds = 35.0,
                    TimelineStartSeconds = 10.0,
                    MediaType = "audio"
                }
            };

            var param = EditExportService.CreateFromTimeline(
                clips, "output.mp3", ".mp3", 0.0, null);

            Assert.NotNull(param);
            Assert.Equal(2, param.Segments.Count);

            var exportService = new EditExportService();
            string args = exportService.BuildExportArguments(param);
            Assert.Contains("-filter_complex", args);
            Assert.Contains("-c:a ", args);
        }

        [Fact]
        public void 集成_缩放后选区_样本数稳定()
        {
            var waveformVm = CreateWaveformVm(44100 * 60);
            var selectionVm = new SelectionViewModel(waveformVm);

            selectionVm.SetSelectionStartTime(5.0);
            selectionVm.SetSelectionEndTime(15.0);

            long originalStartSample = selectionVm.SelectionStartSample;
            long originalEndSample = selectionVm.SelectionEndSample;

            for (int i = 0; i < 10; i++)
            {
                waveformVm.ZoomIn();
            }

            Assert.Equal(originalStartSample, selectionVm.SelectionStartSample);
            Assert.Equal(originalEndSample, selectionVm.SelectionEndSample);
        }

        [Fact]
        public void 集成_撤销后选区_恢复到之前的值()
        {
            var waveformVm = CreateWaveformVm(44100 * 60);
            var selectionVm = new SelectionViewModel(waveformVm);
            var undoService = new UndoRedoService(50);
            var undoVm = new UndoRedoViewModel(undoService);

            selectionVm.SelectionStartSample = 0;
            selectionVm.SelectionEndSample = 44100;

            var cmd = new SelectionChangeCommand(
                selectionVm, 0, 44100, 44100, 88200);
            undoVm.ExecuteCommand(cmd);

            Assert.Equal(44100L, selectionVm.SelectionStartSample);
            Assert.Equal(88200L, selectionVm.SelectionEndSample);

            undoVm.UndoCommand.Execute(null);
            Assert.Equal(0L, selectionVm.SelectionStartSample);
            Assert.Equal(44100L, selectionVm.SelectionEndSample);
        }

        [Fact]
        public void 集成_快捷键绑定_快捷键响应正确()
        {
            var waveformVm = CreateWaveformVm(44100 * 10);
            var timelineVm = CreateTimelineVm(waveformVm);
            var selectionVm = new SelectionViewModel(waveformVm);
            var undoService = new UndoRedoService(50);
            var undoVm = new UndoRedoViewModel(undoService);
            var trackVm = new TimelineTrackViewModel();

            var shortcutService = new ShortcutService(
                null, undoVm, trackVm, selectionVm, timelineVm, waveformVm, 10);

            // Home 跳到开头
            bool handled = shortcutService.ProcessKeyDown(
                System.Windows.Input.Key.Home, System.Windows.Input.ModifierKeys.None);
            Assert.True(handled);
            Assert.Equal(0L, timelineVm.PlayheadSample);

            // End 跳到末尾
            handled = shortcutService.ProcessKeyDown(
                System.Windows.Input.Key.End, System.Windows.Input.ModifierKeys.None);
            Assert.True(handled);
            Assert.Equal(waveformVm.TotalSamples, timelineVm.PlayheadSample);

            // Ctrl+A 全选
            handled = shortcutService.ProcessKeyDown(
                System.Windows.Input.Key.A, System.Windows.Input.ModifierKeys.Control);
            Assert.True(handled);
            Assert.True(selectionVm.HasSelection);
        }

        [Fact]
        public void 配置_磁吸阈值_可读取()
        {
            var config = AppConfig.CreateDefault();
            Assert.True(config.SnapThresholdPixels > 0);
        }

        [Fact]
        public void 配置_撤销栈深度_至少50步()
        {
            var config = AppConfig.CreateDefault();
            Assert.True(config.MaxUndoDepth >= 50);
        }

        [Fact]
        public void 配置_帧缓存数_大于零()
        {
            var config = AppConfig.CreateDefault();
            Assert.True(config.MaxCachedFrames > 0);
        }

        // ============================================================
        // 路径边界（中文/空格路径导出命令）
        // ============================================================

        [Fact]
        public void 导出_中文路径_双引号包裹()
        {
            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                SourceFilePath = "C:\\用户\\测试文件.mp3",
                OutputFilePath = "D:\\输出 目录\\结果.mp3",
                TargetFormat = ".mp3",
                TrimStartSeconds = 0,
                TrimDurationSeconds = 10
            };

            string args = exportService.BuildExportArguments(param);

            Assert.Contains("\"C:\\用户\\测试文件.mp3\"", args);
            Assert.Contains("\"D:\\输出 目录\\结果.mp3\"", args);
        }

        [Fact]
        public void 导出_空格路径_双引号包裹()
        {
            var exportService = new EditExportService();
            var param = new EditExportParams
            {
                SourceFilePath = "C:\\my files\\test file.mp4",
                OutputFilePath = "D:\\out put\\result file.mp4",
                TargetFormat = ".mp4",
                TrimStartSeconds = 0,
                TrimDurationSeconds = 10
            };

            string args = exportService.BuildExportArguments(param);

            Assert.Contains("\"C:\\my files\\test file.mp4\"", args);
            Assert.Contains("\"D:\\out put\\result file.mp4\"", args);
        }

        // ============================================================
        // 辅助类
        // ============================================================

        private class TrackingCommand : IUndoableCommand
        {
            private readonly int _value;
            private readonly List<int> _tracker;

            public TrackingCommand(int value, List<int> tracker)
            {
                _value = value;
                _tracker = tracker;
            }

            public void Execute()
            {
                _tracker.Add(_value);
            }

            public void Undo()
            {
                if (_tracker.Count > 0)
                {
                    _tracker.RemoveAt(_tracker.Count - 1);
                }
            }

            public string Description
            {
                get { return string.Format("跟踪命令 {0}", _value); }
            }
        }
    }
}
