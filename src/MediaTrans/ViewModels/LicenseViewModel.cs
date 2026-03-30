using System;
using System.Windows;
using MediaTrans.Commands;
using MediaTrans.Services;

namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 授权管理 ViewModel — 控制授权对话框的数据绑定与交互
    /// </summary>
    public class LicenseViewModel : ViewModelBase
    {
        private readonly LicenseService _licenseService;
        private readonly MachineCodeService _machineCodeService;

        private string _licenseCode;
        private string _machineCode;
        private string _statusMessage;
        private bool _isActivated;
        private string _activatedVersion;
        private bool _isActivating;

        public LicenseViewModel(LicenseService licenseService, MachineCodeService machineCodeService)
        {
            if (licenseService == null)
            {
                throw new ArgumentNullException("licenseService");
            }
            if (machineCodeService == null)
            {
                throw new ArgumentNullException("machineCodeService");
            }

            _licenseService = licenseService;
            _machineCodeService = machineCodeService;
            _licenseCode = "";
            _statusMessage = "";
            _isActivating = false;

            // 获取机器码
            try
            {
                _machineCode = _machineCodeService.GetMachineCode();
            }
            catch (Exception)
            {
                _machineCode = "获取失败";
            }

            // 初始化授权状态
            RefreshLicenseStatus();

            // 初始化命令
            ActivateCommand = new RelayCommand(OnActivate, CanActivate);
            CopyMachineCodeCommand = new RelayCommand(OnCopyMachineCode);
        }

        /// <summary>
        /// 激活码输入
        /// </summary>
        public string LicenseCode
        {
            get { return _licenseCode; }
            set
            {
                SetProperty(ref _licenseCode, value, "LicenseCode");
                ActivateCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 机器码（只读展示）
        /// </summary>
        public string MachineCode
        {
            get { return _machineCode; }
        }

        /// <summary>
        /// 状态提示消息
        /// </summary>
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value, "StatusMessage"); }
        }

        /// <summary>
        /// 是否已激活
        /// </summary>
        public bool IsActivated
        {
            get { return _isActivated; }
            private set { SetProperty(ref _isActivated, value, "IsActivated"); }
        }

        /// <summary>
        /// 已激活的版本号
        /// </summary>
        public string ActivatedVersion
        {
            get { return _activatedVersion; }
            private set { SetProperty(ref _activatedVersion, value, "ActivatedVersion"); }
        }

        /// <summary>
        /// 是否正在激活中
        /// </summary>
        public bool IsActivating
        {
            get { return _isActivating; }
            private set
            {
                SetProperty(ref _isActivating, value, "IsActivating");
                ActivateCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 激活命令
        /// </summary>
        public RelayCommand ActivateCommand { get; private set; }

        /// <summary>
        /// 复制机器码命令
        /// </summary>
        public RelayCommand CopyMachineCodeCommand { get; private set; }

        /// <summary>
        /// 激活成功事件（供宿主窗口关闭对话框使用）
        /// </summary>
        public event EventHandler ActivationSucceeded;

        /// <summary>
        /// 执行激活
        /// </summary>
        private void OnActivate(object parameter)
        {
            if (string.IsNullOrEmpty(_licenseCode))
            {
                StatusMessage = "请输入激活码";
                return;
            }

            IsActivating = true;
            StatusMessage = "正在验证激活码...";

            bool result = _licenseService.Activate(_licenseCode.Trim());

            if (result)
            {
                RefreshLicenseStatus();
                StatusMessage = "激活成功！";
                var handler = ActivationSucceeded;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            else
            {
                StatusMessage = "激活失败：激活码无效或与本机不匹配";
            }

            IsActivating = false;
        }

        /// <summary>
        /// 判断是否可以执行激活
        /// </summary>
        private bool CanActivate(object parameter)
        {
            return !_isActivating && !string.IsNullOrEmpty(_licenseCode);
        }

        /// <summary>
        /// 复制机器码到剪贴板
        /// </summary>
        private void OnCopyMachineCode(object parameter)
        {
            if (!string.IsNullOrEmpty(_machineCode))
            {
                try
                {
                    Clipboard.SetText(_machineCode);
                    StatusMessage = "机器码已复制到剪贴板";
                }
                catch (Exception)
                {
                    StatusMessage = "复制失败，请手动复制";
                }
            }
        }

        /// <summary>
        /// 刷新授权状态
        /// </summary>
        private void RefreshLicenseStatus()
        {
            IsActivated = _licenseService.IsActivated;
            ActivatedVersion = _licenseService.ActivatedVersion;
        }
    }
}
