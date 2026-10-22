using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PureWin.Services
{
    public static class RegistryService
    {
        private static readonly string RegfilesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Regfiles");

        /// <summary>
        /// .reg dosyasını doğrudan reg.exe ile içe aktarır (5ms içinde atomic uygulanır)
        /// </summary>
        public static bool ImportRegFile(string fileName, bool isUndo = false)
        {
            try
            {
                string targetDir = isUndo ? Path.Combine(RegfilesDir, "undo") : RegfilesDir;
                string fullPath = Path.Combine(targetDir, fileName);

                if (!File.Exists(fullPath))
                {
                    // Alternatif olarak doğrudan tam yol kontrolü
                    if (File.Exists(fileName))
                        fullPath = fileName;
                    else
                        return false;
                }

                // reg.exe'nin kesinlikle kabul etmesi için UTF-16LE Unicode BOM kontrolü ve düzeltmesi
                EnsureUnicodeEncoding(fullPath);

                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"import \"{fullPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                bool exited = process.WaitForExit(8000);
                if (!exited)
                {
                    try { process.Kill(); } catch { }
                    return false;
                }

                bool success = process.ExitCode == 0;

                if (success && (fileName.Contains("Copilot", StringComparison.OrdinalIgnoreCase) || fileName.Contains("AI", StringComparison.OrdinalIgnoreCase)))
                {
                    SyncOOSU10(isUndo);
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error importing {fileName}: {ex.Message}");
                return false;
            }
        }

        private static void SyncOOSU10(bool isUndo)
        {
            try
            {
                string[] searchPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "OOSU10.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", "OOSU10.exe"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OOSU10.exe")
                };

                string oosuExe = string.Empty;
                foreach (var p in searchPaths)
                {
                    if (File.Exists(p)) { oosuExe = p; break; }
                }

                if (string.IsNullOrEmpty(oosuExe)) return;

                string symbol = isUndo ? "-" : "+";
                string[] settings = new[]
                {
                    "C002", "C007", "C008", "C010", "C011", "C012", "C013", "C014", "C015",
                    "C101", "C102", "C103", "C104", "C201", "C203", "C204", "C205", "C206",
                    "C207", "C208", "C209", "C210", "C211",
                    "M001", "M003", "M004", "M005", "M006", "M010", "M011", "M012", "M013",
                    "M014", "M015", "M016", "M017", "M018", "M019", "M022", "M024", "M025",
                    "M026", "M027", "M028", "M029", "M030", "M031", "M032", "M033", "M034", "M103",
                    "P010"
                };

                string tempCfg = Path.Combine(Path.GetTempPath(), "purewin_oosu_sync.cfg");
                using (var writer = new StreamWriter(tempCfg, false, System.Text.Encoding.ASCII))
                {
                    foreach (var s in settings)
                    {
                        writer.WriteLine($"{s}\t{symbol}");
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = oosuExe,
                    Arguments = $"\"{tempCfg}\" /quiet /nosrp",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);

                try { File.Delete(tempCfg); } catch { }
            }
            catch { }
        }

        private static void EnsureUnicodeEncoding(string filePath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                if (bytes.Length < 2 || bytes[0] != 0xFF || bytes[1] != 0xFE)
                {
                    string text = File.ReadAllText(filePath);
                    File.WriteAllText(filePath, text, System.Text.Encoding.Unicode);
                }
            }
            catch { }
        }

        /// <summary>
        /// .reg dosyasındaki anahtarları okuyup sistemde şu an geçerli olup olmadığını test eder
        /// </summary>
        public static bool IsRegFileApplied(string fileName)
        {
            try
            {
                string fullPath = Path.Combine(RegfilesDir, fileName);
                if (!File.Exists(fullPath))
                {
                    if (File.Exists(fileName))
                        fullPath = fileName;
                    else
                        return false;
                }

                string[] lines = File.ReadAllLines(fullPath);
                string currentHive = string.Empty;
                string currentSubKey = string.Empty;
                int matchedChecks = 0;
                int totalChecks = 0;

                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("Windows Registry"))
                        continue;

                    // 1. [-HKEY_...] anahtar silme kontrolü
                    if (line.StartsWith("[-") && line.EndsWith("]"))
                    {
                        string path = line.Substring(2, line.Length - 3);
                        int firstSlash = path.IndexOf('\\');
                        if (firstSlash > 0)
                        {
                            string delHive = path.Substring(0, firstSlash);
                            string delSubKey = path.Substring(firstSlash + 1);
                            totalChecks++;

                            RegistryHive hive = delHive switch
                            {
                                "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
                                "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
                                "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
                                "HKEY_USERS" => RegistryHive.Users,
                                _ => RegistryHive.CurrentUser
                            };

                            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                            using var subKey = baseKey.OpenSubKey(delSubKey, false);
                            if (subKey == null)
                            {
                                matchedChecks++;
                            }
                        }
                        continue;
                    }

                    // 2. [HKEY_...] anahtar başlığı
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string path = line.Substring(1, line.Length - 2);
                        int firstSlash = path.IndexOf('\\');
                        if (firstSlash > 0)
                        {
                            currentHive = path.Substring(0, firstSlash);
                            currentSubKey = path.Substring(firstSlash + 1);
                        }
                        continue;
                    }

                    // 3. "ValueName"=dword:... veya "ValueName"="string" veya "ValueName"=- veya @="..."
                    int eqIndex = line.IndexOf('=');
                    if (eqIndex > 0 && !string.IsNullOrEmpty(currentHive))
                    {
                        string rawValueName = line.Substring(0, eqIndex).Trim();
                        string rawValueData = line.Substring(eqIndex + 1).Trim();

                        string valueName = rawValueName.Trim('"');
                        if (rawValueName == "@") valueName = ""; // Varsayılan değer

                        totalChecks++;

                        RegistryHive hive = currentHive switch
                        {
                            "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
                            "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
                            "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
                            "HKEY_USERS" => RegistryHive.Users,
                            _ => RegistryHive.CurrentUser
                        };

                        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                        using var subKey = baseKey.OpenSubKey(currentSubKey, false);

                        // Değer silme kuralı ("ValueName"=-)
                        if (rawValueData == "-")
                        {
                            if (subKey == null || subKey.GetValue(valueName) == null)
                            {
                                matchedChecks++;
                            }
                            continue;
                        }

                        if (subKey != null)
                        {
                            var actualVal = subKey.GetValue(valueName);
                            if (actualVal != null)
                            {
                                if (actualVal is byte[] actualBytes && rawValueData.StartsWith("hex:"))
                                {
                                    string hexPart = rawValueData.Substring(4).Replace(" ", "").Replace("\\", "");
                                    string[] hexTokens = hexPart.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    byte[] expectedBytes = new byte[hexTokens.Length];
                                    for (int i = 0; i < hexTokens.Length; i++)
                                    {
                                        expectedBytes[i] = Convert.ToByte(hexTokens[i], 16);
                                    }

                                    bool match = actualBytes.Length >= expectedBytes.Length;
                                    if (match)
                                    {
                                        for (int i = 0; i < expectedBytes.Length; i++)
                                        {
                                            if (actualBytes[i] != expectedBytes[i])
                                            {
                                                match = false;
                                                break;
                                            }
                                        }
                                    }
                                    if (match)
                                    {
                                        matchedChecks++;
                                    }
                                }
                                else
                                {
                                    string expectedVal = ParseExpectedValue(rawValueData);
                                    string actualStr = actualVal.ToString()?.Trim() ?? string.Empty;

                                    bool isMatch = string.Equals(actualStr, expectedVal, StringComparison.OrdinalIgnoreCase);

                                    // Sayısal (DWORD) karşılaştırma: int taşmasını ve signed/unsigned (-1 ile 4294967295) farkını çöz
                                    if (!isMatch &&
                                        long.TryParse(actualStr, out long aNum) &&
                                        long.TryParse(expectedVal, out long eNum))
                                    {
                                        isMatch = (aNum == eNum) || ((uint)aNum == (uint)eNum);
                                    }

                                    if (isMatch)
                                    {
                                        matchedChecks++;
                                    }
                                }
                            }
                        }
                    }
                }

                if (totalChecks == 0) return false;
                if (totalChecks == 1) return matchedChecks == 1;
                if (totalChecks == 2) return matchedChecks == 2;

                // 3 veya daha fazla kontrol içeren dosyalarda (ör. 15-18 anahtarlı büyük telemetri ve sistem kuralları):
                // Farklı Windows 11 sürümleri arasındaki küçük ikincil farkları tolere etmek için %70 eşleşme yeterlidir.
                return ((double)matchedChecks / totalChecks) >= 0.70;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// AI servisinin (WSAIFabricSvc) otomatik başlatılmasının engellenip engellenmediğini kontrol eder.
        /// Hem "Manual" (3) hem de "Disabled" (4) durumları otomatik başlatmayı engellediği için geçerli sayılır.
        /// Raphire Win11Debloat gibi araçlar servisi "Manual" (3) olarak beklediğinden uygulama da 3 yazar.
        /// </summary>
        public static bool IsAIServiceAutoStartPrevented()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\WSAIFabricSvc", false);
                if (key == null) return false;
                object? start = key.GetValue("Start");
                return start is int value && (value == 3 || value == 4);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Widget'ların kapatılıp kapatılmadığını kontrol eder.
        /// Windows 11 bazı sürümlerde TaskbarDa değerini siliyor; bu yüzden resmi Dsh politikası
        /// tek başına da yeterli kabul edilir (aksi halde toggle her açılışta kapalı görünüyordu).
        /// </summary>
        public static bool AreWidgetsDisabled()
        {
            try
            {
                using (var dsh = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Dsh", false))
                {
                    if (dsh?.GetValue("AllowNewsAndInterests") is int policy && policy == 0)
                        return true;
                }

                using (var advanced = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", false))
                {
                    if (advanced?.GetValue("TaskbarDa") is int taskbar && taskbar == 0)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// OneDrive'ın gezgin sol panelinden gizlenip gizlenmediğini kontrol eder.
        /// OneDrive kurulu değilse ilgili CLSID anahtarı hiç oluşmaz; bu durumda tweak daha önce
        /// uygulanmışsa (snapshot varsa) "gizli" kabul edilir. Aksi halde toggle her açılışta kapanıyordu.
        /// </summary>
        public static bool IsOneDriveHidden()
        {
            try
            {
                string[] clsids =
                {
                    "{018D5C66-4533-4307-9B53-224DE2ED1FE6}", // OneDrive (kişisel)
                    "{04271989-C4D2-4316-9EA9-D3F7A8A5F1A2}"  // OneDrive for Business
                };

                bool anyKeyExists = false;
                foreach (string clsid in clsids)
                {
                    using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\CLSID\{clsid}", false);
                    if (key == null) continue;

                    object? value = key.GetValue("System.IsPinnedToNameSpaceTree");
                    if (value == null) continue;

                    anyKeyExists = true;
                    if (!(value is int pinned && pinned == 0))
                        return false;
                }

                if (!anyKeyExists)
                    return TweakSnapshotService.HasSnapshot("HideOnedrive");

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ParseExpectedValue(string rawData)
        {
            if (rawData.StartsWith("dword:", StringComparison.OrdinalIgnoreCase))
            {
                string hex = rawData.Substring(6);
                // uint ile parse ederek 0xFFFFFFFF (4294967295) gibi tam DWORD aralığını destekle
                if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint dec))
                    return dec.ToString();
            }
            if (rawData.StartsWith("\"") && rawData.EndsWith("\"") && rawData.Length >= 2)
            {
                string inner = rawData.Substring(1, rawData.Length - 2);
                // .reg dosyasındaki kaçışlı ters çizgileri (C:\\Windows) gerçek yola (C:\Windows) dönüştür
                return inner.Replace(@"\\", @"\");
            }
            return rawData;
        }

        /// <summary>
        /// Dosya Gezgini'ni (Windows Explorer) kapatıp yeniden başlatır
        /// </summary>
        public static void RestartExplorer()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("explorer"))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                    catch { }
                }

                string explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                Process.Start(new ProcessStartInfo
                {
                    FileName = explorerPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to restart explorer: {ex.Message}");
            }
        }

        /// <summary>
        /// Windows 11 Sistem Tepsisi simgelerinin durumunu kontrol eder (NotifyIconSettings)
        /// </summary>
        public static bool IsAllTrayIconsShown()
        {
            try
            {
                using var notifyKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", false);
                if (notifyKey == null) return false;

                var subKeyNames = notifyKey.GetSubKeyNames();
                if (subKeyNames.Length == 0) return false;

                int promotedCount = 0;
                int totalCount = 0;

                foreach (var subName in subKeyNames)
                {
                    using var subKey = notifyKey.OpenSubKey(subName, false);
                    if (subKey != null)
                    {
                        totalCount++;
                        var val = subKey.GetValue("IsPromoted");
                        if (val is int intVal && intVal == 1)
                        {
                            promotedCount++;
                        }
                    }
                }

                if (totalCount == 0) return false;
                // Uygulamaların en az %80'i görev çubuğuna terfi ettirilmişse (promoted) açık kabul edilir
                return ((double)promotedCount / totalCount) >= 0.80;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IsAllTrayIconsShown failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Windows 11'de tüm arka plan uygulamalarını görev çubuğuna taşır veya gizler
        /// </summary>
        public static async Task SetAllTrayIconsStateAsync(bool showAll)
        {
            await Task.Run(() =>
            {
                try
                {
                    // 1. Eski Windows uyumluluğu için EnableAutoTray ayarla
                    using (var expKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer", true))
                    {
                        expKey?.SetValue("EnableAutoTray", showAll ? 0 : 1, RegistryValueKind.DWord);
                    }

                    // 2. Windows 11 Modern Görev Çubuğu (NotifyIconSettings) altındaki tüm uygulamaları aç/kapat
                    using var notifyKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", true);
                    if (notifyKey != null)
                    {
                        var subKeyNames = notifyKey.GetSubKeyNames();
                        foreach (var subName in subKeyNames)
                        {
                            try
                            {
                                using var subKey = notifyKey.OpenSubKey(subName, true);
                                subKey?.SetValue("IsPromoted", showAll ? 1 : 0, RegistryValueKind.DWord);
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SetAllTrayIconsStateAsync failed: {ex.Message}");
                }
            });
        }
    }
}
