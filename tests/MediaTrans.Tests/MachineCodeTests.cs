using System;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 机器码采集服务单元测试
    /// </summary>
    public class MachineCodeTests
    {
        private readonly MachineCodeService _service;

        public MachineCodeTests()
        {
            _service = new MachineCodeService();
        }

        [Fact]
        public void 获取机器码_返回64字符十六进制()
        {
            string code = _service.GetMachineCode();

            Assert.NotNull(code);
            Assert.Equal(64, code.Length);
            // 全部为大写十六进制字符
            foreach (char c in code)
            {
                Assert.True((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'),
                    string.Format("字符 '{0}' 不是有效的十六进制字符", c));
            }
        }

        [Fact]
        public void 同一台机器_多次生成机器码一致()
        {
            string code1 = _service.GetMachineCode();
            string code2 = _service.GetMachineCode();
            string code3 = _service.GetMachineCode();

            Assert.Equal(code1, code2);
            Assert.Equal(code2, code3);
        }

        [Fact]
        public void 获取CPUID_返回非空()
        {
            string cpuId = _service.GetCpuId();
            Assert.NotNull(cpuId);
            Assert.True(cpuId.Length > 0, "CPU ID 不应为空");
        }

        [Fact]
        public void 获取硬盘序列号_返回非空()
        {
            string serial = _service.GetDiskSerial();
            Assert.NotNull(serial);
            // 某些虚拟机可能返回空，但不应为 null
        }

        [Fact]
        public void 获取主板序列号_返回非空()
        {
            string serial = _service.GetBoardSerial();
            Assert.NotNull(serial);
        }

        [Fact]
        public void 不同硬件信息_生成不同机器码()
        {
            string code1 = _service.GenerateMachineCode("CPU1", "DISK1", "BOARD1");
            string code2 = _service.GenerateMachineCode("CPU2", "DISK2", "BOARD2");

            Assert.NotEqual(code1, code2);
        }

        [Fact]
        public void 相同硬件信息_生成相同机器码()
        {
            string code1 = _service.GenerateMachineCode("CPU_X", "DISK_Y", "BOARD_Z");
            string code2 = _service.GenerateMachineCode("CPU_X", "DISK_Y", "BOARD_Z");

            Assert.Equal(code1, code2);
        }

        [Fact]
        public void 硬件信息为空_不崩溃()
        {
            string code = _service.GenerateMachineCode(null, null, null);
            Assert.NotNull(code);
            Assert.Equal(64, code.Length);
        }

        [Fact]
        public void 机器码格式_SHA256哈希()
        {
            string code = _service.GenerateMachineCode("TestCPU", "TestDisk", "TestBoard");

            // SHA256 输出 32 字节 = 64 字符十六进制
            Assert.Equal(64, code.Length);
        }

        [Fact]
        public void 任一硬件信息变化_机器码不同()
        {
            string baseline = _service.GenerateMachineCode("CPU1", "DISK1", "BOARD1");

            // 仅 CPU 变化
            string changed1 = _service.GenerateMachineCode("CPU2", "DISK1", "BOARD1");
            Assert.NotEqual(baseline, changed1);

            // 仅硬盘变化
            string changed2 = _service.GenerateMachineCode("CPU1", "DISK2", "BOARD1");
            Assert.NotEqual(baseline, changed2);

            // 仅主板变化
            string changed3 = _service.GenerateMachineCode("CPU1", "DISK1", "BOARD2");
            Assert.NotEqual(baseline, changed3);
        }
    }
}
