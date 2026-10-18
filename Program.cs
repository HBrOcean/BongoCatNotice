using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using BongoCatNotice.Views;

namespace BongoCatNotice
{
    static class Program
    {
        /// <summary>单实例锁名。重复启动时直接退出，避免桌面上出现多只猫。</summary>
        private const string SingleInstanceMutex = "BongoCatNotice.SingleInstance";

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            using Mutex mutex = new(initiallyOwned: true, SingleInstanceMutex, out bool createdNew);
            if (!createdNew)
                return;

            AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrashLog(e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                WriteCrashLog(e.Exception);
                e.SetObserved();
            };

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex);
                throw;
            }
        }

        /// <summary>
        /// 把未处理异常追加到 exe 同目录的 crash.log。
        /// 桌宠没有控制台，崩溃时用户只能看到窗口消失，留个日志便于排查。
        /// </summary>
        private static void WriteCrashLog(Exception? ex)
        {
            if (ex is null)
                return;

            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "crash.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch
            {
                // 日志写不进去也不能反过来影响程序
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        private static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
