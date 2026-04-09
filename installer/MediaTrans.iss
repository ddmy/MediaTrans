; MediaTrans 专业版 — Inno Setup 安装脚本
; 编译命令: ISCC.exe installer\MediaTrans.iss
; 或通过 build.bat 自动调用
;
; 前置条件:
;   1. Release 编译输出位于 src\MediaTrans\bin\Release\
;   2. 混淆后输出位于 src\MediaTrans\bin\Confused\（可选，优先使用混淆版本）
;   3. FFmpeg 静态编译版位于 lib\ffmpeg\（ffmpeg.exe + ffprobe.exe）

#define MyAppName "MediaTrans"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "MediaTrans"
#define MyAppExeName "MediaTrans.exe"
#define MyAppDescription "专业音视频处理工具"

; 根目录（相对于 .iss 文件位置）
#define ProjectRoot "..\"

; 优先使用混淆版本，否则使用 Release 版本
#ifdef UseConfused
  #define AppSourceDir ProjectRoot + "src\MediaTrans\bin\Confused"
#else
  #define AppSourceDir ProjectRoot + "src\MediaTrans\bin\Release"
#endif

#define ReleaseDir ProjectRoot + "src\MediaTrans\bin\Release"
#define FFmpegDir ProjectRoot + "lib\ffmpeg"
#define OutputDir ProjectRoot + "dist"

[Setup]
; 应用基本信息
AppId={{E8A7F3B2-4C5D-6E7F-8A9B-0C1D2E3F4A5B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; 输出目录和文件名
OutputDir={#OutputDir}
OutputBaseFilename=MediaTrans_Setup_{#MyAppVersion}
; 安装图标（如有）
; SetupIconFile={#ProjectRoot}src\MediaTrans\Assets\app.ico
; 压缩设置
Compression=lzma2/max
SolidCompression=yes
; 权限：不需要管理员权限（安装到用户目录）
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; 支持路径中的空格和中文
AllowUNCPath=no
DirExistsWarning=auto
; 最低 Windows 版本（Win7 SP1）
MinVersion=6.1sp1
; 卸载设置
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
; 架构（支持 32 位和 64 位）
ArchitecturesAllowed=x86compatible x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; 显示许可协议（如有）
; LicenseFile={#ProjectRoot}LICENSE.txt
; 向导页面设置
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.CreateDesktopIcon=创建桌面快捷方式
english.CreateQuickLaunchIcon=创建快速启动栏图标
english.AdditionalIcons=附加图标:

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"
Name: "quicklaunchicon"; Description: "创建快速启动栏图标"; GroupDescription: "附加图标:"; Flags: unchecked

[Files]
; 主程序（优先混淆版本）
Source: "{#AppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; 依赖 DLL（始终从 Release 目录取，混淆不处理依赖）
Source: "{#ReleaseDir}\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ReleaseDir}\SkiaSharp.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ReleaseDir}\libSkiaSharp.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ReleaseDir}\NAudio.dll"; DestDir: "{app}"; Flags: ignoreversion

; SkiaSharp 原生库（x86/x64）
Source: "{#ReleaseDir}\x86\libSkiaSharp.dll"; DestDir: "{app}\x86"; Flags: ignoreversion
Source: "{#ReleaseDir}\x64\libSkiaSharp.dll"; DestDir: "{app}\x64"; Flags: ignoreversion

; 配置文件
Source: "{#ReleaseDir}\Config\AppConfig.json"; DestDir: "{app}\Config"; Flags: ignoreversion onlyifdoesntexist

; FFmpeg 静态编译版（如果存在则打包）
#ifexist "..\lib\ffmpeg\ffmpeg.exe"
Source: "{#FFmpegDir}\ffmpeg.exe"; DestDir: "{app}\lib\ffmpeg"; Flags: ignoreversion
#endif
#ifexist "..\lib\ffmpeg\ffprobe.exe"
Source: "{#FFmpegDir}\ffprobe.exe"; DestDir: "{app}\lib\ffmpeg"; Flags: ignoreversion
#endif

[Icons]
; 开始菜单
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"

; 桌面快捷方式
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; 快速启动（仅 Win7）
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
; 安装完成后运行
Filename: "{app}\{#MyAppExeName}"; Description: "立即运行 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 卸载时清理应用生成的文件
Type: filesandordirs; Name: "{app}\Config"
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\lib"
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"

[Code]
// .NET Framework 4.5.2 注册表检测
const
  DOTNET_452_RELEASE = 379893;

function IsDotNetInstalled: Boolean;
var
  releaseValue: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
    'Release', releaseValue);
  if Result then
    Result := (releaseValue >= DOTNET_452_RELEASE);
end;

// 初始化安装向导：检查 .NET Framework
function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  
  if not IsDotNetInstalled then
  begin
    if MsgBox('MediaTrans 需要 .NET Framework 4.5.2 或更高版本。'#13#10#13#10 +
              '是否立即下载安装 .NET Framework 4.5.2？'#13#10 +
              '（安装完成后请重新运行本安装程序）',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      // 引导用户到微软官方下载页面
      ShellExec('open', 
        'https://dotnet.microsoft.com/download/dotnet-framework/net452',
        '', '', SW_SHOW, ewNoWait, ErrorCode);
    end;
    Result := False;
  end;
end;

// 卸载时清理注册表和 AppData 残留
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // 清理 LocalAppData\MediaTrans（授权文件等）
    AppDataDir := ExpandConstant('{localappdata}\{#MyAppName}');
    if DirExists(AppDataDir) then
    begin
      if MsgBox('是否删除应用数据（包括授权信息）？', mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(AppDataDir, True, True, True);
      end;
    end;
  end;
end;
