using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PureWin.Services
{
    public static class EdgeRemovalService
    {
        /// <summary>
        /// Sistemde Microsoft Edge'in kurulu olup olmadığını tespit eder
        /// </summary>
        public static bool IsEdgeInstalled()
        {
            try
            {
                // 1. 32-bit Registry (WOW6432Node)
                using var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                using var edgeKey32 = hklm32.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge");
                if (edgeKey32 != null) return true;

                // 2. 64-bit Registry
                using var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var edgeKey64 = hklm64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge");
                if (edgeKey64 != null) return true;

                // 3. Dosya yolu kontrolü
                string edgeExePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe");
                if (File.Exists(edgeExePath)) return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Microsoft Edge'i zorla kaldırır ve artık dosyalarını süpürür
        /// </summary>
        public static async Task<(bool Success, string Message)> ForceRemoveEdgeAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 1. Uninstall String'ini bul
                    string uninstallCmd = string.Empty;

                    using (var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    {
                        using var key = hklm32.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge");
                        if (key != null)
                        {
                            uninstallCmd = key.GetValue("UninstallString")?.ToString() ?? string.Empty;
                        }
                    }

                    if (string.IsNullOrEmpty(uninstallCmd))
                    {
                        using (var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                        {
                            using var key = hklm64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge");
                            if (key != null)
                            {
                                uninstallCmd = key.GetValue("UninstallString")?.ToString() ?? string.Empty;
                            }
                        }
                    }

                    // 2. PowerShell komut dosyasını hazırla ve çalıştır
                    string script = $@"
                        # Edge kaldırma kilidini aç
                        try {{
                            $regView = [Microsoft.Win32.RegistryView]::Registry32
                            $hklm = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $regView)
                            $edgeUpdateKey = $hklm.CreateSubKey('SOFTWARE\Microsoft\EdgeUpdateDev')
                            $edgeUpdateKey.SetValue('AllowUninstall', '')
                        }} catch {{}}

                        # Stub klasörü oluştur
                        $edgeStub = ""$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe""
                        New-Item $edgeStub -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
                        New-Item ""$edgeStub\MicrosoftEdge.exe"" -ItemType File -Force -ErrorAction SilentlyContinue | Out-Null

                        # Kaldırıcıyı çalıştır
                        if ('{uninstallCmd.Replace("'", "''")}') {{
                            $cmd = '{uninstallCmd.Replace("'", "''")}' + ' --force-uninstall'
                            Start-Process cmd.exe -ArgumentList ""/c $cmd"" -WindowStyle Hidden -Wait
                        }}

                        # Kısayolları ve artıkları temizle
                        $edgePaths = @(
                            ""$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk"",
                            ""$env:APPDATA\Microsoft\Internet Explorer\Quick Launch\Microsoft Edge.lnk"",
                            ""$env:APPDATA\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Microsoft Edge.lnk"",
                            ""$env:PUBLIC\Desktop\Microsoft Edge.lnk"",
                            ""$env:USERPROFILE\Desktop\Microsoft Edge.lnk"",
                            ""$edgeStub""
                        )
                        foreach ($p in $edgePaths) {{
                            if (Test-Path $p) {{ Remove-Item -Path $p -Force -Recurse -ErrorAction SilentlyContinue }}
                        }}

                        # Başlangıç kayıtlarını sil
                        Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'MicrosoftEdgeAutoLaunch_*' -ErrorAction SilentlyContinue
                        Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Microsoft Edge Update' -ErrorAction SilentlyContinue
                    ";

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        process.WaitForExit(30000); // 30 saniye
                    }

                    bool stillInstalled = IsEdgeInstalled();
                    if (!stillInstalled)
                    {
                        return (true, "Microsoft Edge ve artık bileşenleri sistemden başarıyla kaldırıldı!");
                    }
                    else
                    {
                        return (false, "Microsoft Edge sistemden kaldırılamadı veya hala kurulu görünüyor.");
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"Edge kaldırma işlemi sırasında hata oluştu: {ex.Message}");
                }
            });
        }
    }
}
