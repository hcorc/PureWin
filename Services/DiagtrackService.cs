using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PureWin.Services
{
    public static class DiagtrackService
    {
        private const string SubKey = @"SYSTEM\CurrentControlSet\Control\WMI\AutoLogger\Diagtrack-Listener";
        private const string FullRegKey = @"HKLM\SYSTEM\CurrentControlSet\Control\WMI\AutoLogger\Diagtrack-Listener";
        private const string FullRegKey001 = @"HKLM\SYSTEM\ControlSet001\Control\WMI\AutoLogger\Diagtrack-Listener";

        /// <summary>
        /// DiagTrack AutoLogger dinleyicisinin kapalı (Start = 0) olup olmadığını kontrol eder.
        /// </summary>
        public static async Task<bool> IsDisabledAsync()
        {
            return await Task.Run(() =>
            {
                // 1. Doğrudan .NET Registry ile oku
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(SubKey, false);
                    if (key != null)
                    {
                        var val = key.GetValue("Start");
                        if (val is int intVal) return intVal == 0;
                        if (val != null && int.TryParse(val.ToString(), out int parsed)) return parsed == 0;
                    }
                }
                catch { }

                // 2. reg.exe query ile kontrol et
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"query \"{FullRegKey}\" /v Start",
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
                        proc.WaitForExit(2000);
                        if (output.Contains("0x0")) return true;
                        if (output.Contains("0x1")) return false;
                    }
                }
                catch { }

                return false;
            });
        }

        /// <summary>
        /// DiagTrack AutoLogger dinleyicisini kapatır (0) veya varsayılana açar (1).
        /// Gerekirse Windows 11 erişim kısıtlamasını aşmak için SYSTEM köprüsünü kullanır.
        /// </summary>
        public static async Task<bool> SetStateAsync(bool disable)
        {
            int targetVal = disable ? 0 : 1;

            return await Task.Run(() =>
            {
                bool directSuccess = false;

                // 1. .reg dosyasını içe aktarmayı dene
                string regFile = disable ? "Disable_Diagtrack_Listener.reg" : "Enable_Diagtrack_Listener.reg";
                RegistryService.ImportRegFile(regFile, isUndo: !disable);

                // 2. reg.exe add ile doğrudan dene
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"add \"{FullRegKey}\" /v Start /t REG_DWORD /d {targetVal} /f",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(2000);
                    directSuccess = proc?.ExitCode == 0;

                    // ControlSet001 için de yaz
                    var psi001 = new ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"add \"{FullRegKey001}\" /v Start /t REG_DWORD /d {targetVal} /f",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var proc001 = Process.Start(psi001);
                    proc001?.WaitForExit(2000);
                }
                catch { }

                // 3. Erişim engellendiyse (Windows 11 korumalı anahtar): SYSTEM köprüsünü devreye sok
                if (!directSuccess)
                {
                    ExecuteAsSystem(targetVal);
                }

                return true;
            });
        }

        private static void ExecuteAsSystem(int targetVal)
        {
            // 1. Önce Yönetici (Administrator) yetkisiyle doğrudan ACL izinlerini genişletmeyi dene (Disk'e dosya yazılmaz)
            try
            {
                var psiAcl = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"try {{ $acl = Get-Acl 'HKLM:\\{SubKey}'; $rule = New-Object System.Security.AccessControl.RegistryAccessRule('Administrators','FullControl','ContainerInherit,ObjectInherit','None','Allow'); $acl.AddAccessRule($rule); Set-Acl 'HKLM:\\{SubKey}' $acl }} catch {{}}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var pAcl = Process.Start(psiAcl);
                pAcl?.WaitForExit(2000);

                // İzin genişletildikten sonra doğrudan yaz
                using var pReg = Process.Start(new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"add \"{FullRegKey}\" /v Start /t REG_DWORD /d {targetVal} /f",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                pReg?.WaitForExit(2000);
                if (pReg?.ExitCode == 0) return;
            }
            catch { }

            // 2. Windows 11 TrustedInstaller/SYSTEM kilidi varsa: 
            // Diske (Temp klasörüne) kesinlikle dosya yazmadan, doğrudan bellek üzerinden schtasks parametresi ile çalıştır
            string taskName = "PureWin_DiagTrack_Bridge";
            try
            {
                string inlineCommand = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \\\"Set-ItemProperty -Path 'HKLM:\\{SubKey}' -Name 'Start' -Value {targetVal} -Type DWord -Force; Set-ItemProperty -Path 'HKLM:\\SYSTEM\\ControlSet001\\Control\\WMI\\AutoLogger\\Diagtrack-Listener' -Name 'Start' -Value {targetVal} -Type DWord -Force\\\"";

                var psiCreate = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/create /tn \"{taskName}\" /tr \"{inlineCommand}\" /sc ONCE /st 23:59 /ru SYSTEM /rl HIGHEST /f",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var pCreate = Process.Start(psiCreate);
                pCreate?.WaitForExit(3000);

                var psiRun = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/run /tn \"{taskName}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var pRun = Process.Start(psiRun);
                pRun?.WaitForExit(3000);

                System.Threading.Thread.Sleep(500);

                var psiDel = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/delete /tn \"{taskName}\" /f",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var pDel = Process.Start(psiDel);
                pDel?.WaitForExit(2000);
            }
            catch { }
        }
    }
}
