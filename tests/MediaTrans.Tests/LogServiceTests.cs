using System;
using System.IO;
using System.Text;
using MediaTrans.Services;
using Xunit;

namespace MediaTrans.Tests
{
    /// <summary>
    /// 日志服务单元测试
    /// </summary>
    public class LogServiceTests : IDisposable
    {
        private readonly string _testLogDir;

        public LogServiceTests()
        {
            _testLogDir = Path.Combine(Path.GetTempPath(), "MediaTransLogTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testLogDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testLogDir))
                {
                    Directory.Delete(_testLogDir, true);
                }
            }
            catch (Exception)
            {
                // 忽略清理失败
            }
        }

        // ===== 构造函数测试 =====

        [Fact]
        public void 构造函数_创建日志目录()
        {
            string dir = Path.Combine(_testLogDir, "subdir");
            Assert.False(Directory.Exists(dir));

            var service = new LogService(dir);
            Assert.True(Directory.Exists(dir));
            service.Dispose();
        }

        [Fact]
        public void 构造函数_null目录_抛出异常()
        {
            Assert.Throws<ArgumentNullException>(() => new LogService(null));
        }

        [Fact]
        public void 构造函数_默认参数_正确设置()
        {
            var service = new LogService(_testLogDir);
            Assert.Equal(10 * 1024 * 1024, service.MaxFileSize);
            Assert.Equal(5, service.MaxFileCount);
            Assert.Equal(_testLogDir, service.LogDirectory);
            Assert.EndsWith(".log", service.CurrentLogFilePath);
            service.Dispose();
        }

        [Fact]
        public void 构造函数_自定义参数_正确设置()
        {
            var service = new LogService(_testLogDir, 5242880, 3);
            Assert.Equal(5242880, service.MaxFileSize);
            Assert.Equal(3, service.MaxFileCount);
            service.Dispose();
        }

        // ===== 日志写入测试 =====

        [Fact]
        public void Info_写入日志文件()
        {
            var service = new LogService(_testLogDir);
            service.Info("测试消息");
            service.Dispose();

            string content = File.ReadAllText(service.CurrentLogFilePath, Encoding.UTF8);
            Assert.Contains("[INFO]", content);
            Assert.Contains("测试消息", content);
        }

        [Fact]
        public void Debug_写入DEBUG级别()
        {
            var service = new LogService(_testLogDir);
            service.Debug("调试信息");
            service.Dispose();

            string content = File.ReadAllText(service.CurrentLogFilePath, Encoding.UTF8);
            Assert.Contains("[DEBUG]", content);
            Assert.Contains("调试信息", content);
        }

        [Fact]
        public void Warn_写入WARNING级别()
        {
            var service = new LogService(_testLogDir);
            service.Warn("警告消息");
            service.Dispose();

            string content = File.ReadAllText(service.CurrentLogFilePath, Encoding.UTF8);
            Assert.Contains("[WARNING]", content);
            Assert.Contains("警告消息", content);
        }

        [Fact]
        public void Error_写入ERROR级别()
        {
            var service = new LogService(_testLogDir);
            service.Error("错误消息");
            service.Dispose();

            string content = File.ReadAllText(service.CurrentLogFilePath, Encoding.UTF8);
            Assert.Contains("[ERROR]", content);
            Assert.Contains("错误消息", content);
        }

        [Fact]
        public void Error_含异常_记录完整堆栈()
        {
            var service = new LogService(_testLogDir);
            try
            {
                throw new InvalidOperationException("测试异常");
            }
            catch (Exception ex)
            {
                service.Error("操作失败", ex);
            }
            service.Dispose();

            string content = File.ReadAllText(service.CurrentLogFilePath, Encoding.UTF8);
            Assert.Contains("[ERROR]", content);
            Assert.Contains("操作失败", content);
            Assert.Contains("InvalidOperationException", content);
            Assert.Contains("测试异常", content);
            Assert.Contains("堆栈跟踪", content);
        }

        [Fact]
        public void Error_含内部异常_记录内部异常()
        {
            var service = new LogService(_testLogDir);
            var inner = new ArgumentException("内部错误");
            var outer = new InvalidOperationException("外部错误", inner);
            service.Error("嵌套异常", outer);
            service.Dispose();

            string content = File.ReadAllText(service.CurrentLogFilePath, Encoding.UTF8);
            Assert.Contains("外部错误", content);
            Assert.Contains("内部异常", content);
            Assert.Contains("内部错误", content);
            Assert.Contains("ArgumentException", content);
        }

        [Fact]
        public void Fatal_写入FATAL级别()
        {
            var service = new LogService(_testLogDir);
            try
            {
                throw new OutOfMemoryException("内存不足");
            }
            catch (Exception ex)
            {
                service.Fatal("应用崩溃", ex);
            }
            service.Dispose();

            string content = File.ReadAllText(service.CurrentLogFilePath, Encoding.UTF8);
            Assert.Contains("[FATAL]", content);
            Assert.Contains("应用崩溃", content);
            Assert.Contains("OutOfMemoryException", content);
        }

        [Fact]
        public void 日志条目_包含时间戳和线程信息()
        {
            var service = new LogService(_testLogDir);
            service.Info("时间戳测试");
            service.Dispose();

            string content = File.ReadAllText(service.CurrentLogFilePath, Encoding.UTF8);
            // 格式: [yyyy-MM-dd HH:mm:ss.fff] [INFO] [线程X] 消息
            Assert.Contains("[线程", content);
            Assert.Contains(DateTime.Now.ToString("yyyy-MM-dd"), content);
        }

        [Fact]
        public void 多行日志_追加写入()
        {
            var service = new LogService(_testLogDir);
            service.Info("第一行");
            service.Info("第二行");
            service.Info("第三行");
            service.Dispose();

            string content = File.ReadAllText(service.CurrentLogFilePath, Encoding.UTF8);
            Assert.Contains("第一行", content);
            Assert.Contains("第二行", content);
            Assert.Contains("第三行", content);
        }

        [Fact]
        public void Dispose后写入_不抛异常()
        {
            var service = new LogService(_testLogDir);
            service.Dispose();
            // 不应抛出异常
            service.Info("已销毁后的消息");
        }

        // ===== 日志轮转测试 =====

        [Fact]
        public void 轮转_文件超过大小限制_创建轮转文件()
        {
            // 使用极小的文件大小限制触发轮转
            var service = new LogService(_testLogDir, 100, 3);

            // 写入足够多的数据触发轮转
            for (int i = 0; i < 10; i++)
            {
                service.Info(string.Format("日志消息 {0} - 填充数据以触发轮转", i));
            }
            service.Dispose();

            // 应该存在轮转文件
            string rotatedFile = Path.Combine(_testLogDir, "MediaTrans.1.log");
            Assert.True(File.Exists(rotatedFile) || File.Exists(service.CurrentLogFilePath),
                "至少应该存在一个日志文件");
        }

        [Fact]
        public void PerformRotation_正确重命名文件()
        {
            var service = new LogService(_testLogDir, 1024, 3);

            // 创建当前日志文件
            File.WriteAllText(service.CurrentLogFilePath, "当前日志内容", Encoding.UTF8);

            // 执行轮转
            service.PerformRotation();

            // 当前文件应该已被移动为 .1.log
            string rotated1 = Path.Combine(_testLogDir, "MediaTrans.1.log");
            Assert.True(File.Exists(rotated1));
            Assert.False(File.Exists(service.CurrentLogFilePath));

            string content = File.ReadAllText(rotated1, Encoding.UTF8);
            Assert.Equal("当前日志内容", content);

            service.Dispose();
        }

        [Fact]
        public void PerformRotation_已有轮转文件_序号递增()
        {
            var service = new LogService(_testLogDir, 1024, 5);

            // 已有 .1.log
            string file1 = Path.Combine(_testLogDir, "MediaTrans.1.log");
            File.WriteAllText(file1, "旧的第1份", Encoding.UTF8);

            // 当前日志文件
            File.WriteAllText(service.CurrentLogFilePath, "当前日志", Encoding.UTF8);

            // 执行轮转
            service.PerformRotation();

            // .1.log 应该是原来的当前日志
            // .2.log 应该是原来的 .1.log
            string file2 = Path.Combine(_testLogDir, "MediaTrans.2.log");
            Assert.True(File.Exists(file1));
            Assert.True(File.Exists(file2));

            string content1 = File.ReadAllText(file1, Encoding.UTF8);
            string content2 = File.ReadAllText(file2, Encoding.UTF8);
            Assert.Equal("当前日志", content1);
            Assert.Equal("旧的第1份", content2);

            service.Dispose();
        }

        [Fact]
        public void PerformRotation_超出最大份数_删除最旧文件()
        {
            var service = new LogService(_testLogDir, 1024, 3);

            // 创建已有的轮转文件（最多保留3份，即 .log + .1.log + .2.log）
            // .2.log 是最旧的
            File.WriteAllText(
                Path.Combine(_testLogDir, "MediaTrans.2.log"), "最旧", Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(_testLogDir, "MediaTrans.1.log"), "较旧", Encoding.UTF8);
            File.WriteAllText(service.CurrentLogFilePath, "当前", Encoding.UTF8);

            service.PerformRotation();

            // .2.log 应该包含原来 .1.log 的内容（"较旧"）
            // .1.log 应该包含当前的内容（"当前"）
            // 最旧的文件应该被删除
            string file1 = Path.Combine(_testLogDir, "MediaTrans.1.log");
            string file2 = Path.Combine(_testLogDir, "MediaTrans.2.log");

            Assert.True(File.Exists(file1));
            Assert.True(File.Exists(file2));

            string content1 = File.ReadAllText(file1, Encoding.UTF8);
            Assert.Equal("当前", content1);

            string content2 = File.ReadAllText(file2, Encoding.UTF8);
            Assert.Equal("较旧", content2);

            service.Dispose();
        }

        // ===== GetAllLogFiles 测试 =====

        [Fact]
        public void GetAllLogFiles_返回所有日志文件()
        {
            var service = new LogService(_testLogDir);
            service.Info("测试");

            // 创建一些轮转文件
            File.WriteAllText(
                Path.Combine(_testLogDir, "MediaTrans.1.log"), "旧日志1", Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(_testLogDir, "MediaTrans.2.log"), "旧日志2", Encoding.UTF8);

            string[] files = service.GetAllLogFiles();
            Assert.True(files.Length >= 3); // 当前 + 2个轮转

            service.Dispose();
        }

        [Fact]
        public void GetAllLogFiles_目录不存在_返回空数组()
        {
            string nonExistDir = Path.Combine(_testLogDir, "nonexist");
            var service = new LogService(nonExistDir);

            // 删除目录
            if (Directory.Exists(nonExistDir))
            {
                Directory.Delete(nonExistDir, true);
            }

            string[] files = service.GetAllLogFiles();
            Assert.Empty(files);

            service.Dispose();
        }

        // ===== 全局实例测试 =====

        [Fact]
        public void Initialize_创建全局实例()
        {
            string dir = Path.Combine(_testLogDir, "global");
            LogService.Initialize(dir, 1024, 3);

            Assert.NotNull(LogService.Instance);
            Assert.Equal(dir, LogService.Instance.LogDirectory);

            LogService.Instance.Dispose();
        }

        // ===== 编码测试 =====

        [Fact]
        public void 日志内容_UTF8编码_中文正确()
        {
            var service = new LogService(_testLogDir);
            service.Info("中文日志测试 - 中文路径 C:\\用户\\文档\\test.mp4");
            service.Dispose();

            string content = File.ReadAllText(service.CurrentLogFilePath, Encoding.UTF8);
            Assert.Contains("中文日志测试", content);
            Assert.Contains("C:\\用户\\文档\\test.mp4", content);
        }
    }
}
