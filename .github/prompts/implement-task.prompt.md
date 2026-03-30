---
mode: "agent"
description: "实现 MediaTrans 项目中的一个具体 Task"
tools: ["run_in_terminal", "read_file", "create_file", "replace_string_in_file", "grep_search", "file_search", "semantic_search", "get_errors"]
---

# 实现 Task

你是 MediaTrans 项目的开发者。请严格按照以下流程实现指定的 Task。

## 输入

用户会提供一个 Task 编号（如 `1.5`）。你需要：

1. 阅读 `doc/tasks.md` 找到该 Task 的完整描述和验收标准
2. 确认该 Task 的前置依赖在 `doc/tasks.md` 中全部为 ✅
3. 如果前置依赖未完成，**停止并告知用户**

## 实现流程

### 第 1 步：理解上下文
- 阅读 Task 描述、验收标准、前置依赖
- 阅读相关已有代码（前置 Task 产出的文件）
- 阅读 `src/MediaTrans/Config/AppConfig.json` 了解已有配置项

### 第 2 步：规划
- 列出需要创建/修改的文件清单
- 列出需要新增的配置项（写入 AppConfig.json）
- 列出需要新增的单元测试用例

### 第 3 步：编码
严格遵守以下规则：
- **C# 5.0 语法**：不使用 `?.`、`$""`、`nameof`、自动属性初始化器、表达式体成员
- **路径安全**：FFmpeg 命令中路径双引号包裹；文件操作处理空格/中文
- **FFmpeg 显式编解码器**：所有命令包含 `-c:v` / `-c:a`
- **统一配置**：可配置值从 AppConfig.json 读取，不硬编码
- **UI 响应性**：耗时操作放后台线程，Dispatcher 回调 UI
- **UTF-8**：文件读写指定 `Encoding.UTF8`

### 第 4 步：编译验证
```
MSBuild MediaTrans.sln /t:Rebuild /p:Configuration=Debug
```
必须零 Error。Warning 尽量消除。

### 第 5 步：运行测试
```
packages\xunit.runner.console\tools\net452\xunit.console.exe tests\MediaTrans.Tests\bin\Debug\MediaTrans.Tests.dll
```
所有测试必须通过。

### 第 6 步：更新进度
- 更新 `doc/tasks.md` 中对应 Task 行：状态 ⬜ → ✅，编译列和测试列标记 ✅
- 更新 `doc/tasks.md` 顶部总进度表的完成数和百分比

### 第 7 步：生成 Commit Message
格式：`<type>(<task>): <描述>`
示例：`feat(1.5): FFmpeg 集成层 - 进程封装与 Job Object 绑定`
