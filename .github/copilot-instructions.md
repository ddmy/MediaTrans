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

### C5 — 编译自测
每个 Task 完成后必须：
1. `MSBuild /t:Rebuild` 编译通过（零 Error，零 Warning 尽量）
2. 运行对应的 xUnit 单元测试全部通过
3. 确认后方可进入下一个 Task

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

## Task 工作流

> **必读**：在开始任何 Task 之前，你 **必须** 先读取 `doc/tasks.md` 获取该 Task 的描述、验收标准、前置依赖和当前进度状态。

开发按 `doc/tasks.md` 中定义的 42 个 Task 顺序推进。每个 Task 必须：

1. **开始前**：读取 `doc/tasks.md`，确认前置依赖 Task 全部为 ✅
2. **开发中**：严格遵守 8 条工程约束
3. **完成后**：
   - 编译通过（零 Error）
   - 对应单元测试通过
   - 更新 `doc/tasks.md` 中对应 Task 行的状态（⬜→✅）、编译列和测试列
   - 更新 `doc/tasks.md` 顶部总进度表
   - 提交 commit，message 格式：`feat(1.5): FFmpeg 集成层` 或 `test(1.14): 里程碑 1 测试`

## Commit Message 格式

```
<type>(<task>): <简短描述>

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
feat(1.5): FFmpeg 集成层 - 进程封装与 Job Object 绑定
test(1.14): FFmpeg 指令生成器单元测试 + 编码兼容性测试
fix(2.2): 修复波形图快速缩放时闪烁问题
```

## 参考文档

> **重要**：以下文档必须在执行任何 Task 前阅读。`copilot-instructions.md` 会自动加载，但 `doc/` 下的文件需要你主动读取。

- 需求文档：`doc/需求.md`
- Task 列表 + 进度跟踪（含决策记录、验收标准、状态追踪）：`doc/tasks.md`

## .github 目录结构与用途

```
.github/
├── copilot-instructions.md      # [自动加载] 全局指令，每次对话自动注入
├── AGENTS.md                    # Agent 角色定义（developer/reviewer/tester/planner）
└── prompts/                     # Prompt 命令（可通过 @workspace /命令名 调用）
    ├── implement-task.prompt.md  # 实现 Task 的 7 步标准流程
    ├── review-code.prompt.md     # 代码审查检查清单（语法/约束/安全/MVVM）
    ├── write-tests.prompt.md     # xUnit 测试编写规范与示例
    ├── plan-next.prompt.md       # 依赖分析，找出当前可执行的 Task
    └── fix-build.prompt.md       # 编译/测试失败的修复指南 + C# 5.0 速查
```

### 各文件加载机制

| 文件 | 加载方式 | 何时使用 |
|------|---------|---------|
| `copilot-instructions.md` | **自动**：每次对话自动注入上下文 | 始终生效，无需手动操作 |
| `AGENTS.md` | **自动**：VS Code Copilot 识别 Agent 角色 | 以特定角色（developer/reviewer 等）工作时 |
| `prompts/*.prompt.md` | **按需**：作为 Prompt 命令调用 | 执行特定工作流时（实现/审查/测试/规划/修复） |
| `doc/tasks.md` | **手动读取**：需主动 `read_file` | **每个 Task 开始前必读** |
| `doc/需求.md` | **手动读取**：需主动 `read_file` | 需要确认产品需求细节时 |

### AI 工作规则

1. **开始任何 Task 前**：必须先读取 `doc/tasks.md`，获取 Task 描述、验收标准、前置依赖和当前进度
2. **实现 Task 时**：按照 `prompts/implement-task.prompt.md` 的 7 步流程执行
3. **完成 Task 后**：更新 `doc/tasks.md` 中的状态行和顶部进度表
4. **代码审查时**：按照 `prompts/review-code.prompt.md` 的检查清单逐项核查
5. **编写测试时**：按照 `prompts/write-tests.prompt.md` 的规范编写
6. **规划下一步时**：按照 `prompts/plan-next.prompt.md` 分析依赖关系
7. **编译/测试失败时**：按照 `prompts/fix-build.prompt.md` 的流程定位修复
