@echo off
REM 安装 ConfuserEx CLI
REM 通过 NuGet 下载 ConfuserEx.Final 包

setlocal

set SCRIPT_DIR=%~dp0
set PACKAGES_DIR=%SCRIPT_DIR%packages

REM 检查 NuGet 是否可用
where nuget >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [错误] 未找到 nuget.exe，请先安装 NuGet CLI
    exit /b 1
)

REM 检查是否已安装
if exist "%PACKAGES_DIR%\ConfuserEx.Final.1.0.0\tools\Confuser.CLI.exe" (
    echo [信息] ConfuserEx 已安装
    exit /b 0
)

echo 正在下载 ConfuserEx...
nuget install ConfuserEx.Final -Version 1.0.0 -OutputDirectory "%PACKAGES_DIR%"

if %ERRORLEVEL% NEQ 0 (
    echo [错误] 下载 ConfuserEx 失败
    exit /b 1
)

echo [成功] ConfuserEx 安装完成
echo CLI 路径: %PACKAGES_DIR%\ConfuserEx.Final.1.0.0\tools\Confuser.CLI.exe
endlocal
