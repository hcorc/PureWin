using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PureWin.Services
{
    /// <summary>
    /// Kaldırılan uygulamaların (UWP / Win32 / Desktop) sistemde bıraktığı tüm
    /// dosya, klasör, kayıt defteri anahtarı, başlangıç girdisi ve kısayol artıklarını
    /// Revo Uninstaller mantığıyla derinlemesine tarayıp temizleyen motor.
    /// </summary>
    public static class ResidualCleanerService
    {
        /// <summary>
        /// Güvenlik Kalkanı: Genel HKLM vendor/COM/StateRepository silme koruması.
        /// PureWin mimarisinde sistem kararlılığını garanti etmek için genel HKLM silmeleri KAPALIDIR.
        /// Yalnızca uygulamaya özel, güvenli CapabilityAccessManager izin kayıtları
        /// CleanHklmCapabilityTraces tarafından ayrıca temizlenir.
        /// </summary>
        public static bool EnableHklmDeletion => false;

        // Asla silinmemesi gereken korumalı/kritik sistem ve donanım üretici kelimeleri
        private static readonly HashSet<string> BlacklistedTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "microsoft", "windows", "system", "system32", "syswow64", "systemapps", "explorer", "shell",
            "core", "default", "common", "programdata", "appdata", "users", "local", "locallow", "roaming",
            "temp", "desktop", "start menu", "classes", "software", "currentversion", "policies",
            "edge", "webview2", "all users", "public", "program files", "program files (x86)",
            "windows defender", "security", "securityhealth", "bonjour", "driver", "drivers",
            "king", "google", "apple", "amazon", "adobe", "intel", "amd", "nvidia", "realtek",
            "qualcomm", "lenovo", "dell", "asus", "hp", "acer", "samsung", "kingston", "logitech",
            "corsair", "publisher", "developer", "company", "corp", "games", "gaming", "online", "store"
        };

        // Asla silinmemesi gereken korumalı sistem dizinleri
        private static readonly HashSet<string> ProtectedDirectories = new(StringComparer.OrdinalIgnoreCase);

        static ResidualCleanerService()
        {
            try
            {
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.System));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.Programs));
                AddProtected(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));
                AddProtected(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages"));
                AddProtected(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft"));
                AddProtected(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft"));
                AddProtected(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft"));
                // Kullanıcı kişisel dosya/belge dizini (%USERPROFILE%\OneDrive) KESİNLİKLE KORUNUR!
                AddProtected(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing protected paths: {ex.Message}");
            }
        }

        private static void AddProtected(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                ProtectedDirectories.Add(Path.GetFullPath(path).TrimEnd('\\'));
            }
        }

        #region Token Çıkarma & Eşleştirme (Ayrıştırma)

        /// <summary>
        /// AppId ve DisplayName içerisinden güvenli, spesifik anahtar kelimeleri çıkarır
        /// </summary>
        public static HashSet<string> ExtractTokens(string appId, string? displayName)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(appId))
                return tokens;

            // Tam AppId'yi ekle (Örn: SpotifyAB.SpotifyMusic, Clipchamp.Clipchamp)
            if (IsValidToken(appId))
                tokens.Add(appId);

            // Bilinen özel haritalamalar
            if (appId.Contains("Spotify", StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(displayName) && displayName.Contains("Spotify", StringComparison.OrdinalIgnoreCase)))
            {
                tokens.Add("Spotify");
                tokens.Add("SpotifyMusic");
            }
            if (appId.Contains("549981C3F5F10", StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(displayName) && displayName.Contains("Cortana", StringComparison.OrdinalIgnoreCase)))
            {
                tokens.Add("Cortana");
                tokens.Add("549981C3F5F10");
            }
            if (appId.Contains("Teams", StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(displayName) && displayName.Contains("Teams", StringComparison.OrdinalIgnoreCase)))
            {
                tokens.Add("Teams");
                tokens.Add("msteams");
            }
            if (appId.Contains("CandyCrush", StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(displayName) && displayName.Contains("Candy Crush", StringComparison.OrdinalIgnoreCase)))
            {
                tokens.Add("CandyCrush");
                tokens.Add("CandyCrushSaga");
            }
            if (appId.Contains("Clipchamp", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("Clipchamp");
            }
            if (appId.Contains("ZuneMusic", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("ZuneMusic");
                tokens.Add("Zune");
            }
            if (appId.Contains("ZuneVideo", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("ZuneVideo");
            }
            if (appId.Contains("SkypeApp", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("SkypeApp");
                tokens.Add("Skype");
            }
            if (appId.Contains("Todos", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("Todos");
            }
            if (appId.Contains("Getstarted", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("Getstarted");
            }
            if (appId.Contains("BingWeather", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("BingWeather");
            }
            if (appId.Contains("BingNews", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("BingNews");
            }
            if (appId.Contains("BingFinance", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("BingFinance");
            }
            if (appId.Contains("BingSports", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("BingSports");
            }
            if (appId.Contains("Solitaire", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("MicrosoftSolitaireCollection");
                tokens.Add("Solitaire");
            }
            if (appId.Contains("PowerAutomate", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("PowerAutomate");
                tokens.Add("PowerAutomateDesktop");
            }
            if (appId.Contains("XboxApp", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("XboxApp");
            }
            if (appId.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("Microsoft.OneDrive");
                tokens.Add("OneDrive");
            }

            // Windows Yerleşik Uygulamaları Özel Haritalamaları
            if (appId.Contains("WindowsCalculator", StringComparison.OrdinalIgnoreCase) || appId.Contains("Calculator", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("WindowsCalculator");
                tokens.Add("Calculator");
            }
            if (appId.Contains("Paint", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("Paint");
                tokens.Add("MSPaint");
            }
            if (appId.Contains("Photos", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("WindowsPhotos");
                tokens.Add("Photos");
            }
            if (appId.Contains("WindowsMediaPlayer", StringComparison.OrdinalIgnoreCase) || appId.Contains("MediaPlayer", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("WindowsMediaPlayer");
                tokens.Add("MediaPlayer");
            }
            if (appId.Contains("WindowsCamera", StringComparison.OrdinalIgnoreCase) || appId.Contains("Camera", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("WindowsCamera");
                tokens.Add("Camera");
            }
            if (appId.Contains("WindowsMaps", StringComparison.OrdinalIgnoreCase) || appId.Contains("Maps", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("WindowsMaps");
                tokens.Add("Maps");
            }
            if (appId.Contains("WindowsTerminal", StringComparison.OrdinalIgnoreCase) || appId.Contains("Terminal", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("WindowsTerminal");
                tokens.Add("Terminal");
            }
            if (appId.Contains("WindowsAlarms", StringComparison.OrdinalIgnoreCase) || appId.Contains("Alarms", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("WindowsAlarms");
                tokens.Add("Alarms");
            }
            if (appId.Contains("WindowsSoundRecorder", StringComparison.OrdinalIgnoreCase) || appId.Contains("SoundRecorder", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("WindowsSoundRecorder");
                tokens.Add("SoundRecorder");
            }
            if (appId.Contains("WindowsFeedbackHub", StringComparison.OrdinalIgnoreCase) || appId.Contains("FeedbackHub", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("WindowsFeedbackHub");
                tokens.Add("FeedbackHub");
            }

            // Noktalı paket adından son parçayı ve birleşik Windows adlarını güvenli şekilde al
            if (appId.Contains('.'))
            {
                string[] parts = appId.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string lastPart = parts[^1];
                    if (IsValidToken(lastPart))
                        tokens.Add(lastPart);

                    // Eğer son parça 'Windows' ile başlıyorsa kök adı da çıkar (Örn: WindowsCalculator -> Calculator)
                    if (lastPart.StartsWith("Windows", StringComparison.OrdinalIgnoreCase) && lastPart.Length > 7)
                    {
                        string subName = lastPart.Substring(7);
                        if (IsValidToken(subName))
                            tokens.Add(subName);
                    }

                    // Eğer sondan bir önceki parça 'Windows' ise birleşik adı ekle (Örn: Microsoft.Windows.Photos -> WindowsPhotos)
                    if (parts.Length >= 2 && parts[^2].Equals("Windows", StringComparison.OrdinalIgnoreCase))
                    {
                        string compound = "Windows" + lastPart;
                        if (IsValidToken(compound))
                            tokens.Add(compound);
                    }
                }
            }

            return tokens;
        }

        private static bool IsValidToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length < 4)
                return false;

            if (BlacklistedTokens.Contains(token))
                return false;

            return true;
        }

        private static readonly HashSet<string> ThirdPartyVendorPrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Google", "Apple", "Adobe", "Amazon", "Sony", "Intel", "NVIDIA", "AMD",
            "Realtek", "Qualcomm", "Logitech", "Corsair", "Steam", "Epic", "Ubisoft",
            "Electronic Arts", "EA", "Battle.net", "Blizzard", "Samsung", "Asus", "Lenovo", "Dell", "HP"
        };

        /// <summary>
        /// Hedef uygulama adı veya kelime sınırlarına göre KESİN ve TUZAK KORUMALI eşleşme kontrolü yapar.
        /// Örneğin token "Photos" iken "Google Photos" veya "Camera" iken "Adobe Camera Raw" gibi
        /// 3. parti üreticilere ait klasör/kayıtları ASLA eşleştirmez ve silmez.
        /// </summary>
        public static bool MatchesStrict(string targetName, HashSet<string> tokens)
        {
            if (string.IsNullOrWhiteSpace(targetName)) return false;

            string cleanTarget = targetName.Trim();

            // Hedef ad yabancı bir 3. parti üreticiye aitse ve token o üreticiyi içermiyorsa ASLA eşleştirme (Tuzak Koruması)
            foreach (var vendor in ThirdPartyVendorPrefixes)
            {
                if (cleanTarget.StartsWith(vendor + " ", StringComparison.OrdinalIgnoreCase) ||
                    cleanTarget.StartsWith(vendor + ".", StringComparison.OrdinalIgnoreCase) ||
                    cleanTarget.StartsWith(vendor + "_", StringComparison.OrdinalIgnoreCase))
                {
                    if (!tokens.Contains(vendor))
                        return false;
                }
            }

            foreach (var token in tokens)
            {
                // 1. Tam eşleşme (Exact Match)
                if (cleanTarget.Equals(token, StringComparison.OrdinalIgnoreCase))
                    return true;

                // 2. Microsoft veya resmi ön eki ile güvenli eşleşme
                if (cleanTarget.Equals($"Microsoft {token}", StringComparison.OrdinalIgnoreCase) ||
                    cleanTarget.Equals($"Microsoft.{token}", StringComparison.OrdinalIgnoreCase) ||
                    cleanTarget.Equals($"Microsoft_{token}", StringComparison.OrdinalIgnoreCase) ||
                    cleanTarget.Equals($"{token} App", StringComparison.OrdinalIgnoreCase) ||
                    cleanTarget.Equals($"{token}.exe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // 3. Çok parçalı özel tokenler (Örn: "Spotify Music", "Candy Crush Saga")
                if (token.Contains(' ') && cleanTarget.Equals(token, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Kısayol (.lnk) dosyalarının kesin eşleşme kontrolü.
        /// Kullanıcının kendi özel kısayollarını veya "Helper", "Viewer" türevlerini KESİNLİKLE eşleştirmez.
        /// </summary>
        public static bool MatchesShortcut(string fileNameWithoutExt, string appId, string? displayName, HashSet<string> tokens)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExt)) return false;

            // 1. DisplayName ile tam eşleşme (Örn: "Spotify Müzik", "Microsoft Teams", "PureWin Mock Target")
            if (!string.IsNullOrEmpty(displayName) && fileNameWithoutExt.Equals(displayName, StringComparison.OrdinalIgnoreCase))
                return true;

            // 2. AppId ile tam eşleşme
            if (fileNameWithoutExt.Equals(appId, StringComparison.OrdinalIgnoreCase))
                return true;

            string noSpaceName = fileNameWithoutExt.Replace(" ", "");

            // 3. Token ile tam eşleşme, boşluksuz birleşik eşleşme veya "Microsoft {Token}"
            foreach (var token in tokens)
            {
                if (fileNameWithoutExt.Equals(token, StringComparison.OrdinalIgnoreCase) ||
                    noSpaceName.Equals(token, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (fileNameWithoutExt.Equals($"Microsoft {token}", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        #endregion

        #region Kayıt Defteri (.reg) Yedekleme

        /// <summary>
        /// Belirtilen kayıt defteri anahtarını silmeden önce .reg dosyası olarak dışa aktarır.
        /// </summary>
        public static string? BackupRegistryKey(string fullKeyPath)
        {
            try
            {
                string backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PureWin", "Backups", "Registry"
                );
                Directory.CreateDirectory(backupDir);

                string safeName = fullKeyPath
                    .Replace("HKEY_CURRENT_USER\\", "HKCU_")
                    .Replace("HKEY_LOCAL_MACHINE\\", "HKLM_")
                    .Replace("\\", "_")
                    .Replace(":", "");

                string backupFile = Path.Combine(backupDir, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.reg");

                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"export \"{fullKeyPath}\" \"{backupFile}\" /y",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    bool exited = process.WaitForExit(5000);
                    if (!exited)
                    {
                        try { process.Kill(); } catch { }
                        return null;
                    }
                    if (process.ExitCode == 0 && File.Exists(backupFile))
                    {
                        Debug.WriteLine($"[RegistryBackup] Yedek alındı: {backupFile}");
                        return backupFile;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RegistryBackup] Hata: {ex.Message}");
            }
            return null;
        }

        #endregion

        #region Tarama (Scan / Dry-Run) Aşaması

        /// <summary>
        /// Silme yapmadan önce sistemdeki artık klasör, kısayol ve kayıt defteri anahtarlarını önceden tarar.
        /// </summary>
        public static ResidualScanResult ScanResiduals(string appId, string? displayName = null)
        {
            var scan = new ResidualScanResult();
            if (string.IsNullOrWhiteSpace(appId))
                return scan;

            var tokens = ExtractTokens(appId, displayName);
            if (tokens.Count == 0)
                return scan;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            // 1. Packages Klasörü
            string packagesDir = Path.Combine(localAppData, "Packages");
            if (Directory.Exists(packagesDir))
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(packagesDir))
                    {
                        string dirName = Path.GetFileName(dir);
                        if (dirName.StartsWith(appId + "_", StringComparison.OrdinalIgnoreCase) ||
                            tokens.Any(t => dirName.StartsWith(t + "_", StringComparison.OrdinalIgnoreCase)))
                        {
                            scan.Folders.Add(dir);
                        }
                    }
                }
                catch { }
            }

            // 2. AppData & LocalAppData & ProgramData Klasörleri
            string[] baseUserDirs = { appData, localAppData, programData };
            foreach (var baseDir in baseUserDirs)
            {
                if (!Directory.Exists(baseDir)) continue;

                foreach (var token in tokens)
                {
                    string directTarget = Path.Combine(baseDir, token);
                    if (Directory.Exists(directTarget) && !IsProtectedDirectory(directTarget))
                    {
                        scan.Folders.Add(directTarget);
                    }

                    string msTarget = Path.Combine(baseDir, "Microsoft", token);
                    if (Directory.Exists(msTarget) && !IsProtectedDirectory(msTarget))
                    {
                        scan.Folders.Add(msTarget);
                    }

                    string msSpaceTarget = Path.Combine(baseDir, "Microsoft " + token);
                    if (Directory.Exists(msSpaceTarget) && !IsProtectedDirectory(msSpaceTarget))
                    {
                        scan.Folders.Add(msSpaceTarget);
                    }
                }
            }

            // 3. Kısayollar
            var shortcutDirs = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };
            foreach (var sDir in shortcutDirs)
            {
                if (!Directory.Exists(sDir)) continue;
                try
                {
                    foreach (var file in Directory.GetFiles(sDir, "*.lnk", SearchOption.AllDirectories))
                    {
                        string nameNoExt = Path.GetFileNameWithoutExtension(file);
                        if (MatchesShortcut(nameNoExt, appId, displayName, tokens))
                        {
                            scan.Shortcuts.Add(file);
                        }
                    }
                }
                catch { }
            }

            // 4. HKCU Kayıt Defteri Anahtarları
            ScanRegistryKeys(Registry.CurrentUser, @"Software", "HKEY_CURRENT_USER", tokens, scan);
            ScanRegistryKeys(Registry.CurrentUser, @"Software\Microsoft", "HKEY_CURRENT_USER", tokens, scan);
            ScanUninstallKeys(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", "HKEY_CURRENT_USER", tokens, displayName, scan);
            ScanStartupValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", tokens, scan);

            // 5. HKLM Kayıt Defteri Anahtarları (Bilgi amaçlı tespit)
            ScanRegistryKeys(Registry.LocalMachine, @"SOFTWARE", "HKEY_LOCAL_MACHINE", tokens, scan, isHklm: true);
            ScanUninstallKeys(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", "HKEY_LOCAL_MACHINE", tokens, displayName, scan, isHklm: true);

            return scan;
        }

        private static void ScanRegistryKeys(RegistryKey root, string subPath, string rootName, HashSet<string> tokens, ResidualScanResult scan, bool isHklm = false)
        {
            try
            {
                using var key = root.OpenSubKey(subPath);
                if (key == null) return;

                foreach (var name in key.GetSubKeyNames())
                {
                    if (tokens.Any(t => name.Equals(t, StringComparison.OrdinalIgnoreCase)) && !BlacklistedTokens.Contains(name))
                    {
                        string fullPath = $@"{rootName}\{subPath}\{name}";
                        if (isHklm)
                            scan.HklmKeys.Add(fullPath);
                        else
                            scan.RegistryKeys.Add(fullPath);
                    }
                }
            }
            catch { }
        }

        private static void ScanUninstallKeys(RegistryKey root, string uninstallPath, string rootName, HashSet<string> tokens, string? displayName, ResidualScanResult scan, bool isHklm = false)
        {
            try
            {
                using var uKey = root.OpenSubKey(uninstallPath);
                if (uKey == null) return;

                foreach (var subName in uKey.GetSubKeyNames())
                {
                    if (BlacklistedTokens.Contains(subName)) continue;

                    bool match = false;
                    if (tokens.Any(t => subName.Equals(t, StringComparison.OrdinalIgnoreCase)))
                    {
                        match = true;
                    }
                    else
                    {
                        using var sub = uKey.OpenSubKey(subName);
                        if (sub != null)
                        {
                            string? dName = sub.GetValue("DisplayName")?.ToString();
                            if (!string.IsNullOrEmpty(dName) &&
                                (MatchesStrict(dName, tokens) || (!string.IsNullOrEmpty(displayName) && dName.Equals(displayName, StringComparison.OrdinalIgnoreCase))))
                            {
                                match = true;
                            }
                        }
                    }

                    if (match)
                    {
                        string fullPath = $@"{rootName}\{uninstallPath}\{subName}";
                        if (isHklm)
                            scan.HklmKeys.Add(fullPath);
                        else
                            scan.RegistryKeys.Add(fullPath);
                    }
                }
            }
            catch { }
        }

        private static void ScanStartupValues(RegistryKey root, string runPath, HashSet<string> tokens, ResidualScanResult scan)
        {
            try
            {
                using var runKey = root.OpenSubKey(runPath);
                if (runKey == null) return;

                foreach (var valName in runKey.GetValueNames())
                {
                    if (BlacklistedTokens.Contains(valName)) continue;

                    string? valData = runKey.GetValue(valName)?.ToString();
                    if (MatchesStrict(valName, tokens) || (!string.IsNullOrEmpty(valData) && MatchesStrict(valData, tokens)))
                    {
                        scan.RegistryValues.Add($@"HKCU\{runPath} -> {valName}");
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Temizleme (Execution) Aşaması

        /// <summary>
        /// Artık taramasını yapar, kayıt defteri için .reg yedeği alır ve artıkları siler.
        /// </summary>
        public static async Task<ResidualCleanResult> CleanResidualsAsync(string appId, string? displayName = null, bool backupRegistry = true, bool allowHklm = false)
        {
            return await Task.Run(() =>
            {
                var result = new ResidualCleanResult();
                if (string.IsNullOrWhiteSpace(appId))
                    return result;

                var tokens = ExtractTokens(appId, displayName);
                if (tokens.Count == 0)
                    return result;

                Debug.WriteLine($"[ResidualCleaner] Temizlik başlatıldı: {appId}. Tokenler: {string.Join(", ", tokens)}");

                // 1. İlgili çalışan arka plan süreçlerini sonlandır
                KillMatchingProcesses(tokens);

                // 2. Dosya Sistemi Artıklarını Temizle
                CleanFileSystemResiduals(appId, tokens, result);

                // 3. Kısayol Artıklarını Temizle
                CleanShortcutResiduals(appId, displayName, tokens, result);

                // 4. Kayıt Defteri (.reg Yedeği Al ve Temizle)
                CleanRegistryResiduals(appId, tokens, displayName, backupRegistry, allowHklm, result);

                // 5. UWP/Appx uygulamalarının kullanıcı ve izin kayıtları
                CleanUwpRegistryTraces(appId, tokens, result);

                Debug.WriteLine($"[ResidualCleaner] Bitti: {appId}. Silinen Klasör: {result.DeletedFoldersCount}, Dosya: {result.DeletedFilesCount}, Reg Anahtarı: {result.DeletedRegistryKeysCount}, Değer: {result.DeletedRegistryValuesCount}, Yedek: {result.CreatedBackups.Count}");
                return result;
            });
        }

        private static void KillMatchingProcesses(HashSet<string> tokens)
        {
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        string pName = p.ProcessName;
                        bool match = tokens.Any(t => pName.Equals(t, StringComparison.OrdinalIgnoreCase));
                        if (!match && tokens.Contains("OneDrive"))
                        {
                            match = pName.Equals("OneDriveSetup", StringComparison.OrdinalIgnoreCase) ||
                                    pName.Equals("OneDriveStandaloneUpdater", StringComparison.OrdinalIgnoreCase) ||
                                    pName.Equals("FileCoAuth", StringComparison.OrdinalIgnoreCase);
                        }

                        if (match && !BlacklistedTokens.Contains(pName))
                        {
                            p.Kill();
                            p.WaitForExit(1000);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void CleanFileSystemResiduals(string appId, HashSet<string> tokens, ResidualCleanResult result)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // 1. %LOCALAPPDATA%\Packages (UWP)
            string packagesDir = Path.Combine(localAppData, "Packages");
            if (Directory.Exists(packagesDir))
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(packagesDir))
                    {
                        string dirName = Path.GetFileName(dir);
                        if (dirName.StartsWith(appId + "_", StringComparison.OrdinalIgnoreCase) ||
                            tokens.Any(t => dirName.StartsWith(t + "_", StringComparison.OrdinalIgnoreCase)))
                        {
                            SafeDeleteDirectory(dir, result);
                        }
                    }
                }
                catch { }
            }

            // 2. %APPDATA%, %LOCALAPPDATA%, %PROGRAMDATA% (Tam eşleşen dizinler)
            string[] baseUserDirs = { appData, localAppData, programData };
            foreach (var baseDir in baseUserDirs)
            {
                if (!Directory.Exists(baseDir)) continue;

                foreach (var token in tokens)
                {
                    string directTarget = Path.Combine(baseDir, token);
                    if (Directory.Exists(directTarget) && !IsProtectedDirectory(directTarget))
                    {
                        SafeDeleteDirectory(directTarget, result);
                    }

                    string msTarget = Path.Combine(baseDir, "Microsoft", token);
                    if (Directory.Exists(msTarget) && !IsProtectedDirectory(msTarget))
                    {
                        SafeDeleteDirectory(msTarget, result);
                    }

                    string msSpaceTarget = Path.Combine(baseDir, "Microsoft " + token);
                    if (Directory.Exists(msSpaceTarget) && !IsProtectedDirectory(msSpaceTarget))
                    {
                        SafeDeleteDirectory(msSpaceTarget, result);
                    }
                }
            }

            // 3. UserProfile .<token> (Örn: .spotify)
            foreach (var token in tokens)
            {
                string dotTarget = Path.Combine(userProfile, "." + token.ToLowerInvariant());
                if (Directory.Exists(dotTarget) && !IsProtectedDirectory(dotTarget))
                {
                    SafeDeleteDirectory(dotTarget, result);
                }
            }

            // 4. %TEMP% dizini
            try
            {
                string tempDir = Path.GetTempPath();
                if (Directory.Exists(tempDir))
                {
                    foreach (var entry in Directory.GetFileSystemEntries(tempDir))
                    {
                        string name = Path.GetFileName(entry);
                        if (MatchesStrict(name, tokens))
                        {
                            if (Directory.Exists(entry))
                                SafeDeleteDirectory(entry, result);
                            else if (File.Exists(entry))
                                SafeDeleteFile(entry, result);
                        }
                    }
                }
            }
            catch { }
        }

        private static void CleanShortcutResiduals(string appId, string? displayName, HashSet<string> tokens, ResidualCleanResult result)
        {
            var shortcutDirs = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            foreach (var sDir in shortcutDirs)
            {
                if (!Directory.Exists(sDir)) continue;
                try
                {
                    foreach (var file in Directory.GetFiles(sDir, "*.lnk", SearchOption.AllDirectories))
                    {
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        if (MatchesShortcut(fileNameWithoutExt, appId, displayName, tokens))
                        {
                            SafeDeleteFile(file, result);
                        }
                    }
                }
                catch { }
            }
        }

        private static void CleanRegistryResiduals(string appId, HashSet<string> tokens, string? displayName, bool backupRegistry, bool allowHklm, ResidualCleanResult result)
        {
            // 1. HKCU\Software\<Token> (Örn: HKCU\Software\Spotify)
            CleanVendorKeys(Registry.CurrentUser, @"Software", "HKEY_CURRENT_USER", tokens, backupRegistry, result);

            // 2. HKCU\Software\Microsoft\<Token> (Örn: HKCU\Software\Microsoft\Cortana, OneDrive)
            CleanVendorKeys(Registry.CurrentUser, @"Software\Microsoft", "HKEY_CURRENT_USER", tokens, backupRegistry, result);

            // 2.1 Güvenlik süpürmesi: HKCU\Software\Microsoft altında token ile doğrudan eşleşen anahtarlar
            try
            {
                using var msKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft", writable: true);
                if (msKey != null)
                {
                    foreach (var token in tokens)
                    {
                        if (!BlacklistedTokens.Contains(token))
                        {
                            try
                            {
                                msKey.DeleteSubKeyTree(token, throwOnMissingSubKey: false);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // 3. HKCU politikaları (WindowsPhotos gibi uygulamaya özel politika anahtarları)
            CleanVendorKeys(Registry.CurrentUser, @"Software\Policies\Microsoft", "HKEY_CURRENT_USER", tokens, backupRegistry, result);

            // 4. HKCU Uninstall Kayıtları
            CleanUninstallKeysInternal(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", "HKEY_CURRENT_USER", tokens, displayName, backupRegistry, result);

            // 4. HKCU Startup (Run) Girdileri
            CleanStartupValuesInternal(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", tokens, result);

            // 5. HKLM Temizliği: Yalnızca allowHklm açık ve EnableHklmDeletion açık ise çalışır!
            if (allowHklm && EnableHklmDeletion)
            {
                CleanVendorKeys(Registry.LocalMachine, @"SOFTWARE", "HKEY_LOCAL_MACHINE", tokens, backupRegistry, result);
                CleanVendorKeys(Registry.LocalMachine, @"SOFTWARE\WOW6432Node", "HKEY_LOCAL_MACHINE", tokens, backupRegistry, result);
                CleanUninstallKeysInternal(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", "HKEY_LOCAL_MACHINE", tokens, displayName, backupRegistry, result);
            }
            else
            {
                Debug.WriteLine("[ResidualCleaner] HKLM temizliği güvenlik nedeniyle KAPALI/ATLANDI.");
            }
        }

        /// <summary>
        /// UWP/Appx uygulamalarının kaldırma sonrası kullanıcıya ait izlerini temizler.
        /// Sistem veritabanı (StateRepository), COM kayıtları ve AppxAllUserStore gibi
        /// Windows tarafından yönetilen alanlara dokunmaz.
        /// </summary>
        private static void CleanUwpRegistryTraces(string appId, HashSet<string> tokens, ResidualCleanResult result)
        {
            string[] hkcuPaths =
            {
                @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                @"Software\Microsoft\Windows\CurrentVersion\Start\TileProperties",
                @"Software\Microsoft\Phone\ShellUI\WindowSizing",
                @"Software\Microsoft\Windows NT\CurrentVersion\HostActivityManager\CommitHistory",
                @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\PolicyCache",
                @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppContainer\Storage",
                @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations"
            };

            foreach (string path in hkcuPaths)
            {
                try
                {
                    using var parent = Registry.CurrentUser.OpenSubKey(path, writable: true);
                    if (parent == null) continue;

                    bool allowTokenFallback = path.EndsWith(@"\UrlAssociations", StringComparison.OrdinalIgnoreCase);

                    foreach (string name in parent.GetSubKeyNames().ToArray())
                    {
                        if (!IsPackageTraceName(name, appId, tokens, allowTokenFallback))
                            continue;

                        string fullPath = $@"HKEY_CURRENT_USER\{path}\{name}";
                        string? backup = BackupRegistryKey(fullPath);
                        if (backup != null)
                            result.CreatedBackups.Add(backup);

                        try
                        {
                            parent.DeleteSubKeyTree(name, false);
                            result.DeletedRegistryKeysCount++;
                        }
                        catch { }
                    }
                }
                catch { }
            }

            CleanHklmCapabilityTraces(appId, tokens, result);
        }

        /// <summary>
        /// HKLM'de yalnızca uygulamaya özel CapabilityAccessManager izin kayıtlarını temizler.
        /// Genel HKLM/COM/StateRepository silme politikası hâlâ kapalıdır.
        /// </summary>
        private static void CleanHklmCapabilityTraces(string appId, HashSet<string> tokens, ResidualCleanResult result)
        {
            const string basePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\Capabilities";

            try
            {
                using var capabilities = Registry.LocalMachine.OpenSubKey(basePath, false);
                if (capabilities == null) return;

                foreach (string capability in capabilities.GetSubKeyNames())
                {
                    using var apps = capabilities.OpenSubKey(capability + @"\Apps", writable: true);
                    if (apps == null) continue;

                    foreach (string appName in apps.GetSubKeyNames().ToArray())
                    {
                        if (!IsPackageTraceName(appName, appId, tokens, allowTokenFallback: false))
                            continue;

                        string fullPath = $@"HKEY_LOCAL_MACHINE\{basePath}\{capability}\Apps\{appName}";
                        string? backup = BackupRegistryKey(fullPath);
                        if (backup != null)
                            result.CreatedBackups.Add(backup);

                        try
                        {
                            apps.DeleteSubKeyTree(appName, false);
                            result.DeletedRegistryKeysCount++;
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static bool IsPackageTraceName(
            string name,
            string appId,
            HashSet<string> tokens,
            bool allowTokenFallback)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (name.Contains(appId, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!allowTokenFallback)
                return false;

            // Üçüncü parti üreticilerin benzer adlı kayıtlarını koru.
            if (ThirdPartyVendorPrefixes.Any(v => name.StartsWith(v, StringComparison.OrdinalIgnoreCase)))
                return false;

            foreach (string token in tokens)
            {
                if (token.Length < 6 || BlacklistedTokens.Contains(token))
                    continue;

                if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void CleanVendorKeys(RegistryKey root, string basePath, string rootName, HashSet<string> tokens, bool backupRegistry, ResidualCleanResult result)
        {
            // Genel HKLM vendor anahtarı silme güvenlik nedeniyle yasaktır.
            if (rootName.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[ResidualCleaner] HKLM Vendor silme engellendi: {rootName}\\{basePath}");
                return;
            }

            try
            {
                using var parentKey = root.OpenSubKey(basePath, writable: true);
                if (parentKey == null) return;

                foreach (var name in parentKey.GetSubKeyNames())
                {
                    if (BlacklistedTokens.Contains(name)) continue;

                    // KESİN EŞLEŞME: Yalnızca tam olarak token adına eşit olan anahtar silinir!
                    if (tokens.Any(t => name.Equals(t, StringComparison.OrdinalIgnoreCase)))
                    {
                        string fullPath = $@"{rootName}\{basePath}\{name}";
                        if (backupRegistry)
                        {
                            string? backup = BackupRegistryKey(fullPath);
                            if (backup != null)
                                result.CreatedBackups.Add(backup);
                        }

                        try
                        {
                            parentKey.DeleteSubKeyTree(name, throwOnMissingSubKey: false);

                            // Gerçek diff doğrulaması: Anahtar gerçekten silindi mi?
                            using var checkKey = parentKey.OpenSubKey(name);
                            if (checkKey == null)
                            {
                                result.DeletedRegistryKeysCount++;
                                Debug.WriteLine($"[ResidualCleaner] Registry silindi: {fullPath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ResidualCleaner] Registry silinemedi ({fullPath}): {ex.Message}");
                        }
                    }
                }
            }
            catch { }
        }

        private static void CleanUninstallKeysInternal(RegistryKey root, string uninstallPath, string rootName, HashSet<string> tokens, string? displayName, bool backupRegistry, ResidualCleanResult result)
        {
            // HKLM uninstall anahtarlarını silme güvenlik nedeniyle KESİNLİKLE YASAKTIR
            if (rootName.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[ResidualCleaner] HKLM Uninstall silme engellendi: {rootName}\\{uninstallPath}");
                return;
            }

            try
            {
                using var uninstallKey = root.OpenSubKey(uninstallPath, writable: true);
                if (uninstallKey == null) return;

                foreach (var subName in uninstallKey.GetSubKeyNames())
                {
                    if (BlacklistedTokens.Contains(subName)) continue;

                    bool shouldDelete = false;

                    if (tokens.Any(t => subName.Equals(t, StringComparison.OrdinalIgnoreCase)))
                    {
                        shouldDelete = true;
                    }
                    else
                    {
                        try
                        {
                            using var sub = uninstallKey.OpenSubKey(subName);
                            if (sub != null)
                            {
                                string? dName = sub.GetValue("DisplayName")?.ToString();
                                if (!string.IsNullOrEmpty(dName))
                                {
                                    if (MatchesStrict(dName, tokens) ||
                                        (!string.IsNullOrEmpty(displayName) && dName.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        shouldDelete = true;
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    if (shouldDelete)
                    {
                        string fullPath = $@"{rootName}\{uninstallPath}\{subName}";
                        if (backupRegistry)
                        {
                            string? backup = BackupRegistryKey(fullPath);
                            if (backup != null)
                                result.CreatedBackups.Add(backup);
                        }

                        try
                        {
                            uninstallKey.DeleteSubKeyTree(subName, throwOnMissingSubKey: false);

                            // Gerçek diff doğrulaması
                            using var checkKey = uninstallKey.OpenSubKey(subName);
                            if (checkKey == null)
                            {
                                result.DeletedRegistryKeysCount++;
                                Debug.WriteLine($"[ResidualCleaner] Uninstall anahtarı silindi: {fullPath}");
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void CleanStartupValuesInternal(RegistryKey root, string runPath, HashSet<string> tokens, ResidualCleanResult result)
        {
            try
            {
                using var runKey = root.OpenSubKey(runPath, writable: true);
                if (runKey == null) return;

                foreach (var valName in runKey.GetValueNames())
                {
                    if (BlacklistedTokens.Contains(valName)) continue;

                    string? valData = runKey.GetValue(valName)?.ToString();
                    if (MatchesStrict(valName, tokens) || (!string.IsNullOrEmpty(valData) && MatchesStrict(valData, tokens)))
                    {
                        try
                        {
                            runKey.DeleteValue(valName, throwOnMissingValue: false);

                            // Gerçek diff doğrulaması
                            if (runKey.GetValue(valName) == null)
                            {
                                result.DeletedRegistryValuesCount++;
                                Debug.WriteLine($"[ResidualCleaner] Startup Run değeri silindi: {valName}");
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Güvenli Dosya/Klasör Silme Yardımcıları

        private static bool IsProtectedDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;

            try
            {
                string fullPath = Path.GetFullPath(path).TrimEnd('\\');

                // Sürücü kökü ise (C:\, D:\ vb.)
                if (Path.GetPathRoot(fullPath)?.TrimEnd('\\').Equals(fullPath, StringComparison.OrdinalIgnoreCase) == true)
                    return true;

                if (ProtectedDirectories.Contains(fullPath))
                    return true;

                // Windows dizini altı (Temp hariç)
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
                if (fullPath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase) &&
                    !fullPath.Contains("Temp", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }

            return false;
        }

        private static void SafeDeleteDirectory(string dirPath, ResidualCleanResult result)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath) || IsProtectedDirectory(dirPath))
                return;

            try
            {
                var dir = new DirectoryInfo(dirPath);

                // Kök dizin kavşak noktası (Junction / Symlink) ise ASLA hedefe girme; yalnızca kavşağı sil!
                if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    dir.Delete();
                    if (!Directory.Exists(dirPath))
                    {
                        result.DeletedFoldersCount++;
                        Debug.WriteLine($"[ResidualCleaner] ReparsePoint kavşağı çözüldü: {dirPath}");
                    }
                    return;
                }

                // Salt okunur bayrakları kaldırırken reparse point / kavşak dizinlerini atla
                var dirQueue = new Queue<string>();
                dirQueue.Enqueue(dirPath);
                int filesCount = 0;

                while (dirQueue.Count > 0)
                {
                    string current = dirQueue.Dequeue();
                    try
                    {
                        var curDi = new DirectoryInfo(current);
                        if ((curDi.Attributes & FileAttributes.ReparsePoint) != 0)
                            continue;

                        foreach (var file in curDi.EnumerateFiles())
                        {
                            try
                            {
                                if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                                    file.Attributes &= ~FileAttributes.ReadOnly;
                                filesCount++;
                            }
                            catch { }
                        }

                        foreach (var sub in curDi.EnumerateDirectories())
                        {
                            if ((sub.Attributes & FileAttributes.ReparsePoint) == 0)
                            {
                                dirQueue.Enqueue(sub.FullName);
                            }
                        }
                    }
                    catch { }
                }

                dir.Delete(true);

                // Gerçek diff kontrolü: Dizin disktan gerçekten silindi mi?
                if (!Directory.Exists(dirPath))
                {
                    result.DeletedFoldersCount++;
                    result.DeletedFilesCount += filesCount;
                    Debug.WriteLine($"[ResidualCleaner] Dizin silindi: {dirPath}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResidualCleaner] Dizin ilk denemede silinemedi ({dirPath}): {ex.Message}. Yeniden deneniyor...");
                try
                {
                    System.Threading.Thread.Sleep(300);
                    if (Directory.Exists(dirPath))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c rd /s /q \"{dirPath}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using var p = Process.Start(psi);
                        if (p != null)
                        {
                            bool exited = p.WaitForExit(3000);
                            if (!exited)
                            {
                                try { p.Kill(); } catch { }
                            }
                        }

                        // Fallback sonrası doğrulanmış diff kontrolü
                        if (!Directory.Exists(dirPath))
                        {
                            result.DeletedFoldersCount++;
                            Debug.WriteLine($"[ResidualCleaner] Dizin cmd fallback ile silindi: {dirPath}");
                        }
                    }
                }
                catch { }
            }
        }

        private static void SafeDeleteFile(string filePath, ResidualCleanResult result)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            try
            {
                var fi = new FileInfo(filePath);
                if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                    fi.Attributes &= ~FileAttributes.ReadOnly;

                fi.Delete();

                // Gerçek diff doğrulaması: Dosya diskten gerçekten silindi mi?
                if (!File.Exists(filePath))
                {
                    result.DeletedFilesCount++;
                    Debug.WriteLine($"[ResidualCleaner] Dosya silindi: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResidualCleaner] Dosya silinemedi ({filePath}): {ex.Message}");
            }
        }

        #endregion
    }

    public class ResidualScanResult
    {
        public List<string> Folders { get; set; } = new();
        public List<string> Shortcuts { get; set; } = new();
        public List<string> RegistryKeys { get; set; } = new();
        public List<string> RegistryValues { get; set; } = new();
        public List<string> HklmKeys { get; set; } = new();

        public int TotalItemsFound => Folders.Count + Shortcuts.Count + RegistryKeys.Count + RegistryValues.Count;
    }

    public class ResidualCleanResult
    {
        public int DeletedFoldersCount { get; set; }
        public int DeletedFilesCount { get; set; }
        public int DeletedRegistryKeysCount { get; set; }
        public int DeletedRegistryValuesCount { get; set; }
        public List<string> CreatedBackups { get; set; } = new();

        public int TotalItemsPurged => DeletedFoldersCount + DeletedFilesCount + DeletedRegistryKeysCount + DeletedRegistryValuesCount;
    }
}
