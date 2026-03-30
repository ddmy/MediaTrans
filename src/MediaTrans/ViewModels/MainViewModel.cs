namespace MediaTrans.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private string _title;
        private string _statusText;

        public MainViewModel()
        {
            _title = "MediaTrans";
            _statusText = "就绪";
        }

        /// <summary>
        /// 窗口标题
        /// </summary>
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value, "Title"); }
        }

        /// <summary>
        /// 状态栏文本
        /// </summary>
        public string StatusText
        {
            get { return _statusText; }
            set { SetProperty(ref _statusText, value, "StatusText"); }
        }
    }
}
