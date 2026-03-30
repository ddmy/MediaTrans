using System;
using System.Collections.Generic;
using Xunit;
using MediaTrans.Services;
using MediaTrans.ViewModels;
using MediaTrans.Models;

namespace MediaTrans.Tests
{
    /// <summary>
    /// UndoRedoService 单元测试
    /// </summary>
    public class UndoRedoServiceTests
    {
        // ========== 简单命令模拟 ==========

        /// <summary>
        /// 测试用可撤销命令 — 递增/递减一个计数器
        /// </summary>
        private class CounterCommand : IUndoableCommand
        {
            private readonly Action _execute;
            private readonly Action _undo;
            private readonly string _desc;

            public string Description { get { return _desc; } }

            public CounterCommand(Action execute, Action undo, string desc)
            {
                _execute = execute;
                _undo = undo;
                _desc = desc;
            }

            public void Execute() { _execute(); }
            public void Undo() { _undo(); }
        }

        // ========== 构造函数测试 ==========

        [Fact]
        public void 默认构造_深度50()
        {
            var svc = new UndoRedoService();
            Assert.Equal(50, svc.MaxDepth);
            Assert.False(svc.CanUndo);
            Assert.False(svc.CanRedo);
            Assert.Equal(0, svc.UndoCount);
            Assert.Equal(0, svc.RedoCount);
        }

        [Fact]
        public void 自定义深度构造()
        {
            var svc = new UndoRedoService(100);
            Assert.Equal(100, svc.MaxDepth);
        }

        [Fact]
        public void 深度小于1_默认为50()
        {
            var svc = new UndoRedoService(0);
            Assert.Equal(50, svc.MaxDepth);
        }

        // ========== ExecuteCommand 测试 ==========

        [Fact]
        public void ExecuteCommand_null_抛出异常()
        {
            var svc = new UndoRedoService();
            Assert.Throws<ArgumentNullException>(() => svc.ExecuteCommand(null));
        }

        [Fact]
        public void ExecuteCommand_执行命令并压栈()
        {
            var svc = new UndoRedoService();
            int counter = 0;
            var cmd = new CounterCommand(() => counter++, () => counter--, "加1");

            svc.ExecuteCommand(cmd);

            Assert.Equal(1, counter);
            Assert.True(svc.CanUndo);
            Assert.Equal(1, svc.UndoCount);
            Assert.Equal("加1", svc.UndoDescription);
        }

        [Fact]
        public void ExecuteCommand_清空重做栈()
        {
            var svc = new UndoRedoService();
            int counter = 0;
            var cmd1 = new CounterCommand(() => counter++, () => counter--, "步骤1");
            var cmd2 = new CounterCommand(() => counter += 10, () => counter -= 10, "步骤2");

            svc.ExecuteCommand(cmd1);
            svc.Undo(); // 撤销后，重做栈有 1 个
            Assert.True(svc.CanRedo);

            svc.ExecuteCommand(cmd2); // 新命令清空重做栈
            Assert.False(svc.CanRedo);
            Assert.Equal(0, svc.RedoCount);
        }

        // ========== Undo 测试 ==========

        [Fact]
        public void Undo_空栈_返回false()
        {
            var svc = new UndoRedoService();
            Assert.False(svc.Undo());
        }

        [Fact]
        public void Undo_恢复状态()
        {
            var svc = new UndoRedoService();
            int counter = 0;
            var cmd = new CounterCommand(() => counter++, () => counter--, "加1");

            svc.ExecuteCommand(cmd);
            Assert.Equal(1, counter);

            bool result = svc.Undo();
            Assert.True(result);
            Assert.Equal(0, counter);
            Assert.False(svc.CanUndo);
            Assert.True(svc.CanRedo);
        }

        [Fact]
        public void 多次撤销_逐步恢复()
        {
            var svc = new UndoRedoService();
            int counter = 0;

            for (int i = 0; i < 5; i++)
            {
                int step = i + 1;
                svc.ExecuteCommand(new CounterCommand(
                    () => counter += step,
                    () => counter -= step,
                    string.Format("加{0}", step)));
            }

            Assert.Equal(15, counter); // 1+2+3+4+5

            svc.Undo(); // 撤销 +5
            Assert.Equal(10, counter);

            svc.Undo(); // 撤销 +4
            Assert.Equal(6, counter);

            svc.Undo(); // 撤销 +3
            Assert.Equal(3, counter);
        }

        // ========== Redo 测试 ==========

        [Fact]
        public void Redo_空栈_返回false()
        {
            var svc = new UndoRedoService();
            Assert.False(svc.Redo());
        }

        [Fact]
        public void Redo_重做命令()
        {
            var svc = new UndoRedoService();
            int counter = 0;
            var cmd = new CounterCommand(() => counter++, () => counter--, "加1");

            svc.ExecuteCommand(cmd);
            svc.Undo();
            Assert.Equal(0, counter);

            bool result = svc.Redo();
            Assert.True(result);
            Assert.Equal(1, counter);
            Assert.True(svc.CanUndo);
            Assert.False(svc.CanRedo);
        }

        // ========== 撤销/重做交替测试 ==========

        [Fact]
        public void 连续20步操作_逐一撤销再重做()
        {
            var svc = new UndoRedoService(50);
            int counter = 0;

            // 执行 20 步
            for (int i = 0; i < 20; i++)
            {
                int step = 1;
                svc.ExecuteCommand(new CounterCommand(
                    () => counter += step,
                    () => counter -= step,
                    string.Format("步骤{0}", i + 1)));
            }

            Assert.Equal(20, counter);
            Assert.Equal(20, svc.UndoCount);

            // 逐一撤销 20 步
            for (int i = 0; i < 20; i++)
            {
                Assert.True(svc.Undo());
                Assert.Equal(20 - i - 1, counter);
            }

            Assert.Equal(0, counter);
            Assert.False(svc.CanUndo);
            Assert.Equal(20, svc.RedoCount);

            // 逐一重做 20 步
            for (int i = 0; i < 20; i++)
            {
                Assert.True(svc.Redo());
                Assert.Equal(i + 1, counter);
            }

            Assert.Equal(20, counter);
            Assert.True(svc.CanUndo);
            Assert.False(svc.CanRedo);
        }

        // ========== 深度限制测试 ==========

        [Fact]
        public void 超过最大深度_移除最早命令()
        {
            var svc = new UndoRedoService(5);
            int counter = 0;

            for (int i = 0; i < 10; i++)
            {
                int step = 1;
                svc.ExecuteCommand(new CounterCommand(
                    () => counter += step,
                    () => counter -= step,
                    string.Format("步骤{0}", i + 1)));
            }

            Assert.Equal(10, counter);
            Assert.Equal(5, svc.UndoCount); // 只保留最近 5 步

            // 撤销 5 步
            for (int i = 0; i < 5; i++)
            {
                svc.Undo();
            }

            Assert.Equal(5, counter); // 只能撤销 5 步，不是回到 0
            Assert.False(svc.CanUndo);
        }

        // ========== Clear 测试 ==========

        [Fact]
        public void Clear_清空所有栈()
        {
            var svc = new UndoRedoService();
            int counter = 0;

            svc.ExecuteCommand(new CounterCommand(() => counter++, () => counter--, "1"));
            svc.ExecuteCommand(new CounterCommand(() => counter++, () => counter--, "2"));
            svc.Undo();

            Assert.True(svc.CanUndo);
            Assert.True(svc.CanRedo);

            svc.Clear();

            Assert.False(svc.CanUndo);
            Assert.False(svc.CanRedo);
            Assert.Equal(0, svc.UndoCount);
            Assert.Equal(0, svc.RedoCount);
        }

        // ========== StateChanged 事件测试 ==========

        [Fact]
        public void StateChanged_执行时触发()
        {
            var svc = new UndoRedoService();
            int eventCount = 0;
            svc.StateChanged += (s, e) => eventCount++;

            svc.ExecuteCommand(new CounterCommand(() => { }, () => { }, "测试"));

            Assert.Equal(1, eventCount);
        }

        [Fact]
        public void StateChanged_撤销时触发()
        {
            var svc = new UndoRedoService();
            svc.ExecuteCommand(new CounterCommand(() => { }, () => { }, "测试"));

            int eventCount = 0;
            svc.StateChanged += (s, e) => eventCount++;
            svc.Undo();

            Assert.Equal(1, eventCount);
        }

        [Fact]
        public void StateChanged_重做时触发()
        {
            var svc = new UndoRedoService();
            svc.ExecuteCommand(new CounterCommand(() => { }, () => { }, "测试"));
            svc.Undo();

            int eventCount = 0;
            svc.StateChanged += (s, e) => eventCount++;
            svc.Redo();

            Assert.Equal(1, eventCount);
        }

        [Fact]
        public void StateChanged_清空时触发()
        {
            var svc = new UndoRedoService();
            svc.ExecuteCommand(new CounterCommand(() => { }, () => { }, "测试"));

            int eventCount = 0;
            svc.StateChanged += (s, e) => eventCount++;
            svc.Clear();

            Assert.Equal(1, eventCount);
        }

        // ========== 描述获取测试 ==========

        [Fact]
        public void UndoDescription_空栈_返回空串()
        {
            var svc = new UndoRedoService();
            Assert.Equal(string.Empty, svc.UndoDescription);
        }

        [Fact]
        public void RedoDescription_空栈_返回空串()
        {
            var svc = new UndoRedoService();
            Assert.Equal(string.Empty, svc.RedoDescription);
        }

        [Fact]
        public void GetUndoDescriptions_返回正确顺序()
        {
            var svc = new UndoRedoService();
            svc.ExecuteCommand(new CounterCommand(() => { }, () => { }, "第一步"));
            svc.ExecuteCommand(new CounterCommand(() => { }, () => { }, "第二步"));
            svc.ExecuteCommand(new CounterCommand(() => { }, () => { }, "第三步"));

            var descriptions = svc.GetUndoDescriptions();
            Assert.Equal(3, descriptions.Count);
            Assert.Equal("第三步", descriptions[0]); // 最新在前
            Assert.Equal("第二步", descriptions[1]);
            Assert.Equal("第一步", descriptions[2]);
        }

        [Fact]
        public void GetRedoDescriptions_返回正确顺序()
        {
            var svc = new UndoRedoService();
            svc.ExecuteCommand(new CounterCommand(() => { }, () => { }, "第一步"));
            svc.ExecuteCommand(new CounterCommand(() => { }, () => { }, "第二步"));
            svc.Undo();
            svc.Undo();

            var descriptions = svc.GetRedoDescriptions();
            Assert.Equal(2, descriptions.Count);
            Assert.Equal("第一步", descriptions[0]); // 最后撤销的在前
            Assert.Equal("第二步", descriptions[1]);
        }

        // ========== MaxDepth 属性测试 ==========

        [Fact]
        public void MaxDepth_可动态修改()
        {
            var svc = new UndoRedoService(10);
            svc.MaxDepth = 20;
            Assert.Equal(20, svc.MaxDepth);
        }

        [Fact]
        public void MaxDepth_设为0_取1()
        {
            var svc = new UndoRedoService(10);
            svc.MaxDepth = 0;
            Assert.Equal(1, svc.MaxDepth);
        }
    }

    /// <summary>
    /// 具体可撤销命令测试
    /// </summary>
    public class UndoableCommandsTests
    {
        // ========== SelectionChangeCommand 测试 ==========

        [Fact]
        public void SelectionChangeCommand_null_抛异常()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SelectionChangeCommand(null, 0, 0, 100, 200));
        }

        [Fact]
        public void SelectionChangeCommand_执行和撤销()
        {
            var waveformVm = new WaveformViewModel();
            waveformVm.Initialize(1000000, 44100, 1000);
            var selVm = new SelectionViewModel(waveformVm);

            var cmd = new SelectionChangeCommand(selVm, 0, 0, 1000, 5000);

            cmd.Execute();
            Assert.Equal(1000, selVm.SelectionStartSample);
            Assert.Equal(5000, selVm.SelectionEndSample);

            cmd.Undo();
            Assert.Equal(0, selVm.SelectionStartSample);
            Assert.Equal(0, selVm.SelectionEndSample);
        }

        [Fact]
        public void SelectionChangeCommand_描述正确()
        {
            var waveformVm = new WaveformViewModel();
            waveformVm.Initialize(1000000, 44100, 1000);
            var selVm = new SelectionViewModel(waveformVm);
            var cmd = new SelectionChangeCommand(selVm, 0, 0, 100, 200);

            Assert.Equal("裁剪区间变更", cmd.Description);
        }

        // ========== ClipAddCommand 测试 ==========

        [Fact]
        public void ClipAddCommand_null参数_抛异常()
        {
            var clip = new TimelineClip() { SourceFilePath = "test.mp4", SourceStartSeconds = 0, SourceEndSeconds = 10, MediaType = "video" };
            Assert.Throws<ArgumentNullException>(() => new ClipAddCommand(null, clip));

            var trackVm = new TimelineTrackViewModel();
            Assert.Throws<ArgumentNullException>(() => new ClipAddCommand(trackVm, null));
        }

        [Fact]
        public void ClipAddCommand_添加和撤销()
        {
            var trackVm = new TimelineTrackViewModel();
            var clip = new TimelineClip() { SourceFilePath = "test.mp4", SourceStartSeconds = 0, SourceEndSeconds = 10, MediaType = "video" };
            var cmd = new ClipAddCommand(trackVm, clip);

            cmd.Execute();
            Assert.Equal(1, trackVm.GetClipList().Count);

            cmd.Undo();
            Assert.Equal(0, trackVm.GetClipList().Count);
        }

        [Fact]
        public void ClipAddCommand_插入模式()
        {
            var trackVm = new TimelineTrackViewModel();
            var clip1 = new TimelineClip() { SourceFilePath = "a.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };
            var clip2 = new TimelineClip() { SourceFilePath = "b.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };
            var clip3 = new TimelineClip() { SourceFilePath = "c.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };

            trackVm.AddClip(clip1);
            trackVm.AddClip(clip3);

            var cmd = new ClipAddCommand(trackVm, clip2, 1); // 插入到索引 1

            cmd.Execute();
            Assert.Equal(3, trackVm.GetClipList().Count);
            Assert.Same(clip2, trackVm.GetClipList()[1]);

            cmd.Undo();
            Assert.Equal(2, trackVm.GetClipList().Count);
        }

        [Fact]
        public void ClipAddCommand_描述正确()
        {
            var trackVm = new TimelineTrackViewModel();
            var clip = new TimelineClip() { SourceFilePath = "test.mp4", SourceStartSeconds = 0, SourceEndSeconds = 10, MediaType = "video" };
            var cmd = new ClipAddCommand(trackVm, clip);

            Assert.Equal("添加片段", cmd.Description);
        }

        // ========== ClipRemoveCommand 测试 ==========

        [Fact]
        public void ClipRemoveCommand_null参数_抛异常()
        {
            var clip = new TimelineClip() { SourceFilePath = "test.mp4", SourceStartSeconds = 0, SourceEndSeconds = 10, MediaType = "video" };
            Assert.Throws<ArgumentNullException>(() => new ClipRemoveCommand(null, clip, 0));

            var trackVm = new TimelineTrackViewModel();
            Assert.Throws<ArgumentNullException>(() => new ClipRemoveCommand(trackVm, null, 0));
        }

        [Fact]
        public void ClipRemoveCommand_删除和撤销()
        {
            var trackVm = new TimelineTrackViewModel();
            var clip = new TimelineClip() { SourceFilePath = "test.mp4", SourceStartSeconds = 0, SourceEndSeconds = 10, MediaType = "video" };
            trackVm.AddClip(clip);

            var cmd = new ClipRemoveCommand(trackVm, clip, 0);

            cmd.Execute();
            Assert.Equal(0, trackVm.GetClipList().Count);

            cmd.Undo();
            Assert.Equal(1, trackVm.GetClipList().Count);
        }

        [Fact]
        public void ClipRemoveCommand_撤销后恢复到原位置()
        {
            var trackVm = new TimelineTrackViewModel();
            var clip1 = new TimelineClip() { SourceFilePath = "a.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };
            var clip2 = new TimelineClip() { SourceFilePath = "b.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };
            var clip3 = new TimelineClip() { SourceFilePath = "c.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };
            trackVm.AddClip(clip1);
            trackVm.AddClip(clip2);
            trackVm.AddClip(clip3);

            // 删除中间的 clip2（索引 1）
            var cmd = new ClipRemoveCommand(trackVm, clip2, 1);
            cmd.Execute();
            Assert.Equal(2, trackVm.GetClipList().Count);

            cmd.Undo();
            Assert.Equal(3, trackVm.GetClipList().Count);
            Assert.Same(clip2, trackVm.GetClipList()[1]); // 恢复到原位置
        }

        // ========== ClipMoveCommand 测试 ==========

        [Fact]
        public void ClipMoveCommand_null参数_抛异常()
        {
            Assert.Throws<ArgumentNullException>(() => new ClipMoveCommand(null, 0, 1));
        }

        [Fact]
        public void ClipMoveCommand_移动和撤销()
        {
            var trackVm = new TimelineTrackViewModel();
            var clip1 = new TimelineClip() { SourceFilePath = "a.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };
            var clip2 = new TimelineClip() { SourceFilePath = "b.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };
            var clip3 = new TimelineClip() { SourceFilePath = "c.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };
            trackVm.AddClip(clip1);
            trackVm.AddClip(clip2);
            trackVm.AddClip(clip3);

            var cmd = new ClipMoveCommand(trackVm, 0, 2); // 把第一个移到最后
            cmd.Execute();

            Assert.Same(clip2, trackVm.GetClipList()[0]);
            Assert.Same(clip3, trackVm.GetClipList()[1]);
            Assert.Same(clip1, trackVm.GetClipList()[2]);

            cmd.Undo(); // 移回来
            Assert.Same(clip1, trackVm.GetClipList()[0]);
            Assert.Same(clip2, trackVm.GetClipList()[1]);
            Assert.Same(clip3, trackVm.GetClipList()[2]);
        }

        // ========== GainChangeCommand 测试 ==========

        [Fact]
        public void GainChangeCommand_null参数_抛异常()
        {
            Assert.Throws<ArgumentNullException>(() => new GainChangeCommand(null, 0, 6));
        }

        [Fact]
        public void GainChangeCommand_执行和撤销()
        {
            var gainVm = new GainViewModel();

            var cmd = new GainChangeCommand(gainVm, 0.0, 6.0);

            cmd.Execute();
            Assert.Equal(6.0, gainVm.GainDb);

            cmd.Undo();
            Assert.Equal(0.0, gainVm.GainDb);
        }

        [Fact]
        public void GainChangeCommand_描述包含增益值()
        {
            var gainVm = new GainViewModel();
            var cmd = new GainChangeCommand(gainVm, 0.0, 6.0);

            Assert.Contains("+6.0 dB", cmd.Description);
        }

        [Fact]
        public void GainChangeCommand_多步撤销()
        {
            var svc = new UndoRedoService();
            var gainVm = new GainViewModel();

            svc.ExecuteCommand(new GainChangeCommand(gainVm, 0.0, 3.0));
            Assert.Equal(3.0, gainVm.GainDb);

            svc.ExecuteCommand(new GainChangeCommand(gainVm, 3.0, 6.0));
            Assert.Equal(6.0, gainVm.GainDb);

            svc.ExecuteCommand(new GainChangeCommand(gainVm, 6.0, -3.0));
            Assert.Equal(-3.0, gainVm.GainDb);

            svc.Undo();
            Assert.Equal(6.0, gainVm.GainDb);

            svc.Undo();
            Assert.Equal(3.0, gainVm.GainDb);

            svc.Undo();
            Assert.Equal(0.0, gainVm.GainDb);
        }
    }

    /// <summary>
    /// UndoRedoViewModel 单元测试
    /// </summary>
    public class UndoRedoViewModelTests
    {
        private class SimpleCommand : IUndoableCommand
        {
            public int Value { get; set; }
            public string Description { get { return "简单命令"; } }
            public void Execute() { Value++; }
            public void Undo() { Value--; }
        }

        [Fact]
        public void 构造函数_null_抛异常()
        {
            Assert.Throws<ArgumentNullException>(() => new UndoRedoViewModel(null));
        }

        [Fact]
        public void 构造函数_初始状态正确()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);

            Assert.False(vm.CanUndo);
            Assert.False(vm.CanRedo);
            Assert.Equal(0, vm.UndoCount);
            Assert.Equal(0, vm.RedoCount);
            Assert.Equal("无操作历史", vm.StatusText);
        }

        [Fact]
        public void ExecuteCommand_更新状态()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);
            var cmd = new SimpleCommand();

            vm.ExecuteCommand(cmd);

            Assert.True(vm.CanUndo);
            Assert.Equal(1, vm.UndoCount);
            Assert.Contains("简单命令", vm.StatusText);
            Assert.Contains("1步", vm.StatusText);
        }

        [Fact]
        public void UndoCommand_执行撤销()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);
            var cmd = new SimpleCommand();

            vm.ExecuteCommand(cmd);
            Assert.Equal(1, cmd.Value);

            vm.UndoCommand.Execute(null);
            Assert.Equal(0, cmd.Value);
            Assert.False(vm.CanUndo);
            Assert.True(vm.CanRedo);
        }

        [Fact]
        public void RedoCommand_执行重做()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);
            var cmd = new SimpleCommand();

            vm.ExecuteCommand(cmd);
            vm.UndoCommand.Execute(null);
            vm.RedoCommand.Execute(null);

            Assert.Equal(1, cmd.Value);
            Assert.True(vm.CanUndo);
            Assert.False(vm.CanRedo);
        }

        [Fact]
        public void ClearHistoryCommand_清空历史()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);

            vm.ExecuteCommand(new SimpleCommand());
            vm.ExecuteCommand(new SimpleCommand());

            vm.ClearHistoryCommand.Execute(null);

            Assert.False(vm.CanUndo);
            Assert.False(vm.CanRedo);
            Assert.Equal("无操作历史", vm.StatusText);
        }

        [Fact]
        public void 属性变更通知_CanUndo()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);
            var changedProps = new List<string>();
            vm.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName);

            vm.ExecuteCommand(new SimpleCommand());

            Assert.Contains("CanUndo", changedProps);
            Assert.Contains("UndoCount", changedProps);
        }

        [Fact]
        public void UndoCommand_无法撤销时不可执行()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);

            Assert.False(vm.UndoCommand.CanExecute(null));
        }

        [Fact]
        public void RedoCommand_无法重做时不可执行()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);

            Assert.False(vm.RedoCommand.CanExecute(null));
        }

        [Fact]
        public void ClearHistoryCommand_无历史时不可执行()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);

            Assert.False(vm.ClearHistoryCommand.CanExecute(null));
        }

        [Fact]
        public void Service属性_返回注入的服务()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);

            Assert.Same(svc, vm.Service);
        }

        [Fact]
        public void StatusText_撤销后更新()
        {
            var svc = new UndoRedoService();
            var vm = new UndoRedoViewModel(svc);

            vm.ExecuteCommand(new SimpleCommand());
            vm.ExecuteCommand(new SimpleCommand());
            Assert.Contains("2步", vm.StatusText);

            vm.UndoCommand.Execute(null);
            Assert.Contains("1步", vm.StatusText);

            vm.UndoCommand.Execute(null);
            Assert.Equal("无操作历史", vm.StatusText);
        }
    }

    /// <summary>
    /// 综合集成测试 — 模拟真实编辑场景
    /// </summary>
    public class UndoRedoIntegrationTests
    {
        [Fact]
        public void 混合操作_选区和增益的组合撤销()
        {
            var svc = new UndoRedoService();
            var waveformVm = new WaveformViewModel();
            waveformVm.Initialize(1000000, 44100, 1000);
            var selVm = new SelectionViewModel(waveformVm);
            var gainVm = new GainViewModel();

            // 步骤1：设置选区
            svc.ExecuteCommand(new SelectionChangeCommand(selVm, 0, 0, 1000, 5000));
            Assert.Equal(1000, selVm.SelectionStartSample);

            // 步骤2：调增益
            svc.ExecuteCommand(new GainChangeCommand(gainVm, 0.0, 6.0));
            Assert.Equal(6.0, gainVm.GainDb);

            // 步骤3：修改选区
            svc.ExecuteCommand(new SelectionChangeCommand(selVm, 1000, 5000, 2000, 8000));
            Assert.Equal(2000, selVm.SelectionStartSample);

            // 撤销步骤3
            svc.Undo();
            Assert.Equal(1000, selVm.SelectionStartSample);
            Assert.Equal(5000, selVm.SelectionEndSample);
            Assert.Equal(6.0, gainVm.GainDb); // 增益不受影响

            // 撤销步骤2
            svc.Undo();
            Assert.Equal(0.0, gainVm.GainDb);
            Assert.Equal(1000, selVm.SelectionStartSample); // 选区不受影响

            // 撤销步骤1
            svc.Undo();
            Assert.Equal(0, selVm.SelectionStartSample);
            Assert.Equal(0, selVm.SelectionEndSample);
        }

        [Fact]
        public void 片段操作_添加删除移动的组合撤销()
        {
            var svc = new UndoRedoService();
            var trackVm = new TimelineTrackViewModel();
            var clip1 = new TimelineClip() { SourceFilePath = "a.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };
            var clip2 = new TimelineClip() { SourceFilePath = "b.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };
            var clip3 = new TimelineClip() { SourceFilePath = "c.mp4", SourceStartSeconds = 0, SourceEndSeconds = 5, MediaType = "video" };

            // 添加三个片段
            svc.ExecuteCommand(new ClipAddCommand(trackVm, clip1));
            svc.ExecuteCommand(new ClipAddCommand(trackVm, clip2));
            svc.ExecuteCommand(new ClipAddCommand(trackVm, clip3));
            Assert.Equal(3, trackVm.GetClipList().Count);

            // 移动片段
            svc.ExecuteCommand(new ClipMoveCommand(trackVm, 0, 2));
            Assert.Same(clip2, trackVm.GetClipList()[0]);

            // 删除片段
            svc.ExecuteCommand(new ClipRemoveCommand(trackVm, clip2, 0));
            Assert.Equal(2, trackVm.GetClipList().Count);

            // 全部撤销
            svc.Undo(); // 恢复删除
            Assert.Equal(3, trackVm.GetClipList().Count);

            svc.Undo(); // 恢复移动
            Assert.Same(clip1, trackVm.GetClipList()[0]);
            Assert.Same(clip2, trackVm.GetClipList()[1]);
            Assert.Same(clip3, trackVm.GetClipList()[2]);

            svc.Undo(); // 撤销第三个片段添加
            Assert.Equal(2, trackVm.GetClipList().Count);

            svc.Undo(); // 撤销第二个
            Assert.Equal(1, trackVm.GetClipList().Count);

            svc.Undo(); // 撤销第一个
            Assert.Equal(0, trackVm.GetClipList().Count);
        }

        [Fact]
        public void 撤销重做交替_状态一致性()
        {
            var svc = new UndoRedoService();
            var gainVm = new GainViewModel();

            double[] steps = new double[] { 3.0, 6.0, -3.0, 10.0, -10.0 };
            double prev = 0.0;

            // 执行 5 步增益变化
            foreach (double step in steps)
            {
                svc.ExecuteCommand(new GainChangeCommand(gainVm, prev, step));
                prev = step;
            }

            Assert.Equal(-10.0, gainVm.GainDb);

            // 撤销 3 步
            svc.Undo();
            Assert.Equal(10.0, gainVm.GainDb);
            svc.Undo();
            Assert.Equal(-3.0, gainVm.GainDb);
            svc.Undo();
            Assert.Equal(6.0, gainVm.GainDb);

            // 重做 2 步
            svc.Redo();
            Assert.Equal(-3.0, gainVm.GainDb);
            svc.Redo();
            Assert.Equal(10.0, gainVm.GainDb);

            // 插入新命令（应清空剩余重做栈）
            svc.ExecuteCommand(new GainChangeCommand(gainVm, 10.0, 0.0));
            Assert.Equal(0.0, gainVm.GainDb);
            Assert.False(svc.CanRedo); // 重做栈被清空
        }
    }
}
