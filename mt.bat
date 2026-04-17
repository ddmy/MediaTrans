@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion

set ROOT=%~dp0
set SOLUTION=%ROOT%MediaTrans.sln
set CONFIG=%~2
if "%CONFIG%"=="" set CONFIG=Debug

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
        goto :found_msbuild
    )
)

echo [error] MSBuild.exe not found.
exit /b 1

:found_msbuild
set XUNIT=%ROOT%packages\xunit.runner.console.2.4.2\tools\net452\xunit.console.exe

if "%~1"=="" goto :usage

if /i "%~1"=="build" goto :build
if /i "%~1"=="run" goto :run
if /i "%~1"=="test" goto :test
if /i "%~1"=="pack" goto :pack
if /i "%~1"=="help" goto :usage

echo [error] Unknown command: %~1
goto :usage

:build
echo [info] Build %CONFIG%
"%MSBUILD%" "%SOLUTION%" -t:Build -p:Configuration=%CONFIG% -v:minimal -nologo
exit /b %ERRORLEVEL%

:run
echo [info] Build and run %CONFIG%
"%MSBUILD%" "%SOLUTION%" -t:Build -p:Configuration=%CONFIG% -v:minimal -nologo
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

set EXE=%ROOT%src\MediaTrans\bin\%CONFIG%\MediaTrans.exe
if not exist "%EXE%" (
    echo [error] Not found: %EXE%
    exit /b 1
)
start "MediaTrans" "%EXE%"
exit /b 0

:test
echo [info] Build Debug for tests
"%MSBUILD%" "%SOLUTION%" -t:Build -p:Configuration=Debug -v:minimal -nologo
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

if not exist "%XUNIT%" (
    echo [error] xUnit runner not found: %XUNIT%
    exit /b 1
)

"%XUNIT%" "%ROOT%tests\MediaTrans.Tests\bin\Debug\MediaTrans.Tests.dll" -nologo
exit /b %ERRORLEVEL%

:pack
call "%ROOT%build.bat"
exit /b %ERRORLEVEL%

:usage
echo.
echo MediaTrans quick commands
echo.
echo   mt build [Debug^|Release]     Build solution (default: Debug)
echo   mt run [Debug^|Release]       Build then run MediaTrans.exe
echo   mt test                        Build Debug and run xUnit tests
echo   mt pack                        Run full packaging pipeline (build.bat)
echo   mt help                        Show this help
echo.
exit /b 1
