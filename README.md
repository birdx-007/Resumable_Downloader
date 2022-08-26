# Resumable_Downloader

WPF应用程序项目（使用Prism MVVM框架），基于HTTP协议的文件下载器（支持断点续传）。

开发环境：.NET6.0	VS2022Community

主界面（View）如下，使用VS2022自带xaml设计器设计：

![](./pic/1.png)

主逻辑（ViewModel）位于DownloadViewModel.cs中。

目前支持功能：输入下载链接、下载目录（下载目录可通过“预览”打开文件对话框便捷选择）进行单线程后台下载；下载时若发现下载目录下有同名临时文件（.tmp），则进行续传下载；下载中途暂停下载，继续下载，任意时刻取消下载；实时显示下载进度及下载速度。

## 开发日志

### 2022/8/23

第一个可基本顺利运行的版本完成。

待改进之处：暂停下载功能引入了一些忙等待机制，对性能有一定影响。

```c#
//下载，进度条进度更新
fileStream.Write(buffer, 0, readLen);
ProgressValue += readLen;
//有点忙等待的写法，用于暂停、暂停时取消、取消时的停止下载效果，尚可改进
while (isPausing && isDownloading)
{
    System.Threading.Thread.Sleep(100);
}
readLen = stream.Read(buffer, 0, buffer.Length);
```

### 2022/8/24

修复了发现的一些bug。固定主界面窗体的大小为960*640，并禁用窗体最大化。增加了以下功能：引入异常检测，检测到异常则下载失败；下载失败时可选择重试；下载时若发现下载目录下有同名文件，则询问是否覆写。

~~待改进之处：下载时如果断网，程序不能进行超时检测；下载失败时弹出的消息框不能反映具体原因。~~（见2022/8/26）

### 2022/8/26

增加了条件编译的Debug组件，现在可在VS的Debug模式下（或在Release模式下#define DEBUG）启用Debug信息框，记录并实时显示程序运行过程中的消息以及异常。启用Debug消息框的界面如下：

![](./pic/DEBUG.png)

此外，根据2022/8/24中的改进意见，增加了网络超时检测（阈值设置为10s）。
