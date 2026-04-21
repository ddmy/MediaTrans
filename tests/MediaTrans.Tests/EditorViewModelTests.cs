using System;
using System.IO;
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
    }
}
