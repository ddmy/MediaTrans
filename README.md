# MediaTrans 专业版

> 一款运行在 Windows 7+ 的现代化音视频处理工具，内置 FFmpeg 引擎，支持格式转换、波形编辑、裁剪拼接与离线 RSA 授权。

---

## 目录

- [功能概览](#功能概览)
- [开发环境搭建](#开发环境搭建)
- [如何构建](#如何构建)
- [如何运行测试](#如何运行测试)
- [打包正式版本](#打包正式版本)
- [维护公私钥](#维护公私钥)
- [生成激活码](#生成激活码)
- [项目结构](#项目结构)
- [技术栈](#技术栈)
- [工程约束](#工程约束)

---

## 功能概览

| 功能 | 说明 |
|------|------|
| 格式转换 | 支持 MP4、AVI、MKV、MOV、MP3、WAV、FLAC、AAC、OGG 等主流格式互转 |
| 提取音轨 / 视轨 | 从视频中单独提取音频或无声视频 |
| 波形编辑器 | 可视化波形预览、标记入出点、裁剪片段 |
| 音量增益 | -20dB ~ +20dB 增益调节，导出时生效 |
| 多段拼接 | 将多个音视频文件拼接为单一文件 |
| 离线授权 | RSA-2048 离线激活码，无需联网 |
| 深色主题 | 全自定义窗口边框与深色 UI |

---

## 开发环境搭建

### 前置要求

| 工具 | 版本要求 | 说明 |
|------|----------|------|
| Visual Studio | 2019 或 2022（推荐） | 需安装 **.NET 桌面开发** 工作负载 |
| .NET Framework | 4.5.2 | Visual Studio 安装时勾选或单独安装 Developer Pack |
| Windows SDK | 7.0 或更高 | 通常随 Visual Studio 自动安装 |
| NuGet | 已内置于 VS | 用于还原依赖包 |

### 克隆仓库

```bash
git clone https://github.com/ddmy/MediaTrans.git
cd MediaTrans
```

### 还原 NuGet 包

首次打开解决方案时，Visual Studio 会自动还原包。也可以手动运行：

```bash
nuget restore MediaTrans.sln
# 或使用 .nuget/nuget.exe（已内置）
.nuget\nuget.exe restore MediaTrans.sln
```

依赖包版本（见 `src/MediaTrans/packages.config`）：

| 包 | 版本 |
|----|------|
| SkiaSharp | 1.68.3 |
| NAudio | 1.10.0 |
| Newtonsoft.Json | 13.0.3 |
| Microsoft.NETFramework.ReferenceAssemblies.net452 | 1.0.3 |

---

## 如何构建

### Visual Studio（推荐）

1. 双击打开 `MediaTrans.sln`
2. 右键解决方案 → **生成解决方案**（`Ctrl+Shift+B`）
3. 输出目录：`src/MediaTrans/bin/Debug/` 或 `bin/Release/`

### 命令行（MSBuild）

```bash
# Debug 版本
msbuild MediaTrans.sln /t:Rebuild /p:Configuration=Debug /p:Platform="Any CPU"

# Release 版本
msbuild MediaTrans.sln /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU"
```

### 构建脚本

项目根目录提供了快捷构建脚本：

```bash
build.bat
```

---

## 如何运行测试

测试项目位于 `tests/MediaTrans.Tests/`，使用 xUnit 框架。

### Visual Studio

1. 打开 **测试资源管理器**（菜单：测试 → 测试资源管理器）
2. 点击 **全部运行**

### 命令行

```bash
# 还原测试项目依赖
nuget restore tests/MediaTrans.Tests/MediaTrans.Tests.csproj

# 运行所有测试（需要安装 xunit.runner.console）
# 方式一：MSTest
msbuild tests/MediaTrans.Tests/MediaTrans.Tests.csproj /t:Rebuild
packages\xunit.runner.console.2.4.1\tools\net452\xunit.console.exe tests\MediaTrans.Tests\bin\Debug\MediaTrans.Tests.dll

# 方式二：dotnet test（仅 SDK 风格项目）
dotnet test tests/MediaTrans.Tests/
```

### 测试覆盖范围

| 测试模块 | 说明 |
|----------|------|
| FFmpegCommandBuilder | 命令参数构建正确性 |
| EditExportService | 裁剪/拼接参数验证 |
| ConversionService | 格式映射与预设构建 |
| WaveformViewModel | 缩放、平移、视口计算 |
| SelectionViewModel | 选区拖拽逻辑 |
| LicenseService | 激活码验证 |
| MachineCodeService | 机器码生成 |

---

## 打包正式版本

### 前置准备

1. 确保 `lib/ffmpeg/` 目录下有以下文件（静态编译版，无外部依赖）：
   - `ffmpeg.exe`
   - `ffprobe.exe`
2. 将 Release 版本编译成功：`msbuild /p:Configuration=Release`

### 使用 Inno Setup 打包

安装脚本位于 `installer/` 目录。

1. 安装 [Inno Setup 6.x](https://jrsoftware.org/isinfo.php)
2. 双击打开 `installer/MediaTrans.iss`
3. 点击 **编译**（`Ctrl+F9`）
4. 生成的安装包在 `installer/Output/MediaTrans_vX.X_Setup.exe`

### 版本号管理

在打包前，更新以下位置的版本号：

- `installer/MediaTrans.iss` → `MyAppVersion`
- `src/MediaTrans/Properties/AssemblyInfo.cs` → `[assembly: AssemblyVersion(...)]`

### 代码混淆（可选）

正式发布建议使用 [ConfuserEx](https://github.com/mkbel/ConfuserEx) 对输出程序集进行混淆：

```bash
ConfuserEx.CLI.exe -n installer/confuserex.crproj
```

---

## 维护公私钥

MediaTrans 使用 **RSA-2048 非对称加密** 进行离线授权，私钥由开发者单独保管，公钥编译进客户端。

### ⚠️ 安全注意事项

- **私钥绝对不能提交到 Git 仓库**
- **私钥离线保存**，建议加密存储在 U 盘或密码管理器中
- 私钥丢失意味着无法再为老用户生成激活码

### 生成新密钥对

使用 `tools/KeyGenerator` 项目（独立控制台程序）：

```bash
cd tools/KeyGenerator
msbuild KeyGenerator.csproj /p:Configuration=Release
bin\Release\KeyGenerator.exe
```

输出示例：
```
=== RSA-2048 密钥对已生成 ===

[公钥 - 嵌入客户端]
<RSAKeyValue><Modulus>...</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>

[私钥 - 开发者保管，勿提交！]
<RSAKeyValue><Modulus>...</Modulus>...<D>...</D></RSAKeyValue>

已保存到:
  public_key.xml
  private_key.xml（⚠️ 请立即移至安全位置）
```

### 更新客户端公钥

将生成的公钥内容替换到 `src/MediaTrans/Services/LicenseService.cs` 中的 `PublicKey` 常量：

```csharp
private const string PublicKey = "<RSAKeyValue>...</RSAKeyValue>";
```

---

## 生成激活码

使用 `tools/LicenseIssuer` 项目（独立控制台程序，**仅开发者使用**）。

### 前提

- 手上有 RSA 私钥文件（`private_key.xml`）
- 知道用户的**机器码**（用户在软件许可证窗口可复制）

### 步骤

```bash
cd tools/LicenseIssuer
msbuild LicenseIssuer.csproj /p:Configuration=Release
bin\Release\LicenseIssuer.exe
```

输入提示：
```
请输入私钥文件路径: C:\keys\private_key.xml
请输入用户机器码: A1B2C3D4E5F6...
请输入授权版本 (1=专业版): 1
请输入有效期（天，0=永久）: 0

=== 激活码已生成 ===
XXXXX-XXXXX-XXXXX-XXXXX-XXXXX
```

将激活码发送给用户，用户在软件中输入并点击"激活"即可。

### 激活码结构

```
[机器码哈希] + [授权版本] + [过期时间] → RSA 私钥签名 → Base32 编码
```

客户端使用内置公钥验签，完全离线，无服务器依赖。

---

## 项目结构

```
MediaTrans/
├── MediaTrans.sln                   # 解决方案文件
├── build.bat                        # 快捷构建脚本
├── src/
│   └── MediaTrans/                  # WPF 主项目
│       ├── Models/                  # 数据模型（AppConfig, MediaFileInfo...）
│       ├── ViewModels/              # MVVM ViewModel 层
│       ├── Views/                   # XAML 视图（MainWindow, LicenseWindow）
│       ├── Services/                # 业务服务
│       │   ├── FFmpegService.cs     # FFmpeg 进程封装
│       │   ├── ConversionService.cs # 格式转换
│       │   ├── EditExportService.cs # 裁剪/拼接导出
│       │   ├── AudioPlaybackService.cs # NAudio 播放
│       │   ├── WaveformRenderService.cs # SkiaSharp 波形渲染
│       │   └── LicenseService.cs   # RSA 授权验证
│       ├── Commands/                # RelayCommand
│       ├── Converters/              # WPF 值转换器
│       ├── Assets/
│       │   └── DarkTheme.xaml      # 深色主题样式
│       └── Config/
│           └── AppConfig.json      # 统一配置文件
├── tests/
│   └── MediaTrans.Tests/           # xUnit 单元测试
├── tools/
│   ├── KeyGenerator/               # RSA 密钥生成工具（开发者专用）
│   └── LicenseIssuer/              # 激活码签发工具（开发者专用）
└── lib/
    └── ffmpeg/                     # FFmpeg 静态编译版（随程序分发）
        ├── ffmpeg.exe
        └── ffprobe.exe
```

---

## 技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| C# / WPF | 5.0 语法 / .NET 4.5.2 | 主应用框架 |
| SkiaSharp | 1.68.3 | 波形图 GPU 加速渲染 |
| NAudio | 1.10.0 | 音频解码与 WaveOut 播放 |
| Newtonsoft.Json | 13.0.3 | AppConfig.json 序列化 |
| FFmpeg | 静态编译版 | 所有音视频处理后端 |
| xUnit | 2.x | 单元测试 |
| Inno Setup | 6.x | Windows 安装包 |
| ConfuserEx | - | 代码混淆（可选） |

---

## 工程约束

| 约束 | 说明 |
|------|------|
| C1 零外部依赖 | 用户无需安装额外运行时，FFmpeg/SkiaSharp/NAudio 全部内置 |
| C2 路径安全 | FFmpeg 命令路径使用双引号包裹，支持空格和中文 |
| C3 进程安全 | FFmpeg 子进程绑定 Windows Job Object，主进程退出自动终止 |
| C4 UI 响应性 | 耗时操作在 ThreadPool 执行，通过 Dispatcher 回调 UI |
| C5 编译自测 | 每次改动后必须 Build + 运行对应单元测试 |
| C6 编码安全 | 文件读写统一 UTF-8 编码 |
| C7 显式编解码器 | FFmpeg 命令必须指定 `-c:v` / `-c:a`，不依赖默认值 |
| C8 统一配置 | 所有可配置项收拢到 `AppConfig.json`，禁止硬编码 |

---

## 常见问题

**Q: 运行时提示找不到 ffmpeg.exe？**

确保 `lib/ffmpeg/ffmpeg.exe` 和 `ffprobe.exe` 存在，并在 `Config/AppConfig.json` 中配置了正确路径（相对于可执行文件目录）。

**Q: 波形图不显示？**

波形渲染需要 NAudio 能打开音频文件。视频文件需要包含音轨。纯视频文件会显示"无法读取音频波形"提示。

**Q: 激活后重启失效？**

检查程序是否有对 `%AppData%\MediaTrans\` 目录的写权限。激活信息存储在该目录下的 `license.dat` 文件中。

---

*MediaTrans © 2024 — All Rights Reserved*
