---
mode: "agent"
description: "修复编译错误和测试失败"
tools: ["run_in_terminal", "read_file", "replace_string_in_file", "grep_search", "get_errors"]
---

# 修复编译/测试问题

你是 MediaTrans 项目的修复工程师。当编译或测试失败时，使用此 prompt 定位并修复问题。

## 流程

### 第 1 步：收集错误信息

编译错误：
```
MSBuild MediaTrans.sln /t:Rebuild /p:Configuration=Debug 2>&1
```

测试失败：
```
packages\xunit.runner.console\tools\net452\xunit.console.exe tests\MediaTrans.Tests\bin\Debug\MediaTrans.Tests.dll
```

### 第 2 步：分类错误

| 类型 | 典型原因 | 修复方向 |
|------|---------|---------|
| CS1525 / CS1061 | 使用了 C# 6.0+ 语法 | 改写为 C# 5.0 等效代码 |
| CS0246 | 缺少 using 或程序集引用 | 添加 using / NuGet 引用 |
| CS0103 | 未定义的变量/方法 | 检查拼写或前置 Task 是否完成 |
| 测试断言失败 | 逻辑错误 | 检查被测代码和测试期望值 |
| FileNotFound | 配置/资源文件缺失 | 检查 csproj 中的文件引用和 CopyToOutputDirectory |

### 第 3 步：修复

修复时必须遵守：
- C# 5.0 语法
- 不破坏其他已通过的测试
- 不引入新的硬编码值

### 第 4 步：验证

修复后重新编译 + 运行全部测试，确认零 Error + 全部通过。

## 常见 C# 5.0 改写速查

```csharp
// ❌ C# 6.0+                    →  ✅ C# 5.0
obj?.Method()                     →  if (obj != null) { obj.Method(); }
obj?.Property                     →  obj != null ? obj.Property : null
$"Hello {name}"                   →  string.Format("Hello {0}", name)
nameof(PropertyName)              →  "PropertyName"
public int X { get; } = 5;       →  public int X { get; private set; }  // 构造函数赋值
void M() => DoIt();              →  void M() { DoIt(); }
dict?.Count ?? 0                  →  dict != null ? dict.Count : 0
```
