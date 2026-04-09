using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using LicenseIssuer;
using Microsoft.Win32;

namespace LicenseIssuerUI
{
    public partial class MainWindow : Window
    {
        private readonly LicenseIssuerService _issuerService;
        private string _privateKeyPath;

        public MainWindow()
        {
            InitializeComponent();
            _issuerService = new LicenseIssuerService();
        }

        // ==================== 事件处理 ====================

        private void BtnBrowseKey_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Title = "选择 RSA 私钥文件";
            dlg.Filter = "PEM 私钥文件 (*.pem)|*.pem|所有文件 (*.*)|*.*";
            dlg.CheckFileExists = true;

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            _privateKeyPath = dlg.FileName;

            // 验证文件内容是否像一个 PEM 私钥
            try
            {
                string content = File.ReadAllText(_privateKeyPath, Encoding.UTF8);
                if (!content.Contains("PRIVATE KEY"))
                {
                    SetStatus("警告：所选文件可能不是有效的 RSA 私钥文件", isError: true);
                }
                else
                {
                    SetStatus(string.Format("已加载私钥：{0}", _privateKeyPath));
                }
            }
            catch (Exception ex)
            {
                SetStatus(string.Format("读取私钥文件失败：{0}", ex.Message), isError: true);
                _privateKeyPath = null;
            }

            // 显示文件名（路径较长时只显示文件名）
            TxtKeyPath.Tag = _privateKeyPath ?? "";
            TxtKeyPath.Text = _privateKeyPath != null ? System.IO.Path.GetFileName(_privateKeyPath) : "";

            UpdateGenerateButton();
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
            string version = TxtVersion.Text.Trim();

            if (string.IsNullOrEmpty(_privateKeyPath) || !File.Exists(_privateKeyPath))
            {
                SetStatus("错误：私钥文件不存在，请重新选择", isError: true);
                return;
            }

            try
            {
                string privateKeyPem = File.ReadAllText(_privateKeyPath, Encoding.UTF8);
                string licenseCode = _issuerService.IssueLicense(privateKeyPem, machineCode, version);

                TxtLicenseCode.Text = licenseCode;
                OutputSection.Visibility = Visibility.Visible;

                SetStatus(string.Format("激活码已生成 — 机器码: {0}...  版本: {1}",
                    machineCode.Length >= 8 ? machineCode.Substring(0, 8) : machineCode, version));
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

            bool hasKey = !string.IsNullOrEmpty(_privateKeyPath) && File.Exists(_privateKeyPath);
            bool hasMachineCode = !string.IsNullOrEmpty(TxtMachineCode.Text.Trim());
            bool hasVersion = !string.IsNullOrEmpty(TxtVersion.Text.Trim());

            BtnGenerate.IsEnabled = hasKey && hasMachineCode && hasVersion;
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
