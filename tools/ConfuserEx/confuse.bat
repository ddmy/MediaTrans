@echo off
REM MediaTrans 代码混淆脚本
REM 前置条件：先完成 Release 编译
REM 用法：confuse.bat
REM 输出：src\MediaTrans\bin\Confused\MediaTrans.exe

setlocal

set SCRIPT_DIR=%~dp0
set CONFUSER_CLI=%SCRIPT_DIR%packages\ConfuserEx.Final.1.0.0\tools\Confuser.CLI.exe
set CRPROJ=%SCRIPT_DIR%MediaTrans.crproj

REM 检查 ConfuserEx CLI 是否存在
if not exist "%CONFUSER_CLI%" (
    echo [错误] 未找到 Confuser.CLI.exe
    echo 请运行 setup-confuserex.bat 安装 ConfuserEx
    exit /b 1
)

REM 检查配置文件
if not exist "%CRPROJ%" (
    echo [错误] 未找到 MediaTrans.crproj 配置文件
    exit /b 1
)

REM 检查 Release 编译输出
set RELEASE_DIR=%SCRIPT_DIR%..\..\src\MediaTrans\bin\Release\
if not exist "%RELEASE_DIR%MediaTrans.exe" (
    echo [错误] 未找到 Release 编译输出，请先执行 Release 编译
    exit /b 1
)

REM 创建混淆输出目录
set OUTPUT_DIR=%SCRIPT_DIR%..\..\src\MediaTrans\bin\Confused\
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo ============================================
echo  MediaTrans 代码混淆
echo ============================================
echo 输入: %RELEASE_DIR%MediaTrans.exe
echo 输出: %OUTPUT_DIR%
echo 配置: %CRPROJ%
echo ============================================

REM 运行 ConfuserEx 混淆
"%CONFUSER_CLI%" "%CRPROJ%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [错误] 混淆失败！错误代码: %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo [成功] 混淆完成！
echo 混淆后的文件位于: %OUTPUT_DIR%

REM 验证输出文件存在
if exist "%OUTPUT_DIR%MediaTrans.exe" (
    echo [验证] MediaTrans.exe 已生成
) else (
    echo [警告] 未找到混淆后的 MediaTrans.exe
    exit /b 1
)

endlocal
