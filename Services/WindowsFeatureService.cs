using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PureWin.Services
{
    public static class WindowsFeatureService
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (bool isEnabled, DateTime timestamp)> FeatureCache = new(StringComparer.OrdinalIgnoreCase);

        public static async Task<bool> IsFeatureEnabledAsync(string featureName)
        {
            if (FeatureCache.TryGetValue(featureName, out var cached) && (DateTime.UtcNow - cached.timestamp).TotalSeconds < 30)
            {
                return cached.isEnabled;
            }

            return await Task.Run(() =>
            {
                try
                {
                    // 1. Sandbox ve WSL için anında sistem kontrolü (0 milisaniye)
                    if (featureName.Equals("Containers-DisposableClientVM", StringComparison.OrdinalIgnoreCase))
                    {
                        string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
                        if (System.IO.File.Exists(System.IO.Path.Combine(sys, "WindowsSandbox.exe")))
                        {
                            FeatureCache[featureName] = (true, DateTime.UtcNow);
                            return true;
                        }
                    }
                    else if (featureName.Equals("Microsoft-Windows-Subsystem-Linux", StringComparison.OrdinalIgnoreCase))
                    {
                        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\LxssManager");
                        if (key != null)
                        {
                            FeatureCache[featureName] = (true, DateTime.UtcNow);
                            return true;
                        }
                    }

                    // 2. DISM kontrolü (hızlı 2sn timeout)
                    var psi = new ProcessStartInfo
                    {
                        FileName = "dism.exe",
                        Arguments = $"/Online /Get-FeatureInfo /FeatureName:{featureName}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using var process = Process.Start(psi);
                    if (process == null) return false;

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(2000);

                    // Hem Türkçe (Etkin) hem İngilizce (Enabled) sistem kontrolü
                    bool result = output.Contains("State : Enabled", StringComparison.OrdinalIgnoreCase) ||
                                  output.Contains("Durum : Etkin", StringComparison.OrdinalIgnoreCase) ||
                                  output.Contains("Durum: Etkin", StringComparison.OrdinalIgnoreCase);

                    FeatureCache[featureName] = (result, DateTime.UtcNow);
                    return result;
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task<bool> SetFeatureStateAsync(string featureName, bool enable)
        {
            FeatureCache.TryRemove(featureName, out _);
            return await Task.Run(() =>
            {
                try
                {
                    string action = enable ? "Enable-Feature" : "Disable-Feature";
                    var psi = new ProcessStartInfo
                    {
                        FileName = "dism.exe",
                        Arguments = $"/Online /{action} /FeatureName:{featureName} /NoRestart",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using var process = Process.Start(psi);
                    if (process == null) return false;

                    process.WaitForExit(60000); // Özellik açma biraz sürebilir
                    // 0: Başarılı, 3010: Başarılı (Yeniden Başlatma Gerekli - ERROR_SUCCESS_REBOOT_REQUIRED)
                    return process.ExitCode == 0 || process.ExitCode == 3010;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
