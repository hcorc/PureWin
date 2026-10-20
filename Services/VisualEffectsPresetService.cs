using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace PureWin.Services
{
    public class VisualEffectsRegistryEntry
    {
        public string Hive { get; set; } = "HKEY_CURRENT_USER";
        public string SubKey { get; set; } = string.Empty;
        public string ValueName { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public string? ValueKind { get; set; }
        public string? Data { get; set; }
    }

    public class VisualEffectsSnapshot
    {
        public int Version { get; set; } = 1;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public List<VisualEffectsRegistryEntry> PreviousValues { get; set; } = new();
    }

    /// <summary>
    /// Windows'un "En iyi performans için ayarla" görsel efekt presetini uygulayan,
    /// font smoothing (ClearType) ayarlarını koruyan ve geri alma sadakati sunan servis.
    /// </summary>
    public static class VisualEffectsPresetService
    {
        public static string? LastRestoreWarning { get; set; }

        private const int SnapshotVersion = 1;

        private static readonly (string Hive, string SubKey, string ValueName)[] TargetKeys = new[]
        {
            ("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting"),
            ("HKEY_CURRENT_USER", @"Control Panel\Desktop", "UserPreferencesMask"),
            ("HKEY_CURRENT_USER", @"Control Panel\Desktop", "DragFullWindows"),
            ("HKEY_CURRENT_USER", @"Control Panel\Desktop", "FontSmoothing"),
            ("HKEY_CURRENT_USER", @"Control Panel\Desktop", "FontSmoothingType"),
            ("HKEY_CURRENT_USER", @"Control Panel\Desktop", "FontSmoothingGamma"),
            ("HKEY_CURRENT_USER", @"Control Panel\Desktop", "FontSmoothingOrientation"),
            ("HKEY_CURRENT_USER", @"Control Panel\Desktop\WindowMetrics", "MinAnimate"),
            ("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations"),
            ("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect"),
            ("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "IconsOnly"),
            ("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow"),
            ("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\DWM", "EnableAeroPeek"),
            ("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails"),
            ("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency")
        };

        private static readonly byte[] ExpectedUserPreferencesMask = new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 };

        /// <summary>
        /// Snapshot dosyası geçerliyse ve sistemdeki mevcut değerler uygulanan preset ile tam eşleşiyorsa true döner.
        /// Snapshot yoksa, geri alma verisi olmadığı için güvenli davranış olarak false döner.
        /// </summary>
        public static bool IsApplied()
        {
            if (!HasValidSnapshot())
                return false;

            return IsAppliedStateMatch();
        }

        /// <summary>
        /// Sistemdeki ilgili registry değerlerinin Best Performance preset değerleriyle eşleşip eşleşmediğini kontrol eder.
        /// </summary>
        private static bool IsAppliedStateMatch()
        {
            try
            {
                // 1. VisualFXSetting == 3 (Custom)
                var vfx = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", null);
                if (vfx is not int vfxInt || vfxInt != 3) return false;

                // 2. DragFullWindows == "0"
                var dfw = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "DragFullWindows", null)?.ToString();
                if (dfw != "0") return false;

                // 3. UserPreferencesMask == 90, 12, 03, 80, 10, 00, 00, 00
                var upm = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "UserPreferencesMask", null) as byte[];
                if (upm == null || !upm.SequenceEqual(ExpectedUserPreferencesMask)) return false;

                // 4. MinAnimate == "0"
                var ma = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "MinAnimate", null)?.ToString();
                if (ma != "0") return false;

                // 5. Explorer Advanced
                const string advKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
                if (Registry.GetValue(advKey, "TaskbarAnimations", null) is not int ta || ta != 0) return false;
                if (Registry.GetValue(advKey, "ListviewAlphaSelect", null) is not int lva || lva != 0) return false;
                if (Registry.GetValue(advKey, "IconsOnly", null) is not int io || io != 1) return false;
                if (Registry.GetValue(advKey, "ListviewShadow", null) is not int lvs || lvs != 0) return false;

                // 6. DWM
                const string dwmKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM";
                if (Registry.GetValue(dwmKey, "EnableAeroPeek", null) is not int eap || eap != 0) return false;
                if (Registry.GetValue(dwmKey, "AlwaysHibernateThumbnails", null) is not int aht || aht != 0) return false;

                // 7. Transparency == 0
                const string themeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                if (Registry.GetValue(themeKey, "EnableTransparency", null) is not int et || et != 0) return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VisualEffectsPresetService] IsAppliedStateMatch error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Geçerli ve eksiksiz bir snapshot dosyası olup olmadığını denetler.
        /// </summary>
        public static bool HasValidSnapshot()
        {
            try
            {
                string path = GetSnapshotPath();
                if (!File.Exists(path))
                    return false;

                return IsValidSnapshot(LoadSnapshotFile(path));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidSnapshot(VisualEffectsSnapshot? snapshot)
        {
            if (snapshot == null || snapshot.Version != SnapshotVersion || snapshot.PreviousValues == null)
                return false;

            if (snapshot.PreviousValues.Count != TargetKeys.Length)
                return false;

            var expectedKeys = new HashSet<string>(
                TargetKeys.Select(t => GetTargetKey(t.Hive, t.SubKey, t.ValueName)),
                StringComparer.OrdinalIgnoreCase);
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in snapshot.PreviousValues)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.Hive) ||
                    string.IsNullOrWhiteSpace(entry.SubKey) ||
                    string.IsNullOrWhiteSpace(entry.ValueName))
                {
                    return false;
                }

                string key = GetTargetKey(entry.Hive, entry.SubKey, entry.ValueName);
                if (!expectedKeys.Contains(key) || !seenKeys.Add(key))
                    return false;

                if (!entry.Exists)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.ValueKind) ||
                    !Enum.TryParse<RegistryValueKind>(entry.ValueKind, true, out var kind) ||
                    kind == RegistryValueKind.Unknown ||
                    kind == RegistryValueKind.None)
                {
                    return false;
                }

                if (entry.Data == null)
                    return false;

                if (kind == RegistryValueKind.Binary)
                {
                    try
                    {
                        Convert.FromBase64String(entry.Data);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            return seenKeys.Count == TargetKeys.Length;
        }

        private static string GetTargetKey(string hive, string subKey, string valueName)
            => $"{hive}\\{subKey}\\{valueName}";

        /// <summary>
        /// Snapshot dosyasının hedef yolunu belirler. Öncelik uygulama klasöründeki Config dizinidir.
        /// Geçerli bir fallback dosyası varsa o kullanılır.
        /// </summary>
        public static string GetSnapshotPath()
        {
            string primaryPath = GetPrimarySnapshotPath();
            if (File.Exists(primaryPath))
                return primaryPath;

            string fallbackPath = GetFallbackSnapshotPath();
            if (File.Exists(fallbackPath))
                return fallbackPath;

            return primaryPath;
        }

        private static string GetPrimarySnapshotPath()
            => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "VisualEffectsSnapshot.json");

        private static string GetFallbackSnapshotPath()
            => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PureWin",
                "VisualEffectsSnapshot.json");

        /// <summary>
        /// Görsel efektleri en iyi performansa ayarlar, font smoothing değerlerini korur.
        /// Geri alma snapshot'ı registry değişikliklerinden ÖNCE kaydedilir; böylece kayıt
        /// başarısız olursa hiçbir sistem ayarı değiştirilmez.
        /// </summary>
        public static bool Apply()
        {
            LastRestoreWarning = null;

            try
            {
                string snapshotPath = GetSnapshotPath();
                bool snapshotFileExists = File.Exists(snapshotPath);
                VisualEffectsSnapshot? snapshot = snapshotFileExists ? LoadSnapshotFile(snapshotPath) : null;
                bool createdNewSnapshot = false;

                if (snapshot != null && IsValidSnapshot(snapshot))
                {
                    // Mevcut geri alma noktası korunur; üzerine yazılmaz.
                }
                else
                {
                    if (snapshotFileExists)
                    {
                        Debug.WriteLine("[VisualEffectsPresetService] Existing snapshot is invalid; creating a new restore point.");
                        LastRestoreWarning = "Mevcut görsel efekt snapshot dosyası okunamadığı için yeni bir geri alma noktası oluşturuldu.";
                    }

                    if (!TryCaptureCurrentState(out var previousValues))
                    {
                        LastRestoreWarning = "Görsel efektler uygulanamadı: mevcut kayıt defteri değerleri okunamadı.";
                        return false;
                    }

                    snapshot = new VisualEffectsSnapshot
                    {
                        Version = SnapshotVersion,
                        CreatedAtUtc = DateTime.UtcNow,
                        PreviousValues = previousValues
                    };
                    createdNewSnapshot = true;

                    // Snapshot'ı herhangi bir registry değişikliğinden önce kaydet.
                    if (!SaveSnapshotFile(snapshotPath, snapshot))
                    {
                        LastRestoreWarning = "Görsel efektler uygulanamadı: geri alma snapshot dosyası oluşturulamadı.";
                        return false;
                    }
                }

                var previousValuesForRollback = snapshot!.PreviousValues;
                var presetEntries = BuildPresetEntries(previousValuesForRollback);

                // Canlı animasyon durumunu (istemci alanı + pencere animasyonları) hemen güncelle.
                // Registry değerleri en son yazıldığı için MinAnimate dahil tüm değerler preset ile birebir aynı kalır.
                SystemSettingsService.SetClientAnimationState(disable: true);
                SystemSettingsService.SetWindowAnimationState(disable: true);

                if (!WriteRegistryEntries(presetEntries))
                {
                    // Yarım kalan uygulamayı geri al.
                    SystemSettingsService.SetClientAnimationState(disable: IsAnimationDisabledInSnapshot(previousValuesForRollback));
                    SystemSettingsService.SetWindowAnimationState(disable: IsAnimationDisabledInSnapshot(previousValuesForRollback));
                    bool rollbackOk = WriteRegistryEntries(previousValuesForRollback);

                    if (rollbackOk)
                    {
                        if (createdNewSnapshot)
                            DeleteSnapshotFiles();

                        LastRestoreWarning = "Görsel efektler uygulanamadı; önceki ayarlar geri yüklendi.";
                    }
                    else
                    {
                        LastRestoreWarning = "Görsel efektler uygulanamadı ve önceki ayarlar tam olarak geri yüklenemedi. Snapshot dosyası korundu.";
                    }

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VisualEffectsPresetService] Apply error: {ex.Message}");
                LastRestoreWarning = $"Görsel efektler uygulanırken hata oluştu: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Snapshot dosyasındaki değerleri birebir geri yükler ve başarılıysa snapshot dosyasını siler.
        /// Snapshot yoksa veya bozuksa güvenli fallback (Enable_Animations.reg + Enable_Transparency.reg) uygular.
        /// </summary>
        public static bool Restore()
        {
            LastRestoreWarning = null;

            string snapshotPath = GetSnapshotPath();
            bool snapshotFileExists = File.Exists(snapshotPath);
            VisualEffectsSnapshot? snapshot = snapshotFileExists ? LoadSnapshotFile(snapshotPath) : null;

            if (snapshot != null && IsValidSnapshot(snapshot))
            {
                try
                {
                    var previousValues = snapshot.PreviousValues;

                    // Önce canlı animasyon durumunu ayarla, sonra registry değerlerini birebir yaz.
                    // Böylece SPI çağrısı, önceden hiç var olmayan MinAnimate değerini geri yaratmaz.
                    bool animationsWereDisabled = IsAnimationDisabledInSnapshot(previousValues);
                    SystemSettingsService.SetClientAnimationState(disable: animationsWereDisabled);
                    SystemSettingsService.SetWindowAnimationState(disable: animationsWereDisabled);

                    if (!WriteRegistryEntries(previousValues))
                    {
                        LastRestoreWarning = "Görsel efektler geri yüklenemedi. Snapshot dosyası korundu; tekrar deneyebilirsiniz.";
                        return false;
                    }

                    if (!DeleteSnapshotFiles())
                    {
                        LastRestoreWarning = "Görsel efektler geri yüklendi ancak snapshot dosyası silinemedi. Dosyayı Config klasöründen silebilirsiniz.";
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[VisualEffectsPresetService] Error during restore from snapshot: {ex.Message}");
                    LastRestoreWarning = $"Görsel efektler geri yüklenirken hata oluştu: {ex.Message}";
                    return false;
                }
            }

            // Snapshot yoksa veya bozuksa: Güvenli fallback.
            try
            {
                Debug.WriteLine("[VisualEffectsPresetService] Snapshot missing or corrupted. Applying safe fallback...");

                bool animationsOk = RegistryService.ImportRegFile("Enable_Animations.reg", isUndo: true);
                bool transparencyOk = RegistryService.ImportRegFile("Enable_Transparency.reg", isUndo: true);
                bool visualFxOk = true;

                try
                {
                    Registry.SetValue(
                        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                        "VisualFXSetting",
                        0,
                        RegistryValueKind.DWord);
                }
                catch (Exception ex)
                {
                    visualFxOk = false;
                    Debug.WriteLine($"[VisualEffectsPresetService] Fallback VisualFXSetting write failed: {ex.Message}");
                }

                SystemSettingsService.SetClientAnimationState(disable: false);
                SystemSettingsService.SetWindowAnimationState(disable: false);

                if (animationsOk && transparencyOk && visualFxOk)
                {
                    DeleteSnapshotFiles();
                    LastRestoreWarning = "Önceki görsel efekt snapshot dosyası bulunamadığı veya okunamadığı için ayarlar güvenli varsayılanlarla (Animasyonlar ve Saydamlık Açık) sıfırlandı.";
                    return true;
                }

                LastRestoreWarning = "Görsel efekt snapshot dosyası bulunamadı ve güvenli geri yükleme tam olarak uygulanamadı.";
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VisualEffectsPresetService] Fallback restore failed: {ex.Message}");
                LastRestoreWarning = $"Görsel efektler geri yüklenirken hata oluştu: {ex.Message}";
                return false;
            }
        }

        private static bool TryCaptureCurrentState(out List<VisualEffectsRegistryEntry> entries)
        {
            entries = new List<VisualEffectsRegistryEntry>(TargetKeys.Length);

            foreach (var target in TargetKeys)
            {
                if (!TryReadRegistryEntry(target.Hive, target.SubKey, target.ValueName, out var entry))
                    return false;

                entries.Add(entry);
            }

            return true;
        }

        private static List<VisualEffectsRegistryEntry> BuildPresetEntries(List<VisualEffectsRegistryEntry> previousValues)
        {
            var entries = new List<VisualEffectsRegistryEntry>
            {
                CreatePresetEntry(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                    "VisualFXSetting",
                    "DWord",
                    "3"),
                CreatePresetEntry(
                    @"Control Panel\Desktop",
                    "UserPreferencesMask",
                    "Binary",
                    Convert.ToBase64String(ExpectedUserPreferencesMask)),
                CreatePresetEntry(
                    @"Control Panel\Desktop",
                    "DragFullWindows",
                    "String",
                    "0")
            };

            // 4-7. Font smoothing değerleri (ClearType korunacak) önceki değerleriyle aynen yazılır.
            entries.Add(CreatePreservedEntry(previousValues, @"Control Panel\Desktop", "FontSmoothing", "String", "2"));
            entries.Add(CreatePreservedEntry(previousValues, @"Control Panel\Desktop", "FontSmoothingType", "DWord", "2"));
            entries.Add(CreatePreservedEntry(previousValues, @"Control Panel\Desktop", "FontSmoothingGamma", "DWord", "0"));
            entries.Add(CreatePreservedEntry(previousValues, @"Control Panel\Desktop", "FontSmoothingOrientation", "DWord", "1"));

            entries.Add(CreatePresetEntry(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "String", "0"));
            entries.Add(CreatePresetEntry(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", "DWord", "0"));
            entries.Add(CreatePresetEntry(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", "DWord", "0"));
            entries.Add(CreatePresetEntry(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "IconsOnly", "DWord", "1"));
            entries.Add(CreatePresetEntry(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", "DWord", "0"));
            entries.Add(CreatePresetEntry(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", "DWord", "0"));
            entries.Add(CreatePresetEntry(@"Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails", "DWord", "0"));
            entries.Add(CreatePresetEntry(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", "DWord", "0"));

            return entries;
        }

        private static VisualEffectsRegistryEntry CreatePresetEntry(string subKey, string valueName, string valueKind, string data)
        {
            return new VisualEffectsRegistryEntry
            {
                Hive = "HKEY_CURRENT_USER",
                SubKey = subKey,
                ValueName = valueName,
                Exists = true,
                ValueKind = valueKind,
                Data = data
            };
        }

        private static VisualEffectsRegistryEntry CreatePreservedEntry(
            List<VisualEffectsRegistryEntry> previousValues,
            string subKey,
            string valueName,
            string defaultKind,
            string defaultData)
        {
            var previous = FindPreviousValue(previousValues, subKey, valueName);

            return new VisualEffectsRegistryEntry
            {
                Hive = "HKEY_CURRENT_USER",
                SubKey = subKey,
                ValueName = valueName,
                Exists = true,
                ValueKind = previous != null && previous.Exists && !string.IsNullOrWhiteSpace(previous.ValueKind)
                    ? previous.ValueKind
                    : defaultKind,
                Data = previous != null && previous.Exists && previous.Data != null
                    ? previous.Data
                    : defaultData
            };
        }

        private static VisualEffectsRegistryEntry? FindPreviousValue(
            IEnumerable<VisualEffectsRegistryEntry> values,
            string subKey,
            string valueName)
        {
            return values.FirstOrDefault(p =>
                string.Equals(p.SubKey, subKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.ValueName, valueName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsAnimationDisabledInSnapshot(IEnumerable<VisualEffectsRegistryEntry> values)
        {
            var minAnimate = FindPreviousValue(values, @"Control Panel\Desktop\WindowMetrics", "MinAnimate");
            return minAnimate != null && minAnimate.Exists && minAnimate.Data == "0";
        }

        private static bool TryReadRegistryEntry(
            string hive,
            string subKeyPath,
            string valueName,
            out VisualEffectsRegistryEntry entry)
        {
            entry = new VisualEffectsRegistryEntry
            {
                Hive = hive,
                SubKey = subKeyPath,
                ValueName = valueName,
                Exists = false
            };

            try
            {
                using var rootKey = GetRootKey(hive);
                using var key = rootKey.OpenSubKey(subKeyPath, false);
                if (key == null)
                    return true;

                var rawValue = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (rawValue == null)
                    return true;

                var kind = key.GetValueKind(valueName);
                entry.Exists = true;
                entry.ValueKind = kind.ToString();

                switch (kind)
                {
                    case RegistryValueKind.Binary:
                        if (rawValue is not byte[] bytes)
                            return false;
                        entry.Data = Convert.ToBase64String(bytes);
                        break;

                    case RegistryValueKind.DWord:
                        if (rawValue is not int dword)
                            return false;
                        entry.Data = dword.ToString();
                        break;

                    case RegistryValueKind.QWord:
                        if (rawValue is not long qword)
                            return false;
                        entry.Data = qword.ToString();
                        break;

                    case RegistryValueKind.MultiString:
                        if (rawValue is not string[] multi)
                            return false;
                        entry.Data = string.Join("\0", multi);
                        break;

                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        entry.Data = rawValue.ToString() ?? string.Empty;
                        break;

                    default:
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VisualEffectsPresetService] Read error for {subKeyPath}\\{valueName}: {ex.Message}");
                return false;
            }
        }

        private static bool WriteRegistryEntries(IEnumerable<VisualEffectsRegistryEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (!WriteRegistryEntry(entry))
                    return false;
            }

            return true;
        }

        private static bool WriteRegistryEntry(VisualEffectsRegistryEntry entry)
        {
            try
            {
                using var rootKey = GetRootKey(entry.Hive);

                if (!entry.Exists)
                {
                    using var existingKey = rootKey.OpenSubKey(entry.SubKey, true);
                    if (existingKey == null)
                        return true; // Anahtar yoksa değer de yoktur.

                    if (existingKey.GetValue(entry.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) != null)
                        existingKey.DeleteValue(entry.ValueName, throwOnMissingValue: false);

                    return true;
                }

                using var writeKey = rootKey.CreateSubKey(entry.SubKey, true);
                if (writeKey == null)
                    return false;

                var kind = RegistryValueKind.String;
                if (!string.IsNullOrWhiteSpace(entry.ValueKind) &&
                    !Enum.TryParse(entry.ValueKind, true, out kind))
                {
                    return false;
                }

                switch (kind)
                {
                    case RegistryValueKind.Binary:
                        byte[] bytes = string.IsNullOrEmpty(entry.Data)
                            ? Array.Empty<byte>()
                            : Convert.FromBase64String(entry.Data);
                        writeKey.SetValue(entry.ValueName, bytes, RegistryValueKind.Binary);
                        break;

                    case RegistryValueKind.DWord:
                        if (!int.TryParse(entry.Data, out int intValue))
                            return false;
                        writeKey.SetValue(entry.ValueName, intValue, RegistryValueKind.DWord);
                        break;

                    case RegistryValueKind.QWord:
                        if (!long.TryParse(entry.Data, out long longValue))
                            return false;
                        writeKey.SetValue(entry.ValueName, longValue, RegistryValueKind.QWord);
                        break;

                    case RegistryValueKind.MultiString:
                        string[] multi = (entry.Data ?? string.Empty).Split('\0');
                        writeKey.SetValue(entry.ValueName, multi, RegistryValueKind.MultiString);
                        break;

                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        writeKey.SetValue(entry.ValueName, entry.Data ?? string.Empty, kind);
                        break;

                    default:
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VisualEffectsPresetService] Write error for {entry.SubKey}\\{entry.ValueName}: {ex.Message}");
                return false;
            }
        }

        private static RegistryKey GetRootKey(string hive)
        {
            return (hive.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase) ||
                    hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase))
                ? Registry.LocalMachine
                : Registry.CurrentUser;
        }

        private static VisualEffectsSnapshot? LoadSnapshotFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<VisualEffectsSnapshot>(json, options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VisualEffectsPresetService] Error loading snapshot {path}: {ex.Message}");
                return null;
            }
        }

        private static bool SaveSnapshotFile(string targetPath, VisualEffectsSnapshot snapshot)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(snapshot, options);

            if (TryWriteSnapshotFile(targetPath, json))
                return true;

            string fallbackPath = GetFallbackSnapshotPath();
            if (!string.Equals(targetPath, fallbackPath, StringComparison.OrdinalIgnoreCase) &&
                TryWriteSnapshotFile(fallbackPath, json))
            {
                Debug.WriteLine($"[VisualEffectsPresetService] Saved snapshot to fallback location: {fallbackPath}");
                return true;
            }

            return false;
        }

        private static bool TryWriteSnapshotFile(string targetPath, string json)
        {
            string tempPath = targetPath + ".tmp";

            try
            {
                string? directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(tempPath, json, utf8NoBom);
                File.Move(tempPath, targetPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VisualEffectsPresetService] SaveSnapshotFile error for {targetPath}: {ex.Message}");

                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { }

                return false;
            }
        }

        private static bool DeleteSnapshotFiles()
        {
            bool success = true;

            foreach (string path in new[] { GetPrimarySnapshotPath(), GetFallbackSnapshotPath() })
            {
                foreach (string file in new[] { path, path + ".tmp" })
                {
                    try
                    {
                        if (File.Exists(file))
                            File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[VisualEffectsPresetService] DeleteSnapshotFiles error for {file}: {ex.Message}");
                        success = false;
                    }
                }
            }

            return success;
        }
    }
}
