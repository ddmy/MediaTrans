using System;
using System.Collections.Generic;
using Xunit;
using MediaTrans.Services;
using MediaTrans.ViewModels;

namespace MediaTrans.Tests
{
    /// <summary>
    /// GainService 单元测试
    /// </summary>
    public class GainServiceTests
    {
        // ========== DbToLinear 测试 ==========

        [Fact]
        public void DbToLinear_零dB_返回1()
        {
            double result = GainService.DbToLinear(0.0);
            Assert.Equal(1.0, result, 6);
        }

        [Fact]
        public void DbToLinear_正6dB_约为2倍()
        {
            double result = GainService.DbToLinear(6.0);
            Assert.Equal(1.9953, result, 3);
        }

        [Fact]
        public void DbToLinear_负6dB_约为0点5倍()
        {
            double result = GainService.DbToLinear(-6.0);
            Assert.Equal(0.5012, result, 3);
        }

        [Fact]
        public void DbToLinear_正20dB_约为10倍()
        {
            double result = GainService.DbToLinear(20.0);
            Assert.Equal(10.0, result, 3);
        }

        [Fact]
        public void DbToLinear_负20dB_约为0点1倍()
        {
            double result = GainService.DbToLinear(-20.0);
            Assert.Equal(0.1, result, 3);
        }

        // ========== LinearToDb 测试 ==========

        [Fact]
        public void LinearToDb_1倍_返回0dB()
        {
            double result = GainService.LinearToDb(1.0);
            Assert.Equal(0.0, result, 6);
        }

        [Fact]
        public void LinearToDb_2倍_约为正6dB()
        {
            double result = GainService.LinearToDb(2.0);
            Assert.Equal(6.0206, result, 3);
        }

        [Fact]
        public void LinearToDb_0点5倍_约为负6dB()
        {
            double result = GainService.LinearToDb(0.5);
            Assert.Equal(-6.0206, result, 3);
        }

        [Fact]
        public void LinearToDb_零或负值_返回最小值()
        {
            Assert.Equal(GainService.MinGainDb, GainService.LinearToDb(0));
            Assert.Equal(GainService.MinGainDb, GainService.LinearToDb(-1));
        }

        // ========== 往返转换精度测试 ==========

        [Theory]
        [InlineData(-20.0)]
        [InlineData(-10.0)]
        [InlineData(-6.0)]
        [InlineData(0.0)]
        [InlineData(6.0)]
        [InlineData(10.0)]
        [InlineData(20.0)]
        public void DbToLinear_LinearToDb_往返一致(double db)
        {
            double linear = GainService.DbToLinear(db);
            double roundTrip = GainService.LinearToDb(linear);
            Assert.Equal(db, roundTrip, 6);
        }

        // ========== ClampGainDb 测试 ==========

        [Fact]
        public void ClampGainDb_范围内_不变()
        {
            Assert.Equal(5.0, GainService.ClampGainDb(5.0));
            Assert.Equal(-10.0, GainService.ClampGainDb(-10.0));
            Assert.Equal(0.0, GainService.ClampGainDb(0.0));
        }

        [Fact]
        public void ClampGainDb_超过上限_钳位到20()
        {
            Assert.Equal(20.0, GainService.ClampGainDb(25.0));
            Assert.Equal(20.0, GainService.ClampGainDb(100.0));
        }

        [Fact]
        public void ClampGainDb_低于下限_钳位到负20()
        {
            Assert.Equal(-20.0, GainService.ClampGainDb(-25.0));
            Assert.Equal(-20.0, GainService.ClampGainDb(-100.0));
        }

        // ========== SnapToStep 测试 ==========

        [Fact]
        public void SnapToStep_已对齐_不变()
        {
            Assert.Equal(5.0, GainService.SnapToStep(5.0));
            Assert.Equal(-3.5, GainService.SnapToStep(-3.5));
            Assert.Equal(0.0, GainService.SnapToStep(0.0));
        }

        [Fact]
        public void SnapToStep_未对齐_对齐到最近步进()
        {
            Assert.Equal(5.0, GainService.SnapToStep(5.1));
            Assert.Equal(5.0, GainService.SnapToStep(5.2));
            Assert.Equal(5.5, GainService.SnapToStep(5.3));
            Assert.Equal(-3.5, GainService.SnapToStep(-3.6));
        }

        // ========== FormatGainText 测试 ==========

        [Fact]
        public void FormatGainText_正数_带加号()
        {
            Assert.Equal("+6.0 dB", GainService.FormatGainText(6.0));
            Assert.Equal("+0.5 dB", GainService.FormatGainText(0.5));
        }

        [Fact]
        public void FormatGainText_负数_带减号()
        {
            Assert.Equal("-6.0 dB", GainService.FormatGainText(-6.0));
            Assert.Equal("-3.5 dB", GainService.FormatGainText(-3.5));
        }

        [Fact]
        public void FormatGainText_零_无符号()
        {
            Assert.Equal("0.0 dB", GainService.FormatGainText(0.0));
        }

        // ========== ApplyGainToPcm16 测试 ==========

        [Fact]
        public void ApplyGainToPcm16_null输入_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() => GainService.ApplyGainToPcm16(null, 6.0));
        }

        [Fact]
        public void ApplyGainToPcm16_零dB_返回原数据副本()
        {
            byte[] input = new byte[] { 0x00, 0x40 }; // 16384
            byte[] result = GainService.ApplyGainToPcm16(input, 0.0);

            Assert.NotSame(input, result);
            Assert.Equal(input, result);
        }

        [Fact]
        public void ApplyGainToPcm16_正增益_振幅增大()
        {
            // 输入采样值 = 1000 (小端序: 0xE8, 0x03)
            byte[] input = new byte[] { 0xE8, 0x03 };
            byte[] result = GainService.ApplyGainToPcm16(input, 20.0); // 10x 增益

            short outputSample = (short)(result[0] | (result[1] << 8));
            Assert.Equal(10000, outputSample);
        }

        [Fact]
        public void ApplyGainToPcm16_负增益_振幅减小()
        {
            // 输入采样值 = 10000 (小端序)
            short inputVal = 10000;
            byte[] input = new byte[] { (byte)(inputVal & 0xFF), (byte)((inputVal >> 8) & 0xFF) };
            byte[] result = GainService.ApplyGainToPcm16(input, -20.0); // 0.1x 增益

            short outputSample = (short)(result[0] | (result[1] << 8));
            Assert.Equal(1000, outputSample);
        }

        [Fact]
        public void ApplyGainToPcm16_溢出保护_钳位到最大值()
        {
            short inputVal = 20000;
            byte[] input = new byte[] { (byte)(inputVal & 0xFF), (byte)((inputVal >> 8) & 0xFF) };
            byte[] result = GainService.ApplyGainToPcm16(input, 20.0); // 10x = 200000，超出 short.MaxValue

            short outputSample = (short)(result[0] | (result[1] << 8));
            Assert.Equal(short.MaxValue, outputSample);
        }

        [Fact]
        public void ApplyGainToPcm16_负值溢出保护_钳位到最小值()
        {
            short inputVal = -20000;
            byte[] input = new byte[] { (byte)(inputVal & 0xFF), (byte)((inputVal >> 8) & 0xFF) };
            byte[] result = GainService.ApplyGainToPcm16(input, 20.0); // 10x = -200000，超出 short.MinValue

            short outputSample = (short)(result[0] | (result[1] << 8));
            Assert.Equal(short.MinValue, outputSample);
        }

        [Fact]
        public void ApplyGainToPcm16_多采样处理正确()
        {
            // 两个采样：1000 和 -1000
            short val1 = 1000;
            short val2 = -1000;
            byte[] input = new byte[]
            {
                (byte)(val1 & 0xFF), (byte)((val1 >> 8) & 0xFF),
                (byte)(val2 & 0xFF), (byte)((val2 >> 8) & 0xFF)
            };

            byte[] result = GainService.ApplyGainToPcm16(input, 6.0); // ~2x
            double gain = GainService.DbToLinear(6.0);

            short out1 = (short)(result[0] | (result[1] << 8));
            short out2 = (short)(result[2] | (result[3] << 8));

            Assert.Equal((short)(1000 * gain), out1);
            Assert.Equal((short)(-1000 * gain), out2);
        }

        // ========== ApplyGainToFloat 测试 ==========

        [Fact]
        public void ApplyGainToFloat_null输入_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() => GainService.ApplyGainToFloat(null, 6.0));
        }

        [Fact]
        public void ApplyGainToFloat_零dB_返回等值数组()
        {
            float[] input = new float[] { 0.5f, -0.3f };
            float[] result = GainService.ApplyGainToFloat(input, 0.0);

            // 0dB 时 linear = 1.0，结果与输入相同
            Assert.Equal((double)input[0], (double)result[0], 4);
            Assert.Equal((double)input[1], (double)result[1], 4);
        }

        [Fact]
        public void ApplyGainToFloat_正增益_振幅增大()
        {
            float[] input = new float[] { 0.1f };
            float[] result = GainService.ApplyGainToFloat(input, 20.0); // 10x

            Assert.Equal(1.0, (double)result[0], 4); // 0.1 * 10 = 1.0，刚好不溢出
        }

        [Fact]
        public void ApplyGainToFloat_溢出钳位到1()
        {
            float[] input = new float[] { 0.5f };
            float[] result = GainService.ApplyGainToFloat(input, 20.0); // 0.5 * 10 = 5.0 → 钳位到 1.0

            Assert.Equal(1.0, (double)result[0], 4);
        }

        [Fact]
        public void ApplyGainToFloat_负溢出钳位到负1()
        {
            float[] input = new float[] { -0.5f };
            float[] result = GainService.ApplyGainToFloat(input, 20.0); // -0.5 * 10 = -5.0 → 钳位到 -1.0

            Assert.Equal(-1.0, (double)result[0], 4);
        }

        [Fact]
        public void ApplyGainToFloat_返回新数组()
        {
            float[] input = new float[] { 0.5f };
            float[] result = GainService.ApplyGainToFloat(input, 6.0);

            Assert.NotSame(input, result);
        }

        // ========== ApplyGainToFloatInPlace 测试 ==========

        [Fact]
        public void ApplyGainToFloatInPlace_null输入_不抛出异常()
        {
            GainService.ApplyGainToFloatInPlace(null, 6.0); // 不应抛异常
        }

        [Fact]
        public void ApplyGainToFloatInPlace_零dB_不修改()
        {
            float[] input = new float[] { 0.5f, -0.3f };
            float[] original = new float[] { 0.5f, -0.3f };
            GainService.ApplyGainToFloatInPlace(input, 0.0);

            Assert.Equal((double)original[0], (double)input[0], 4);
            Assert.Equal((double)original[1], (double)input[1], 4);
        }

        [Fact]
        public void ApplyGainToFloatInPlace_正增益_原地修改()
        {
            float[] input = new float[] { 0.1f };
            GainService.ApplyGainToFloatInPlace(input, 6.0);

            double expected = 0.1 * GainService.DbToLinear(6.0);
            Assert.Equal(expected, (double)input[0], 4);
        }

        [Fact]
        public void ApplyGainToFloatInPlace_溢出钳位()
        {
            float[] input = new float[] { 0.5f, -0.5f };
            GainService.ApplyGainToFloatInPlace(input, 20.0);

            Assert.Equal(1.0, (double)input[0], 4);
            Assert.Equal(-1.0, (double)input[1], 4);
        }
    }

    /// <summary>
    /// GainViewModel 单元测试
    /// </summary>
    public class GainViewModelTests
    {
        [Fact]
        public void 构造函数_初始状态正确()
        {
            var vm = new GainViewModel();

            Assert.Equal(0.0, vm.GainDb);
            Assert.Equal("0.0 dB", vm.GainText);
            Assert.Equal(1.0, vm.GainLinear, 6);
            Assert.Equal(1.0, (double)vm.PlaybackVolume, 4);
        }

        [Fact]
        public void 设置增益_属性正确更新()
        {
            var vm = new GainViewModel();
            vm.GainDb = 6.0;

            Assert.Equal(6.0, vm.GainDb);
            Assert.Equal("+6.0 dB", vm.GainText);
            Assert.True(vm.GainLinear > 1.0);
        }

        [Fact]
        public void 设置增益_自动对齐步进()
        {
            var vm = new GainViewModel();
            vm.GainDb = 5.3;

            Assert.Equal(5.5, vm.GainDb); // 对齐到 0.5 步进
        }

        [Fact]
        public void 设置增益_超过上限钳位()
        {
            var vm = new GainViewModel();
            vm.GainDb = 25.0;

            Assert.Equal(20.0, vm.GainDb);
        }

        [Fact]
        public void 设置增益_低于下限钳位()
        {
            var vm = new GainViewModel();
            vm.GainDb = -25.0;

            Assert.Equal(-20.0, vm.GainDb);
        }

        [Fact]
        public void 增益变化_触发事件()
        {
            var vm = new GainViewModel();
            bool eventFired = false;
            vm.GainChanged += (s, e) => { eventFired = true; };

            vm.GainDb = 6.0;

            Assert.True(eventFired);
        }

        [Fact]
        public void 增益不变_不触发事件()
        {
            var vm = new GainViewModel();
            bool eventFired = false;
            vm.GainChanged += (s, e) => { eventFired = true; };

            vm.GainDb = 0.0; // 初始就是 0

            Assert.False(eventFired);
        }

        [Fact]
        public void 属性变更通知_GainDb()
        {
            var vm = new GainViewModel();
            var changedProps = new List<string>();
            vm.PropertyChanged += (s, e) => { changedProps.Add(e.PropertyName); };

            vm.GainDb = 6.0;

            Assert.Contains("GainDb", changedProps);
            Assert.Contains("GainText", changedProps);
            Assert.Contains("GainLinear", changedProps);
            Assert.Contains("PlaybackVolume", changedProps);
        }

        [Fact]
        public void IncreaseGainCommand_增加0点5dB()
        {
            var vm = new GainViewModel();
            vm.IncreaseGainCommand.Execute(null);

            Assert.Equal(0.5, vm.GainDb);
        }

        [Fact]
        public void DecreaseGainCommand_减少0点5dB()
        {
            var vm = new GainViewModel();
            vm.DecreaseGainCommand.Execute(null);

            Assert.Equal(-0.5, vm.GainDb);
        }

        [Fact]
        public void ResetGainCommand_归零()
        {
            var vm = new GainViewModel();
            vm.GainDb = 10.0;

            vm.ResetGainCommand.Execute(null);

            Assert.Equal(0.0, vm.GainDb);
            Assert.Equal("0.0 dB", vm.GainText);
        }

        [Fact]
        public void IncreaseGainCommand_达到上限后不可执行()
        {
            var vm = new GainViewModel();
            vm.GainDb = 20.0;

            Assert.False(vm.IncreaseGainCommand.CanExecute(null));
        }

        [Fact]
        public void DecreaseGainCommand_达到下限后不可执行()
        {
            var vm = new GainViewModel();
            vm.GainDb = -20.0;

            Assert.False(vm.DecreaseGainCommand.CanExecute(null));
        }

        [Fact]
        public void ResetGainCommand_已归零时不可执行()
        {
            var vm = new GainViewModel();

            Assert.False(vm.ResetGainCommand.CanExecute(null));
        }

        [Fact]
        public void ResetGainCommand_不为零时可执行()
        {
            var vm = new GainViewModel();
            vm.GainDb = 5.0;

            Assert.True(vm.ResetGainCommand.CanExecute(null));
        }

        [Fact]
        public void PlaybackVolume_正增益_钳位到1()
        {
            var vm = new GainViewModel();
            vm.GainDb = 20.0; // 线性 10.0，但 PlaybackVolume 钳位到 1.0

            Assert.Equal(1.0, (double)vm.PlaybackVolume, 4);
        }

        [Fact]
        public void PlaybackVolume_负增益_反映衰减()
        {
            var vm = new GainViewModel();
            vm.GainDb = -20.0; // 线性 0.1

            Assert.Equal(0.1, (double)vm.PlaybackVolume, 2);
        }

        [Fact]
        public void ApplyGainToSamples_null输入_返回null()
        {
            var vm = new GainViewModel();
            vm.GainDb = 6.0;

            Assert.Null(vm.ApplyGainToSamples(null));
        }

        [Fact]
        public void ApplyGainToSamples_零dB_返回原引用()
        {
            var vm = new GainViewModel();
            var samples = new float[] { 0.5f };

            var result = vm.ApplyGainToSamples(samples);

            Assert.Same(samples, result); // 零 dB 返回原引用
        }

        [Fact]
        public void ApplyGainToSamples_非零dB_返回增益后数据()
        {
            var vm = new GainViewModel();
            vm.GainDb = 6.0;
            var samples = new float[] { 0.1f };

            var result = vm.ApplyGainToSamples(samples);

            Assert.NotSame(samples, result);
            Assert.True(result[0] > 0.1f);
        }

        [Fact]
        public void 连续增减_值正确累加()
        {
            var vm = new GainViewModel();

            // 增加 3 次 = +1.5dB
            vm.IncreaseGainCommand.Execute(null);
            vm.IncreaseGainCommand.Execute(null);
            vm.IncreaseGainCommand.Execute(null);

            Assert.Equal(1.5, vm.GainDb);

            // 减少 5 次 = -1.0dB
            vm.DecreaseGainCommand.Execute(null);
            vm.DecreaseGainCommand.Execute(null);
            vm.DecreaseGainCommand.Execute(null);
            vm.DecreaseGainCommand.Execute(null);
            vm.DecreaseGainCommand.Execute(null);

            Assert.Equal(-1.0, vm.GainDb);
        }

        [Fact]
        public void 负数增益_GainText显示正确()
        {
            var vm = new GainViewModel();
            vm.GainDb = -3.5;

            Assert.Equal("-3.5 dB", vm.GainText);
        }

        [Fact]
        public void 带RenderService构造_清除缓存触发()
        {
            // 使用 null PCM service 创建渲染服务会抛异常
            // 但验证构造函数不抛异常
            var vm = new GainViewModel(null);
            vm.GainDb = 6.0; // 无 renderService，不应抛异常

            Assert.Equal(6.0, vm.GainDb);
        }
    }
}
