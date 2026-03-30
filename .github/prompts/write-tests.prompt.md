---
mode: "agent"
description: "为 MediaTrans 指定模块编写 xUnit 单元测试"
tools: ["run_in_terminal", "read_file", "create_file", "replace_string_in_file", "grep_search", "file_search", "get_errors"]
---

# 编写单元测试

你是 MediaTrans 项目的测试工程师。请为指定模块编写全面的 xUnit 单元测试。

## 测试项目

测试代码放在 `tests/MediaTrans.Tests/` 项目中（xUnit，.NET Framework 4.5.2）。

## 测试规范

### 命名约定
- 测试类：`<被测类名>Tests`（如 `FFmpegCommandBuilderTests`）
- 测试方法：`<方法名>_<场景>_<预期结果>`（如 `Build_WithVideoCodec_IncludesExplicitCodecParam`）

### 必须覆盖的场景

#### FFmpeg 相关
- 每条生成的 FFmpeg 命令 **必须断言包含** `-c:v` 和/或 `-c:a`
- 路径含空格的测试用例（如 `C:\My Folder\测试 文件.mp4`）
- 路径含中文的测试用例
- 超长路径测试（>260 字符）
- 各格式转换的编解码器映射正确性

#### 配置相关
- `AppConfig.json` 不存在时自动生成默认值
- 配置读取/写入正确性
- 配置值类型校验

#### 授权相关
- 正确激活码验证通过
- 错误激活码验证失败
- 篡改后的激活码验证失败
- 空值/超长值边界测试
- 机器码生成一致性（同参数多次调用一致）

#### 编辑器相关
- Undo/Redo 栈操作正确性
- 选区计算精度
- 磁吸算法吸附阈值
- 增益 dB 到线性值的转换精度
- LRU 缓存淘汰正确性

### C# 5.0 语法要求

测试代码同样必须使用 C# 5.0 语法。

```csharp
// ✅ 正确
[Fact]
public void Build_WithVideoCodec_IncludesCodecParam()
{
    var builder = new FFmpegCommandBuilder();
    string cmd = builder
        .Input("input.mp4")
        .VideoCodec("libx264")
        .AudioCodec("aac")
        .Output("output.mp4")
        .Build();

    Assert.Contains("-c:v libx264", cmd);
    Assert.Contains("-c:a aac", cmd);
}

// ✅ 路径含空格测试
[Fact]
public void Build_PathWithSpaces_WrappedInQuotes()
{
    var builder = new FFmpegCommandBuilder();
    string cmd = builder
        .Input("C:\\My Folder\\input file.mp4")
        .VideoCodec("libx264")
        .AudioCodec("aac")
        .Output("C:\\Output Dir\\output file.mp4")
        .Build();

    Assert.Contains("\"C:\\My Folder\\input file.mp4\"", cmd);
    Assert.Contains("\"C:\\Output Dir\\output file.mp4\"", cmd);
}
```

### 测试素材

编码兼容性测试素材通过 FFmpeg 脚本自动生成（不依赖外部素材文件）：

```bash
# 生成各编码格式测试素材
ffmpeg -f lavfi -i testsrc=duration=3:size=320x240:rate=25 -c:v libx264 test_h264.mp4
ffmpeg -f lavfi -i sine=frequency=440:duration=3 -c:a aac test_aac.m4a
ffmpeg -f lavfi -i sine=frequency=440:duration=3 -c:a libmp3lame test_mp3.mp3
```

## 输出

1. 创建/更新测试类文件
2. 确保 `MediaTrans.Tests.csproj` 中包含新文件引用
3. 运行测试并确认通过
4. 报告测试覆盖情况
