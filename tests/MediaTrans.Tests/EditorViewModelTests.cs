using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MediaTrans.Models;
using MediaTrans.Services;
using MediaTrans.ViewModels;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// EditorViewModel 拼接相关测试
    /// </summary>
    public class EditorViewModelTests
    {
        private EditorViewModel CreateVm(out string configPath)
        {
            configPath = Path.Combine(Path.GetTempPath(), string.Format("mediatrans_test_{0}.json", Guid.NewGuid().ToString("N")));
            var configService = new ConfigService(configPath);
            configService.Load();

            var ffmpegService = new FFmpegService("ffmpeg.exe", "ffprobe.exe");
            return new EditorViewModel(ffmpegService, configService);
        }

        private static MediaFileInfo CreateRealMediaFile()
        {
            return new MediaFileInfo
            {
                FilePath = @"C:\\temp\\a.mp4",
                FileName = "a.mp4",
                DurationSeconds = 12,
                HasAudio = true,
                HasVideo = true,
                Width = 1280,
                Height = 720
            };
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(instance, value);
        }

        private static void PrepareWaveformSession(EditorViewModel vm)
        {
            var file = CreateRealMediaFile();
            SetPrivateField(vm, "_currentFile", file);
            SetPrivateField(vm, "_audioReady", true);
            SetPrivateField(vm, "_sourceDurationSeconds", file.DurationSeconds);
            SetPrivateField(vm, "_sessionDurationSeconds", file.DurationSeconds);
            SetPrivateField(vm, "_hasSessionEdits", false);
            SeedSessionSegments(vm, file.DurationSeconds);
            SeedPlaybackSampleRate(vm, 44100);
            vm.WaveformVm.Initialize(44100 * 12, 44100, 1000);
            vm.UpdateWaveformViewportWidth(1000);
            vm.SelectionVm.SelectAll();
            vm.TimelineVm.SetPlayheadTime(0);
        }

        private static void SeedSessionSegments(EditorViewModel vm, double durationSeconds)
        {
            var field = typeof(EditorViewModel).GetField("_sessionSegments", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);

            var list = field.GetValue(vm) as IList;
            Assert.NotNull(list);
            list.Clear();

            Type segmentType = typeof(EditorViewModel).GetNestedType("SessionSegment", BindingFlags.NonPublic);
            Assert.NotNull(segmentType);

            object segment = Activator.CreateInstance(segmentType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { 0d, durationSeconds },
                null);
            Assert.NotNull(segment);
            list.Add(segment);
        }

        private static void SeedPlaybackSampleRate(EditorViewModel vm, int sampleRate)
        {
            var playbackField = typeof(EditorViewModel).GetField("_playbackService", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(playbackField);

            var playbackService = playbackField.GetValue(vm);
            Assert.NotNull(playbackService);

            SetPrivateField(playbackService, "_sampleRate", sampleRate);
        }

        [Fact]
        public void TryAddVirtualGapSegment_合法时长_添加成功()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                string error;
                bool ok = vm.TryAddVirtualGapSegment(1500, out error);

                Assert.True(ok);
                Assert.True(string.IsNullOrEmpty(error));
                Assert.Single(vm.SpliceFiles);
                Assert.True(vm.SpliceFiles[0].IsVirtualGap);
                Assert.Equal(1500, vm.SpliceFiles[0].VirtualGapDurationMs);
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }

        [Fact]
        public void TryAddVirtualGapSegment_超范围_添加失败()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                string error;
                bool ok = vm.TryAddVirtualGapSegment(40000, out error);

                Assert.False(ok);
                Assert.False(string.IsNullOrEmpty(error));
                Assert.Empty(vm.SpliceFiles);
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }

        [Fact]
        public void SpliceExportCommand_仅虚拟片段_不可导出()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                string error;
                vm.TryAddVirtualGapSegment(1000, out error);
                vm.TryAddVirtualGapSegment(1000, out error);

                Assert.False(vm.SpliceExportCommand.CanExecute(null));
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }

        [Fact]
        public void SpliceExportCommand_一个真实文件加一个虚拟片段_可导出()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                vm.AddFileToSplice(CreateRealMediaFile());
                string error;
                vm.TryAddVirtualGapSegment(800, out error);

                Assert.True(vm.SpliceExportCommand.CanExecute(null));
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }

        [Fact]
        public void CreateSelectionFromPixels_同步更新裁剪时间文本()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                PrepareWaveformSession(vm);

                vm.CreateSelectionFromPixels(100, 600);

                Assert.True(vm.SelectionVm.HasSelection);
                Assert.Equal("00:00:01.200", vm.TrimStartText);
                Assert.Equal("00:00:07.200", vm.TrimEndText);
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }

        [Fact]
        public void ZoomWaveformAtRatio_放大后更新缩放和刻度()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                PrepareWaveformSession(vm);
                double beforeScale = vm.WaveformScaleX;

                vm.ZoomWaveformAtRatio(120, 0.5);

                Assert.True(vm.WaveformScaleX > beforeScale);
                Assert.True(vm.VisibleTickMarks.Count > 0);
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }

        [Fact]
        public void UpdateWaveformViewportWidth_刷新可见刻度集合()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                PrepareWaveformSession(vm);

                vm.UpdateWaveformViewportWidth(640);

                Assert.Equal(640, vm.WaveformVm.ViewportWidthPixels);
                Assert.True(vm.VisibleTickMarks.Count > 0);
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }

        [Fact]
        public void DeleteSelectionExportCommand_中间选区_可执行()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                PrepareWaveformSession(vm);
                vm.CreateSelectionFromPixels(100, 600);

                Assert.True(vm.DeleteSelectionExportCommand.CanExecute(null));
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }

        [Fact]
        public void DeleteSelectionExportCommand_全选删除_不可执行()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                PrepareWaveformSession(vm);
                vm.SelectionVm.SelectAll();

                Assert.False(vm.DeleteSelectionExportCommand.CanExecute(null));
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }

        [Fact]
        public void DeleteSelectionCommand_执行后更新会话状态与播放头()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                PrepareWaveformSession(vm);
                vm.CreateSelectionFromPixels(100, 600);

                vm.DeleteSelectionCommand.Execute(null);

                Assert.True(vm.HasSessionEdits);
                Assert.True(vm.CanUndoEdit);
                Assert.False(vm.SelectionVm.HasSelection);
                Assert.Equal(6d, vm.SessionDurationSeconds, 3);
                Assert.Equal("00:00:01.200", vm.CurrentTimeText);
                Assert.Equal("00:00:00.000", vm.TrimStartText);
                Assert.Equal("00:00:06.000", vm.TrimEndText);
                Assert.Equal(vm.WaveformVm.SecondsToSamples(1.2), vm.TimelineVm.PlayheadSample);
                Assert.Equal("已删除选区：00:00:01.200 - 00:00:07.200", vm.StatusText);
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }

        [Fact]
        public void UndoRedoEditCommand_删除后可恢复并再次应用()
        {
            string configPath;
            var vm = CreateVm(out configPath);
            try
            {
                PrepareWaveformSession(vm);
                vm.TimelineVm.SetPlayheadTime(9);
                vm.CreateSelectionFromPixels(100, 600);

                vm.DeleteSelectionCommand.Execute(null);
                vm.UndoEditCommand.Execute(null);

                Assert.False(vm.HasSessionEdits);
                Assert.False(vm.CanUndoEdit);
                Assert.True(vm.CanRedoEdit);
                Assert.Equal(12d, vm.SessionDurationSeconds, 3);
                Assert.Equal("00:00:09.000", vm.CurrentTimeText);
                Assert.Equal(vm.WaveformVm.SecondsToSamples(9), vm.TimelineVm.PlayheadSample);

                vm.RedoEditCommand.Execute(null);

                Assert.True(vm.HasSessionEdits);
                Assert.True(vm.CanUndoEdit);
                Assert.False(vm.CanRedoEdit);
                Assert.Equal(6d, vm.SessionDurationSeconds, 3);
                Assert.Equal("00:00:01.200", vm.CurrentTimeText);
                Assert.Equal(vm.WaveformVm.SecondsToSamples(1.2), vm.TimelineVm.PlayheadSample);
            }
            finally
            {
                vm.Dispose();
                if (File.Exists(configPath)) File.Delete(configPath);
            }
        }
    }
}
