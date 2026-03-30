using System;
using Xunit;
using MediaTrans.Commands;

namespace MediaTrans.Tests
{
    /// <summary>
    /// RelayCommand 单元测试
    /// </summary>
    public class RelayCommandTests
    {
        [Fact]
        public void Execute_执行委托被调用()
        {
            // 准备
            object received = null;
            var cmd = new RelayCommand(p => { received = p; });

            // 执行
            cmd.Execute("参数值");

            // 验证
            Assert.Equal("参数值", received);
        }

        [Fact]
        public void CanExecute_无判断委托时_返回true()
        {
            // 准备
            var cmd = new RelayCommand(p => { });

            // 验证
            Assert.True(cmd.CanExecute(null));
        }

        [Fact]
        public void CanExecute_有判断委托时_返回委托结果()
        {
            // 准备
            var cmd = new RelayCommand(p => { }, p => (int)p > 0);

            // 验证
            Assert.True(cmd.CanExecute(1));
            Assert.False(cmd.CanExecute(0));
            Assert.False(cmd.CanExecute(-1));
        }

        [Fact]
        public void 构造函数_execute为null_抛出异常()
        {
            // 验证
            Assert.Throws<ArgumentNullException>(() => new RelayCommand(null));
        }
    }
}
