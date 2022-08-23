using Microsoft.WindowsAPICodePack.Dialogs;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Documents;

namespace WpfApp1
{
    public class DownloadViewModel : BindableBase //Prism框架
    {
        public DownloadViewModel()
        {
            _downloadstart = null;
            _downloadcancel = null;
        }
        private static readonly int BufferSize = 1024;
        private bool isDownloading = false;//是否正在下载
        private bool isPausing = false;//是否正在暂停
        private string url = "https://cloud.tsinghua.edu.cn/f/336aab4292e04d43a800/?dl=1";//url
        public string Url
        {
            get { return url; }
            set { if (url != value) { url = value; RaisePropertyChanged(); } }
        }
        private string dirPath = "./";//下载路径
        private string tempFilePathName = "./bird.tmp";//临时文件路径-名称
        public string DirPath
        {
            get { return dirPath; }
            set { if (dirPath != value) { dirPath = value; RaisePropertyChanged(); } }
        }
        private Thread? downloadThread = null;//下载用线程
        private bool downloadEnable = true;//下载按键是否有效
        public bool DownloadEnable
        {
            get { return downloadEnable; }
            set { if (downloadEnable != value) { downloadEnable = value; RaisePropertyChanged(); } }
        }
        private bool pauseEnable = false;//暂停-继续按键是否有效
        public bool PauseEnable
        {
            get { return pauseEnable; }
            set { if (pauseEnable != value) { pauseEnable = value; RaisePropertyChanged(); } }
        }
        private string pauseText = "暂停";//暂停-继续按键文字
        public string PauseText
        {
            get { return pauseText; }
            set { if (pauseText != value) { pauseText = value; RaisePropertyChanged(); } }
        }
        private string state = "Idle";//下载状态
        public string State
        {
            get { return state; }
            set { if (state != value) { state = value; RaisePropertyChanged(); } }
        }
        private string speed="0.00 Bps";//下载速度
        public string Speed
        {
            get { return speed; }
            set { if (speed != value) { speed = value; RaisePropertyChanged(); } }
        }
        private long progressValue;//进度条值
        private long lastProgressValue;
        public long ProgressValue
        {
            get { return progressValue; }
            set { if (progressValue != value) { progressValue = value; RaisePropertyChanged(); } }
        }
        private long maxProgressValue = 10;//最大进度条值
        public long MaxProgressValue
        {
            get { return maxProgressValue; }
            set { if (maxProgressValue != value) { maxProgressValue = value; RaisePropertyChanged(); } }
        }
        private CancellationTokenSource? cancellationTokenSource = null; //用于取消操作
        private DelegateCommand? _downloadchoose;
        public DelegateCommand DownloadChoose
        {
            get
            {
                if (_downloadchoose == null) _downloadchoose = new DelegateCommand(() => ChoosePath());
                return _downloadchoose;
            }
        }
        private DelegateCommand? _downloadstart;
        public DelegateCommand DownloadStart
        {
            get
            {
                if (_downloadstart == null) _downloadstart = new DelegateCommand(() => Start());
                return _downloadstart;
            }
        }
        private DelegateCommand? _downloadcancel;
        public DelegateCommand DownloadCancel
        {
            get
            {
                if (_downloadcancel == null) _downloadcancel = new DelegateCommand(() => Cancel());
                return _downloadcancel;
            }
        }
        private DelegateCommand? _downloadpause;
        public DelegateCommand DownloadPause
        {
            get
            {
                if (_downloadpause == null) _downloadpause = new DelegateCommand(() => PauseResume());
                return _downloadpause;
            }
        }
        /// <summary>
        /// 选择下载路径
        /// </summary>
        public void ChoosePath()
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Cancel) { return; }
            DirPath = Path.Combine(dialog.FileName);
        }
        /// <summary>
        /// 下载
        /// </summary>
        public void Download()
        {
            State = "Preparing";
            //向服务端发出请求（检查用），获取文件名及文件总长度
            HttpWebRequest request = WebRequest.Create(Url) as HttpWebRequest;
            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            string fileName = response.ResponseUri.Segments.Last();
            string filePathName = Path.Combine(DirPath, fileName);
            tempFilePathName = filePathName + ".tmp";
            MaxProgressValue = response.ContentLength;
            //检查是否可断点续传
            request = WebRequest.Create(Url) as HttpWebRequest;
            FileMode mode = FileMode.Create;//新文件或追加原文件
            if (ProgressValue > 0 || File.Exists(tempFilePathName))
            {
                FileInfo fn = new FileInfo(tempFilePathName);
                request.AddRange(fn.Length);
                ProgressValue = lastProgressValue = fn.Length;
                mode = FileMode.Append;
            }
            //下载正式开始
            response = request.GetResponse() as HttpWebResponse;
            Stream stream = response.GetResponseStream();
            FileStream fileStream = new FileStream(tempFilePathName, mode);
            byte[] buffer = new byte[BufferSize];
            Timer timer = new Timer(delegate
            {
                double bits = (double)(ProgressValue - lastProgressValue);//下载速度（bits/s）
                lastProgressValue = ProgressValue;
                Speed = calculateSpeed(bits);
            }, null, 0, 1000);
            PauseEnable = true;
            State = "Downloading...";
            int readLen = stream.Read(buffer, 0, buffer.Length);
            while(readLen>0)
            {
                //下载被取消
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    isPausing = false;
                    ProgressValue = 0;
                    MaxProgressValue = 10;
                    State = "Idle";
                    Speed = "0.00 Bps";
                    timer.Dispose();
                    fileStream.Dispose();
                    File.Delete(tempFilePathName);
                    cancellationTokenSource = null;
                    DownloadEnable = true;
                    PauseEnable = false;
                    MessageBox.Show("下载已取消。\nDownload Cancelled.",
                        "Bird's Message",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information,
                        MessageBoxResult.OK);
                    return;
                }
                //下载，进度条进度更新
                fileStream.Write(buffer, 0, readLen);
                ProgressValue += readLen;
                //有点忙等待的写法，用于暂停、暂停时取消、取消时的停止下载效果，尚可改进
                while (isPausing && isDownloading)
                {
                    System.Threading.Thread.Sleep(100);
                }
                readLen = stream.Read(buffer, 0, buffer.Length);
            }
            //下载完成
            isDownloading = false;
            isPausing = false;
            stream.Close();
            fileStream.Close();
            response.Close();
            State = "Idle";
            Speed = "0.00 Bps";
            timer.Dispose();
            File.Move(tempFilePathName, filePathName);
            ProgressValue = lastProgressValue = 0;
            DownloadEnable = true;
            PauseEnable = false;
            MessageBox.Show("下载已完成。\nDownload Complete.",
                        "Bird's Message",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information,
                        MessageBoxResult.OK);
        }
        /// <summary>
        /// 开始下载
        /// </summary>
        public void Start()
        {
            //获取下载文件保存路径
            if (!Directory.Exists(DirPath))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(DirPath);
                directoryInfo.Create();
            }
            DirPath = Path.Combine(DirPath);
            //初始化属性
            ProgressValue = lastProgressValue = 0;
            MaxProgressValue = 10;
            DownloadEnable = false;
            PauseEnable = false;
            //实例化取消模块
            cancellationTokenSource = new CancellationTokenSource();
            //单线程后台下载
            isDownloading = true;
            isPausing = false;
            downloadThread = new Thread(Download);
            downloadThread.IsBackground = true;
            downloadThread.Start();
        }
        /// <summary>
        /// 取消下载
        /// </summary>
        public void Cancel()
        {
            if (State == "Idle")
            {
                return;
            }
            isPausing = true;//暂停下载
            MessageBoxResult result =
                MessageBox.Show("你确定要取消下载吗？\nSeriously?",
                "Bird's Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if(result == MessageBoxResult.No)
            {
                isPausing = false;
                return;
            }
            if (cancellationTokenSource != null) cancellationTokenSource.Cancel();
            isDownloading = false;
        }
        /// <summary>
        /// 暂停/恢复下载
        /// </summary>
        public void PauseResume()
        {
            if(PauseText=="暂停")
            {
                isPausing = true;
                State = "Pausing";
                PauseText = "继续";
            }
            else
            {
                isPausing = false;
                State = "Resuming";
                PauseText = "暂停";
            }
            
        }
        /// <summary>
        /// 根据每秒bit数计算下载速度
        /// </summary>
        /// <param name="bits">每秒bit数</param>
        /// <returns></returns>
        private string calculateSpeed(double bits)
        {
            string speed;
            if (bits < 1024) speed = bits.ToString("0.00") + " Bps";
            else if (bits >= 1024 && bits < 1024 * 1024) speed = (bits / 1024).ToString("0.00") + " Kbps";
            else if (bits >= 1024 * 1024 && bits < 1024 * 1024 * 1024) speed = (bits / (1024 * 1024)).ToString("0.00") + " Mbps";
            else speed = (bits / (1024 * 1024 * 1024)).ToString("0.00") + " Gbps";
            return speed;
        }
    }
}