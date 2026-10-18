using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using PureWin.Models;

namespace PureWin.Services
{
    public class GamingSnapshot
    {
        public string? PreviousPowerSchemeGuid { get; set; }
        public Dictionary<string, int> ServiceOriginalStarts { get; set; } = new();
        public Dictionary<string, int?> ServiceOriginalDelayedAutostart { get; set; } = new();
        public Dictionary<string, NagleAdapterSnapshot> NagleSettings { get; set; } = new();
    }

    public class NagleAdapterSnapshot
    {
        public int? OriginalTcpAckFrequency { get; set; }
        public int? OriginalTCPNoDelay { get; set; }
    }

    public static class GameOptimizerService
    {
        #region Win32 Display Frequency API
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        private const int ENUM_CURRENT_SETTINGS = -1;

        /// <summary>
        /// Monitörün o anki çözünürlüğündeki aktif Hz değerini ve desteklenen maksimum Hz değerini tespit eder.
        /// </summary>
        public static (int CurrentHz, int MaxHz, bool IsAtMax) GetMonitorRefreshRateInfo()
        {
            int currentHz = 60;
            int maxHz = 60;

            try
            {
                var curMode = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref curMode))
                {
                    currentHz = curMode.dmDisplayFrequency;
                    maxHz = currentHz;
                    int curWidth = curMode.dmPelsWidth;
                    int curHeight = curMode.dmPelsHeight;

                    // O anki çözünürlükteki tüm modları tara ve en yüksek Hz değerini bul
                    var queryMode = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                    int modeNum = 0;
                    while (EnumDisplaySettings(null, modeNum++, ref queryMode))
                    {
                        if (queryMode.dmPelsWidth == curWidth && queryMode.dmPelsHeight == curHeight)
                        {
                            if (queryMode.dmDisplayFrequency > maxHz)
                            {
                                maxHz = queryMode.dmDisplayFrequency;
                            }
                        }
                    }
                }
            }
            catch { }

            if (currentHz <= 0) currentHz = 60;
            if (maxHz <= 0) maxHz = currentHz;

            return (currentHz, maxHz, currentHz >= maxHz);
        }
        #endregion

        #region Canlı Ağ Gecikmesi (Ping) Ölçümü (ICMP + TCP Fallback)
        /// <summary>
        /// Ağ gecikmesini önce ICMP ping ile, başarısız veya engelliyse TCP bağlantısıyla ölçer.
        /// </summary>
        public static async Task<int> MeasureLatencyAsync()
        {
            string[] hosts = { "1.1.1.1", "8.8.8.8" };

            // 1. Yol: Standart ICMP Ping
            foreach (var host in hosts)
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(host, 1200);
                    if (reply.Status == IPStatus.Success && reply.RoundtripTime >= 0)
                    {
                        return Math.Max(1, (int)reply.RoundtripTime);
                    }
                }
                catch { }
            }

            // 2. Yol: ISS ICMP engelliyorsa TCP Soket Fallback (Port 443)
            foreach (var host in hosts)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    using var client = new TcpClient();
                    var connectTask = client.ConnectAsync(host, 443);
                    if (await Task.WhenAny(connectTask, Task.Delay(1500)) == connectTask && client.Connected)
                    {
                        sw.Stop();
                        return Math.Max(1, (int)sw.ElapsedMilliseconds);
                    }
                }
                catch { }
            }

            return -1; // İnternet bağlantısı yok veya ana sunuculardan yanıt alınamadı (Sahte değer üretilmez)
        }
        #endregion

        #region Güç Planı (Power Scheme) Bilgisi
        public static (string Guid, string DisplayName, bool IsUltimate) GetActivePowerSchemeInfo()
        {
            string activeGuid = string.Empty;
            string displayName = "Dengeli";
            bool isUltimate = false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = "/getactivescheme",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(2000);

                    // Örnek çıktı: Power Scheme GUID: a1841308-3541-4fab-bc81-f71556f20b4a  (Güç tasarrufu)
                    int guidIdx = output.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
                    if (guidIdx != -1)
                    {
                        string sub = output.Substring(guidIdx + 5).Trim();
                        int spaceIdx = sub.IndexOf(' ');
                        if (spaceIdx != -1)
                        {
                            activeGuid = sub.Substring(0, spaceIdx).Trim();
                        }
                    }
                }
            }
            catch { }

            // 1. Bilinen standart Windows GUID eşlemeleri (%100 temiz ve kusursuz Türkçe başlıklar)
            if (!string.IsNullOrEmpty(activeGuid))
            {
                if (activeGuid.Equals("a1841308-3541-4fab-bc81-f71556f20b4a", StringComparison.OrdinalIgnoreCase))
                {
                    return (activeGuid, "Güç Tasarrufu", false);
                }
                if (activeGuid.Equals("381b4222-f694-41f0-9685-ff5bb260df2e", StringComparison.OrdinalIgnoreCase) ||
                    activeGuid.Equals("381b4222-f694-41f0-9685-ff5bb560df22", StringComparison.OrdinalIgnoreCase))
                {
                    return (activeGuid, "Dengeli", false);
                }
                if (activeGuid.Equals("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase))
                {
                    return (activeGuid, "Yüksek Performans", false);
                }
                if (activeGuid.Equals("e9a42b02-d5df-448d-aa00-03f14749eb61", StringComparison.OrdinalIgnoreCase))
                {
                    return (activeGuid, "Nihai Performans", true);
                }

                // 2. Kayıt defteri üzerinden FriendlyName sorgusu
                try
                {
                    using var regKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{activeGuid}");
                    if (regKey != null)
                    {
                        var fn = regKey.GetValue("FriendlyName")?.ToString() ?? "";
                        if (fn.Contains("-19") || fn.Contains("Ultimate", StringComparison.OrdinalIgnoreCase) || fn.Contains("Nihai", StringComparison.OrdinalIgnoreCase))
                        {
                            return (activeGuid, "Nihai Performans", true);
                        }
                        if (fn.Contains("-13") || fn.Contains("High performance", StringComparison.OrdinalIgnoreCase) || fn.Contains("Yüksek", StringComparison.OrdinalIgnoreCase))
                        {
                            return (activeGuid, "Yüksek Performans", false);
                        }
                        if (fn.Contains("-15") || fn.Contains("Balanced", StringComparison.OrdinalIgnoreCase) || fn.Contains("Dengeli", StringComparison.OrdinalIgnoreCase))
                        {
                            return (activeGuid, "Dengeli", false);
                        }
                        if (fn.Contains("-11") || fn.Contains("Power saver", StringComparison.OrdinalIgnoreCase) || fn.Contains("Tasarruf", StringComparison.OrdinalIgnoreCase))
                        {
                            return (activeGuid, "Güç Tasarrufu", false);
                        }
                        if (!string.IsNullOrWhiteSpace(fn) && !fn.StartsWith("@"))
                        {
                            displayName = fn.Trim();
                            return (activeGuid, displayName, false);
                        }
                    }
                }
                catch { }
            }

            return (activeGuid, displayName, isUltimate);
        }
        #endregion

        #region Snapshot Katmanı (gaming_state.json)
        private static readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PureWin",
            "gaming_state.json");

        public static GamingSnapshot LoadSnapshot()
        {
            try
            {
                if (File.Exists(StateFilePath))
                {
                    string json = File.ReadAllText(StateFilePath);
                    return JsonSerializer.Deserialize<GamingSnapshot>(json) ?? new GamingSnapshot();
                }
            }
            catch { }
            return new GamingSnapshot();
        }

        public static void SaveSnapshot(GamingSnapshot snapshot)
        {
            try
            {
                string dir = Path.GetDirectoryName(StateFilePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StateFilePath, json);
            }
            catch { }
        }
        #endregion

        #region Varsayılan Ayarların Listesini Üretme
        public static List<GameTweakItem> GetDefaultGameTweaks()
        {
            var list = new List<GameTweakItem>
            {
                // ================= GRUP 1: ÖNERİLEN OYUN AYARLARI =================
                new()
                {
                    Id = "DisableBackgroundApps",
                    Title = "Arka Plan Uygulamalarını Kapat",
                    Description = "Windows Mağaza ve UWP uygulamalarının arka planda çalışıp RAM ve CPU tüketmesini engeller. (Masaüstü uygulamalarını [Steam, Discord] etkilemez).",
                    Category = "Önerilen Oyun Ayarları",
                    Icon = "💤",
                    BadgeText = "Önerilen",
                    BadgeType = "recommended"
                },
                new()
                {
                    Id = "DisableAnimations",
                    Title = "Görsel Efektleri En İyi Performansa Ayarla",
                    Description = "Pencere animasyonlarını, saydamlık ve menü gölgelerini kapatır. (Ekran yazı tipi kenarlarını yumuşatma korunur, yazılar bozulmaz).",
                    Category = "Önerilen Oyun Ayarları",
                    Icon = "✨",
                    BadgeText = "Anında Tepki",
                    BadgeType = "recommended",
                    RequiresExplorerRestart = true
                },
                new()
                {
                    Id = "GameMode",
                    Title = "Oyun Modu (Windows Game Mode)",
                    Description = "Oyun sırasında arka plan görev ve bildirimlerini azaltarak FPS kararlılığı sağlar. (Etkisi sistem donanımına göre değişir).",
                    Category = "Önerilen Oyun Ayarları",
                    Icon = "🎯",
                    BadgeText = "Önerilen",
                    BadgeType = "recommended"
                },
                new()
                {
                    Id = "WindowedOptimizations",
                    Title = "Pencereli Mod İyileştirme Seçenekleri (Windows 11)",
                    Description = "Pencereli veya çerçevesiz oyunlarda modern sunum modeliyle giriş gecikmesini azaltır ve Auto HDR desteği sağlar.",
                    Category = "Önerilen Oyun Ayarları",
                    Icon = "🪟",
                    BadgeText = "Düşük Gecikme",
                    BadgeType = "recommended"
                },
                new()
                {
                    Id = "DisableDVR",
                    Title = "Arka Planda Kaydı Kapat (Xbox Game DVR)",
                    Description = "Oyun sırasında arka planda sürekli çalışan gizli video kaydını durdurarak ekran kartı ve disk üzerindeki gereksiz yükü kaldırır.",
                    Category = "Önerilen Oyun Ayarları",
                    Icon = "📹",
                    BadgeText = "Önerilen",
                    BadgeType = "recommended"
                },
                new()
                {
                    Id = "HAGS",
                    Title = "HAGS (Donanım Hızlandırmalı GPU Zamanlaması)",
                    Description = "Ekran kartının bellek ve render görevlerini doğrudan yönetmesini sağlar; CPU yükünü azaltır. (Yeniden başlatma şarttır).",
                    Category = "Önerilen Oyun Ayarları",
                    Icon = "⚡",
                    BadgeText = "GPU Desteği",
                    BadgeType = "reboot",
                    RequiresReboot = true
                },
                new()
                {
                    Id = "DynamicLighting",
                    Title = "Dinamik Aydınlatmayı Kapat (Windows RGB)",
                    Description = "Windows'un klavye ve fare RGB ışıklarını denetlemesini engelleyerek iCUE, Synapse veya Armoury Crate yazılımlarıyla çakışmasını önler.",
                    Category = "Önerilen Oyun Ayarları",
                    Icon = "🌈",
                    BadgeText = "RGB Uyumluluğu",
                    BadgeType = "recommended"
                },
                new()
                {
                    Id = "UltimatePowerPlan",
                    Title = "Nihai / Yüksek Performans Güç Planını Etkinleştir",
                    Description = "Windows'ta gizli gelen Nihai Performans güç planını açar ve işlemci çekirdek park etmesini (core parking) engeller. (Laptoplarda pil tüketimini artırır).",
                    Category = "Önerilen Oyun Ayarları",
                    Icon = "⚡",
                    BadgeText = "Maksimum Güç",
                    BadgeType = "recommended"
                },

                // ================= GRUP 2: İLERİ DÜZEY & ÖDÜNLEŞİMLİ AYARLAR =================
                new()
                {
                    Id = "SysMain",
                    Title = "Diskten Hızlı Getirmeyi Kapat (SysMain / SuperFetch)",
                    Description = "Eski mekanik sabit disklerde (HDD) gereksiz disk önbelleklemesini kapatır ve %100 disk takılmalarını önler. (Hızlı SSD'lerde etkisi sınırlıdır).",
                    Category = "İleri Düzey & Ödünleşimli Ayarlar",
                    Icon = "💾",
                    BadgeText = "İleri Düzey",
                    BadgeType = "advanced"
                },
                new()
                {
                    Id = "WSearch",
                    Title = "Arama İndekslemesini Kapat (Windows Search)",
                    Description = "Arka planda diski ve işlemciyi yoran arama dizini oluşturmayı durdurur. (Dosya Gezgini ve Başlat menüsü anlık aramalarını yavaşlatabilir).",
                    Category = "İleri Düzey & Ödünleşimli Ayarlar",
                    Icon = "🔍",
                    BadgeText = "İleri Düzey",
                    BadgeType = "advanced"
                },
                new()
                {
                    Id = "Nagle",
                    Title = "Ağ Ping Düşürme (Nagle Algoritmasını Kapat)",
                    Description = "Çevrimiçi oyunlarda veri paketlerinin birikmeden anında gönderilmesini sağlar (TcpAckFrequency & TCPNoDelay). (Bazı oyunlarda ters tepebilir).",
                    Category = "İleri Düzey & Ödünleşimli Ayarlar",
                    Icon = "🌐",
                    BadgeText = "İleri Düzey",
                    BadgeType = "advanced"
                }
            };

            return list;
        }
        #endregion

        #region Donanım ve İşletim Sistemi Destek Kontrolü
        public static (bool isSupported, string reason) CheckTweakSupport(string tweakId)
        {
            int osBuild = Environment.OSVersion.Version.Build;

            switch (tweakId)
            {
                case "WindowedOptimizations":
                    if (osBuild < 22621)
                    {
                        return (false, "Win 11 (22H2+) Gerekir");
                    }
                    return (true, string.Empty);

                case "DynamicLighting":
                    if (osBuild < 22631)
                    {
                        return (false, "Win 11 (23H2+) Gerekir");
                    }
                    return (true, string.Empty);

                case "HAGS":
                    if (osBuild < 19041)
                    {
                        return (false, "Win 10 (2004+) Gerekir");
                    }
                    try
                    {
                        using var gfxKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
                        if (gfxKey == null)
                        {
                            return (false, "Desteklenmiyor / Ayarlanmamış");
                        }

                        var hagsSupportVal = gfxKey.GetValue("HagsSupported", null);
                        if (hagsSupportVal is int hs && hs == 0)
                        {
                            return (false, "GPU / Sürücü Desteklemiyor");
                        }

                        var hwSchVal = gfxKey.GetValue("HwSchMode", null);
                        if (hwSchVal == null)
                        {
                            return (false, "Desteklenmiyor / Ayarlanmamış");
                        }
                    }
                    catch
                    {
                        return (false, "Desteklenmiyor / Ayarlanmamış");
                    }
                    return (true, string.Empty);

                case "GameMode":
                    if (osBuild < 15063)
                    {
                        return (false, "Win 10 (1703+) Gerekir");
                    }
                    return (true, string.Empty);

                default:
                    return (true, string.Empty);
            }
        }

        public static (string Status, string Description, bool IsApplied, bool IsSupported) GetHagsStatus()
        {
            var (isSupported, reason) = CheckTweakSupport("HAGS");
            if (!isSupported)
            {
                return ("Desteklenmiyor / Ayarlanmamış", string.IsNullOrEmpty(reason) ? "GPU veya sistem HAGS desteklemiyor." : reason, false, false);
            }

            try
            {
                var hagsVal = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", null);
                if (hagsVal is int hagsi && hagsi == 2)
                {
                    return ("Açık (Etkin)", "Donanım Hızlandırmalı GPU Zamanlaması etkin. (Yeniden başlatma gerekebilir).", true, true);
                }
                else
                {
                    return ("Kapalı (Devre Dışı)", "Donanım Hızlandırmalı GPU Zamanlaması devre dışı bırakılmış.", false, true);
                }
            }
            catch
            {
                return ("Desteklenmiyor / Ayarlanmamış", "Grafik kayıt defteri okunamadı.", false, false);
            }
        }
        #endregion

        #region Canlı Durum Okuma (Sistemden Gerçek Durumları Sorgulama)
        public static void RefreshLiveStates(List<GameTweakItem> items)
        {
            var (_, _, isUltimate) = GetActivePowerSchemeInfo();

            foreach (var item in items)
            {
                var (isSupported, reason) = CheckTweakSupport(item.Id);
                item.IsSupported = isSupported;
                item.UnsupportedReason = reason;

                if (!isSupported)
                {
                    item.IsApplied = false;
                    item.IsPendingChecked = false;
                    continue;
                }

                bool isApplied = false;
                try
                {
                    switch (item.Id)
                    {
                        case "DisableBackgroundApps":
                            var bgVal = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0);
                            isApplied = (bgVal is int bgi && bgi == 1);
                            break;

                        case "DisableAnimations":
                            var animVal = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "MinAnimate", "1");
                            var transVal = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1);
                            isApplied = (animVal?.ToString() == "0" && transVal is int ti && ti == 0);
                            break;

                        case "GameMode":
                            var gmVal = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
                            isApplied = (gmVal is null || (gmVal is int gmi && gmi == 1));
                            break;

                        case "WindowedOptimizations":
                            var winOptVal = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\DirectX\GraphicsSettings", "SwapEffectUpgradeEnable", 0);
                            isApplied = (winOptVal is int woi && woi == 1);
                            break;

                        case "DisableDVR":
                            var dvrVal = Registry.GetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 1);
                            isApplied = (dvrVal is int dvri && dvri == 0);
                            break;

                        case "HAGS":
                            var hagsVal = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", null);
                            isApplied = (hagsVal is int hagsi && hagsi == 2);
                            break;

                        case "DynamicLighting":
                            var rgbVal = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Lighting", "AmbientLightingEnabled", 1);
                            isApplied = (rgbVal is int rgbi && rgbi == 0);
                            break;

                        case "UltimatePowerPlan":
                            isApplied = isUltimate;
                            break;

                        case "SysMain":
                            var sysVal = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain", "Start", 2);
                            isApplied = (sysVal is int sysi && sysi == 4);
                            break;

                        case "WSearch":
                            var wsVal = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSearch", "Start", 2);
                            isApplied = (wsVal is int wsi && wsi == 4);
                            break;

                        case "Nagle":
                            isApplied = CheckNagleAppliedOnActiveAdapters();
                            break;
                    }
                }
                catch { }

                bool wasStaged = item.IsStaged;
                item.IsApplied = isApplied;
                if (!wasStaged)
                {
                    item.IsPendingChecked = isApplied;
                }
            }
        }

        private static bool CheckNagleAppliedOnActiveAdapters()
        {
            try
            {
                var interfaces = GetActivePhysicalInterfaceGuids();
                if (interfaces.Count == 0) return false;

                foreach (var guid in interfaces)
                {
                    string keyPath = $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{guid}";
                    var ack = Registry.GetValue(keyPath, "TcpAckFrequency", null);
                    var nodelay = Registry.GetValue(keyPath, "TCPNoDelay", null);
                    if (ack is int a && a == 1 && nodelay is int nd && nd == 1)
                        return true;
                }
            }
            catch { }
            return false;
        }

        public static List<string> GetActivePhysicalInterfaceGuids()
        {
            var guids = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                         nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    {
                        if (!nic.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                            !nic.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase) &&
                            !nic.Description.Contains("Loopback", StringComparison.OrdinalIgnoreCase) &&
                            !nic.Description.Contains("Pseudo", StringComparison.OrdinalIgnoreCase))
                        {
                            guids.Add(nic.Id);
                        }
                    }
                }
            }
            catch { }
            return guids;
        }
        #endregion

        #region Değişiklikleri Uygulama / Geri Alma (Atomic Apply & Undo Engine)
        public static async Task ApplySingleTweakAsync(GameTweakItem item, bool enable)
        {
            if (!item.IsSupported)
            {
                // Donanım veya Windows sürümü desteklemiyorsa hiçbir işlem yapma
                return;
            }

            await Task.Run(async () =>
            {
                var snapshot = LoadSnapshot();

                switch (item.Id)
                {
                    case "DisableBackgroundApps":
                        if (enable)
                        {
                            TweakSnapshotService.CreateSnapshot("GameOpt_DisableBackgroundApps", "Disable_Background_Apps.reg", "Enable_Background_Apps.reg");
                            RegistryService.ImportRegFile("Disable_Background_Apps.reg");
                        }
                        else
                        {
                            if (!TweakSnapshotService.RestoreSnapshot("GameOpt_DisableBackgroundApps"))
                                RegistryService.ImportRegFile("Enable_Background_Apps.reg", isUndo: true);
                        }
                        break;

                    case "DisableAnimations":
                        if (enable)
                        {
                            TweakSnapshotService.CreateSnapshot("GameOpt_DisableAnimations", "Disable_Animations.reg", "Enable_Animations.reg");
                            TweakSnapshotService.CreateSnapshot("GameOpt_DisableTransparency", "Disable_Transparency.reg", "Enable_Transparency.reg");
                            RegistryService.ImportRegFile("Disable_Animations.reg");
                            RegistryService.ImportRegFile("Disable_Transparency.reg");
                        }
                        else
                        {
                            bool restoredAnim = TweakSnapshotService.RestoreSnapshot("GameOpt_DisableAnimations");
                            if (!restoredAnim) RegistryService.ImportRegFile("Enable_Animations.reg", isUndo: true);

                            bool restoredTrans = TweakSnapshotService.RestoreSnapshot("GameOpt_DisableTransparency");
                            if (!restoredTrans) RegistryService.ImportRegFile("Enable_Transparency.reg", isUndo: true);
                        }
                        break;

                    case "GameMode":
                        try
                        {
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", enable ? 1 : 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", enable ? 1 : 0, RegistryValueKind.DWord);
                        }
                        catch { }
                        break;

                    case "WindowedOptimizations":
                        try
                        {
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\DirectX\GraphicsSettings", "SwapEffectUpgradeEnable", enable ? 1 : 0, RegistryValueKind.DWord);
                        }
                        catch { }
                        break;

                    case "DisableDVR":
                        if (enable)
                        {
                            TweakSnapshotService.CreateSnapshot("GameOpt_DisableDVR", "Disable_DVR.reg", "Enable_DVR.reg");
                            RegistryService.ImportRegFile("Disable_DVR.reg");
                        }
                        else
                        {
                            if (!TweakSnapshotService.RestoreSnapshot("GameOpt_DisableDVR"))
                                RegistryService.ImportRegFile("Enable_DVR.reg", isUndo: true);
                        }
                        break;

                    case "HAGS":
                        try
                        {
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", enable ? 2 : 1, RegistryValueKind.DWord);
                        }
                        catch { }
                        break;

                    case "DynamicLighting":
                        try
                        {
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Lighting", "AmbientLightingEnabled", enable ? 0 : 1, RegistryValueKind.DWord);
                        }
                        catch { }
                        break;

                    case "UltimatePowerPlan":
                        ApplyUltimatePowerPlan(enable, snapshot);
                        break;

                    case "SysMain":
                        var sysItem = WindowsServicesDatabase.GetAllServices().FirstOrDefault(s => s.ServiceName.Equals("SysMain", StringComparison.OrdinalIgnoreCase))
                                      ?? new ServiceItem { ServiceName = "SysMain", OriginalStartupType = 2 };
                        if (enable)
                        {
                            if (!snapshot.ServiceOriginalStarts.ContainsKey("SysMain"))
                            {
                                var curStart = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain", "Start", 2) as int? ?? 2;
                                snapshot.ServiceOriginalStarts["SysMain"] = curStart;
                            }
                        }
                        else
                        {
                            if (snapshot.ServiceOriginalStarts.TryGetValue("SysMain", out int origSysStart))
                            {
                                sysItem.OriginalStartupType = origSysStart;
                            }
                        }
                        await ServiceManagementService.SetServiceStateAsync(sysItem, disable: enable);
                        break;

                    case "WSearch":
                        var wsItem = WindowsServicesDatabase.GetAllServices().FirstOrDefault(s => s.ServiceName.Equals("WSearch", StringComparison.OrdinalIgnoreCase))
                                     ?? new ServiceItem { ServiceName = "WSearch", OriginalStartupType = 2 };
                        int? wsDelayed = null;
                        bool hasDelayedSnap = false;
                        if (enable)
                        {
                            if (!snapshot.ServiceOriginalStarts.ContainsKey("WSearch"))
                            {
                                var curStart = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSearch", "Start", 2) as int? ?? 2;
                                snapshot.ServiceOriginalStarts["WSearch"] = curStart;
                            }
                            if (!snapshot.ServiceOriginalDelayedAutostart.ContainsKey("WSearch"))
                            {
                                var curDelayed = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSearch", "DelayedAutostart", null) as int?;
                                snapshot.ServiceOriginalDelayedAutostart["WSearch"] = curDelayed;
                            }
                        }
                        else
                        {
                            if (snapshot.ServiceOriginalStarts.TryGetValue("WSearch", out int origWsStart))
                            {
                                wsItem.OriginalStartupType = origWsStart;
                            }
                            if (snapshot.ServiceOriginalDelayedAutostart.TryGetValue("WSearch", out int? origDelayed))
                            {
                                wsDelayed = origDelayed;
                                hasDelayedSnap = true;
                            }
                        }
                        await ServiceManagementService.SetServiceStateAsync(wsItem, disable: enable, delayedAutostart: wsDelayed, hasDelayedSnapshot: hasDelayedSnap);
                        break;

                    case "Nagle":
                        ApplyNagleTweak(enable, snapshot);
                        break;
                }

                SaveSnapshot(snapshot);
            });
        }

        private static void ApplyUltimatePowerPlan(bool enable, GamingSnapshot snapshot)
        {
            try
            {
                if (enable)
                {
                    // 1. Önceki aktif güç planı GUID'ini snapshot'a kaydet (eğer daha önce kaydedilmediyse)
                    var (currentGuid, _, isCurrentUltimate) = GetActivePowerSchemeInfo();
                    if (!isCurrentUltimate && !string.IsNullOrEmpty(currentGuid))
                    {
                        snapshot.PreviousPowerSchemeGuid = currentGuid;
                    }

                    // 2. Nihai Performans planı sistemde var mı bul (aktif olmayan hazır klon da dahil)
                    string? targetGuid = FindUltimateSchemeGuid();

                    if (string.IsNullOrEmpty(targetGuid))
                    {
                        // Sistemde Nihai Performans yoksa template üzerinden klon oluştur
                        using var pDup = Process.Start(new ProcessStartInfo
                        {
                            FileName = "powercfg.exe",
                            Arguments = "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        pDup?.WaitForExit(3000);

                        targetGuid = FindUltimateSchemeGuid();
                    }

                    // 3. Nihai Performans planını etkinleştir
                    if (!string.IsNullOrEmpty(targetGuid))
                    {
                        using var pAct = Process.Start(new ProcessStartInfo
                        {
                            FileName = "powercfg.exe",
                            Arguments = $"/setactive {targetGuid}",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        pAct?.WaitForExit(2000);
                    }
                }
                else
                {
                    // Geri alma: Snapshot'taki orijinal GUID'i etkinleştir
                    string targetGuid = !string.IsNullOrEmpty(snapshot.PreviousPowerSchemeGuid)
                        ? snapshot.PreviousPowerSchemeGuid
                        : "381b4222-f694-41f0-9685-ff5bb260df2e"; // Dengeli (Balanced)

                    using var pRestore = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powercfg.exe",
                        Arguments = $"/setactive {targetGuid}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    pRestore?.WaitForExit(2000);
                }
            }
            catch { }
        }

        private static string? FindUltimateSchemeGuid()
        {
            try
            {
                using var rootKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes");
                if (rootKey != null)
                {
                    foreach (var subName in rootKey.GetSubKeyNames())
                    {
                        // Template GUID kendisi setactive yapılamaz, bu yüzden klon olanı seç
                        if (subName.Equals("e9a42b02-d5df-448d-aa00-03f14749eb61", StringComparison.OrdinalIgnoreCase))
                            continue;

                        using var subKey = rootKey.OpenSubKey(subName);
                        if (subKey != null)
                        {
                            var fn = subKey.GetValue("FriendlyName")?.ToString() ?? "";
                            if (fn.Contains("-19") ||
                                fn.Contains("Ultimate", StringComparison.OrdinalIgnoreCase) ||
                                fn.Contains("Nihai", StringComparison.OrdinalIgnoreCase))
                            {
                                return subName;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static void ApplyNagleTweak(bool enable, GamingSnapshot snapshot)
        {
            try
            {
                var guids = GetActivePhysicalInterfaceGuids();
                foreach (var guid in guids)
                {
                    string keyPath = $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{guid}";
                    using var key = Registry.LocalMachine.OpenSubKey(keyPath, true);
                    if (key == null) continue;

                    if (enable)
                    {
                        // Önceki değerleri snapshot'a kaydet
                        if (!snapshot.NagleSettings.ContainsKey(guid))
                        {
                            var curAck = key.GetValue("TcpAckFrequency") as int?;
                            var curNoDelay = key.GetValue("TCPNoDelay") as int?;
                            snapshot.NagleSettings[guid] = new NagleAdapterSnapshot
                            {
                                OriginalTcpAckFrequency = curAck,
                                OriginalTCPNoDelay = curNoDelay
                            };
                        }

                        key.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                        key.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                    }
                    else
                    {
                        // Geri alma
                        if (snapshot.NagleSettings.TryGetValue(guid, out var orig))
                        {
                            if (orig.OriginalTcpAckFrequency.HasValue)
                                key.SetValue("TcpAckFrequency", orig.OriginalTcpAckFrequency.Value, RegistryValueKind.DWord);
                            else
                                try { key.DeleteValue("TcpAckFrequency"); } catch { }

                            if (orig.OriginalTCPNoDelay.HasValue)
                                key.SetValue("TCPNoDelay", orig.OriginalTCPNoDelay.Value, RegistryValueKind.DWord);
                            else
                                try { key.DeleteValue("TCPNoDelay"); } catch { }
                        }
                        else
                        {
                            // Snapshot yoksa doğrudan temizle
                            try { key.DeleteValue("TcpAckFrequency"); } catch { }
                            try { key.DeleteValue("TCPNoDelay"); } catch { }
                        }
                    }
                }
            }
            catch { }
        }
        #endregion
    }
}
