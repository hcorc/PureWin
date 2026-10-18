using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PureWin.Services
{
    public static class ScheduledTaskService
    {
        // 17 Adet %100 Risksiz ve Temel Telemetri Görevi
        public static readonly List<(string Path, string Name)> CoreTelemetryTasks = new()
        {
            // 1. Temel Sistem Telemetrisi (Raphire Temeli)
            (@"\Microsoft\Windows\Application Experience\", "Microsoft Compatibility Appraiser"),
            (@"\Microsoft\Windows\Application Experience\", "Microsoft Compatibility Appraiser Exp"),
            (@"\Microsoft\Windows\Application Experience\", "ProgramDataUpdater"),
            (@"\Microsoft\Windows\Application Experience\", "StartupAppTask"),
            (@"\Microsoft\Windows\Customer Experience Improvement Program\", "Consolidator"),
            (@"\Microsoft\Windows\Customer Experience Improvement Program\", "UsbCeip"),
            (@"\Microsoft\Windows\DiskDiagnostic\", "Microsoft-Windows-DiskDiagnosticDataCollector"),
            (@"\Microsoft\Windows\DiskDiagnostic\", "Microsoft-Windows-DiskDiagnosticResolver"),
            (@"\Microsoft\Windows\Autochk\", "Proxy"),

            // 2. Haritalar, SIUF Geri Bildirim ve Hata Raporlama
            (@"\Microsoft\Windows\Maps\", "MapsToastTask"),
            (@"\Microsoft\Windows\Maps\", "MapsUpdateTask"),
            (@"\Microsoft\Windows\Feedback\Siuf\", "DmClient"),
            (@"\Microsoft\Windows\Feedback\Siuf\", "DmClientOnScenarioDownload"),
            (@"\Microsoft\Windows\Windows Error Reporting\", "QueueReporting"),

            // 3. Windows 11 Yeni Nesil Telemetri
            (@"\Microsoft\Windows\Sustainability\", "SustainabilityTelemetry"),
            (@"\Microsoft\Windows\Sustainability\", "PowerGridForecastTask"),
            (@"\Microsoft\Windows\PerformanceTrace\", "ShowFeedbackToast")
        };

        // 4 Adet İsteğe Bağlı Telemetri Görevi (Edge & Aile Güvenliği)
        public static readonly List<(string Path, string Name)> OptionalTelemetryTasks = new()
        {
            (@"\", "MicrosoftEdgeUpdateTaskMachineCore"),
            (@"\", "MicrosoftEdgeUpdateTaskMachineUA"),
            (@"\Microsoft\Windows\Shell\", "FamilySafetyMonitor"),
            (@"\Microsoft\Windows\Shell\", "FamilySafetyRefreshTask")
        };

        private static string GetFullTaskName(string path, string name)
        {
            if (string.IsNullOrEmpty(path) || path == @"\")
                return $@"\{name.TrimStart('\\')}";
            return $@"{path.TrimEnd('\\')}\{name.TrimStart('\\')}";
        }

        /// <summary>
        /// Görevlerin sistemde şu an devre dışı (Disabled) olup olmadığını sorgular (hızlı yerel schtasks.exe)
        /// </summary>
        public static async Task<bool> AreTasksDisabledAsync(IEnumerable<(string Path, string Name)> tasks)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var taskList = tasks.ToList();
                    if (taskList.Count == 0) return false;

                    // İlk 4 görevi hızlıca yerel schtasks ile sorgula (5ms sürer)
                    var sample = taskList.Take(4).ToList();
                    int disabledCount = 0;
                    int checkedCount = 0;

                    foreach (var t in sample)
                    {
                        string fullTaskPath = GetFullTaskName(t.Path, t.Name);
                        var psi = new ProcessStartInfo
                        {
                            FileName = "schtasks.exe",
                            Arguments = $"/query /tn \"{fullTaskPath}\" /fo CSV /nh",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };

                        using var proc = Process.Start(psi);
                        if (proc != null)
                        {
                            string output = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit(1000);
                            if (proc.ExitCode == 0)
                            {
                                checkedCount++;
                                if (output.Contains("Disabled", StringComparison.OrdinalIgnoreCase) ||
                                    output.Contains("Devre Dışı", StringComparison.OrdinalIgnoreCase) ||
                                    output.Contains("Devre d", StringComparison.OrdinalIgnoreCase))
                                {
                                    disabledCount++;
                                }
                            }
                        }
                    }

                    if (checkedCount == 0) return false;
                    return disabledCount >= (checkedCount / 2) + (checkedCount % 2);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Scheduled task check error: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Görevleri toplu olarak Devre Dışı Bırakır (disable == true) veya Yeniden Etkinleştirir (disable == false)
        /// Hızlı schtasks.exe paralel çağrısı (15sn PowerShell timeout'unu tamamen çözer)
        /// </summary>
        public static async Task<bool> SetTasksStateAsync(IEnumerable<(string Path, string Name)> tasks, bool disable)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string flag = disable ? "/disable" : "/enable";
                    var taskList = tasks.ToList();
                    if (taskList.Count == 0) return true;

                    Parallel.ForEach(taskList, new ParallelOptions { MaxDegreeOfParallelism = 4 }, t =>
                    {
                        try
                        {
                            string fullTaskPath = GetFullTaskName(t.Path, t.Name);
                            var psi = new ProcessStartInfo
                            {
                                FileName = "schtasks.exe",
                                Arguments = $"/change /tn \"{fullTaskPath}\" {flag}",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            using var proc = Process.Start(psi);
                            proc?.WaitForExit(2000);
                        }
                        catch { }
                    });

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SetTasksStateAsync error: {ex.Message}");
                    return false;
                }
            });
        }
    }
}
