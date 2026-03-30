using System;
using MediaTrans.Services;
using MediaTrans.ViewModels;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 音频播放服务单元测试（不涉及实际硬件播放）
    /// </summary>
    public class AudioPlaybackServiceTests
    {
        [Fact]
        public void 构造_初始状态为Stopped()
        {
            using (var service = new AudioPlaybackService())
            {
                Assert.Equal(PlaybackState.Stopped, service.State);
                Assert.False(service.IsPlaying);
                Assert.False(service.IsPaused);
            }
        }

        [Fact]
        public void 未打开文件_播放无异常()
        {
            using (var service = new AudioPlaybackService())
            {
                service.Play();   // 不应崩溃
                service.Pause();  // 不应崩溃
                service.Stop();   // 不应崩溃
            }
        }

        [Fact]
        public void 未打开文件_Seek无异常()
        {
            using (var service = new AudioPlaybackService())
            {
                service.SeekToSample(44100); // 不应崩溃
            }
        }

        [Fact]
        public void 未打开文件_CurrentPosition为0()
        {
            using (var service = new AudioPlaybackService())
            {
                Assert.Equal(0, service.CurrentPositionSamples);
                Assert.Equal(0, service.CurrentPositionSeconds);
            }
        }

        [Fact]
        public void GetVolume_默认为1()
        {
            using (var service = new AudioPlaybackService())
            {
                Assert.Equal(1.0f, service.GetVolume());
            }
        }

        [Fact]
        public void Dispose_多次调用无异常()
        {
            var service = new AudioPlaybackService();
            service.Dispose();
            service.Dispose(); // 第二次不应崩溃
        }

        [Fact]
        public void PlaybackState枚举_包含三种状态()
        {
            Assert.Equal(0, (int)PlaybackState.Stopped);
            Assert.Equal(1, (int)PlaybackState.Playing);
            Assert.Equal(2, (int)PlaybackState.Paused);
        }
    }

    /// <summary>
    /// 播放 ViewModel 单元测试
    /// </summary>
    public class PlaybackViewModelTests
    {
        private WaveformViewModel CreateWaveformVm(long totalSamples = 441000, int sampleRate = 44100, int width = 1000)
        {
            var vm = new WaveformViewModel();
            vm.Initialize(totalSamples, sampleRate, width);
            return vm;
        }

        private PlaybackViewModel CreatePlaybackVm()
        {
            var wfVm = CreateWaveformVm();
            var tlVm = new TimelineViewModel(wfVm);
            var selVm = new SelectionViewModel(wfVm);
            var service = new AudioPlaybackService();
            return new PlaybackViewModel(service, tlVm, selVm);
        }

        // ===== 构造函数测试 =====

        [Fact]
        public void 构造函数_初始状态()
        {
            using (var vm = CreatePlaybackVm())
            {
                Assert.False(vm.IsPlaying);
                Assert.False(vm.IsPaused);
                Assert.Equal(1.0f, vm.Volume);
                Assert.True(vm.CanPlay);
                Assert.False(vm.CanPause);
                Assert.False(vm.CanStop);
                Assert.Equal("00:00:00.000", vm.PlaybackTimeText);
            }
        }

        [Fact]
        public void 构造函数_null播放服务_抛异常()
        {
            var wfVm = CreateWaveformVm();
            var tlVm = new TimelineViewModel(wfVm);
            var selVm = new SelectionViewModel(wfVm);

            Assert.Throws<ArgumentNullException>(() =>
                new PlaybackViewModel(null, tlVm, selVm));
        }

        [Fact]
        public void 构造函数_null时间轴_抛异常()
        {
            using (var service = new AudioPlaybackService())
            {
                var wfVm = CreateWaveformVm();
                var selVm = new SelectionViewModel(wfVm);

                Assert.Throws<ArgumentNullException>(() =>
                    new PlaybackViewModel(service, null, selVm));
            }
        }

        [Fact]
        public void 构造函数_null选区_抛异常()
        {
            using (var service = new AudioPlaybackService())
            {
                var wfVm = CreateWaveformVm();
                var tlVm = new TimelineViewModel(wfVm);

                Assert.Throws<ArgumentNullException>(() =>
                    new PlaybackViewModel(service, tlVm, null));
            }
        }

        // ===== 音量测试 =====

        [Fact]
        public void Volume_设置有效值()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.Volume = 0.5f;
                Assert.Equal(0.5f, vm.Volume);
            }
        }

        [Fact]
        public void Volume_clamp_不超过1()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.Volume = 1.5f;
                Assert.Equal(1.0f, vm.Volume);
            }
        }

        [Fact]
        public void Volume_clamp_不小于0()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.Volume = -0.5f;
                Assert.Equal(0.0f, vm.Volume);
            }
        }

        // ===== 播放状态逻辑测试 =====

        [Fact]
        public void ExecutePlay_状态变为Playing()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.ExecutePlay(null);
                Assert.True(vm.IsPlaying);
                Assert.False(vm.IsPaused);
                Assert.False(vm.CanPlay);
                Assert.True(vm.CanPause);
                Assert.True(vm.CanStop);

                // 清理
                vm.ExecuteStop(null);
            }
        }

        [Fact]
        public void ExecutePause_状态变为Paused()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.ExecutePlay(null);
                vm.ExecutePause(null);
                Assert.True(vm.IsPlaying);
                Assert.True(vm.IsPaused);
                Assert.True(vm.CanPlay);    // 暂停后可以继续播放
                Assert.False(vm.CanPause);  // 已暂停不能再暂停

                vm.ExecuteStop(null);
            }
        }

        [Fact]
        public void ExecuteStop_状态恢复为Stopped()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.ExecutePlay(null);
                vm.ExecuteStop(null);
                Assert.False(vm.IsPlaying);
                Assert.False(vm.IsPaused);
                Assert.True(vm.CanPlay);
                Assert.False(vm.CanPause);
                Assert.False(vm.CanStop);
            }
        }

        // ===== TogglePlayPause 测试 =====

        [Fact]
        public void TogglePlayPause_从停止到播放()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.TogglePlayPause();
                Assert.True(vm.IsPlaying);
                Assert.False(vm.IsPaused);

                vm.ExecuteStop(null);
            }
        }

        [Fact]
        public void TogglePlayPause_从播放到暂停()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.TogglePlayPause(); // 播放
                vm.TogglePlayPause(); // 暂停
                Assert.True(vm.IsPaused);

                vm.ExecuteStop(null);
            }
        }

        [Fact]
        public void TogglePlayPause_从暂停到播放()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.TogglePlayPause(); // 播放
                vm.TogglePlayPause(); // 暂停
                vm.TogglePlayPause(); // 继续播放
                Assert.True(vm.IsPlaying);
                Assert.False(vm.IsPaused);

                vm.ExecuteStop(null);
            }
        }

        // ===== PlaySelection 测试 =====

        [Fact]
        public void PlaySelection_无选区_不播放()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.PlaySelection();
                Assert.False(vm.IsPlaying); // 无选区不应播放
            }
        }

        [Fact]
        public void PlaySelection_有选区_开始播放()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.SelectionVm.CreateSelection(100, 500);
                vm.PlaySelection();
                Assert.True(vm.IsPlaying);

                vm.ExecuteStop(null);
            }
        }

        // ===== PlayFromPlayhead 测试 =====

        [Fact]
        public void PlayFromPlayhead_开始播放()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.TimelineVm.PlayheadSample = 44100;
                vm.PlayFromPlayhead();
                Assert.True(vm.IsPlaying);

                vm.ExecuteStop(null);
            }
        }

        // ===== 属性变更通知测试 =====

        [Fact]
        public void 属性通知_IsPlaying()
        {
            using (var vm = CreatePlaybackVm())
            {
                bool notified = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == "IsPlaying") notified = true;
                };

                vm.ExecutePlay(null);
                Assert.True(notified);
                vm.ExecuteStop(null);
            }
        }

        [Fact]
        public void 属性通知_Volume()
        {
            using (var vm = CreatePlaybackVm())
            {
                bool notified = false;
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == "Volume") notified = true;
                };

                vm.Volume = 0.5f;
                Assert.True(notified);
            }
        }

        [Fact]
        public void PlaybackTimeText_初始值正确()
        {
            using (var vm = CreatePlaybackVm())
            {
                Assert.Equal("00:00:00.000", vm.PlaybackTimeText);
            }
        }

        [Fact]
        public void UpdatePlaybackPosition_更新时间文本()
        {
            using (var vm = CreatePlaybackVm())
            {
                vm.UpdatePlaybackPosition();
                // 未打开文件，位置为0，时间文本应为初始值
                Assert.Equal("00:00:00.000", vm.PlaybackTimeText);
            }
        }

        // ===== Command 测试 =====

        [Fact]
        public void PlayCommand_不为null()
        {
            using (var vm = CreatePlaybackVm())
            {
                Assert.NotNull(vm.PlayCommand);
                Assert.NotNull(vm.PauseCommand);
                Assert.NotNull(vm.StopCommand);
            }
        }

        [Fact]
        public void PlayCommand_初始可执行()
        {
            using (var vm = CreatePlaybackVm())
            {
                Assert.True(vm.PlayCommand.CanExecute(null));
            }
        }

        // ===== 引用测试 =====

        [Fact]
        public void 引用_PlaybackService()
        {
            using (var vm = CreatePlaybackVm())
            {
                Assert.NotNull(vm.PlaybackService);
            }
        }

        [Fact]
        public void 引用_TimelineVm()
        {
            using (var vm = CreatePlaybackVm())
            {
                Assert.NotNull(vm.TimelineVm);
            }
        }

        [Fact]
        public void 引用_SelectionVm()
        {
            using (var vm = CreatePlaybackVm())
            {
                Assert.NotNull(vm.SelectionVm);
            }
        }

        // ===== Dispose 测试 =====

        [Fact]
        public void Dispose_多次调用无异常()
        {
            var vm = CreatePlaybackVm();
            vm.Dispose();
            vm.Dispose();
        }

        // ===== PositionUpdated 事件测试 =====

        [Fact]
        public void PositionUpdated_事件触发()
        {
            using (var vm = CreatePlaybackVm())
            {
                bool fired = false;
                vm.PositionUpdated += (s, e) => { fired = true; };

                vm.UpdatePlaybackPosition();
                Assert.True(fired);
            }
        }
    }
}
