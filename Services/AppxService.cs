using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using PureWin.Models;

namespace PureWin.Services
{
    public static class AppxService
    {
        // Yalnızca geçerli UWP paket kimliği karakterlerine izin ver (a-z, 0-9, ., _, -)
        private static readonly Regex ValidAppIdRegex = new(@"^[a-zA-Z0-9\.\-_]+$", RegexOptions.Compiled);

        // ASLA ve KESİNLİKLE kaldırılmasına izin verilmeyen kritik Windows çekirdek ve sistem paketleri
        private static readonly HashSet<string> CriticalSystemPackages = new(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.WindowsStore",
            "Microsoft.StorePurchaseApp",
            "Microsoft.DesktopAppInstaller",
            "Microsoft.Windows.ShellExperienceHost",
            "Microsoft.Windows.StartMenuExperienceHost",
            "Microsoft.Windows.Search",
            "Microsoft.Windows.SecHealthUI",
            "Microsoft.SecHealthUI",
            "Microsoft.UI.Xaml",
            "Microsoft.NET.Native.Framework",
            "Microsoft.NET.Native.Runtime",
            "Microsoft.VCLibs",
            "Windows.Defender",
            "Microsoft.Windows.ContentDeliveryManager",
            "Microsoft.Windows.Explorer"
        };

        private static void LogFailure(string message)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PureWin");
                Directory.CreateDirectory(dir);
                string logPath = Path.Combine(dir, "crash.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        /// <summary>
        /// Sistemde o an gerçekten yüklü olan UWP ve Win32 paket/program isimlerini canlı olarak tarar
        /// </summary>
        public static async Task<HashSet<string>> GetInstalledPackagesAsync()
        {
            return await Task.Run(() =>
            {
                var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    // 1. Süper Hızlı Kayıt Defteri UWP Taraması (5-10 milisaniye)
                    using (var userKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
                    {
                        if (userKey != null)
                        {
                            foreach (var name in userKey.GetSubKeyNames())
                                installed.Add(name);
                        }
                    }

                    using (var hklmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications"))
                    {
                        if (hklmKey != null)
                        {
                            foreach (var name in hklmKey.GetSubKeyNames())
                                installed.Add(name);
                        }
                    }

                    using (var inboxKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\InboxApplications"))
                    {
                        if (inboxKey != null)
                        {
                            foreach (var name in inboxKey.GetSubKeyNames())
                                installed.Add(name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Fast UWP reg scan error: {ex.Message}");
                }

                // 2. Fallback: PowerShell Get-AppxPackage (Eğer UWP boş kaldıysa)
                if (installed.Count == 0)
                {
                    try
                    {
                        string script = "Get-AppxPackage -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name";
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };

                        using var process = Process.Start(psi);
                        if (process != null)
                        {
                            while (!process.StandardOutput.EndOfStream)
                            {
                                string? line = process.StandardOutput.ReadLine();
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    installed.Add(line.Trim());
                                }
                            }
                            process.WaitForExit(3000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"GetInstalledPackagesAsync fallback failed: {ex.Message}");
                    }
                }

                // 3. Evrensel Win32 Kayıt Defteri Taraması (HKCU ve HKLM Uninstall Anahtarları)
                ScanWin32UninstallKeys(installed);

                // 4. Bilinen Win32 Masaüstü Yürütülebilir Dosya Kontrolleri
                ScanKnownWin32Paths(installed);

                return installed;
            });
        }

        private static void ScanWin32UninstallKeys(HashSet<string> installed)
        {
            try
            {
                // 1. HKCU Uninstall
                using (var hkcuKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (hkcuKey != null)
                    {
                        ExtractUninstallKeyData(hkcuKey, installed);
                    }
                }

                // 2. HKLM 64-bit Uninstall
                using (var hklmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (hklmKey != null)
                    {
                        ExtractUninstallKeyData(hklmKey, installed);
                    }
                }

                // 3. HKLM 32-bit (Wow6432Node) Uninstall
                using (var wowKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (wowKey != null)
                    {
                        ExtractUninstallKeyData(wowKey, installed);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Win32Scan] Uninstall keys error: {ex.Message}");
            }
        }

        private static void ExtractUninstallKeyData(RegistryKey parentKey, HashSet<string> installed)
        {
            foreach (var subName in parentKey.GetSubKeyNames())
            {
                try
                {
                    using var subKey = parentKey.OpenSubKey(subName);
                    if (subKey == null) continue;

                    installed.Add(subName);

                    string? dispName = subKey.GetValue("DisplayName") as string;
                    if (!string.IsNullOrWhiteSpace(dispName))
                    {
                        installed.Add(dispName);
                        installed.Add(dispName.Replace(" ", "."));
                    }
                }
                catch { }
            }
        }

        private static void ScanKnownWin32Paths(HashSet<string> installed)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                // OneDrive
                string[] oneDrivePaths = new[]
                {
                    Path.Combine(localAppData, @"Microsoft\OneDrive\OneDrive.exe"),
                    Path.Combine(progFiles, @"Microsoft OneDrive\OneDrive.exe"),
                    Path.Combine(progFilesX86, @"Microsoft OneDrive\OneDrive.exe")
                };
                if (oneDrivePaths.Any(File.Exists))
                {
                    installed.Add("Microsoft.OneDrive");
                    installed.Add("Microsoft OneDrive");
                    installed.Add("OneDrive");
                }

                // Microsoft Edge
                if (EdgeRemovalService.IsEdgeInstalled())
                {
                    installed.Add("Microsoft.Edge");
                    installed.Add("Microsoft.MicrosoftEdge");
                }

                // Spotify
                string spotifyExe = Path.Combine(appData, @"Spotify\Spotify.exe");
                if (File.Exists(spotifyExe))
                {
                    installed.Add("SpotifyAB.SpotifyMusic");
                    installed.Add("Spotify");
                }

                // Microsoft Teams (Klasik veya Yeni)
                string[] teamsPaths = new[]
                {
                    Path.Combine(localAppData, @"Microsoft\Teams\current\Teams.exe"),
                    Path.Combine(localAppData, @"Microsoft\WindowsApps\ms-teams.exe")
                };
                if (teamsPaths.Any(File.Exists))
                {
                    installed.Add("MicrosoftTeams");
                    installed.Add("Microsoft.Teams");
                    installed.Add("Teams");
                }
            }
            catch { }
        }

        public static bool IsAppInstalled(BloatwareItem app, HashSet<string> installedPackages)
        {
            if (app == null || installedPackages == null || installedPackages.Count == 0)
                return false;

            // 1. AppId tam veya içeren eşleşme
            if (installedPackages.Contains(app.AppId))
                return true;

            if (installedPackages.Any(p => p.IndexOf(app.AppId, StringComparison.OrdinalIgnoreCase) >= 0))
                return true;

            // 2. DisplayName ile eşleşme (örn. "Microsoft OneDrive", "OneDrive", "Spotify")
            if (!string.IsNullOrWhiteSpace(app.DisplayName))
            {
                string disp = app.DisplayName.Trim();
                if (installedPackages.Contains(disp))
                    return true;

                // Kurulu program adı DisplayName'i içeriyor mu? (Örn: "Microsoft OneDrive".IndexOf("OneDrive") >= 0)
                if (disp.Length >= 4 && installedPackages.Any(p => p.IndexOf(disp, StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }

            // 3. Noktasız ve boşluksuz normalizasyon (Örn: "Microsoft.OneDrive" <-> "Microsoft OneDrive")
            string normAppId = app.AppId.Replace(".", "").Replace(" ", "").Replace("-", "").Replace("_", "");
            if (normAppId.Length >= 4)
            {
                if (installedPackages.Any(p => p.Replace(".", "").Replace(" ", "").Replace("-", "").Replace("_", "").IndexOf(normAppId, StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }

            return false;
        }

        public static async Task<bool> RemoveAppxPackageAsync(string appId, string removalMethod = "Appx", string? displayName = null, bool cleanResiduals = true)
        {
            if (string.IsNullOrWhiteSpace(appId))
                return false;

            appId = appId.Trim();

            // Güvenlik kalkanı: Kritik sistem ve çekirdek paketlerinin silinmesini kesinlikle engelle
            if (CriticalSystemPackages.Any(p => appId.Equals(p, StringComparison.OrdinalIgnoreCase) || appId.StartsWith(p + "_", StringComparison.OrdinalIgnoreCase)))
            {
                Debug.WriteLine($"[AppxService] Kritik sistem paketi korundu, silme reddedildi: {appId}");
                return false;
            }

            // Güvenlik doğrulaması: PowerShell komut enjeksiyonunu önlemek için regex whitelist uygula
            if (!ValidAppIdRegex.IsMatch(appId))
                return false;

            if (appId.Equals("Microsoft.Edge", StringComparison.OrdinalIgnoreCase) ||
                removalMethod.Equals("Edge", StringComparison.OrdinalIgnoreCase))
            {
                var (success, _) = await EdgeRemovalService.ForceRemoveEdgeAsync();
                return success;
            }

            bool psSuccess = await Task.Run(() =>
            {
                string tempPs1 = Path.Combine(Path.GetTempPath(), $"PureWin_Uninstall_{Guid.NewGuid():N}.ps1");
                try
                {
                    string scriptTemplate = @"
                        $ErrorActionPreference = 'Continue'
                        $appId = '__APP_ID__'
                        $removalMethod = '__REMOVAL_METHOD__'

                        # 1. İlgili tüm süreçleri durdur (Kilitli dosya oluşmaması için)
                        $procNames = @(""*$appId*"")
                        if ($appId -match 'Spotify') {
                            $procNames += @('*spotify*', '*spotify.exe*')
                        }
                        if ($appId -match 'Teams') {
                            $procNames += @('*teams*', '*msteams*')
                        }
                        if ($appId -match 'OneDrive') {
                            $procNames += @('*onedrive*', '*filecoauth*')
                        }
                        foreach ($p in $procNames) {
                            Get-Process -Name $p -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
                        }
                        Start-Sleep -Milliseconds 400

                        # 2. Appx / MSIX Paketlerini hem mevcut kullanıcı hem tüm kullanıcılar için kaldır
                        $packages = Get-AppxPackage -AllUsers -Name ""*$appId*"" -ErrorAction SilentlyContinue
                        if (-not $packages) {
                            $packages = Get-AppxPackage -Name ""*$appId*"" -ErrorAction SilentlyContinue
                        }
                        foreach ($pkg in $packages) {
                            Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction Continue
                            Remove-AppxPackage -Package $pkg.PackageFullName -AllUsers -ErrorAction Continue
                        }

                        # 3. İsteğe bağlı sağlanan (Provisioned) paketleri temizle
                        Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { 
                            $_.DisplayName -like ""*$appId*"" -or $_.PackageName -like ""*$appId*"" 
                        } | ForEach-Object {
                            Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction Continue
                        }

                        # 4. Winget kaldırma yöntemi
                        if (Get-Command winget -ErrorAction SilentlyContinue) {
                            winget uninstall --id $appId --accept-source-agreements --disable-interactivity --silent 2>$null
                            if ($appId -match 'Spotify') {
                                winget uninstall --id 'Spotify.Spotify' --accept-source-agreements --disable-interactivity --silent 2>$null
                            }
                            if ($appId -match 'Teams') {
                                winget uninstall --id 'Microsoft.Teams' --accept-source-agreements --disable-interactivity --silent 2>$null
                                winget uninstall --id 'Microsoft.Teams.Classic' --accept-source-agreements --disable-interactivity --silent 2>$null
                            }
                            if ($appId -match 'OneDrive' -and $appId -ne 'Microsoft.OneDrive') {
                                winget uninstall --id 'Microsoft.OneDrive' --accept-source-agreements --disable-interactivity --silent 2>$null
                            }
                        }

                        # 5. Klasik Win32 Kurulumları ve Kayıt Defteri Kaldırıcıları (Desktop App)
                        $uninstallKeys = @(
                            'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
                            'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
                            'HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
                        )
                        Get-ItemProperty $uninstallKeys -ErrorAction SilentlyContinue | Where-Object { 
                            $_.DisplayName -like ""*$appId*"" -or ($_.DisplayName -like '*Spotify*' -and $appId -match 'Spotify') -or ($_.DisplayName -like '*Teams*' -and $appId -match 'Teams') -or ($_.DisplayName -like '*OneDrive*' -and $appId -match 'OneDrive')
                        } | ForEach-Object {
                            if ($_.QuietUninstallString) {
                                Start-Process 'cmd.exe' -ArgumentList ""/c $($_.QuietUninstallString)"" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
                            } elseif ($_.UninstallString) {
                                if ($_.UninstallString -match 'MsiExec\.exe /I(\{[^}]+\})') {
                                    Start-Process 'MsiExec.exe' -ArgumentList ""/X$($matches[1]) /qn /norestart"" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
                                } else {
                                    $unCmd = $_.UninstallString
                                    if ($unCmd -notmatch '/uninstall' -and $appId -match 'OneDrive') {
                                        $unCmd = ""$unCmd /uninstall""
                                    }
                                    Start-Process 'cmd.exe' -ArgumentList ""/c $unCmd /silent /verysilent /qn"" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
                                }
                            }
                        }

                        # 6. Spotify Özel Klasör ve Veri Temizliği
                        if ($appId -match 'Spotify') {
                            $spotifyWin32 = ""$env:APPDATA\Spotify\Spotify.exe""
                            if (Test-Path $spotifyWin32) {
                                Start-Process $spotifyWin32 -ArgumentList ""--uninstall -silent"" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
                            }
                            Remove-Item -Path ""$env:APPDATA\Spotify"" -Recurse -Force -ErrorAction SilentlyContinue
                            Remove-Item -Path ""$env:LOCALAPPDATA\Spotify"" -Recurse -Force -ErrorAction SilentlyContinue
                        }

                        # 7. Teams Özel Temizliği
                        if ($appId -match 'Teams') {
                            Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarMn' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                            if (-not (Test-Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Chat')) {
                                New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Chat' -Force -ErrorAction SilentlyContinue | Out-Null
                            }
                            Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Chat' -Name 'ChatIcon' -Value 3 -Type DWord -Force -ErrorAction SilentlyContinue
                        }

                        # 8. OneDrive Özel Kaldırma ve Gezgin İkonu Temizliği
                        if ($appId -match 'OneDrive') {
                            $oneDriveSetups = @(
                                ""$env:LOCALAPPDATA\Microsoft\OneDrive\OneDriveSetup.exe"",
                                ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe"",
                                ""$env:SystemRoot\System32\OneDriveSetup.exe""
                            )
                            $versionDirs = Get-ChildItem ""$env:LOCALAPPDATA\Microsoft\OneDrive"" -Directory -ErrorAction SilentlyContinue
                            foreach ($vd in $versionDirs) {
                                $setupInVer = Join-Path $vd.FullName ""OneDriveSetup.exe""
                                if (Test-Path $setupInVer) { $oneDriveSetups += $setupInVer }
                            }
                            foreach ($setupExe in $oneDriveSetups) {
                                if (Test-Path $setupExe) {
                                    Start-Process $setupExe -ArgumentList ""/uninstall"" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
                                    break
                                }
                            }

                            # Kaldırıcı arka plan süreçlerinin (OneDriveSetup, StandaloneUpdater vb.) tamamen bitmesini bekle
                            $procList = @('OneDrive', 'OneDriveSetup', 'OneDriveStandaloneUpdater', 'FileCoAuth')
                            $waitSec = 0
                            do {
                                Start-Sleep -Milliseconds 400
                                $waitSec++
                                $active = Get-Process -Name $procList -ErrorAction SilentlyContinue
                            } while ($active -and $waitSec -lt 25)

                            # Kalan süreç varsa zorla sonlandır
                            foreach ($p in $procList) {
                                Get-Process -Name $p -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
                            }
                            Start-Sleep -Milliseconds 400

                            # Gezgin sol gezinme panelindeki hayalet simgeyi kaldır (Unpin from namespace - 32-bit ve 64-bit görünümleri)
                            $clsids = @(
                                ""HKCU:\Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}"",
                                ""HKCU:\Software\Classes\Wow6432Node\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}"",
                                ""HKLM:\SOFTWARE\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}"",
                                ""HKLM:\SOFTWARE\Classes\Wow6432Node\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}"",
                                ""HKLM:\SOFTWARE\Wow6432Node\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}""
                            )
                            foreach ($c in $clsids) {
                                if (Test-Path $c) {
                                    Set-ItemProperty -Path $c -Name 'System.IsPinnedToNameSpaceTree' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                                }
                            }

                            # Desktop NameSpace kaydını sil
                            $nsKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{018D5C66-4533-4307-9B53-224DE2ED1FE6}'
                            if (Test-Path $nsKey) { Remove-Item -Path $nsKey -Recurse -Force -ErrorAction SilentlyContinue }

                            # Başlangıç (Run) anahtarlarındaki ölü kayıtları temizle
                            Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'OneDrive' -Force -ErrorAction SilentlyContinue
                            Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run' -Name 'OneDrive' -Force -ErrorAction SilentlyContinue

                            # 1) Zamanlanmış görev temizliği (Kullanıcı SID'si ile eşleşen ölü OneDrive görevleri)
                            $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
                            Get-ScheduledTask -ErrorAction SilentlyContinue |
                                Where-Object { $_.TaskName -like 'OneDrive*' -and $_.TaskName -like ""*$sid*"" } |
                                Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue

                            # Kaldırma sonrasında oluşturulan logs klasörü dahil tüm OneDrive yerel uygulama dizinini sil
                            $localOneDrive = ""$env:LOCALAPPDATA\Microsoft\OneDrive""
                            if (Test-Path $localOneDrive) {
                                Remove-Item -Path $localOneDrive -Recurse -Force -ErrorAction SilentlyContinue
                            }

                            # Launcher tarafından çıkışta yazılan anahtarı doğrudan sil
                            Remove-Item -Path 'HKCU:\Software\Microsoft\OneDrive' -Recurse -Force -ErrorAction SilentlyContinue

                            # 5) Kullanıcı klasörü (ihtiyatlı — kişisel dosya koruması bozulmadan: yalnızca boşsa veya sadece desktop.ini varsa sil)
                            $od = ""$env:USERPROFILE\OneDrive""
                            if (Test-Path $od) {
                                $real = @(Get-ChildItem $od -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'desktop.ini' })
                                if ($real.Count -eq 0) {
                                    cmd /c ""attrib -r -h -s `""$od`"""" | Out-Null
                                    Remove-Item $od -Recurse -Force -ErrorAction SilentlyContinue
                                }
                            }

                            # 4) Yarış koruması — final doğrulama turu (2-3 geçiş; temiz çıkınca erken bitir)
                            for ($pass = 1; $pass -le 3; $pass++) {
                                Get-Process -Name OneDrive, OneDriveSetup, OneDriveStandaloneUpdater, FileCoAuth -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
                                Start-Sleep -Milliseconds 600
                                $dirty = $false
                                if (Get-Process -Name OneDrive, OneDriveSetup, OneDriveStandaloneUpdater, FileCoAuth -ErrorAction SilentlyContinue) { $dirty = $true }
                                foreach ($c in $clsids) {
                                    if (Test-Path $c) {
                                        $v = (Get-ItemProperty -Path $c -Name 'System.IsPinnedToNameSpaceTree' -ErrorAction SilentlyContinue).'System.IsPinnedToNameSpaceTree'
                                        if ($null -ne $v -and $v -ne 0) {
                                            Set-ItemProperty -Path $c -Name 'System.IsPinnedToNameSpaceTree' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                                            $dirty = $true
                                        }
                                    }
                                }
                                if (Test-Path 'HKCU:\Software\Microsoft\OneDrive') { Remove-Item 'HKCU:\Software\Microsoft\OneDrive' -Recurse -Force -ErrorAction SilentlyContinue; $dirty = $true }
                                if (Test-Path ""$env:LOCALAPPDATA\Microsoft\OneDrive"") { Remove-Item ""$env:LOCALAPPDATA\Microsoft\OneDrive"" -Recurse -Force -ErrorAction SilentlyContinue; $dirty = $true }
                                Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -like 'OneDrive*' -and $_.TaskName -like ""*$sid*"" } | ForEach-Object { Unregister-ScheduledTask -TaskName $_.TaskName -TaskPath $_.TaskPath -Confirm:$false -ErrorAction SilentlyContinue; $dirty = $true }
                                if (-not $dirty) { break }
                            }
                        }

                        # 9. Artık Klasörler, Kısayollar ve Paket İçi Depolama
                        $pkgFolders = Get-ChildItem ""$env:LOCALAPPDATA\Packages"" -Filter ""*$appId*"" -ErrorAction SilentlyContinue
                        foreach ($f in $pkgFolders) {
                            Remove-Item -Path $f.FullName -Recurse -Force -ErrorAction SilentlyContinue
                        }

                        $shortcutDirs = @(
                            ""$env:APPDATA\Microsoft\Windows\Start Menu\Programs"",
                            ""$env:ProgramData\Microsoft\Windows\Start Menu\Programs"",
                            ""$env:USERPROFILE\Desktop"",
                            ""C:\Users\Public\Desktop""
                        )
                        foreach ($sd in $shortcutDirs) {
                            if (Test-Path $sd) {
                                Get-ChildItem -Path $sd -Recurse -Filter ""*$appId*"" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
                                if ($appId -match 'Spotify') {
                                    Get-ChildItem -Path $sd -Recurse -Filter ""*Spotify*"" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
                                }
                                if ($appId -match 'OneDrive') {
                                    Get-ChildItem -Path $sd -Recurse -Filter ""*OneDrive*"" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
                                }
                            }
                        }

                        # Registry AppContainer Storage izleri
                        $appContainer = ""HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppContainer\Storage""
                        if (Test-Path $appContainer) {
                            Get-ChildItem $appContainer -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like ""*$appId*"" } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
                        }
                    ";

                    string script = scriptTemplate.Replace("__APP_ID__", appId).Replace("__REMOVAL_METHOD__", removalMethod);

                    // Komut satırı 32.767 karakter sınırını aşmamak için script'i doğrudan geçici dosyadan çalıştır
                    File.WriteAllText(tempPs1, script, Encoding.UTF8);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -NonInteractive -File \"{tempPs1}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        var stdoutTask = process.StandardOutput.ReadToEndAsync();
                        var stderrTask = process.StandardError.ReadToEndAsync();

                        int timeoutMs = appId.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) ? 90000 : 45000;
                        bool finished = process.WaitForExit(timeoutMs);
                        if (!finished)
                        {
                            try { process.Kill(entireProcessTree: true); } catch { }
                            LogFailure($"[AppxService] Timeout ({timeoutMs / 1000}s) removing {appId}.");
                            return false;
                        }

                        string stdout = stdoutTask.GetAwaiter().GetResult();
                        string stderr = stderrTask.GetAwaiter().GetResult();

                        if (process.ExitCode != 0)
                        {
                            LogFailure($"[AppxService] PowerShell exit code {process.ExitCode} removing {appId}.\nStderr: {stderr}\nStdout: {stdout}");
                        }
                        else if (!string.IsNullOrWhiteSpace(stderr))
                        {
                            LogFailure($"[AppxService] Warning removing {appId}:\nStderr: {stderr}");
                        }

                        return process.ExitCode == 0;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    LogFailure($"[AppxService] Appx removal exception for {appId}: {ex.Message}\n{ex.StackTrace}");
                    return false;
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempPs1))
                            File.Delete(tempPs1);
                    }
                    catch { }
                }
            });

            // 2. AŞAMA: Canlı Doğrulama (Gerçekten silindi mi?)
            bool isStillInstalled = await Task.Run(() =>
            {
                try
                {
                    // 1. HKCU AppModel Packages kontrolü
                    using (var userKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
                    {
                        if (userKey != null)
                        {
                            if (userKey.GetSubKeyNames().Any(n => n.StartsWith(appId + "_", StringComparison.OrdinalIgnoreCase) || n.Equals(appId, StringComparison.OrdinalIgnoreCase)))
                                return true;
                        }
                    }

                    // 2. HKLM AllUserStore kontrolü
                    using (var hklmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications"))
                    {
                        if (hklmKey != null)
                        {
                            if (hklmKey.GetSubKeyNames().Any(n => n.StartsWith(appId + "_", StringComparison.OrdinalIgnoreCase) || n.Equals(appId, StringComparison.OrdinalIgnoreCase)))
                                return true;
                        }
                    }

                    // 3. Win32 kontrolü (OneDrive, Spotify vb.)
                    if (appId.Equals("Microsoft.OneDrive", StringComparison.OrdinalIgnoreCase))
                    {
                        string oneDriveExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\OneDrive\OneDrive.exe");
                        if (File.Exists(oneDriveExe)) return true;
                    }

                    if (appId.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string spotifyExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Spotify\Spotify.exe");
                        if (File.Exists(spotifyExe)) return true;
                    }
                }
                catch { }
                return false;
            });

            // Paket sistemden kaldırılamamışsa artık temizliğini ASLA çalıştırma ve başarısız dön
            if (isStillInstalled)
            {
                LogFailure($"[AppxService] Live verification failed: {appId} is still detected on system after removal attempt. Residuals skipped.");
                return false;
            }

            // 3. AŞAMA: Kaldırma Doğrulandıktan Sonra Derin Artık Temizliği (Disk, Packages, AppData, Registry, Kısayollar)
            if (cleanResiduals)
            {
                try
                {
                    await ResidualCleanerService.CleanResidualsAsync(appId, displayName, backupRegistry: true, allowHklm: false);
                }
                catch (Exception ex)
                {
                    LogFailure($"[AppxService] Residual cleanup exception for {appId}: {ex.Message}");
                }
            }

            return true;
        }
    }
}
