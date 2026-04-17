using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using KeyGenerator;
using LicenseIssuer;

namespace LicenseIssuerUI
{
    public partial class MainWindow : Window
    {
        private readonly LicenseIssuerService _issuerService;
        private readonly RsaKeyGenerator _keyGen;
        private string _privateKeyPem;
        private string _assetsDir;

        public MainWindow()
        {
            InitializeComponent();
            _issuerService = new LicenseIssuerService();
            _keyGen = new RsaKeyGenerator();

            Loaded += MainWindow_Loaded;
        }

        // ==================== 初始化 ====================

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeKeys();
        }

        /// <summary>
        /// 自动定位项目根目录并加载/生成密钥
        /// </summary>
        private void InitializeKeys()
        {
            // 向上查找 MediaTrans.sln 定位项目根目录
            string projectRoot = FindProjectRoot();
            if (projectRoot == null)
            {
                TxtKeyStatus.Text = "⚠ 无法定位项目根目录（未找到 MediaTrans.sln）";
                TxtKeyStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0x90, 0x40));
                SetStatus("密钥加载失败：请将工具放在项目目录下运行", isError: true);
                return;
            }

            _assetsDir = Path.Combine(projectRoot, "src", "MediaTrans", "Assets");

            string privateKeyPath = Path.Combine(_assetsDir, "private_key.pem");
            string publicKeyPath = Path.Combine(_assetsDir, "public_key.pem");

            bool hasPrivate = File.Exists(privateKeyPath);
            bool hasPublic = File.Exists(publicKeyPath);

            if (hasPrivate && hasPublic)
            {
                // 两个文件都在，直接加载
                TxtKeyStatus.Text = "✅ 已加载项目密钥";
                TxtKeyStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x82));
                SetStatus("就绪 — 密钥已从项目目录加载");
            }
            else if (!hasPrivate && !hasPublic)
            {
                // 两个文件都不在，首次生成
                try
                {
                    if (!Directory.Exists(_assetsDir))
                    {
                        Directory.CreateDirectory(_assetsDir);
                    }
                    _keyGen.GenerateKeyPair(_assetsDir);
                    TxtKeyStatus.Text = "✅ 密钥对已自动生成（请重新编译项目）";
                    TxtKeyStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x82));
                    SetStatus(string.Format("密钥已生成到：{0}  ★ 请执行 mt.bat build 重新编译后再签发", _assetsDir));
                    MessageBox.Show(
                        string.Format("已自动生成密钥对到：\n{0}\n\n请先执行 mt.bat build Debug 重新编译项目，\n使新公钥嵌入到 MediaTrans.exe 后再签发激活码。", _assetsDir),
                        "密钥已生成",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    TxtKeyStatus.Text = string.Format("⚠ 密钥生成失败：{0}", ex.Message);
                    TxtKeyStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xE0, 0x50, 0x50));
                    SetStatus("密钥生成失败", isError: true);
                    return;
                }
            }
            else if (!hasPrivate && hasPublic)
            {
                // 仅缺私钥：已有公钥已嵌入 exe，不能覆盖，提示用户手动处理
                TxtKeyStatus.Text = "⚠ 缺少私钥文件（public_key.pem 已存在）";
                TxtKeyStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0x50, 0x50));
                SetStatus("错误：缺少 private_key.pem，请从备份恢复或删除 public_key.pem 后重新生成", isError: true);
                MessageBox.Show(
                    string.Format("在 {0} 中找到 public_key.pem 但缺少 private_key.pem。\n\n"
                    + "该公钥可能已嵌入已发布的 exe，不能随意覆盖。\n\n"
                    + "请选择：\n"
                    + "1. 从备份恢复 private_key.pem 到该目录\n"
                    + "2. 若确认需要重新生成，请手动删除 public_key.pem 后重启本工具", _assetsDir),
                    "缺少私钥",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            else
            {
                // 仅缺公钥（private 存在但 public 不在）：补生成公钥
                TxtKeyStatus.Text = "⚠ 缺少公钥文件，异常状态";
                TxtKeyStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0x50, 0x50));
                SetStatus("错误：缺少 public_key.pem，请删除 private_key.pem 后重新生成密钥对", isError: true);
                return;
            }

            // 读取私钥
            try
            {
                _privateKeyPem = File.ReadAllText(privateKeyPath, Encoding.UTF8);
                if (!_privateKeyPem.Contains("PRIVATE KEY"))
                {
                    TxtKeyStatus.Text = "⚠ 私钥文件内容无效";
                    TxtKeyStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xE0, 0x50, 0x50));
                    _privateKeyPem = null;
                }
            }
            catch (Exception ex)
            {
                TxtKeyStatus.Text = string.Format("⚠ 读取私钥失败：{0}", ex.Message);
                TxtKeyStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0x50, 0x50));
                _privateKeyPem = null;
            }

            UpdateGenerateButton();
        }

        /// <summary>
        /// 向上查找包含 MediaTrans.sln 的目录
        /// </summary>
        private static string FindProjectRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            // 最多向上查找 8 级
            for (int i = 0; i < 8; i++)
            {
                if (dir == null)
                {
                    break;
                }
                if (File.Exists(Path.Combine(dir, "MediaTrans.sln")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private void BtnPasteMachineCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = Clipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    TxtMachineCode.Text = text.Trim();
                }
            }
            catch
            {
                // 剪贴板访问失败时静默忽略
            }
        }

        private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateMachineCode();
            UpdateGenerateButton();
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            string machineCode = TxtMachineCode.Text.Trim();

            if (string.IsNullOrEmpty(_privateKeyPem))
            {
                SetStatus("错误：私钥未加载，请重启工具", isError: true);
                return;
            }

            try
            {
                string licenseCode = _issuerService.IssueLicense(_privateKeyPem, machineCode);

                TxtLicenseCode.Text = licenseCode;
                OutputSection.Visibility = Visibility.Visible;

                SetStatus(string.Format("激活码已生成 — 机器码: {0}...",
                    machineCode.Length >= 8 ? machineCode.Substring(0, 8) : machineCode));
            }
            catch (Exception ex)
            {
                OutputSection.Visibility = Visibility.Collapsed;
                SetStatus(string.Format("生成失败：{0}", ex.Message), isError: true);
                MessageBox.Show(
                    string.Format("生成激活码时发生错误：\n\n{0}", ex.Message),
                    "生成失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnCopyLicense_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtLicenseCode.Text))
            {
                return;
            }

            try
            {
                Clipboard.SetText(TxtLicenseCode.Text);
                SetStatus("激活码已复制到剪贴板");
            }
            catch
            {
                SetStatus("复制失败，请手动选中后复制", isError: true);
            }
        }

        // ==================== 内部辅助 ====================

        private void BtnClearActivation_Click(object sender, RoutedEventArgs e)
        {
            string licenseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MediaTrans");
            string licensePath = Path.Combine(licenseDir, "license.dat");

            if (!File.Exists(licensePath))
            {
                SetStatus("本机未找到激活记录");
                MessageBox.Show("本机未找到激活记录。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                "确定要清除本机的激活状态吗？\n\n清除后需要重新输入激活码才能使用专业版功能。",
                "清除激活状态",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                File.Delete(licensePath);
                SetStatus("已清除本机激活状态");
                MessageBox.Show("本机激活状态已清除。\n\n用户下次启动软件时将回到免费版状态。",
                    "清除成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus(string.Format("清除失败：{0}", ex.Message), isError: true);
                MessageBox.Show(string.Format("清除激活状态失败：\n\n{0}", ex.Message),
                    "清除失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ValidateMachineCode()
        {
            if (TxtMachineCodeHint == null)
            {
                return;
            }

            string code = TxtMachineCode.Text.Trim();

            if (string.IsNullOrEmpty(code))
            {
                TxtMachineCodeHint.Visibility = Visibility.Collapsed;
                return;
            }

            bool isHex = IsValidHex(code);
            bool isLen64 = code.Length == 64;

            if (!isLen64 || !isHex)
            {
                string hint = "";
                if (!isLen64)
                {
                    hint = string.Format("当前 {0} 位，标准机器码为 64 位", code.Length);
                }
                else
                {
                    hint = "内容包含无效字符（机器码应为十六进制字符串）";
                }

                TxtMachineCodeHint.Text = "⚠ " + hint;
                TxtMachineCodeHint.Visibility = Visibility.Visible;
            }
            else
            {
                TxtMachineCodeHint.Visibility = Visibility.Collapsed;
            }
        }

        private static bool IsValidHex(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            foreach (char c in s)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }
            return true;
        }

        private void UpdateGenerateButton()
        {
            if (BtnGenerate == null)
            {
                return;
            }

            bool hasKey = !string.IsNullOrEmpty(_privateKeyPem);
            bool hasMachineCode = !string.IsNullOrEmpty(TxtMachineCode.Text.Trim());

            BtnGenerate.IsEnabled = hasKey && hasMachineCode;
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (TxtStatus == null)
            {
                return;
            }
            TxtStatus.Text = message;
            TxtStatus.Foreground = isError
                ? new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0x50, 0x50))
                : new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x9E, 0x9E, 0xB8));
        }
    }
}
