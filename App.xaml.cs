using System;
using System.IO;
using System.Windows;

namespace PureWin
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                LogCrash("DispatcherUnhandledException", e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogCrash("AppDomainUnhandledException", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown exception"));
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogCrash("UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
        }

        private static void LogCrash(string source, Exception? ex)
        {
            if (ex == null) return;
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}]\n{ex}\n----------------------------------------\n";
                
                // 1. Birincil güvenli konum: %LOCALAPPDATA%\PureWin\crash.log
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string pureWinDir = Path.Combine(localAppData, "PureWin");
                if (!Directory.Exists(pureWinDir))
                {
                    Directory.CreateDirectory(pureWinDir);
                }
                string primaryPath = Path.Combine(pureWinDir, "crash.log");
                File.AppendAllText(primaryPath, logEntry);
            }
            catch
            {
                try
                {
                    // 2. İkincil konum: Uygulama çalışma dizini
                    string fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                    File.AppendAllText(fallbackPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}]\n{ex}\n----------------------------------------\n");
                }
                catch { }
            }
        }
    }
}
