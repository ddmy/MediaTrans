using System;
using Xunit;
using MediaTrans.ViewModels;

namespace MediaTrans.Tests
{
    /// <summary>
    /// ViewModelBase 单元测试
    /// </summary>
    public class ViewModelBaseTests
    {
        /// <summary>
        /// 用于测试的具体 ViewModel 子类
        /// </summary>
        private class TestViewModel : ViewModelBase
        {
            private string _name;
            private int _value;

            public string Name
            {
                get { return _name; }
                set { SetProperty(ref _name, value, "Name"); }
            }

            public int Value
            {
                get { return _value; }
                set { SetProperty(ref _value, value, "Value"); }
            }
        }

        [Fact]
        public void SetProperty_值变更时_触发PropertyChanged()
        {
            // 准备
            var vm = new TestViewModel();
            string changedProperty = null;
            vm.PropertyChanged += (s, e) => { changedProperty = e.PropertyName; };

            // 执行
            vm.Name = "测试";

            // 验证
            Assert.Equal("Name", changedProperty);
            Assert.Equal("测试", vm.Name);
        }

        [Fact]
        public void SetProperty_值相同时_不触发PropertyChanged()
        {
            // 准备
            var vm = new TestViewModel();
            vm.Name = "测试";
            bool fired = false;
            vm.PropertyChanged += (s, e) => { fired = true; };

            // 执行：设置相同的值
            vm.Name = "测试";

            // 验证
            Assert.False(fired, "值相同时不应触发事件");
        }

        [Fact]
        public void SetProperty_多次变更_每次都触发()
        {
            // 准备
            var vm = new TestViewModel();
            int count = 0;
            vm.PropertyChanged += (s, e) => { count++; };

            // 执行
            vm.Name = "值1";
            vm.Name = "值2";
            vm.Value = 42;

            // 验证
            Assert.Equal(3, count);
        }
    }
}
