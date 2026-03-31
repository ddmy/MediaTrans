# GitHub Actions 工作流说明

## 概述

MediaTrans 项目使用 GitHub Actions 进行持续集成和生产版本打包。本文档说明各工作流的用途和触发条件。

---

## 🚀 工作流列表

### 1. **Build Release** (`build-release.yml`)

**用途**: 生产版本打包工作流

**触发条件**:
- 推送到 main/master 分支
- 创建版本标签 `v*` (例如 `v1.0.0`, `v2.1.3`)
- 手工触发 (`workflow_dispatch`)

**工作流步骤**:
1. ✅ 检出代码
2. ✅ 配置 MSBuild 环境
3. ✅ 安装 Inno Setup
4. ✅ MSBuild Release 编译
5. ✅ xUnit 单元测试
6. ✅ ConfuserEx 代码混淆（可选）
7. ✅ Inno Setup 打包
8. ✅ 上传构建产物
9. ✅ 创建 GitHub Release（标签发布时）

**输出**:
- 安装程序: `MediaTrans_Setup_X.Y.Z.exe`
- GitHub Release 页面（标签发布时）
- 构建产物存档（30天保留）

**手工触发参数**:
```
--skip-test      # 跳过单元测试
--skip-confuse   # 跳过代码混淆
```

**示例发布**:
```bash
git tag v1.0.0
git push origin v1.0.0
# 工作流自动触发 → 生成 MediaTrans_Setup_1.0.0.exe → 创建发布版本
```

---

### 2. **PR Validation** (`pr-validation.yml`)

**用途**: 拉取请求代码质量验证

**触发条件**:
- 创建/更新拉取请求（针对 main/master 分支）
- 推送到 main/master 分支

**工作流步骤**:
1. ✅ 检出代码
2. ✅ Release 编译验证
3. ✅ Debug 编译验证
4. ✅ xUnit 单元测试运行
5. ✅ 代码结构检查
6. ✅ 编译输出统计

**输出**:
- PR 检查结果（Pass/Fail）
- 构建摘要报告

**何时使用**:
- 合并前必须通过此工作流
- CI 检查失败时不能合并

---

### 3. **Quick Test** (`quick-test.yml`)

**用途**: 快速编译与测试验证

**触发条件**:
- 拉取请求更改以下文件:
  - `src/**` (源代码)
  - `tests/**` (测试代码)
  - `MediaTrans.sln`
  - `.github/workflows/quick-test.yml`

**工作流步骤**:
1. ✅ 检出代码
2. ✅ Debug 编译
3. ✅ 单元测试运行

**输出**:
- 快速验证结果

**何时使用**:
- 快速反馈开发周期
- 不影响生产版本打包

---

## 📋 工作流对比

| 工作流 | 触发时机 | 编译数 | 测试 | 混淆 | 打包 | 发布 |
|--------|---------|--------|------|------|------|------|
| Build Release | 标签/main/手工 | 1 (Release) | ✅ | ✅ | ✅ | ✅ |
| PR Validation | PR/Push | 2 (Release+Debug) | ✅ | ❌ | ❌ | ❌ |
| Quick Test | PR (源码改动) | 1 (Debug) | ✅ | ❌ | ❌ | ❌ |

---

## 🔄 典型发布流程

### 开发阶段
```
feature branch
    ↓
创建 Pull Request
    ↓
Quick Test + PR Validation (自动运行)
    ↓
代码审查
    ↓
合并到 main/master
    ↓
Build Release + Tests (自动运行)
```

### 发布阶段
```
main/master (最新提交)
    ↓
创建版本标签: git tag v1.0.0
    ↓
推送标签: git push origin v1.0.0
    ↓
Build Release (自动触发)
    ↓
生成安装程序
    ↓
创建 GitHub Release
    ↓
上传文件到发布页面
```

---

## 🎯 发布检查清单

发布新版本前，确保完成以下步骤：

- [ ] 所有 PR 通过代码审查和 CI 检查
- [ ] 本地编译通过 (`build.bat`)
- [ ] 单元测试全部通过
- [ ] 更新版本号 (在 `installer/MediaTrans.iss` 中的 `MyAppVersion`)
- [ ] 更新 CHANGELOG 或发布说明
- [ ] 创建版本标签: `git tag v1.0.0`
- [ ] 推送标签: `git push origin v1.0.0`
- [ ] wait for Build Release workflow complete
- [ ] 验证 GitHub Release 页面
- [ ] 测试下载的安装程序

---

## 🔧 本地构建命令

为了与 CI 保持一致，使用以下命令进行本地构建：

```bash
# 完整构建流程（包含编译、测试、混淆、打包）
build.bat

# 仅编译
msbuild MediaTrans.sln /t:Rebuild /p:Configuration=Release

# 编译 + 测试
msbuild MediaTrans.sln /t:Build /p:Configuration=Debug
packages\xunit.runner.console.2.4.2\tools\net452\xunit.console.exe tests\MediaTrans.Tests\bin\Debug\MediaTrans.Tests.dll

# 跳过某些步骤
build.bat --skip-test          # 跳过测试
build.bat --skip-confuse       # 跳过混淆
build.bat --skip-installer     # 跳过打包
```

---

## 📊 工作流状态徽章

在 README 中添加工作流状态徽章：

```markdown
[![Build Release](https://github.com/YOUR_ORG/mediatrans/actions/workflows/build-release.yml/badge.svg)](https://github.com/YOUR_ORG/mediatrans/actions/workflows/build-release.yml)
[![PR Validation](https://github.com/YOUR_ORG/mediatrans/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/YOUR_ORG/mediatrans/actions/workflows/pr-validation.yml)
[![Quick Test](https://github.com/YOUR_ORG/mediatrans/actions/workflows/quick-test.yml/badge.svg)](https://github.com/YOUR_ORG/mediatrans/actions/workflows/quick-test.yml)
```

---

## 🚨 故障排查

### 编译失败

1. 检查 MSBuild 环境是否正确配置
2. 查看工作流日志获取详细错误信息
3. 本地运行 `build.bat` 复现问题

### 测试失败

1. 检查 xUnit 测试运行器路径
2. 本地运行单元测试: `packages\xunit.runner.console.2.4.2\tools\net452\xunit.console.exe tests\MediaTrans.Tests\bin\Debug\MediaTrans.Tests.dll`
3. 修复失败的测试用例

### 打包失败

1. 检查 Inno Setup 是否正确安装
2. 验证 Release 编译输出目录
3. 检查 `installer/MediaTrans.iss` 配置是否正确

---

## 📞 相关资源

- [GitHub Actions 文档](https://docs.github.com/en/actions)
- [MSBuild 文档](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild)
- [xUnit 文档](https://xunit.net/)
- [Inno Setup 文档](https://jrsoftware.org/isinfo.php)
- [ConfuserEx 文档](https://github.com/mkaring/ConfuserEx)
