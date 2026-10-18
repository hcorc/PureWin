using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using PureWin.Models;

namespace PureWin.Services
{
    public class ServiceSnapshot
    {
        public string ServiceName { get; set; } = string.Empty;
        public int OriginalStart { get; set; }
        public bool HasDelayedAutostart { get; set; }
        public int? DelayedAutostart { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public static class ServiceManagementService
    {
        private const string ServicesKeyPath = @"SYSTEM\CurrentControlSet\Services";

        // Antivirüs veya Windows Güvenlik bileşenlerini asla durdurmaya veya kapatmaya çalışma (Defender Tamper Protection tetiklemez)
        // Ayrıca Windows 11 Başlat Arama ve klavye girişini bozan TextInputManagementService servisini kalıcı koru
        private static readonly HashSet<string> ProtectedSecurityServices = new(StringComparer.OrdinalIgnoreCase)
        {
            "Sense", "WinDefend", "WdNisSvc", "SecurityHealthService", "wscsvc", "MpsSvc", "SgrmBroker",
            "TextInputManagementService", "TabletInputService"
        };

        #region Win32 Native Service APIs (Temiz, Hızlı ve Komut Satırı Bağımsız)
        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManager(string? lpMachineName, string? lpDatabaseName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig(
            IntPtr hService,
            uint nServiceType,
            uint nStartType,
            uint nErrorControl,
            string? lpBinaryPathName,
            string? lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string? lpDependencies,
            string? lpServiceStartName,
            string? lpPassword,
            string? lpDisplayName);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ControlService(IntPtr hService, uint dwControl, ref SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", EntryPoint = "StartServiceW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, string[]? lpServiceArgVectors);

        [DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle")]
        private static extern int CloseServiceHandle(IntPtr hSCObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        private const uint SC_MANAGER_CONNECT = 0x0001;
        private const uint SERVICE_CHANGE_CONFIG = 0x0002;
        private const uint SERVICE_STOP = 0x0020;
        private const uint SERVICE_START = 0x0010;
        private const uint SERVICE_QUERY_STATUS = 0x0004;
        private const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        private const uint SERVICE_CONTROL_STOP = 0x00000001;

        private static bool SetServiceConfigWin32(string serviceName, uint startType, bool disable)
        {
            IntPtr hSCM = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (hSCM == IntPtr.Zero) return false;

            try
            {
                uint desiredAccess = SERVICE_CHANGE_CONFIG | SERVICE_QUERY_STATUS;
                if (disable) desiredAccess |= SERVICE_STOP;
                else desiredAccess |= SERVICE_START;

                IntPtr hService = OpenService(hSCM, serviceName, desiredAccess);
                if (hService == IntPtr.Zero) return false;

                try
                {
                    bool success = ChangeServiceConfig(
                        hService,
                        SERVICE_NO_CHANGE,
                        startType,
                        SERVICE_NO_CHANGE,
                        null, null, IntPtr.Zero, null, null, null, null);

                    if (disable)
                    {
                        var status = new SERVICE_STATUS();
                        ControlService(hService, SERVICE_CONTROL_STOP, ref status);
                    }
                    else
                    {
                        StartService(hService, 0, null);
                    }

                    return success;
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(hSCM);
            }
        }
        #endregion

        /// <summary>
        /// Sistemde o an çalışan tüm aktif servislerin adlarını tek bir komutla ultra-hızlı (~20ms) döndürür.
        /// </summary>
        public static HashSet<string> GetRunningServicesSnapshot()
        {
            var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "query type= service",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string? line;
                    while ((line = proc.StandardOutput.ReadLine()) != null)
                    {
                        if (line.StartsWith("SERVICE_NAME:", StringComparison.OrdinalIgnoreCase))
                        {
                            string name = line.Substring(13).Trim();
                            if (!string.IsNullOrEmpty(name))
                                running.Add(name);
                        }
                    }
                    proc.WaitForExit(3000);
                }
            }
            catch { }
            return running;
        }

        /// <summary>
        /// Servisin sistemdeki canlı durumunu (Çalışıyor mu / Devre dışı mı) kontrol eder.
        /// </summary>
        public static void CheckServiceStatus(ServiceItem item, HashSet<string>? runningCache = null)
        {
            try
            {
                string targetService = ResolveActualServiceName(item);
                if (string.IsNullOrEmpty(targetService) || !DoesServiceExist(targetService))
                {
                    item.IsMissingOnSystem = true;
                    item.StatusText = "● Sistemde Mevcut Değil";
                    item.StatusColor = "#64748B"; // Koyu gri
                    item.IsCurrentDisabled = true;
                    item.IsRunning = false;
                    item.IsSelected = false;
                    return;
                }

                item.IsMissingOnSystem = false;
                int startupType = 3; // Varsayılan: Elle (Manual)
                using (var baseKey = Registry.LocalMachine.OpenSubKey($@"{ServicesKeyPath}\{targetService}", false))
                {
                    if (baseKey != null)
                    {
                        var val = baseKey.GetValue("Start");
                        if (val is int intVal) startupType = intVal;
                        else if (val != null && int.TryParse(val.ToString(), out int pVal)) startupType = pVal;
                    }
                }

                bool isRunning = runningCache != null 
                    ? runningCache.Contains(targetService) 
                    : IsSingleServiceRunning(targetService);

                item.IsRunning = isRunning && startupType != 4;

                if (startupType == 4)
                {
                    item.StatusText = isRunning ? "● Kapatılıyor (Devre Dışı)" : "● Devre Dışı";
                    item.StatusColor = "#64748B"; // Slate Gri (Açık ve Koyu temada yüksek kontrast)
                    item.IsCurrentDisabled = true;
                }
                else if (isRunning)
                {
                    string modeStr = startupType == 2 ? "Otomatik" : "Elle";
                    item.StatusText = $"● Çalışıyor ({modeStr})";
                    item.StatusColor = "#10B981"; // Yeşil
                    item.IsCurrentDisabled = false;
                }
                else
                {
                    string modeStr = startupType == 2 ? "Otomatik" : "Elle";
                    item.StatusText = $"● Durduruldu ({modeStr})";
                    item.StatusColor = "#F59E0B"; // Amber / Sarı
                    item.IsCurrentDisabled = false;
                }

                item.IsSelected = false;
            }
            catch
            {
                item.IsMissingOnSystem = false;
                item.StatusText = "● Durum Alınamadı";
                item.StatusColor = "#64748B";
                item.IsCurrentDisabled = false;
                item.IsRunning = false;
                item.IsSelected = false;
            }
        }

        private static bool IsSingleServiceRunning(string serviceName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"query \"{serviceName}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(2000);
                    return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        private static readonly string ServiceSnapshotsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PureWin",
            "ServiceSnapshots");

        private static string GetServiceSnapshotFilePath(string serviceName) =>
            Path.Combine(ServiceSnapshotsDir, $"{serviceName}.json");

        private static void SaveServiceSnapshot(string serviceName)
        {
            try
            {
                if (!Directory.Exists(ServiceSnapshotsDir))
                {
                    Directory.CreateDirectory(ServiceSnapshotsDir);
                }

                string filePath = GetServiceSnapshotFilePath(serviceName);
                if (File.Exists(filePath))
                {
                    return; // Önceden alınmış orijinal snapshot korunur
                }

                using var key = Registry.LocalMachine.OpenSubKey($@"{ServicesKeyPath}\{serviceName}", false);
                if (key == null) return;

                var startVal = key.GetValue("Start");
                int start = 3;
                if (startVal is int iVal) start = iVal;
                else if (startVal != null && int.TryParse(startVal.ToString(), out int pVal)) start = pVal;

                if (start == 4) return; // Zaten kapalıysa snapshot alma

                var delayedVal = key.GetValue("DelayedAutostart");
                bool hasDelayed = delayedVal != null;
                int? delayedInt = delayedVal is int d ? d : (delayedVal != null && int.TryParse(delayedVal.ToString(), out int pd) ? pd : null);

                var snapshot = new ServiceSnapshot
                {
                    ServiceName = serviceName,
                    OriginalStart = start,
                    HasDelayedAutostart = hasDelayed,
                    DelayedAutostart = delayedInt,
                    Timestamp = DateTime.Now
                };

                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch { }
        }

        private static ServiceSnapshot? LoadAndRemoveServiceSnapshot(string serviceName)
        {
            try
            {
                string filePath = GetServiceSnapshotFilePath(serviceName);
                if (!File.Exists(filePath)) return null;

                string json = File.ReadAllText(filePath, Encoding.UTF8);
                var snapshot = JsonSerializer.Deserialize<ServiceSnapshot>(json);
                try { File.Delete(filePath); } catch { }
                return snapshot;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Servisin durumunu ayarlar: disable=true ise Start=4 yapıp servisi durdurur;
        /// disable=false ise orijinal başlangıç türüne döndürür.
        /// </summary>
        public static async Task SetServiceStateAsync(ServiceItem item, bool disable, int? delayedAutostart = null, bool hasDelayedSnapshot = false)
        {
            if (item == null) return;

            // DiagTrack'e özel: işlem öncesi AutoLogger dinleyici tweak durumunu yakala
            string resolvedName = ResolveActualServiceName(item);
            bool isDiagTrack = !string.IsNullOrEmpty(resolvedName) && resolvedName.Equals("DiagTrack", StringComparison.OrdinalIgnoreCase);
            bool listenerWasApplied = isDiagTrack && await DiagtrackService.IsDisabledAsync();

            await Task.Run(() =>
            {
                string targetService = ResolveActualServiceName(item);
                if (string.IsNullOrEmpty(targetService))
                    return;

                // Korumalı güvenlik ve giriş servislerinin kapatılmasını engelle, ancak kapalıysa geri açılmasına izin ver
                if (disable && ProtectedSecurityServices.Contains(targetService))
                    return;

                try
                {
                    if (disable)
                    {
                        SaveServiceSnapshot(targetService);
                    }

                    ServiceSnapshot? serviceSnapshot = null;
                    if (!disable)
                    {
                        serviceSnapshot = LoadAndRemoveServiceSnapshot(targetService);
                    }

                    int targetStart;
                    if (disable)
                    {
                        targetStart = 4;
                    }
                    else if (serviceSnapshot != null)
                    {
                        targetStart = serviceSnapshot.OriginalStart;
                    }
                    else
                    {
                        targetStart = item.OriginalStartupType > 0 ? item.OriginalStartupType : 3;
                    }

                    string startMode = targetStart switch
                    {
                        2 => "auto",
                        3 => "demand",
                        4 => "disabled",
                        _ => "demand"
                    };

                    // 1. Doğrudan ultra-hızlı Win32 Service API ile başlangıç türünü ve durdurmayı uygula (~0.5ms)
                    bool win32Success = SetServiceConfigWin32(targetService, (uint)targetStart, disable);

                    // 2. Kayıt defterinde Start değerini atomic güncelle (~0.05ms)
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey($@"{ServicesKeyPath}\{targetService}", true);
                        if (key != null)
                        {
                            key.SetValue("Start", targetStart, RegistryValueKind.DWord);
                            if (!disable)
                            {
                                if (serviceSnapshot != null)
                                {
                                    if (serviceSnapshot.HasDelayedAutostart && serviceSnapshot.DelayedAutostart.HasValue)
                                    {
                                        key.SetValue("DelayedAutostart", serviceSnapshot.DelayedAutostart.Value, RegistryValueKind.DWord);
                                    }
                                    else
                                    {
                                        try { key.DeleteValue("DelayedAutostart", false); } catch { }
                                    }
                                }
                                else if (hasDelayedSnapshot)
                                {
                                    if (delayedAutostart.HasValue)
                                    {
                                        key.SetValue("DelayedAutostart", delayedAutostart.Value, RegistryValueKind.DWord);
                                    }
                                    else
                                    {
                                        try { key.DeleteValue("DelayedAutostart", false); } catch { }
                                    }
                                }
                                else if (targetService.Equals("WSearch", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Snapshot yoksa Windows varsayılanı olan DelayedAutostart = 1 değerini yaz
                                    key.SetValue("DelayedAutostart", 1, RegistryValueKind.DWord);
                                }
                            }
                        }
                    }
                    catch { }

                    // 3. Servis yeniden açılıyorsa (enable/undo), servisin derhal çalışmaya başlamasını sağla
                    if (!disable)
                    {
                        try
                        {
                            var psiStart = new ProcessStartInfo
                            {
                                FileName = "sc.exe",
                                Arguments = $"start \"{targetService}\"",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            using var pStart = Process.Start(psiStart);
                            pStart?.WaitForExit(2000);
                        }
                        catch { }
                    }

                    // 4. Fallback: Win32 API başarısız olduysa veya özel izinli servis (SharedAccess) ise harici süreçleri devreye sok
                    if (!win32Success || targetService.Equals("SharedAccess", StringComparison.OrdinalIgnoreCase))
                    {
                        // sc.exe config ile dene
                        try
                        {
                            var psiConfig = new ProcessStartInfo
                            {
                                FileName = "sc.exe",
                                Arguments = $"config \"{targetService}\" start= {startMode}",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            using var pConfig = Process.Start(psiConfig);
                            pConfig?.WaitForExit(2000);
                        }
                        catch { }

                        // sc.exe stop / start ile dene
                        try
                        {
                            string scAction = disable ? "stop" : "start";
                            var psiAction = new ProcessStartInfo
                            {
                                FileName = "sc.exe",
                                Arguments = $"{scAction} \"{targetService}\"",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            using var pAction = Process.Start(psiAction);
                            pAction?.WaitForExit(2000);
                        }
                        catch { }

                        // Gerekirse son çare PowerShell
                        try
                        {
                            string psMode = targetStart == 4 ? "Disabled" : (targetStart == 2 ? "Automatic" : "Manual");
                            string psCmd = $"Set-Service -Name '{targetService}' -StartupType {psMode} -ErrorAction SilentlyContinue; ";
                            if (disable)
                                psCmd += $"Stop-Service -Name '{targetService}' -Force -ErrorAction SilentlyContinue;";

                            var psiPs = new ProcessStartInfo
                            {
                                FileName = "powershell.exe",
                                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{psCmd}\"",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            using var pPs = Process.Start(psiPs);
                            pPs?.WaitForExit(3000);
                        }
                        catch { }
                    }

                    // Metin Giriş / Tablet Hizmeti yeniden açıldığında Windows 11 Başlat Arama'nın hemen çalışması için ctfmon'u tetikle
                    if (!disable && (targetService.Equals("TextInputManagementService", StringComparison.OrdinalIgnoreCase) ||
                                     targetService.Equals("TabletInputService", StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ctfmon.exe"),
                                UseShellExecute = true
                            });

                            using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                            runKey?.SetValue("ctfmon", @"C:\Windows\System32\ctfmon.exe");
                        }
                        catch { }
                    }

                    // 6. Canlı durumu anında doğrula ve servis RUNNING olana kadar asenkron poll et
                    CheckServiceStatus(item);
                    if (!disable && !item.IsRunning)
                    {
                        // Servis geçiş aşamasında (START_PENDING) olabilir; en fazla ~10 saniye boyunca poll et
                        for (int i = 0; i < 20; i++)
                        {
                            Thread.Sleep(500);
                            CheckServiceStatus(item);
                            if (item.IsRunning)
                                break;
                        }
                    }
                }
                catch { }
            });

            // DiagTrack durdurulurken/başlatılırken Windows AutoLogger Start'ı 1'e geri yazar (SYSTEM/svchost, durma anında).
            // Kullanıcı listener tweak'ini önceden uygulamışsa, işlemin sonunda niyetini koru ve yeniden sabitle.
            if (isDiagTrack && listenerWasApplied)
            {
                if (disable)
                {
                    // Durdurmanın gerçekten tamamlanmasını bekle (yazma svchost durma/teardown anında gerçekleşir);
                    // Servis ve sc.exe süreci durana kadar poll et (maksimum ~15 saniye)
                    for (int i = 0; i < 30 && (item.IsRunning || IsSingleServiceRunning(resolvedName)); i++)
                    {
                        await Task.Delay(500);
                        CheckServiceStatus(item);
                    }
                    await Task.Delay(300); // Geç gelebilecek registry yazmasına karşı emniyet payı
                }

                if (!(await DiagtrackService.IsDisabledAsync()))
                {
                    await DiagtrackService.SetStateAsync(disable: true);
                }
            }
        }

        private static string ResolveActualServiceName(ServiceItem item)
        {
            if (DoesServiceExist(item.ServiceName))
                return item.ServiceName;

            if (!string.IsNullOrEmpty(item.FallbackServiceName) && DoesServiceExist(item.FallbackServiceName))
                return item.FallbackServiceName;

            return item.ServiceName;
        }

        private static bool DoesServiceExist(string name)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"{ServicesKeyPath}\{name}", false);
                return key != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
