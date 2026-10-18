using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Xml;
using Microsoft.Win32;
using PureWin.Models;

namespace PureWin.Services
{
    public static class StartupAppService
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Run32SubKey = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";

        private const string ApprovedRunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedRun32SubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
        private const string ApprovedStartupFolderSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        private const string AppModelSystemAppDataSubKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";

        /// <summary>
        /// Görev Yöneticisi ile birebir uyumlu Son BIOS Zamanı (Firmware POST time).
        /// HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power -> FwPOSTTime (ms)
        /// </summary>
        public static string GetLastBiosTime()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power");
                if (key != null)
                {
                    var val = key.GetValue("FwPOSTTime");
                    if (val is int intVal && intVal > 0)
                    {
                        double sec = intVal / 1000.0;
                        return $"{sec:0.0} Saniye";
                    }
                    if (val is long longVal && longVal > 0)
                    {
                        double sec = longVal / 1000.0;
                        return $"{sec:0.0} Saniye";
                    }
                }
            }
            catch
            {
                // Sessizce devam et
            }

            return "Ölçülemedi";
        }

        /// <summary>
        /// Windows Kayıt Defteri, Başlangıç Klasörleri ve Modern Windows Mağaza (MSIX/UWP) görevlerini tarar.
        /// </summary>
        public static List<StartupAppItem> GetStartupApps()
        {
            var apps = new List<StartupAppItem>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. HKCU Run
            ScanRegistryKey(Registry.CurrentUser, RunSubKey, ApprovedRunSubKey, true, StartupSourceType.HkcuRun, "Kayıt Defteri (HKCU)", apps, seenIds);

            // 2. HKLM Run
            ScanRegistryKey(Registry.LocalMachine, RunSubKey, ApprovedRunSubKey, false, StartupSourceType.HklmRun, "Kayıt Defteri (HKLM)", apps, seenIds);

            // 3. HKLM Run32 (WOW6432Node)
            ScanRegistryKey(Registry.LocalMachine, Run32SubKey, ApprovedRun32SubKey, false, StartupSourceType.HklmRun32, "Kayıt Defteri (HKLM 32-bit)", apps, seenIds);

            // 4. Kullanıcı Başlangıç Klasörü (shell:startup)
            string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            ScanStartupFolder(userStartup, ApprovedStartupFolderSubKey, true, StartupSourceType.UserStartupFolder, "Başlangıç Klasörü", apps, seenIds);

            // 5. Ortak Başlangıç Klasörü (shell:common startup)
            string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            ScanStartupFolder(commonStartup, ApprovedStartupFolderSubKey, false, StartupSourceType.CommonStartupFolder, "Ortak Başlangıç Klasörü", apps, seenIds);

            // 6. Modern Windows Mağaza & MSIX Başlangıç Görevleri (Terminal, Claude, LocalSend, PC Manager vb.)
            ScanUwpStartupTasks(apps, seenIds);

            // Sıralama: Önce Etkin olanlar, sonra ada göre
            apps.Sort((a, b) =>
            {
                if (a.IsEnabled != b.IsEnabled)
                    return a.IsEnabled ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            return apps;
        }

        private static void ScanRegistryKey(
            RegistryKey rootKey,
            string subKeyPath,
            string approvedSubKeyPath,
            bool isCurrentUser,
            StartupSourceType sourceType,
            string sourceDisplay,
            List<StartupAppItem> apps,
            HashSet<string> seenIds)
        {
            try
            {
                using var runKey = rootKey.OpenSubKey(subKeyPath, false);
                if (runKey == null) return;

                using var approvedKey = rootKey.OpenSubKey(approvedSubKeyPath, false);

                string[] valueNames = runKey.GetValueNames();
                foreach (string valName in valueNames)
                {
                    if (string.IsNullOrWhiteSpace(valName)) continue;

                    string id = $"{sourceType}_{valName}";
                    if (seenIds.Contains(id)) continue;

                    var rawVal = runKey.GetValue(valName);
                    if (rawVal == null) continue;

                    string commandLine = rawVal.ToString() ?? string.Empty;
                    var (filePath, args) = ParseCommandLine(commandLine);

                    // Durum kontrolü (StartupApproved)
                    bool isEnabled = true; // Varsayılan: Etkin
                    if (approvedKey != null)
                    {
                        var approvedVal = approvedKey.GetValue(valName) as byte[];
                        if (approvedVal != null && approvedVal.Length > 0)
                        {
                            // 0x02 veya çift sayı = Etkin, 0x03 veya tek sayı = Devre Dışı
                            isEnabled = (approvedVal[0] % 2 == 0 && approvedVal[0] != 0);
                        }
                    }

                    // Yayıncı ve Dostça İsim Çözümleme
                    string publisher = "Bilinmeyen Yayıncı";
                    string displayName = valName;
                    long fileSize = 0;

                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(filePath);
                            if (!string.IsNullOrWhiteSpace(fvi.CompanyName))
                                publisher = fvi.CompanyName.Trim();

                            if (!string.IsNullOrWhiteSpace(fvi.FileDescription))
                                displayName = fvi.FileDescription.Trim();
                            else if (!string.IsNullOrWhiteSpace(fvi.ProductName))
                                displayName = fvi.ProductName.Trim();

                            fileSize = new FileInfo(filePath).Length;
                        }
                        catch
                        {
                            // Dosya okunamıyorsa devam et
                        }
                    }

                    // Başlangıç Etkisi Hesaplama
                    var (impact, impactDisplay) = CalculateImpact(displayName, valName, filePath, publisher, fileSize);

                    // İkon Çıkarma
                    var (iconSource, fallbackIcon) = ResolveIcon(filePath, displayName);

                    var item = new StartupAppItem
                    {
                        Id = id,
                        Name = displayName,
                        Publisher = publisher,
                        CommandLine = commandLine,
                        FilePath = filePath,
                        Arguments = args,
                        DisplayPath = string.IsNullOrWhiteSpace(args) ? filePath : $"{filePath} {args}",
                        SourceType = sourceType,
                        SourceDisplay = sourceDisplay,
                        RegistryKeyPath = subKeyPath,
                        StartupApprovedKeyPath = approvedSubKeyPath,
                        IsCurrentUser = isCurrentUser,
                        ValueName = valName,
                        IsFileBased = false,
                        IsEnabled = isEnabled,
                        Impact = impact,
                        ImpactDisplay = impactDisplay,
                        IconSource = iconSource,
                        FallbackIcon = fallbackIcon
                    };

                    apps.Add(item);
                    seenIds.Add(id);
                }
            }
            catch
            {
                // İlgili kayıt defteri anahtarı açılamıyorsa devam et
            }
        }

        private static void ScanStartupFolder(
            string folderPath,
            string approvedSubKeyPath,
            bool isCurrentUser,
            StartupSourceType sourceType,
            string sourceDisplay,
            List<StartupAppItem> apps,
            HashSet<string> seenIds)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return;

            try
            {
                using var approvedKey = isCurrentUser
                    ? Registry.CurrentUser.OpenSubKey(approvedSubKeyPath, false)
                    : Registry.LocalMachine.OpenSubKey(approvedSubKeyPath, false);

                var files = Directory.GetFiles(folderPath);
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    if (string.Equals(fileName, "desktop.ini", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string id = $"{sourceType}_{fileName}";
                    if (seenIds.Contains(id)) continue;

                    string displayName = Path.GetFileNameWithoutExtension(file);
                    string targetPath = file;
                    string publisher = "Bilinmeyen Yayıncı";
                    long fileSize = 0;

                    // Eğer kısayolsa (.lnk)
                    if (Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            fileSize = new FileInfo(file).Length;
                        }
                        catch { }
                    }
                    else if (File.Exists(file))
                    {
                        try
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(file);
                            if (!string.IsNullOrWhiteSpace(fvi.CompanyName))
                                publisher = fvi.CompanyName.Trim();
                            if (!string.IsNullOrWhiteSpace(fvi.FileDescription))
                                displayName = fvi.FileDescription.Trim();
                            fileSize = new FileInfo(file).Length;
                        }
                        catch { }
                    }

                    // Durum kontrolü (StartupApproved\StartupFolder)
                    bool isEnabled = true;
                    if (approvedKey != null)
                    {
                        var approvedVal = approvedKey.GetValue(fileName) as byte[];
                        if (approvedVal != null && approvedVal.Length > 0)
                        {
                            isEnabled = (approvedVal[0] % 2 == 0 && approvedVal[0] != 0);
                        }
                    }

                    var (impact, impactDisplay) = CalculateImpact(displayName, fileName, targetPath, publisher, fileSize);
                    var (iconSource, fallbackIcon) = ResolveIcon(file, displayName);

                    var item = new StartupAppItem
                    {
                        Id = id,
                        Name = displayName,
                        Publisher = publisher,
                        CommandLine = file,
                        FilePath = file,
                        Arguments = string.Empty,
                        DisplayPath = file,
                        SourceType = sourceType,
                        SourceDisplay = sourceDisplay,
                        RegistryKeyPath = folderPath,
                        StartupApprovedKeyPath = approvedSubKeyPath,
                        IsCurrentUser = isCurrentUser,
                        ValueName = fileName,
                        IsFileBased = true,
                        IsEnabled = isEnabled,
                        Impact = impact,
                        ImpactDisplay = impactDisplay,
                        IconSource = iconSource,
                        FallbackIcon = fallbackIcon
                    };

                    apps.Add(item);
                    seenIds.Add(id);
                }
            }
            catch
            {
                // Başlangıç klasörü taranamıyorsa devam et
            }
        }

        /// <summary>
        /// Modern Windows Mağaza & MSIX/UWP paketlerinin başlangıç görevlerini (windows.startupTask) tarar.
        /// </summary>
        private static void ScanUwpStartupTasks(List<StartupAppItem> apps, HashSet<string> seenIds)
        {
            try
            {
                using var baseKey = Registry.CurrentUser.OpenSubKey(AppModelSystemAppDataSubKey, false);
                if (baseKey == null) return;

                string[] packageFamilies = baseKey.GetSubKeyNames();
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string windowsAppsBase = Path.Combine(programFiles, "WindowsApps");

                foreach (string pkgFamily in packageFamilies)
                {
                    if (string.IsNullOrWhiteSpace(pkgFamily)) continue;

                    try
                    {
                        using var pkgKey = baseKey.OpenSubKey(pkgFamily, false);
                        if (pkgKey == null) continue;

                        string[] subKeys = pkgKey.GetSubKeyNames();
                        foreach (string subName in subKeys)
                        {
                            // Sistem klasörlerini filtrele
                            if (subName.Equals("Schemas", StringComparison.OrdinalIgnoreCase) ||
                                subName.Equals("PSR", StringComparison.OrdinalIgnoreCase) ||
                                subName.Equals("HAM", StringComparison.OrdinalIgnoreCase) ||
                                subName.Equals("SplashScreen", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string taskSubKeyPath = $@"{AppModelSystemAppDataSubKey}\{pkgFamily}\{subName}";
                            using var taskKey = baseKey.OpenSubKey($@"{pkgFamily}\{subName}", false);
                            if (taskKey == null) continue;

                            var stateVal = taskKey.GetValue("State");
                            if (stateVal == null) continue;

                            // StartupTask anahtarları State ile birlikte UserEnabledStartupOnce veya ShowNotification içerir
                            bool isStartupTask = taskKey.GetValue("UserEnabledStartupOnce") != null || taskKey.GetValue("ShowNotification") != null;
                            if (!isStartupTask) continue;

                            string id = $"UWP_{pkgFamily}_{subName}";
                            if (seenIds.Contains(id)) continue;

                            int stateInt = Convert.ToInt32(stateVal);
                            bool isEnabled = (stateInt == 2); // 2 = Etkin, 0 veya 1 = Devre Dışı

                            // PackageFullName'i Schemas anahtarından oku
                            string packageFullName = string.Empty;
                            using (var schemasKey = pkgKey.OpenSubKey("Schemas", false))
                            {
                                packageFullName = schemasKey?.GetValue("PackageFullName")?.ToString() ?? string.Empty;
                            }

                            string installFolder = !string.IsNullOrWhiteSpace(packageFullName)
                                ? Path.Combine(windowsAppsBase, packageFullName)
                                : string.Empty;

                            string displayName = string.Empty;
                            string publisher = string.Empty;
                            string exeRelative = string.Empty;

                            // Manifestodan bilgi okumayı dene
                            string manifestPath = !string.IsNullOrWhiteSpace(installFolder)
                                ? Path.Combine(installFolder, "AppxManifest.xml")
                                : string.Empty;

                            if (File.Exists(manifestPath))
                            {
                                try
                                {
                                    var doc = new XmlDocument();
                                    doc.Load(manifestPath);
                                    var nsmgr = new XmlNamespaceManager(doc.NameTable);
                                    nsmgr.AddNamespace("p", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

                                    var propNode = doc.SelectSingleNode("//p:Properties", nsmgr) ?? doc.SelectSingleNode("//Properties");
                                    if (propNode != null)
                                    {
                                        var dispNode = propNode["DisplayName"];
                                        if (dispNode != null && !dispNode.InnerText.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                                        {
                                            displayName = dispNode.InnerText.Trim();
                                        }
                                        var pubNode = propNode["PublisherDisplayName"];
                                        if (pubNode != null)
                                        {
                                            publisher = pubNode.InnerText.Trim();
                                        }
                                    }

                                    var appNode = doc.SelectSingleNode("//p:Application", nsmgr) ?? doc.SelectSingleNode("//Application");
                                    var exeAttr = appNode?.Attributes?["Executable"];
                                    if (exeAttr != null)
                                    {
                                        exeRelative = exeAttr.Value;
                                    }
                                }
                                catch { }
                            }

                            // ms-resource veya boşsa dostça isimleri ata
                            if (string.IsNullOrWhiteSpace(displayName) || displayName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                            {
                                if (pkgFamily.Contains("WindowsTerminal", StringComparison.OrdinalIgnoreCase))
                                    displayName = "Terminal";
                                else if (pkgFamily.Contains("MicrosoftPCManager", StringComparison.OrdinalIgnoreCase))
                                    displayName = "PC Manager";
                                else if (pkgFamily.Contains("LocalSend", StringComparison.OrdinalIgnoreCase))
                                    displayName = "LocalSend";
                                else if (pkgFamily.Contains("Claude", StringComparison.OrdinalIgnoreCase))
                                    displayName = "Claude";
                                else
                                {
                                    string rawPrefix = pkgFamily.Split('_')[0];
                                    displayName = rawPrefix.Replace(".", " ").Trim();
                                }
                            }

                            if (string.IsNullOrWhiteSpace(publisher))
                            {
                                if (pkgFamily.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                                    publisher = "Microsoft Corporation";
                                else if (pkgFamily.Contains("Claude", StringComparison.OrdinalIgnoreCase))
                                    publisher = "Anthropic, PBC";
                                else
                                    publisher = "Bilinmeyen Yayıncı";
                            }

                            string cleanExePath = string.Empty;
                            if (!string.IsNullOrWhiteSpace(exeRelative) && !string.IsNullOrWhiteSpace(installFolder))
                            {
                                string exeCandidate = exeRelative.Split(' ')[0];
                                cleanExePath = Path.Combine(installFolder, exeCandidate);
                            }

                            var (iconSource, fallbackIcon) = ResolveIcon(cleanExePath, displayName);

                            var item = new StartupAppItem
                            {
                                Id = id,
                                Name = displayName,
                                Publisher = publisher,
                                CommandLine = cleanExePath,
                                FilePath = cleanExePath,
                                InstallFolder = installFolder,
                                PackageFamilyName = pkgFamily,
                                StartupTaskKey = subName,
                                SourceType = StartupSourceType.AppxPackage,
                                SourceDisplay = "Windows Paketi (MSIX)",
                                RegistryKeyPath = taskSubKeyPath,
                                ValueName = subName,
                                IsCurrentUser = true,
                                IsFileBased = false,
                                IsEnabled = isEnabled,
                                Impact = StartupImpact.Unknown,
                                ImpactDisplay = "Yok / Düşük",
                                IconSource = iconSource,
                                FallbackIcon = fallbackIcon
                            };

                            apps.Add(item);
                            seenIds.Add(id);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// Bir başlangıç uygulamasını etkinleştirir veya devre dışı bırakır (StartupApproved veya UWP State).
        /// </summary>
        public static bool SetStartupAppState(StartupAppItem item, bool enable)
        {
            if (item == null) return false;

            try
            {
                // UWP / MSIX Mağaza Uygulamaları
                if (item.IsUwpPackage)
                {
                    using var key = Registry.CurrentUser.CreateSubKey(item.RegistryKeyPath, true);
                    if (key == null) return false;

                    int newState = enable ? 2 : 0;
                    key.SetValue("State", newState, RegistryValueKind.DWord);
                    item.IsEnabled = enable;
                    return true;
                }

                // Klasik Win32 ve Başlangıç Klasörü Uygulamaları (StartupApproved)
                using var root = item.IsCurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
                using var regKey = root.CreateSubKey(item.StartupApprovedKeyPath, true);
                if (regKey == null) return false;

                byte[]? existing = regKey.GetValue(item.ValueName) as byte[];
                byte[] bytes = new byte[12];

                if (existing != null && existing.Length >= 12)
                {
                    Array.Copy(existing, bytes, 12);
                }

                // 0x02 = Etkin, 0x03 = Devre Dışı
                bytes[0] = enable ? (byte)0x02 : (byte)0x03;

                if (!enable)
                {
                    // Devre dışı bırakılma zaman damgası (FILETIME)
                    long fileTime = DateTime.UtcNow.ToFileTime();
                    byte[] timeBytes = BitConverter.GetBytes(fileTime);
                    Array.Copy(timeBytes, 0, bytes, 4, 8);
                }

                regKey.SetValue(item.ValueName, bytes, RegistryValueKind.Binary);
                item.IsEnabled = enable;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartupAppService] SetStartupAppState hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Uygulamanın veya paketin dosya konumunu Windows Gezgini'nde açar.
        /// </summary>
        public static void OpenItemLocation(StartupAppItem item)
        {
            if (item == null) return;

            try
            {
                // 1. Doğrudan dosya mevcutsa seçili olarak aç
                if (!string.IsNullOrWhiteSpace(item.FilePath) && File.Exists(item.FilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{item.FilePath}\"",
                        UseShellExecute = true
                    });
                    return;
                }

                // 2. Paket yükleme klasörü varsa klasörü aç
                if (!string.IsNullOrWhiteSpace(item.InstallFolder) && Directory.Exists(item.InstallFolder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{item.InstallFolder}\"",
                        UseShellExecute = true
                    });
                    return;
                }

                // 3. Dosya yolu bir klasörse klasörü aç
                if (!string.IsNullOrWhiteSpace(item.FilePath) && Directory.Exists(item.FilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{item.FilePath}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartupAppService] OpenItemLocation hatası: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Komut satırı dizesini ayrıştırarak temiz dosya yolu ve argümanları döndürür.
        /// </summary>
        public static (string filePath, string arguments) ParseCommandLine(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return (string.Empty, string.Empty);

            string trimmed = Environment.ExpandEnvironmentVariables(commandLine.Trim());

            // Tırnak içindeyse
            if (trimmed.StartsWith("\""))
            {
                int nextQuote = trimmed.IndexOf('\"', 1);
                if (nextQuote > 1)
                {
                    string path = trimmed.Substring(1, nextQuote - 1).Trim();
                    string args = nextQuote + 1 < trimmed.Length ? trimmed.Substring(nextQuote + 1).Trim() : string.Empty;
                    return (path, args);
                }
            }

            // .exe aranarak ayrıştırma
            int exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex > 0)
            {
                string path = trimmed.Substring(0, exeIndex + 4).Trim();
                string args = exeIndex + 4 < trimmed.Length ? trimmed.Substring(exeIndex + 4).Trim() : string.Empty;
                return (path, args);
            }

            // Boşluk ile ayrıştırma
            int spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex > 0)
            {
                string path = trimmed.Substring(0, spaceIndex).Trim();
                string args = trimmed.Substring(spaceIndex + 1).Trim();
                return (path, args);
            }

            return (trimmed, string.Empty);
        }

        /// <summary>
        /// Uygulamanın Windows açılışındaki performans etkisini belirler.
        /// </summary>
        private static (StartupImpact impact, string display) CalculateImpact(
            string name, string valName, string path, string publisher, long fileSize)
        {
            string searchStr = $"{name} {valName} {Path.GetFileName(path)} {publisher}".ToLowerInvariant();

            // Yüksek Etki: Bilinen ağır başlatıcılar, istemciler veya 20MB'tan büyük dosyalar
            if (searchStr.Contains("discord") ||
                searchStr.Contains("steam") ||
                searchStr.Contains("epicgames") ||
                searchStr.Contains("spotify") ||
                searchStr.Contains("chrome") ||
                searchStr.Contains("edge") ||
                searchStr.Contains("riot") ||
                searchStr.Contains("battlenet") ||
                searchStr.Contains("teams") ||
                searchStr.Contains("skype") ||
                searchStr.Contains("onedrive") ||
                searchStr.Contains("dropbox") ||
                searchStr.Contains("torrent") ||
                searchStr.Contains("docker") ||
                searchStr.Contains("virtualbox") ||
                fileSize > 20 * 1024 * 1024)
            {
                return (StartupImpact.High, "⚠️ Yüksek Etki");
            }

            // Düşük Etki: Donanım/Sürücü yardımcıları ve sistem tepsi simgeleri
            if (searchStr.Contains("securityhealth") ||
                searchStr.Contains("ctfmon") ||
                searchStr.Contains("realtek") ||
                searchStr.Contains("rtkaudu") ||
                searchStr.Contains("nvidia") ||
                searchStr.Contains("nvcontainer") ||
                searchStr.Contains("intel") ||
                searchStr.Contains("amd") ||
                searchStr.Contains("audio") ||
                searchStr.Contains("synaptics") ||
                searchStr.Contains("touchpad"))
            {
                return (StartupImpact.Low, "🌱 Düşük Etki");
            }

            // Orta Etki: Yardımcı araçlar, indirme yöneticileri, küçük arka plan araçları
            if (fileSize > 5 * 1024 * 1024)
            {
                return (StartupImpact.Medium, "⚡ Orta Etki");
            }

            return (StartupImpact.Medium, "⚡ Orta Etki");
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;

        /// <summary>
        /// Dosyadan WPF ImageSource ikonu veya uygun kategori emojisini üretir.
        /// </summary>
        private static (BitmapSource? icon, string fallback) ResolveIcon(string filePath, string name)
        {
            string lowerName = name.ToLowerInvariant();
            string fallback = "🚀";

            if (lowerName.Contains("discord") || lowerName.Contains("chat") || lowerName.Contains("skype") || lowerName.Contains("teams"))
                fallback = "💬";
            else if (lowerName.Contains("steam") || lowerName.Contains("game") || lowerName.Contains("riot") || lowerName.Contains("epic"))
                fallback = "🎮";
            else if (lowerName.Contains("browser") || lowerName.Contains("chrome") || lowerName.Contains("opera") || lowerName.Contains("edge") || lowerName.Contains("firefox"))
                fallback = "🌐";
            else if (lowerName.Contains("terminal") || lowerName.Contains("powershell") || lowerName.Contains("cmd"))
                fallback = "💻";
            else if (lowerName.Contains("claude") || lowerName.Contains("ai"))
                fallback = "🤖";
            else if (lowerName.Contains("localsend") || lowerName.Contains("share"))
                fallback = "📡";
            else if (lowerName.Contains("pc manager") || lowerName.Contains("pcmanager"))
                fallback = "🛠️";
            else if (lowerName.Contains("audio") || lowerName.Contains("realtek") || lowerName.Contains("sound"))
                fallback = "🔊";
            else if (lowerName.Contains("security") || lowerName.Contains("antivirus") || lowerName.Contains("defender"))
                fallback = "🛡️";
            else if (lowerName.Contains("download") || lowerName.Contains("cloud") || lowerName.Contains("warp"))
                fallback = "☁️";

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return (null, fallback);
            }

            try
            {
                var shinfo = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);

                if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                {
                    var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    // UI Thread dışından erişim için dondur (Freeze)
                    bitmapSource.Freeze();

                    // GDI handle'ını serbest bırak
                    DestroyIcon(shinfo.hIcon);

                    return (bitmapSource, fallback);
                }
            }
            catch
            {
                // İkon çıkarma başarısız olursa fallback ikon kullan
            }

            return (null, fallback);
        }
    }
}
