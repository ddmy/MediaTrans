using System;
using System.Diagnostics;
using System.IO;

namespace MediaTrans.Services
{
    /// <summary>
    /// 管理 Node.js 音乐搜索服务进程的生命周期
    /// 通过 JobObject 绑定，主进程退出时自动终止子进程
    /// </summary>
    public class NodeServiceManager : IDisposable
    {
        private Process _process;
        private JobObject _jobObject;
        private bool _disposed;
        private string _nodePath;
        private readonly string _serverScript;
        private readonly int _port;

        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return _process != null && !_process.HasExited;
            }
        }

        /// <summary>
        /// 服务端口
        /// </summary>
        public int Port
        {
            get { return _port; }
        }

        /// <summary>
        /// 服务基础 URL
        /// </summary>
        public string BaseUrl
        {
            get { return string.Format("http://127.0.0.1:{0}", _port); }
        }

        public NodeServiceManager(string nodePath, string serverScript, int port)
        {
            _nodePath = nodePath;
            _serverScript = serverScript;
            _port = port;
        }

        /// <summary>
        /// 查找可用的 Node.js 运行时路径
        /// 优先级：配置路径 → 系统 PATH → 常见安装位置
        /// </summary>
        public static string FindNodePath(string configuredPath)
        {
            // 1. 检查配置路径
            if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath))
            {
                return configuredPath;
            }

            // 2. 从系统 PATH 中查找
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] pathDirs = pathEnv.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string dir in pathDirs)
            {
                string candidate = Path.Combine(dir.Trim(), "node.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // 3. 检查常见安装位置
            string[] commonPaths = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "node.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs", "node.exe"),
                @"C:\Program Files\nodejs\node.exe",
                @"C:\Program Files (x86)\nodejs\node.exe"
            };
            foreach (string p in commonPaths)
            {
                if (File.Exists(p))
                {
                    return p;
                }
            }

            return null;
        }

        /// <summary>
        /// 状态消息回调（用于向 UI 报告依赖安装进度等）
        /// </summary>
        public Action<string> StatusCallback { get; set; }

        /// <summary>
        /// 启动 Node.js 服务
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;

            // 自动查找 Node.js
            string resolvedNode = FindNodePath(_nodePath);
            if (string.IsNullOrEmpty(resolvedNode))
            {
                throw new FileNotFoundException(
                    "未找到 Node.js 运行时。\n\n请安装 Node.js (https://nodejs.org) 后重试，\n或将 node.exe 放到 lib\\nodejs\\ 目录下。");
            }
            _nodePath = resolvedNode;

            if (!File.Exists(_serverScript))
            {
                throw new FileNotFoundException(
                    string.Format("音乐服务脚本未找到: {0}", _serverScript));
            }

            // 自动安装依赖（如果缺失或不完整）
            string serverDir = Path.GetDirectoryName(_serverScript);
            if (NeedInstallDependencies(serverDir))
            {
                ReportStatus("正在安装音乐服务依赖，首次使用需要等待...");
                InstallDependencies(resolvedNode, serverDir);
                ReportStatus("依赖安装完成，正在启动服务...");
            }

            _jobObject = new JobObject();

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = _nodePath;
            startInfo.Arguments = string.Format("\"{0}\" --port {1}", _serverScript, _port);
            startInfo.WorkingDirectory = Path.GetDirectoryName(_serverScript);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            _process = new Process();
            _process.StartInfo = startInfo;
            _process.EnableRaisingEvents = true;
            _process.Start();

            // 绑定到 Job Object，主进程退出时自动终止
            _jobObject.AssignProcess(_process.Handle);

            // 异步读取输出（防止缓冲区阻塞）
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        /// <summary>
        /// 检查是否需要安装或修复依赖
        /// </summary>
        private static bool NeedInstallDependencies(string serverDir)
        {
            string nodeModulesDir = Path.Combine(serverDir, "node_modules");
            if (!Directory.Exists(nodeModulesDir))
            {
                return true;
            }

            // 检查关键依赖包是否存在
            string[] requiredPackages = new string[]
            {
                "axios",
                "NeteaseCloudMusicApi"
            };
            foreach (string pkg in requiredPackages)
            {
                string pkgDir = Path.Combine(nodeModulesDir, pkg);
                if (!Directory.Exists(pkgDir))
                {
                    return true;
                }
                // 检查 package.json 是否存在（防止目录残缺）
                string pkgJson = Path.Combine(pkgDir, "package.json");
                if (!File.Exists(pkgJson))
                {
                    return true;
                }
            }
            return false;
        }

        private void ReportStatus(string message)
        {
            if (StatusCallback != null)
            {
                StatusCallback(message);
            }
        }

        /// <summary>
        /// 使用 npm install 安装 Node.js 依赖
        /// </summary>
        private void InstallDependencies(string nodePath, string workingDir)
        {
            // 查找 npm: 通常与 node.exe 同目录
            string nodeDir = Path.GetDirectoryName(nodePath);
            string npmCmd = Path.Combine(nodeDir, "npm.cmd");
            if (!File.Exists(npmCmd))
            {
                // 尝试 npx 目录
                npmCmd = Path.Combine(nodeDir, "npm");
                if (!File.Exists(npmCmd))
                {
                    // 回退：直接用 node 运行 npm-cli.js
                    string npmCli = Path.Combine(nodeDir, "node_modules", "npm", "bin", "npm-cli.js");
                    if (File.Exists(npmCli))
                    {
                        RunProcess(nodePath, string.Format("\"{0}\" install --production", npmCli), workingDir, 60000);
                        return;
                    }
                    // 找不到 npm，跳过（可能依赖已内置）
                    return;
                }
            }

            RunProcess(npmCmd, "install --production", workingDir, 60000);
        }

        /// <summary>
        /// 运行一个同步子进程
        /// </summary>
        private static void RunProcess(string fileName, string arguments, string workingDir, int timeoutMs)
        {
            var si = new ProcessStartInfo();
            si.FileName = fileName;
            si.Arguments = arguments;
            si.WorkingDirectory = workingDir;
            si.UseShellExecute = false;
            si.CreateNoWindow = true;
            si.RedirectStandardOutput = true;
            si.RedirectStandardError = true;

            using (var proc = new Process())
            {
                proc.StartInfo = si;
                proc.Start();
                proc.WaitForExit(timeoutMs);
                if (!proc.HasExited)
                {
                    try { proc.Kill(); } catch { }
                }
            }
        }

        /// <summary>
        /// 停止 Node.js 服务
        /// </summary>
        public void Stop()
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _process.Kill();
                    _process.WaitForExit(3000);
                }
                catch (InvalidOperationException)
                {
                    // 进程已退出
                }
            }

            if (_process != null)
            {
                _process.Dispose();
                _process = null;
            }

            if (_jobObject != null)
            {
                _jobObject.Dispose();
                _jobObject = null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
