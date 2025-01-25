using Microsoft.UI.Xaml;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using WinRT.Interop;

namespace FangJia.Helpers
{
    public static partial class PipeHelper
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string PipeName = "FangJia";
        private static CancellationTokenSource? _cancellationTokenSource;

        public static void StartApp(string mode)
        {
            var connected = false;
            var retries = 20;

            while (!connected && retries > 0)
            {
                try
                {
                    Logger.Info("尝试连接已有实例...");
                    using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                    client.Connect(1000);

                    using var writer = new StreamWriter(client);
                    writer.WriteLine(mode);
                    writer.Flush();

                    connected = true;
                    Logger.Info("成功连接到已有实例，并发送消息。");
                }
                catch (Exception ex)
                {
                    retries--;
                    Logger.Warn($"连接失败，剩余重试次数: {retries}");
                    Logger.Error($"连接已有实例时发生异常：{ex.Message}", ex);

                    if (retries > 0)
                        Thread.Sleep(500);
                }
            }

            if (!connected)
            {
                Logger.Error("多次尝试后仍无法连接到已有实例，操作失败。");
            }
        }

        public static void RestartApp()
        {
            Logger.Info("准备重启应用程序...");

            // 获取当前应用程序路径
            var appPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(appPath))
            {
                Logger.Error("无法获取应用程序路径，重启操作失败。");
                return;
            }

            try
            {
                // 启动新实例
                Logger.Info("启动新实例...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = appPath,
                    Arguments = "ReStart",
                    UseShellExecute = true
                };
                Process.Start(startInfo);

                Logger.Info("新实例已启动，当前实例即将退出。");

                // 退出当前实例
                Environment.Exit(0); // 适配 WinUI3 的退出方式
            }
            catch (Exception ex)
            {
                Logger.Error($"重启应用程序时发生异常：{ex.Message}", ex);
            }
        }


        public static void StartPipeServer()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            Logger.Info("管道服务器已启动，等待连接...");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message);

                    server.WaitForConnection();
                    Logger.Info("客户端已连接到管道。");

                    using var reader = new StreamReader(server);
                    var message = reader.ReadLine();

                    switch (message)
                    {
                        case "SHOW":
                            Logger.Info("接收到 SHOW 消息，准备显示主窗口。");
                            Console.WriteLine(@"接收到 SHOW 消息，准备显示主窗口。");
                            ShowMainWindow();
                            ClearStaleProcesses("FangJia");
                            break;
                        case "RESTART":
                            Logger.Info("接收到 RESTART 消息，关闭当前实例，以应用新实例。");
                            Application.Current.Exit();
                            break;
                    }

                    server.Disconnect();
                    Logger.Info("管道连接已断开。");
                }
                catch (ObjectDisposedException ex)
                {
                    Logger.Warn($"管道服务器已关闭：{ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"管道服务器发生异常：{ex.Message}", ex);
                }
            }

            Logger.Info("管道服务器已停止运行。");
        }

        public static void StopPipeServer()
        {
            try
            {
                Logger.Info("正在停止管道服务器...");
                _cancellationTokenSource?.Cancel();
                Logger.Info("管道服务器已成功停止。");
            }
            catch (Exception ex)
            {
                Logger.Error($"停止管道服务器时发生异常：{ex.Message}", ex);
            }
        }
        public static void ClearStaleProcesses(string processName)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var staleProcesses = Process.GetProcessesByName(processName)
                    .Where(p => p.Id != currentProcess.Id); // 排除当前进程

                foreach (var process in staleProcesses)
                {
                    try
                    {
                        process.Kill(); // 强制终止进程
                        process.WaitForExit(); // 等待进程退出
                        Console.WriteLine($@"已终止残留进程: {process.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($@"无法终止进程 {process.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"清除残留进程时发生异常: {ex.Message}");
            }
        }
        private static void ShowMainWindow()
        {
            if (App.MainDispatcherQueue != null)
            {
                App.MainDispatcherQueue.TryEnqueue(() =>
                {
                    if (App.Window == null)
                    {
                        Logger.Warn("无法找到主窗口实例，无法激活。");
                        return;
                    }

                    App.Window.Activate();
                    var appWindow = WindowHelper.GetAppWindow(App.Window);
                    appWindow.Show();
                    SetForegroundWindow(WindowNative.GetWindowHandle(App.Window));
                });
            }
            else
            {
                // 如果主线程 DispatcherQueue 不可用，记录日志或处理错误
                Logger.Warn("主线程 DispatcherQueue 不可用！");
            }
        }

        /// <summary>
        /// 将窗口置于最前。
        /// </summary>
        /// <param name="hWnd">窗口句柄。</param>
        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial void SetForegroundWindow(IntPtr hWnd);
    }
}