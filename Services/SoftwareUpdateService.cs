using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PureWin.Models;

namespace PureWin.Services
{
    public static class SoftwareUpdateService
    {
        private static readonly Regex AnsiRegex = new(@"\x1B\[[^@-~]*[@-~]", RegexOptions.Compiled);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

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

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;

        /// <summary>
        /// winget.exe yürütülebilir dosyasının tam yolunu bulur.
        /// </summary>
        public static string? FindWingetPath()
        {
            try
            {
                // 1. Standart LocalAppData WindowsApps yolu
                string localAppPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\WindowsApps\winget.exe");
                if (File.Exists(localAppPath))
                    return localAppPath;

                // 2. Program Files WindowsApps altında ara
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string windowsApps = Path.Combine(progFiles, "WindowsApps");
                if (Directory.Exists(windowsApps))
                {
                    var matches = Directory.GetFiles(windowsApps, "winget.exe", SearchOption.AllDirectories);
                    if (matches.Length > 0)
                        return matches[0];
                }

                // 3. PATH üzerinde doğrudan 'winget' komutu çalışıyor mu
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                if (proc != null)
                {
                    proc.WaitForExit(3000);
                    if (proc.ExitCode == 0)
                        return "winget";
                }
            }
            catch
            {
                // Yoksay
            }

            return null;
        }

        /// <summary>
        /// Sistemde winget aracının kurulu olup olmadığını doğrular.
        /// </summary>
        public static bool IsWingetAvailable()
        {
            return FindWingetPath() != null;
        }

        /// <summary>
        /// Güncelleme bekleyen tüm yazılımları winget üzerinden sorgular ve ayrıştırır.
        /// </summary>
        public static async Task<List<SoftwareUpdateItem>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<SoftwareUpdateItem>();
            string? wingetPath = FindWingetPath();
            if (string.IsNullOrEmpty(wingetPath))
                return results;

            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = wingetPath,
                        Arguments = "upgrade --locale en-US",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using var proc = new Process { StartInfo = psi };
                    proc.Start();

                    var lines = new List<string>();
                    while (!proc.StandardOutput.EndOfStream)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            proc.Kill();
                            return results;
                        }

                        string? rawLine = proc.StandardOutput.ReadLine();
                        if (rawLine != null)
                        {
                            string clean = AnsiRegex.Replace(rawLine, "").TrimEnd();
                            if (!string.IsNullOrWhiteSpace(clean))
                                lines.Add(clean);
                        }
                    }

                    proc.WaitForExit(30000);

                    // Tablo başlığını ve ayraç satırını bul
                    int separatorIndex = -1;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].Contains("---") && lines[i].Length > 10)
                        {
                            separatorIndex = i;
                            break;
                        }
                    }

                    if (separatorIndex <= 0 || separatorIndex >= lines.Count)
                        return results;

                    string headerLine = lines[separatorIndex - 1];
                    int idxId = headerLine.IndexOf("Id", StringComparison.OrdinalIgnoreCase);
                    int idxVersion = headerLine.IndexOf("Version", StringComparison.OrdinalIgnoreCase);
                    int idxAvailable = headerLine.IndexOf("Available", StringComparison.OrdinalIgnoreCase);
                    int idxSource = headerLine.IndexOf("Source", StringComparison.OrdinalIgnoreCase);

                    if (idxId <= 0 || idxVersion <= 0 || idxAvailable <= 0)
                        return results;

                    // Ayraçtan sonraki veri satırlarını ayrıştır
                    for (int i = separatorIndex + 1; i < lines.Count; i++)
                    {
                        string line = lines[i];

                        // Özet satırlarını veya dipnotları atla
                        if (line.Contains("upgrades available", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("have pins", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("<") ||
                            line.Contains("package(s) have version numbers", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (line.Length <= idxId)
                            continue;

                        try
                        {
                            string name = line.Substring(0, Math.Min(line.Length, idxId)).Trim();

                            string id = (idxVersion > idxId && line.Length >= idxVersion)
                                ? line.Substring(idxId, idxVersion - idxId).Trim()
                                : (line.Length > idxId ? line.Substring(idxId).Trim() : string.Empty);

                            string version = string.Empty;
                            if (idxAvailable > idxVersion && line.Length >= idxAvailable)
                            {
                                version = line.Substring(idxVersion, idxAvailable - idxVersion).Trim();
                            }
                            else if (line.Length > idxVersion)
                            {
                                version = line.Substring(idxVersion).Trim();
                            }

                            string available = string.Empty;
                            if (idxSource > idxAvailable && line.Length >= idxSource)
                            {
                                available = line.Substring(idxAvailable, idxSource - idxAvailable).Trim();
                            }
                            else if (line.Length > idxAvailable)
                            {
                                available = line.Substring(idxAvailable).Trim();
                            }

                            string source = (idxSource > 0 && line.Length > idxSource)
                                ? line.Substring(idxSource).Trim()
                                : "winget";

                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(available))
                                continue;

                            var item = new SoftwareUpdateItem
                            {
                                Id = id,
                                Name = name,
                                CurrentVersion = version,
                                AvailableVersion = available,
                                Source = source,
                                State = SoftwareUpdateState.Ready,
                                StatusMessage = "Güncelle"
                            };

                            EnrichAppMetadata(item);
                            results.Add(item);
                        }
                        catch
                        {
                            // Tekil satır ayrıştırma hatasını yoksay
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SoftwareUpdateService] CheckForUpdatesAsync hatası: {ex.Message}");
                }

                return results;
            }, cancellationToken);
        }

        /// <summary>
        /// Uygulama için yayıncı bilgisi ve uygulama simgesini çözer.
        /// </summary>
        private static void EnrichAppMetadata(SoftwareUpdateItem item)
        {
            // 1. Id üzerinden yayıncı tahmini (Örn: Google.Chrome -> Google)
            string publisher = string.Empty;
            if (item.Id.Contains("."))
            {
                var parts = item.Id.Split('.');
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    publisher = parts[0];
                }
            }

            // 2. Kayıt defterinde kurulu programlar listesinde arayarak simge ve tam yayıncı adını bul
            string iconPath = string.Empty;
            try
            {
                FindInstalledAppInfo(item.Name, item.Id, ref publisher, ref iconPath);
            }
            catch { }

            item.Publisher = string.IsNullOrWhiteSpace(publisher) ? "Bilinmeyen Yayıncı" : publisher;

            // 3. Simge veya Emoji ata
            var (iconSource, fallbackIcon) = ResolveIcon(iconPath, item.Name, item.Id);
            item.IconSource = iconSource;
            item.FallbackIcon = fallbackIcon;
        }

        /// <summary>
        /// Kayıt Defterindeki Uninstall anahtarlarından programın DisplayIcon ve Publisher değerlerini bulur.
        /// </summary>
        private static void FindInstalledAppInfo(string appName, string packageId, ref string publisher, ref string iconPath)
        {
            string[] uninstallKeys = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var rootKey in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var subPath in uninstallKeys)
                {
                    using var key = rootKey.OpenSubKey(subPath);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var appSubKey = key.OpenSubKey(subKeyName);
                        if (appSubKey == null) continue;

                        string dispName = appSubKey.GetValue("DisplayName")?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(dispName)) continue;

                        if (dispName.Equals(appName, StringComparison.OrdinalIgnoreCase) ||
                            dispName.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                            appName.Contains(dispName, StringComparison.OrdinalIgnoreCase))
                        {
                            string pub = appSubKey.GetValue("Publisher")?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(pub))
                                publisher = pub;

                            string dispIcon = appSubKey.GetValue("DisplayIcon")?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(dispIcon))
                            {
                                // Virgülden sonrasını ayıkla (Örn: C:\path\app.exe,0)
                                int commaIdx = dispIcon.IndexOf(',');
                                if (commaIdx > 0)
                                    dispIcon = dispIcon.Substring(0, commaIdx).Trim('\"', ' ');
                                else
                                    dispIcon = dispIcon.Trim('\"', ' ');

                                if (File.Exists(dispIcon))
                                {
                                    iconPath = dispIcon;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Uygulama için Win32 Shell simgesi veya yedek kategori emojisini üretir.
        /// </summary>
        private static (BitmapSource? icon, string fallback) ResolveIcon(string filePath, string name, string id)
        {
            string lower = (name + " " + id).ToLowerInvariant();
            string fallback = "📦";

            if (lower.Contains("chrome") || lower.Contains("edge") || lower.Contains("firefox") || lower.Contains("browser") || lower.Contains("opera") || lower.Contains("brave"))
                fallback = "🌐";
            else if (lower.Contains("discord") || lower.Contains("chat") || lower.Contains("slack") || lower.Contains("telegram") || lower.Contains("whatsapp") || lower.Contains("teams"))
                fallback = "💬";
            else if (lower.Contains("code") || lower.Contains("studio") || lower.Contains("terminal") || lower.Contains("powershell") || lower.Contains("git") || lower.Contains("python"))
                fallback = "💻";
            else if (lower.Contains("7-zip") || lower.Contains("7zip") || lower.Contains("winrar") || lower.Contains("zip"))
                fallback = "🗜️";
            else if (lower.Contains("vlc") || lower.Contains("spotify") || lower.Contains("media") || lower.Contains("music") || lower.Contains("player"))
                fallback = "🎵";
            else if (lower.Contains("steam") || lower.Contains("game") || lower.Contains("antigravity") || lower.Contains("epic"))
                fallback = "🎮";
            else if (lower.Contains("warp") || lower.Contains("cloudflare") || lower.Contains("vpn") || lower.Contains("shield") || lower.Contains("security"))
                fallback = "🛡️";

            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                try
                {
                    var shinfo = new SHFILEINFO();
                    IntPtr hImg = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
                    if (hImg != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                    {
                        var bs = Imaging.CreateBitmapSourceFromHIcon(
                            shinfo.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze();
                        DestroyIcon(shinfo.hIcon);
                        return (bs, fallback);
                    }
                }
                catch { }
            }

            return (null, fallback);
        }

        /// <summary>
        /// winget sürecini eşzamansız çalıştırır, çıkış kodunu ve çıktısını döndürür.
        /// </summary>
        private static (int exitCode, string output) ExecuteWingetProcess(string wingetPath, string arguments, int timeoutMs = 600000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = wingetPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var proc = new Process { StartInfo = psi };
                var sb = new StringBuilder();

                proc.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                proc.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                bool finished = proc.WaitForExit(timeoutMs);
                if (!finished)
                {
                    try { proc.Kill(); } catch { }
                    return (-1, "Zaman aşımı");
                }

                return (proc.ExitCode, sb.ToString());
            }
            catch (Exception ex)
            {
                return (-999, ex.Message);
            }
        }

        /// <summary>
        /// Tek bir yazılımı winget ile arka planda sessizce günceller (Akıllı çift katmanlı deneme ve sistem paketi desteğiyle).
        /// </summary>
        public static async Task<bool> UpdateAppAsync(SoftwareUpdateItem item, CancellationToken cancellationToken = default)
        {
            string? wingetPath = FindWingetPath();
            if (string.IsNullOrEmpty(wingetPath))
            {
                item.State = SoftwareUpdateState.Failed;
                item.StatusMessage = "winget bulunamadı";
                return false;
            }

            item.State = SoftwareUpdateState.Updating;
            item.StatusMessage = "Güncelleniyor...";

            return await Task.Run(() =>
            {
                try
                {
                    // 1. Aşama: Standart Sessiz Yükseltme
                    string upgradeArgs = $"upgrade --id \"{item.Id}\" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
                    var (exitCode, _) = ExecuteWingetProcess(wingetPath, upgradeArgs);

                    bool isSuccess = (exitCode == 0 || exitCode == unchecked((int)0x8A150001) || exitCode == unchecked((int)0x8A150014));

                    // 2. Aşama: Eğer başarısız olduysa (Edge veya sistem bileşenlerindeki teknoloji uyuşmazlığı gibi durumlarda),
                    // --force parametresiyle zorlamalı yükleme yaparak aşmayı dene
                    if (!isSuccess)
                    {
                        string installForceArgs = $"install --id \"{item.Id}\" --force --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
                        var (forceExit, _) = ExecuteWingetProcess(wingetPath, installForceArgs);
                        isSuccess = (forceExit == 0 || forceExit == unchecked((int)0x8A150001) || forceExit == unchecked((int)0x8A150014));
                    }

                    // 3. Aşama: Microsoft Edge için yerleşik EdgeUpdate servis motorunu tetikleme
                    if (!isSuccess && item.Id.Contains("Edge", StringComparison.OrdinalIgnoreCase))
                    {
                        string edgeUpdatePath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                            @"Microsoft\EdgeUpdate\MicrosoftEdgeUpdate.exe");

                        if (File.Exists(edgeUpdatePath))
                        {
                            using var edgeProc = Process.Start(new ProcessStartInfo
                            {
                                FileName = edgeUpdatePath,
                                Arguments = "/ua /installsource manual",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            });
                            if (edgeProc != null)
                            {
                                bool exited = edgeProc.WaitForExit(20000);
                                if (!exited)
                                {
                                    try { edgeProc.Kill(); } catch { }
                                }
                                else
                                {
                                    isSuccess = (edgeProc.ExitCode == 0);
                                }
                            }
                        }
                    }

                    if (isSuccess)
                    {
                        item.State = SoftwareUpdateState.Completed;
                        item.StatusMessage = "✓ Güncel";
                        item.CurrentVersion = item.AvailableVersion;
                        return true;
                    }
                    else
                    {
                        item.State = SoftwareUpdateState.Failed;
                        item.StatusMessage = "Güncelleme Başarısız";
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SoftwareUpdateService] UpdateAppAsync hatası: {ex.Message}");
                    item.State = SoftwareUpdateState.Failed;
                    item.StatusMessage = "Hata Oluştu";
                    item.ErrorMessage = ex.Message;
                    return false;
                }
            }, cancellationToken);
        }
    }
}
