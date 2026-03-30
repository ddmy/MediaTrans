# MediaTrans 专业版 — Copilot 全局指令

## 项目概述

MediaTrans 是一款 Windows 桌面端音视频处理工具，基于 C# WPF + .NET Framework 4.5.2 开发，内置 FFmpeg 引擎，支持格式转换、交互式波形编辑、RSA 离线授权。目标兼容 Windows 7 SP1 及以上系统。

## 解决方案结构

```
MediaTrans/
├── MediaTrans.sln
├── src/MediaTrans/              # WPF 主项目 (WinExe, .NET Framework 4.5.2)
│   ├── Models/                  # 数据模型（AppConfig, MediaFileInfo 等）
│   ├── ViewModels/              # MVVM ViewModel（继承 ViewModelBase）
│   ├── Views/                   # XAML 视图
│   ├── Services/                # 业务服务（FFmpegService, ConfigService, LicenseService 等）
│   ├── Converters/              # WPF IValueConverter 实现
│   ├── Commands/                # ICommand 实现（RelayCommand）
│   ├── Config/AppConfig.json    # 统一配置文件
│   ├── Assets/                  # 图标、字体等资源
│   └── Properties/
├── tests/MediaTrans.Tests/      # xUnit 单元测试
├── tools/KeyGenerator/          # RSA 密钥对生成（独立控制台，仅开发者使用）
├── tools/LicenseIssuer/         # 激活码签发（独立控制台，仅开发者使用）
└── lib/ffmpeg/                  # FFmpeg 静态编译版（内置分发）
```

## 技术栈约束

| 项目 | 版本/规范 |
|------|----------|
| 语言 | C# 5.0（兼容 .NET 4.5.2，不可使用 C# 6+ 语法如 `?.`、`nameof`、字符串内插 `$""`） |
| 框架 | .NET Framework 4.5.2（不可使用 .NET Core/5/6/7/8） |
| UI | WPF，MVVM 模式（无第三方 MVVM 框架，手写 ViewModelBase + RelayCommand） |
| 绘图 | SkiaSharp ≤2.80.x（更高版本不兼容 .NET 4.5.2） |
| 音频 | NAudio（WaveOut 播放） |
| 测试 | xUnit + FlaUI（UI 自动化） |
| JSON | 手写轻量 JSON 序列化或 Newtonsoft.Json（无 System.Text.Json） |
| 安装包 | Inno Setup |
| 混淆 | ConfuserEx |

### C# 语法限制（重要）

由于目标是 .NET Framework 4.5.2，**必须使用 C# 5.0 语法**。以下语法**禁止使用**：

```csharp
// ❌ 禁止：C# 6.0+ 语法
var name = obj?.Property;              // Null 条件运算符
var text = $"Hello {name}";            // 字符串内插
nameof(variable);                       // nameof 表达式
public int Prop { get; } = 5;          // 自动属性初始化器
using static System.Math;              // using static
public void Method() => DoSomething(); // 表达式体成员
Dictionary<string, int> d = new() {};  // 目标类型 new

// ✅ 正确：C# 5.0 语法
var name = obj != null ? obj.Property : null;
var text = string.Format("Hello {0}", name);
// 用字符串常量代替 nameof
public int Prop { get; set; }          // 构造函数中赋值
using System;                          // 普通 using
public void Method() { DoSomething(); }
var d = new Dictionary<string, int>(); 
```

## 工程约束（8 条铁律，每次编码/审查必须遵守）

### C1 — 零外部依赖安装
FFmpeg、SkiaSharp、NAudio 全部内置在项目中。用户机器上不需要额外安装任何运行时或工具。FFmpeg 以静态编译版放在 `lib/ffmpeg/` 目录。

### C2 — 路径安全
所有文件路径操作必须处理空格和中文字符。FFmpeg 命令参数中的路径必须用双引号包裹：
```csharp
// ✅ 正确
string args = string.Format("-i \"{0}\" \"{1}\"", inputPath, outputPath);
// ❌ 错误
string args = string.Format("-i {0} {1}", inputPath, outputPath);
```

### C3 — 进程安全
FFmpeg 子进程必须绑定到 Windows Job Object。主进程退出时自动终止所有子进程，防止 ffmpeg.exe 残留。

### C4 — UI 响应性
所有耗时操作（>50ms）必须在后台线程执行（`Task.Run` / `ThreadPool`），通过 `Dispatcher.Invoke`/`BeginInvoke` 回调 UI。长操作必须提供取消按钮。禁止在 UI 线程执行 FFmpeg 调用、文件 I/O、网络请求。

### C5 — 编译自测（核心纪律）
**每次编码完成后，必须进行充分自测，确认无问题后方可结束。** 具体要求：
1. `MSBuild /t:Rebuild` 编译通过（零 Error，零 Warning 尽量）
2. 运行 xUnit 单元测试全部通过
3. 如果编译或测试失败，**必须立即排查原因并修复**，循环执行直到全部通过
4. **禁止跳过自测环节**，禁止在测试未通过时结束工作

### C6 — 编码安全
文件读写统一使用 UTF-8 编码：
```csharp
File.WriteAllText(path, content, Encoding.UTF8);
File.ReadAllText(path, Encoding.UTF8);
```

### C7 — FFmpeg 显式编解码器
所有 FFmpeg 命令必须通过 `-c:v` / `-c:a` 显式指定编解码器，绝不依赖系统默认行为：
```
# ✅ 正确
ffmpeg -i input.avi -c:v libx264 -c:a aac output.mp4
# ❌ 错误
ffmpeg -i input.avi output.mp4
```

### C8 — 统一配置文件
所有可配置项收拢到 `Config/AppConfig.json`，通过 `ConfigService` 读写，禁止硬编码魔法值。包括：FFmpeg 路径、默认输出目录、转换预设参数、帧缓存大小、波形分块宽度、LRU 缓存帧数、吸附阈值、Undo 栈深度、日志轮转参数、水印文字/位置等。

## 编码规范

### 命名
- 类/方法：PascalCase（`ConfigService`, `LoadConfig`）
- 私有字段：`_camelCase`（`_configPath`）
- 局部变量/参数：camelCase（`inputPath`）
- 常量：PascalCase（`MaxUndoDepth`）
- 接口：`I` 前缀（`IFFmpegService`）

### MVVM 模式
- View（XAML）不包含业务逻辑，仅绑定
- ViewModel 继承 `ViewModelBase`（实现 `INotifyPropertyChanged`）
- 命令使用 `RelayCommand`（实现 `ICommand`）
- 服务通过构造函数注入（手动 DI，无容器）

### 异步模式
```csharp
// 后台执行耗时操作
Task.Run(() =>
{
    // 耗时操作...
    var result = ProcessFile(inputPath);
    
    // 回调 UI 线程
    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
    {
        StatusText = "完成";
        ProgressValue = 100;
    }));
});
```

### FFmpeg 命令构建
```csharp
// 通过 FFmpegCommandBuilder 构建命令，确保：
// 1. 路径双引号包裹
// 2. 显式指定 -c:v / -c:a
// 3. 编解码器映射从配置读取
var cmd = new FFmpegCommandBuilder()
    .Input(inputPath)          // 自动加双引号
    .VideoCodec("libx264")     // -c:v libx264
    .AudioCodec("aac")         // -c:a aac
    .Output(outputPath)        // 自动加双引号
    .Build();
```

## 安全红线

1. **客户端代码中绝不包含 RSA 私钥**。私钥仅存在于 `tools/KeyGenerator` 和 `tools/LicenseIssuer` 项目中
2. 公钥作为嵌入资源编译到客户端
3. 不信任用户输入：文件路径、激活码等需校验后再使用
4. 不在日志中输出敏感信息（激活码、机器码完整值等）

## 自测纪律（最重要的工作习惯）

> **铁律**：每次完成编码后，**必须进行充分自测**，确认编译通过且测试全部通过后，才可以认为工作完成。如果发现问题，**必须排查原因并修复，然后重新自测**，循环执行直到全部通过。

### 自测流程

1. **编译验证**：`MSBuild /t:Rebuild` 零 Error
2. **运行单元测试**：全部 xUnit 测试通过
3. **失败处理**：如编译或测试失败 → 分析错误原因 → 修复代码 → 重新执行步骤 1-2
4. **通过确认**：编译 + 测试全部通过后方可结束

```
# 编译
MSBuild MediaTrans.sln /t:Rebuild /p:Configuration=Debug

# 测试
packages\xunit.runner.console\tools\net452\xunit.console.exe tests\MediaTrans.Tests\bin\Debug\MediaTrans.Tests.dll
```

**禁止行为**：
- ❌ 编码后不编译不测试就结束
- ❌ 测试失败时跳过或忽略
- ❌ 仅编译通过但不跑测试

## Commit Message 格式

```
<type>(<scope>): <简短描述>

<可选详细说明>
```

类型：
- `feat` — 新功能
- `fix` — 修复
- `test` — 测试
- `refactor` — 重构
- `docs` — 文档
- `chore` — 构建/工具

示例：
```
feat(converter): 新增 MKV 格式支持
fix(waveform): 修复波形图快速缩放时闪烁问题
test(license): 补充授权模块边界测试
```

## 参考文档

- 需求文档：`doc/需求.md` — 需要确认产品需求细节时主动读取

## .github 目录结构与用途

```
.github/
├── copilot-instructions.md      # [自动加载] 全局指令，每次对话自动注入
├── AGENTS.md                    # Agent 角色定义（developer/reviewer/tester）
└── prompts/                     # Prompt 命令（可通过 @workspace /命令名 调用）
    ├── review-code.prompt.md     # 代码审查检查清单（语法/约束/安全/MVVM）
    ├── write-tests.prompt.md     # xUnit 测试编写规范与示例
    └── fix-build.prompt.md       # 编译/测试失败的修复指南 + C# 5.0 速查
```

### 各文件加载机制

| 文件 | 加载方式 | 何时使用 |
|------|---------|---------|
| `copilot-instructions.md` | **自动**：每次对话自动注入上下文 | 始终生效，无需手动操作 |
| `AGENTS.md` | **自动**：VS Code Copilot 识别 Agent 角色 | 以特定角色（developer/reviewer 等）工作时 |
| `prompts/*.prompt.md` | **按需**：作为 Prompt 命令调用 | 执行特定工作流时（审查/测试/修复） |
| `doc/需求.md` | **手动读取**：需主动 `read_file` | 需要确认产品需求细节时 |

### AI 工作规则

1. **每次编码完成后**：必须执行完整自测流程（编译 + 测试），失败则修复后重新自测，直到全部通过
2. **代码审查时**：按照 `prompts/review-code.prompt.md` 的检查清单逐项核查
3. **编写测试时**：按照 `prompts/write-tests.prompt.md` 的规范编写
4. **编译/测试失败时**：按照 `prompts/fix-build.prompt.md` 的流程定位修复
5. **开发中**：严格遵守 8 条工程约束（特别注意 C5 自测纪律）
