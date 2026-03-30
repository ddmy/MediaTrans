@echo off
REM ============================================================
REM  MediaTrans 一键构建流水线
REM  流程: MSBuild Release → xUnit 测试 → ConfuserEx 混淆 → Inno Setup 打包
REM  用法: build.bat [--skip-test] [--skip-confuse] [--skip-installer]
REM  输出: dist\MediaTrans_Setup_1.0.0.exe
REM ============================================================

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%
set EXIT_CODE=0

REM 解析参数
set SKIP_TEST=0
set SKIP_CONFUSE=0
set SKIP_INSTALLER=0

:parse_args
if "%~1"=="" goto end_parse
if /i "%~1"=="--skip-test" set SKIP_TEST=1
if /i "%~1"=="--skip-confuse" set SKIP_CONFUSE=1
if /i "%~1"=="--skip-installer" set SKIP_INSTALLER=1
shift
goto parse_args
:end_parse

REM ============================================================
REM 查找 MSBuild
REM ============================================================
set MSBUILD=
for %%p in (
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
) do (
    if exist %%p (
        set "MSBUILD=%%~p"
        goto found_msbuild
    )
)
echo [错误] 未找到 MSBuild.exe
exit /b 1
:found_msbuild
echo [信息] MSBuild: %MSBUILD%

REM ============================================================
REM 查找 xUnit 测试运行器
REM ============================================================
set XUNIT_RUNNER=%PROJECT_ROOT%packages\xunit.runner.console.2.4.2\tools\net452\xunit.console.exe
if not exist "%XUNIT_RUNNER%" (
    echo [错误] 未找到 xUnit 测试运行器: %XUNIT_RUNNER%
    exit /b 1
)

REM ============================================================
REM 查找 ConfuserEx CLI
REM ============================================================
set CONFUSER_CLI=%PROJECT_ROOT%tools\ConfuserEx\packages\ConfuserEx.Final.1.0.0\tools\Confuser.CLI.exe
set CONFUSER_CONFIG=%PROJECT_ROOT%tools\ConfuserEx\MediaTrans.crproj

REM ============================================================
REM 查找 Inno Setup
REM ============================================================
set ISCC=
where ISCC.exe >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    for /f "tokens=*" %%i in ('where ISCC.exe') do set "ISCC=%%i"
) else (
    for %%p in (
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    ) do (
        if exist %%p (
            set "ISCC=%%~p"
            goto found_iscc
        )
    )
)
:found_iscc

echo.
echo ============================================================
echo  MediaTrans 一键构建流水线
echo ============================================================
echo  MSBuild:      %MSBUILD%
echo  xUnit:        %XUNIT_RUNNER%
echo  ConfuserEx:   %CONFUSER_CLI%
echo  Inno Setup:   %ISCC%
echo  跳过测试:     %SKIP_TEST%
echo  跳过混淆:     %SKIP_CONFUSE%
echo  跳过安装包:   %SKIP_INSTALLER%
echo ============================================================
echo.

REM ============================================================
REM 步骤 1: MSBuild Release 编译
REM ============================================================
echo [步骤 1/4] MSBuild Release 编译...
"%MSBUILD%" "%PROJECT_ROOT%MediaTrans.sln" -t:Rebuild -p:Configuration=Release -v:minimal -nologo
if %ERRORLEVEL% NEQ 0 (
    echo [错误] Release 编译失败！
    exit /b 1
)
echo [成功] Release 编译完成
echo.

REM ============================================================
REM 步骤 2: xUnit 测试
REM ============================================================
if %SKIP_TEST% EQU 1 (
    echo [跳过] 步骤 2: xUnit 测试
) else (
    echo [步骤 2/4] xUnit 测试...
    REM 先编译 Debug 用于测试
    "%MSBUILD%" "%PROJECT_ROOT%MediaTrans.sln" -t:Build -p:Configuration=Debug -v:minimal -nologo
    if %ERRORLEVEL% NEQ 0 (
        echo [错误] Debug 编译失败！
        exit /b 1
    )

    "%XUNIT_RUNNER%" "%PROJECT_ROOT%tests\MediaTrans.Tests\bin\Debug\MediaTrans.Tests.dll" -nologo
    if %ERRORLEVEL% NEQ 0 (
        echo [错误] 测试失败！请修复后重试。
        exit /b 1
    )
    echo [成功] 所有测试通过
)
echo.

REM ============================================================
REM 步骤 3: ConfuserEx 混淆
REM ============================================================
if %SKIP_CONFUSE% EQU 1 (
    echo [跳过] 步骤 3: ConfuserEx 混淆
) else (
    if not exist "%CONFUSER_CLI%" (
        echo [警告] 未找到 ConfuserEx CLI，跳过混淆
        echo        请运行 tools\ConfuserEx\setup-confuserex.bat 安装
    ) else (
        echo [步骤 3/4] ConfuserEx 代码混淆...
        
        REM 创建混淆输出目录
        if not exist "%PROJECT_ROOT%src\MediaTrans\bin\Confused" mkdir "%PROJECT_ROOT%src\MediaTrans\bin\Confused"
        
        pushd "%PROJECT_ROOT%tools\ConfuserEx"
        "%CONFUSER_CLI%" "%CONFUSER_CONFIG%"
        set CONFUSE_RESULT=%ERRORLEVEL%
        popd
        
        if !CONFUSE_RESULT! NEQ 0 (
            echo [错误] 混淆失败！
            exit /b 1
        )

        REM 验证混淆输出
        if not exist "%PROJECT_ROOT%src\MediaTrans\bin\Confused\MediaTrans.exe" (
            echo [错误] 混淆输出文件不存在！
            exit /b 1
        )
        echo [成功] 混淆完成
    )
)
echo.

REM ============================================================
REM 步骤 4: Inno Setup 打包
REM ============================================================
if %SKIP_INSTALLER% EQU 1 (
    echo [跳过] 步骤 4: Inno Setup 打包
) else (
    if "%ISCC%"=="" (
        echo [警告] 未找到 Inno Setup，跳过安装包生成
        echo        请安装 Inno Setup 6
    ) else (
        echo [步骤 4/4] Inno Setup 安装包生成...
        
        REM 创建输出目录
        if not exist "%PROJECT_ROOT%dist" mkdir "%PROJECT_ROOT%dist"
        
        REM 如果混淆版本存在，使用混淆版本
        if exist "%PROJECT_ROOT%src\MediaTrans\bin\Confused\MediaTrans.exe" (
            echo [信息] 使用混淆版本打包
            "%ISCC%" /DUseConfused "%PROJECT_ROOT%installer\MediaTrans.iss"
        ) else (
            echo [信息] 使用 Release 版本打包（无混淆）
            "%ISCC%" "%PROJECT_ROOT%installer\MediaTrans.iss"
        )
        
        if %ERRORLEVEL% NEQ 0 (
            echo [错误] 安装包生成失败！
            exit /b 1
        )
        
        REM 验证安装包
        if not exist "%PROJECT_ROOT%dist\MediaTrans_Setup_1.0.0.exe" (
            echo [错误] 安装包文件不存在！
            exit /b 1
        )
        echo [成功] 安装包生成完成
    )
)
echo.

REM ============================================================
REM 完成
REM ============================================================
echo ============================================================
echo  构建流水线完成！
echo ============================================================
if exist "%PROJECT_ROOT%dist\MediaTrans_Setup_1.0.0.exe" (
    for %%f in ("%PROJECT_ROOT%dist\MediaTrans_Setup_1.0.0.exe") do (
        echo  安装包: %%~ff
        echo  大小:   %%~zf 字节
    )
)
echo ============================================================

endlocal
exit /b 0
