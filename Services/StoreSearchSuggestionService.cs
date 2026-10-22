using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace PureWin.Services
{
    /// <summary>
    /// Windows Arama'da Microsoft Store uygulama önerilerini kapatır.
    ///
    /// Windows 11 24H2 (26100.9278+), 25H2 (26200.9278+), 26H1 (28000.3086+) ve
    /// 26H2+ sürümlerinde resmi ayar şudur:
    ///   HKCU\Software\Microsoft\Windows\CurrentVersion\SearchSettings
    ///   IsStoreSuggestionsEnabled = 0
    ///
    /// Bu ayarın bulunmadığı eski sürümlerde, Raphire Win11Debloat'un kullandığı
    /// store.db üzerindeki EVERYONE Deny FullControl yöntemi yedek olarak kullanılır.
    /// </summary>
    public static class StoreSearchSuggestionService
    {
        private const string SearchSettingsKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\SearchSettings";

        private const string StoreSuggestionsValueName = "IsStoreSuggestionsEnabled";

        private const string StoreDbRelativePath =
            @"Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\LocalState\store.db";

        private static string StoreDbPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StoreDbRelativePath);

        /// <summary>
        /// Mağaza önerileri şu anda engelli mi?
        /// </summary>
        public static bool IsDisabled()
        {
            try
            {
                if (IsOfficialSettingSupported())
                    return IsOfficialSettingDisabled();

                // Eski Windows sürümlerinde store.db reddi kontrol edilir.
                return HasLegacyStoreDbDeny();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreSearchSuggestionService] IsDisabled error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Mağaza arama önerilerini kapatır.
        /// </summary>
        public static bool Disable()
        {
            try
            {
                bool officialSupported = IsOfficialSettingSupported();
                bool officialApplied = TrySetOfficialSetting(0);

                if (officialSupported)
                {
                    // Eski sürümden kalan store.db reddi varsa temizle; resmi ayar
                    // varken Store'un veritabanına erişmesini engellemeye gerek yok.
                    RemoveLegacyStoreDbDeny();
                }
                else
                {
                    AddLegacyStoreDbDeny();
                }

                RestartSearchUi();

                if (officialSupported)
                    return officialApplied && IsOfficialSettingDisabled();

                return HasLegacyStoreDbDeny();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreSearchSuggestionService] Disable error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Mağaza arama önerilerini yeniden açar.
        /// </summary>
        public static bool Enable()
        {
            try
            {
                bool officialApplied = TrySetOfficialSetting(1);

                // Eski ACL yönteminden kalan reddi her durumda kaldır.
                RemoveLegacyStoreDbDeny();

                RestartSearchUi();

                if (IsOfficialSettingSupported())
                    return officialApplied && !IsOfficialSettingDisabled();

                return !HasLegacyStoreDbDeny();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreSearchSuggestionService] Enable error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resmi IsStoreSuggestionsEnabled ayarının bu Windows sürümünde bulunup
        /// bulunmadığını denetler.
        /// </summary>
        private static bool IsOfficialSettingSupported()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", writable: false);

                if (key == null)
                    return false;

                string? buildText = key.GetValue("CurrentBuildNumber") as string
                    ?? key.GetValue("CurrentBuild")?.ToString();

                if (!int.TryParse(buildText, out int build))
                    return false;

                int ubr = 0;
                object? ubrValue = key.GetValue("UBR");
                if (ubrValue is int ubrInt)
                    ubr = ubrInt;
                else
                    int.TryParse(ubrValue?.ToString(), out ubr);

                // 26H1: 28000.3086+
                if (build >= 28000)
                    return ubr >= 3086;

                // 26H2: 26300+ (ayar yerleşik)
                if (build >= 26300)
                    return true;

                // 25H2: 26200.9278+
                if (build >= 26200)
                    return ubr >= 9278;

                // 24H2: 26100.9278+
                if (build >= 26100)
                    return ubr >= 9278;

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreSearchSuggestionService] Build check error: {ex.Message}");
                return false;
            }
        }

        private static bool IsOfficialSettingDisabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SearchSettingsKeyPath, writable: false);
                object? value = key?.GetValue(StoreSuggestionsValueName);

                if (value == null)
                    return false;

                return Convert.ToInt32(value) == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreSearchSuggestionService] Read setting error: {ex.Message}");
                return false;
            }
        }

        private static bool TrySetOfficialSetting(int value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(SearchSettingsKeyPath, writable: true);
                if (key == null)
                    return false;

                key.SetValue(StoreSuggestionsValueName, value, RegistryValueKind.DWord);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreSearchSuggestionService] Write setting error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Eski yöntemin bıraktığı store.db EVERYONE reddi var mı?
        /// </summary>
        private static bool HasLegacyStoreDbDeny()
        {
            string path = StoreDbPath;

            try
            {
                if (!StoreDbEntryExists(path))
                    return false;

                FileSecurity acl = new FileInfo(path).GetAccessControl(AccessControlSections.Access);

                foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, false, typeof(SecurityIdentifier)))
                {
                    if (rule.AccessControlType != AccessControlType.Deny)
                        continue;

                    if ((rule.FileSystemRights & FileSystemRights.FullControl) != FileSystemRights.FullControl)
                        continue;

                    if (rule.IdentityReference is SecurityIdentifier sid && sid.Value == "S-1-1-0")
                        return true;
                }

                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Dosyaya erişilemiyorsa red kuralı uygulanmış demektir.
                return StoreDbEntryExists(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreSearchSuggestionService] ACL read error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Eski Windows sürümlerinde store.db'ye EVERYONE Deny FullControl ekler.
        /// </summary>
        private static void AddLegacyStoreDbDeny()
        {
            string path = StoreDbPath;

            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                    return;

                if (!StoreDbEntryExists(path))
                {
                    using (File.Create(path)) { }
                }

                FileSecurity acl = new FileInfo(path).GetAccessControl(AccessControlSections.Access);
                var everyone = new SecurityIdentifier("S-1-1-0");

                acl.AddAccessRule(new FileSystemAccessRule(
                    everyone,
                    FileSystemRights.FullControl,
                    AccessControlType.Deny));

                new FileInfo(path).SetAccessControl(acl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreSearchSuggestionService] ACL add error: {ex.Message}");
            }
        }

        /// <summary>
        /// Eski yöntemden kalan store.db reddini kaldırır. Veritabanı silinmez;
        /// Store gerektiğinde kendi verisini güncelleyebilir.
        /// </summary>
        private static void RemoveLegacyStoreDbDeny()
        {
            string path = StoreDbPath;

            try
            {
                if (!StoreDbEntryExists(path))
                    return;

                FileSecurity acl = new FileInfo(path).GetAccessControl(AccessControlSections.Access);
                bool removed = false;

                foreach (FileSystemAccessRule rule in acl
                    .GetAccessRules(true, false, typeof(SecurityIdentifier))
                    .OfType<FileSystemAccessRule>()
                    .ToList())
                {
                    if (rule.AccessControlType == AccessControlType.Deny &&
                        rule.IdentityReference is SecurityIdentifier sid &&
                        sid.Value == "S-1-1-0")
                    {
                        acl.RemoveAccessRule(rule);
                        removed = true;
                    }
                }

                if (removed)
                    new FileInfo(path).SetAccessControl(acl);
            }
            catch (UnauthorizedAccessException)
            {
                // Dosya reddedildiği için normal API ile erişilemiyorsa icacls kullan.
                RunCommand("icacls.exe", $"\"{path}\" /remove:d *S-1-1-0 /C");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreSearchSuggestionService] ACL remove error: {ex.Message}");
            }
        }

        private static bool StoreDbEntryExists(string path)
        {
            try
            {
                string? directory = Path.GetDirectoryName(path);
                string fileName = Path.GetFileName(path);

                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                    return false;

                return Directory.EnumerateFiles(directory, fileName)
                    .Any(entry => string.Equals(
                        Path.GetFileName(entry), fileName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return File.Exists(path);
            }
        }

        private static void RunCommand(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return;

                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit(10000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreSearchSuggestionService] {fileName} error: {ex.Message}");
            }
        }

        /// <summary>
        /// Değişikliğin anında yansıması için arama arayüzünü yeniden başlatır.
        /// Windows, SearchHost ve StartMenuExperienceHost'u bir sonraki aramada
        /// kendiliğinden yeniden başlatır.
        /// </summary>
        private static void RestartSearchUi()
        {
            foreach (string name in new[] { "SearchHost", "SearchApp", "StartMenuExperienceHost" })
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try { process.Kill(); } catch { }
                    finally { process.Dispose(); }
                }
            }
        }
    }
}
