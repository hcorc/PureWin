using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PureWin.Services
{
    public class RegistryValueSnapshot
    {
        public string RootKey { get; set; } = string.Empty; // "HKEY_CURRENT_USER" veya "HKEY_LOCAL_MACHINE"
        public string SubKeyPath { get; set; } = string.Empty;
        public string ValueName { get; set; } = string.Empty;
        public bool Exists { get; set; } = false;
        public string? ValueKind { get; set; } // "DWord", "String", "QWord", "Binary", etc.
        public JsonElement? ValueData { get; set; }
    }

    public class TweakSnapshot
    {
        public string TweakId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<RegistryValueSnapshot> Entries { get; set; } = new();
    }

    public static class TweakSnapshotService
    {
        private static readonly string SnapshotsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PureWin",
            "TweakSnapshots");

        private static readonly string RegfilesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Regfiles");

        private static readonly Regex KeyRegex = new(@"^\[-?(HKEY_CURRENT_USER|HKEY_LOCAL_MACHINE|HKEY_CLASSES_ROOT|HKCU|HKLM|HKCR)\\([^\]]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ValueRegex = new(@"^""([^""]+)""\s*=", RegexOptions.Compiled);
        private static readonly Regex DefaultValueRegex = new(@"^@\s*=", RegexOptions.Compiled);

        static TweakSnapshotService()
        {
            try
            {
                if (!Directory.Exists(SnapshotsDir))
                {
                    Directory.CreateDirectory(SnapshotsDir);
                }
            }
            catch { }
        }

        private static string GetSnapshotFilePath(string tweakId) => Path.Combine(SnapshotsDir, $"{tweakId}.json");

        /// <summary>
        /// Tweak uygulanmadan hemen önce dokunacağı tüm registry anahtarlarının ve değerlerinin ön-snapshot'ını alır.
        /// Eğer önceden alınmış ve geri alınmamış bir snapshot varsa orijinal değeri korumak için üzerine yazmaz.
        /// </summary>
        public static void CreateSnapshot(string tweakId, string applyRegFile, string? undoRegFile = null)
        {
            try
            {
                string snapshotPath = GetSnapshotFilePath(tweakId);
                if (File.Exists(snapshotPath))
                {
                    // Önceki orijinal ön-snapshot korunur
                    return;
                }

                var targets = new HashSet<(string Root, string SubKey, string ValueName)>();

                // 1. applyRegFile içindeki hedefleri topla
                CollectTargetsFromRegFile(applyRegFile, isUndo: false, targets);

                // 2. undoRegFile içindeki hedefleri topla (varsa)
                if (!string.IsNullOrEmpty(undoRegFile))
                {
                    CollectTargetsFromRegFile(undoRegFile, isUndo: true, targets);
                }

                if (targets.Count == 0) return;

                var snapshot = new TweakSnapshot
                {
                    TweakId = tweakId,
                    Timestamp = DateTime.Now
                };

                foreach (var (rootName, subKeyPath, valueName) in targets)
                {
                    var entry = new RegistryValueSnapshot
                    {
                        RootKey = rootName,
                        SubKeyPath = subKeyPath,
                        ValueName = valueName
                    };

                    using var rootKey = GetRootKey(rootName);
                    if (rootKey != null)
                    {
                        using var subKey = rootKey.OpenSubKey(subKeyPath, false);
                        if (subKey != null)
                        {
                            object? val = subKey.GetValue(valueName, null);
                            if (val != null)
                            {
                                entry.Exists = true;
                                var kind = subKey.GetValueKind(valueName);
                                entry.ValueKind = kind.ToString();

                                // JsonElement olarak serialize edilebilmesi için JsonDocument üzerinden geçir
                                string jsonVal = JsonSerializer.Serialize(val);
                                using var doc = JsonDocument.Parse(jsonVal);
                                entry.ValueData = doc.RootElement.Clone();
                            }
                            else
                            {
                                entry.Exists = false;
                            }
                        }
                        else
                        {
                            entry.Exists = false;
                        }
                    }

                    snapshot.Entries.Add(entry);
                }

                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(snapshotPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TweakSnapshotService] CreateSnapshot failed for {tweakId}: {ex.Message}");
            }
        }

        /// <summary>
        /// .reg dosyası olmayan tekil registry değerleri için ön-snapshot alır.
        /// Game Mode gibi doğrudan Registry.SetValue ile yönetilen ayarların
        /// orijinalde "değer yok" durumunu birebir geri getirebilmek için kullanılır.
        /// </summary>
        public static void CreateSnapshotForRegistryValues(
            string tweakId,
            params (string RootKey, string SubKeyPath, string ValueName)[] targets)
        {
            try
            {
                string snapshotPath = GetSnapshotFilePath(tweakId);
                if (File.Exists(snapshotPath))
                    return;

                if (targets == null || targets.Length == 0)
                    return;

                var snapshot = new TweakSnapshot
                {
                    TweakId = tweakId,
                    Timestamp = DateTime.Now
                };

                foreach (var (rootName, subKeyPath, valueName) in targets)
                {
                    var entry = new RegistryValueSnapshot
                    {
                        RootKey = rootName,
                        SubKeyPath = subKeyPath,
                        ValueName = valueName
                    };

                    using var rootKey = GetRootKey(rootName);
                    if (rootKey != null)
                    {
                        using var subKey = rootKey.OpenSubKey(subKeyPath, false);
                        if (subKey != null)
                        {
                            object? val = subKey.GetValue(valueName, null);
                            if (val != null)
                            {
                                entry.Exists = true;
                                var kind = subKey.GetValueKind(valueName);
                                entry.ValueKind = kind.ToString();

                                string jsonVal = JsonSerializer.Serialize(val);
                                using var doc = JsonDocument.Parse(jsonVal);
                                entry.ValueData = doc.RootElement.Clone();
                            }
                        }
                    }

                    snapshot.Entries.Add(entry);
                }

                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(snapshotPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TweakSnapshotService] CreateSnapshotForRegistryValues failed for {tweakId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Alınan snapshot'ı kullanarak ayarları tweak öncesindeki orijinal haline döndürür.
        /// Orijinalde var olan değerleri birebir eski tür ve verisiyle yazar, orijinalde olmayanları ise siler.
        /// Snapshot başarıyla geri yüklendiyse true, snapshot bulunamadıysa false döner.
        /// </summary>
        public static bool RestoreSnapshot(string tweakId)
        {
            try
            {
                string snapshotPath = GetSnapshotFilePath(tweakId);
                if (!File.Exists(snapshotPath))
                {
                    return false;
                }

                string json = File.ReadAllText(snapshotPath, Encoding.UTF8);
                var snapshot = JsonSerializer.Deserialize<TweakSnapshot>(json);
                if (snapshot == null || snapshot.Entries == null || snapshot.Entries.Count == 0)
                {
                    try { File.Delete(snapshotPath); } catch { }
                    return false;
                }

                foreach (var entry in snapshot.Entries)
                {
                    try
                    {
                        using var rootKey = GetRootKey(entry.RootKey);
                        if (rootKey == null) continue;

                        if (entry.Exists && entry.ValueData.HasValue && !string.IsNullOrEmpty(entry.ValueKind))
                        {
                            // Orijinal değer vardı: geri yaz
                            using var subKey = rootKey.CreateSubKey(entry.SubKeyPath, true);
                            if (subKey != null)
                            {
                                if (Enum.TryParse<RegistryValueKind>(entry.ValueKind, out var kind))
                                {
                                    object? typedData = ConvertJsonElementToRegistryType(entry.ValueData.Value, kind);
                                    if (typedData != null)
                                    {
                                        subKey.SetValue(entry.ValueName, typedData, kind);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Orijinalde bu değer sistemde yoktu: tamamen SİL
                            using var subKey = rootKey.OpenSubKey(entry.SubKeyPath, true);
                            if (subKey != null)
                            {
                                try
                                {
                                    subKey.DeleteValue(entry.ValueName, false);
                                }
                                catch { }
                            }
                        }
                    }
                    catch (Exception exEntry)
                    {
                        Debug.WriteLine($"[TweakSnapshotService] Error restoring entry {entry.SubKeyPath}\\{entry.ValueName}: {exEntry.Message}");
                    }
                }

                // Geri yükleme tamamlandı, snapshot dosyasını temizle
                try { File.Delete(snapshotPath); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TweakSnapshotService] RestoreSnapshot failed for {tweakId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Bu tweak için geri alınmamış bir ön-snapshot var mı?
        /// Varsa: ayar bir zamanlar uygulanmış ve geri alınmamış demektir.
        /// </summary>
        public static bool HasSnapshot(string tweakId)
        {
            try
            {
                return File.Exists(GetSnapshotFilePath(tweakId));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kullanılmayan/eskimiş ön-snapshot dosyasını siler. Snapshot kullanmayan (ör. DisableAnimations)
        /// toggle'ların eski sürümlerden kalan dosyalarının diskte birikmesini engeller.
        /// </summary>
        public static void DiscardSnapshot(string tweakId)
        {
            try
            {
                string snapshotPath = GetSnapshotFilePath(tweakId);
                if (File.Exists(snapshotPath))
                {
                    File.Delete(snapshotPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TweakSnapshotService] DiscardSnapshot failed for {tweakId}: {ex.Message}");
            }
        }

        private static void CollectTargetsFromRegFile(string fileName, bool isUndo, HashSet<(string Root, string SubKey, string ValueName)> targets)
        {
            try
            {
                string targetDir = isUndo ? Path.Combine(RegfilesDir, "undo") : RegfilesDir;
                string fullPath = Path.Combine(targetDir, fileName);
                if (!File.Exists(fullPath))
                {
                    if (File.Exists(fileName)) fullPath = fileName;
                    else return;
                }

                // .reg dosyaları UTF-16LE Unicode veya UTF-8 olabilir
                string[] lines = ReadLinesAutoEncoding(fullPath);
                string currentRoot = string.Empty;
                string currentSubKey = string.Empty;

                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                        continue;

                    var keyMatch = KeyRegex.Match(line);
                    if (keyMatch.Success)
                    {
                        string rootStr = keyMatch.Groups[1].Value.ToUpperInvariant();
                        currentRoot = rootStr switch
                        {
                            "HKCU" or "HKEY_CURRENT_USER" => "HKEY_CURRENT_USER",
                            "HKLM" or "HKEY_LOCAL_MACHINE" => "HKEY_LOCAL_MACHINE",
                            "HKCR" or "HKEY_CLASSES_ROOT" => "HKEY_CLASSES_ROOT",
                            _ => rootStr
                        };
                        currentSubKey = keyMatch.Groups[2].Value.Trim();
                        continue;
                    }

                    if (string.IsNullOrEmpty(currentRoot) || string.IsNullOrEmpty(currentSubKey))
                        continue;

                    var valMatch = ValueRegex.Match(line);
                    if (valMatch.Success)
                    {
                        string valName = valMatch.Groups[1].Value;
                        targets.Add((currentRoot, currentSubKey, valName));
                        continue;
                    }

                    if (DefaultValueRegex.IsMatch(line))
                    {
                        targets.Add((currentRoot, currentSubKey, string.Empty));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TweakSnapshotService] CollectTargetsFromRegFile error: {ex.Message}");
            }
        }

        private static string[] ReadLinesAutoEncoding(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            }
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            }
            return File.ReadAllLines(path, Encoding.UTF8);
        }

        private static RegistryKey? GetRootKey(string rootName)
        {
            return rootName.ToUpperInvariant() switch
            {
                "HKEY_CURRENT_USER" or "HKCU" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default),
                "HKEY_LOCAL_MACHINE" or "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default),
                "HKEY_CLASSES_ROOT" or "HKCR" => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Default),
                _ => null
            };
        }

        private static object? ConvertJsonElementToRegistryType(JsonElement element, RegistryValueKind kind)
        {
            try
            {
                switch (kind)
                {
                    case RegistryValueKind.DWord:
                        if (element.ValueKind == JsonValueKind.Number) return element.GetInt32();
                        if (element.ValueKind == JsonValueKind.True) return 1;
                        if (element.ValueKind == JsonValueKind.False) return 0;
                        if (int.TryParse(element.GetString(), out int dw)) return dw;
                        return 0;

                    case RegistryValueKind.QWord:
                        if (element.ValueKind == JsonValueKind.Number) return element.GetInt64();
                        if (long.TryParse(element.GetString(), out long qw)) return qw;
                        return 0L;

                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        return element.GetString() ?? string.Empty;

                    case RegistryValueKind.MultiString:
                        if (element.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<string>();
                            foreach (var item in element.EnumerateArray())
                            {
                                list.Add(item.GetString() ?? "");
                            }
                            return list.ToArray();
                        }
                        return new[] { element.GetString() ?? "" };

                    case RegistryValueKind.Binary:
                        if (element.ValueKind == JsonValueKind.String)
                        {
                            return Convert.FromBase64String(element.GetString() ?? "");
                        }
                        if (element.ValueKind == JsonValueKind.Array)
                        {
                            var bytes = new List<byte>();
                            foreach (var b in element.EnumerateArray())
                            {
                                bytes.Add(b.GetByte());
                            }
                            return bytes.ToArray();
                        }
                        return Array.Empty<byte>();

                    default:
                        return element.GetString();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
