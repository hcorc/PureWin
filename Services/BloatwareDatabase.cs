using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using PureWin.Models;

namespace PureWin.Services
{
    public class AppsCatalog
    {
        public string Version { get; set; } = "2.0";
        public int TotalApps { get; set; }
        public List<BloatwareItem> Apps { get; set; } = new();
    }

    public static class BloatwareDatabase
    {
        private static List<BloatwareItem>? _cachedApps;

        public static List<BloatwareItem> GetDefaultApps()
        {
            if (_cachedApps != null && _cachedApps.Count > 0)
                return CloneApps(_cachedApps);

            try
            {
                string jsonContent = string.Empty;

                // 1. Dosya sisteminden yüklemeyi dene (PureWin\Config\Apps.json veya AppContext.BaseDirectory)
                string diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Apps.json");
                if (File.Exists(diskPath))
                {
                    jsonContent = File.ReadAllText(diskPath);
                }
                else
                {
                    // 2. Gömülü kaynaktan (EmbeddedResource) oku
                    var assembly = Assembly.GetExecutingAssembly();
                    using var stream = assembly.GetManifestResourceStream("PureWin.Config.Apps.json");
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        jsonContent = reader.ReadToEnd();
                    }
                }

                if (!string.IsNullOrWhiteSpace(jsonContent))
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var catalog = JsonSerializer.Deserialize<AppsCatalog>(jsonContent, options);
                    if (catalog != null && catalog.Apps != null && catalog.Apps.Count > 0)
                    {
                        _cachedApps = catalog.Apps;
                        return CloneApps(_cachedApps);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BloatwareDatabase loading failed: {ex.Message}");
            }

            // Fallback (Beklenmedik bir hata durumunda çekirdek liste)
            _cachedApps = GetFallbackApps();
            return CloneApps(_cachedApps);
        }

        private static List<BloatwareItem> CloneApps(List<BloatwareItem> original)
        {
            var list = new List<BloatwareItem>(original.Count);
            foreach (var item in original)
            {
                list.Add(new BloatwareItem
                {
                    AppId = item.AppId,
                    DisplayName = item.DisplayName,
                    Description = item.Description,
                    Category = item.Category,
                    IsSafe = item.IsSafe,
                    RemovalMethod = item.RemovalMethod,
                    IsSelected = false,
                    IsInstalled = false
                });
            }
            return list;
        }

        private static List<BloatwareItem> GetFallbackApps()
        {
            return new List<BloatwareItem>
            {
                new() { AppId = "Microsoft.Edge", DisplayName = "Microsoft Edge & WebView2", Description = "Windows yerleşik web tarayıcısı ve entegrasyon bileşenleri.", IsSafe = false, RemovalMethod = "Edge" },
                new() { AppId = "Clipchamp.Clipchamp", DisplayName = "Clipchamp Video Düzenleyici", Description = "Microsoft video düzenleme yazılımı.", IsSafe = true },
                new() { AppId = "Microsoft.549981C3F5F10", DisplayName = "Microsoft Cortana", Description = "Eski sesli asistan kalıntıları.", IsSafe = true },
                new() { AppId = "Microsoft.BingNews", DisplayName = "Microsoft Haberler", Description = "Haber toplayıcı servis.", IsSafe = true },
                new() { AppId = "Microsoft.BingWeather", DisplayName = "Bing Hava Durumu", Description = "Hava durumu uygulaması.", IsSafe = true },
                new() { AppId = "Microsoft.BingFinance", DisplayName = "Bing Finans", Description = "Ekonomi ve borsa izleme aracı.", IsSafe = true },
                new() { AppId = "Microsoft.BingSports", DisplayName = "Bing Spor", Description = "Spor skorları ve haber servisi.", IsSafe = true },
                new() { AppId = "XP9CXNGPPJ97XX", DisplayName = "Microsoft Copilot", Description = "Bağımsız Copilot arayüzü.", IsSafe = true, RemovalMethod = "WinGet" },
                new() { AppId = "Microsoft.Windows.AIHub", DisplayName = "Copilot+ AI Hub", Description = "Windows 11 24H2 yapay zeka merkezi.", IsSafe = true },
                new() { AppId = "Microsoft.MicrosoftSolitaireCollection", DisplayName = "Solitaire Koleksiyonu", Description = "Reklamlı kağıt oyunları.", IsSafe = true },
                new() { AppId = "Microsoft.WindowsFeedbackHub", DisplayName = "Geri Bildirim Merkezi", Description = "Kullanıcı bildirim ve telemetri servisi.", IsSafe = true },
                new() { AppId = "Microsoft.Getstarted", DisplayName = "İpuçları ve Başlangıç", Description = "Windows tanıtım rehberi.", IsSafe = true },
                new() { AppId = "Microsoft.Microsoft3DViewer", DisplayName = "3D Görüntüleyici", Description = "3B model dosyalarını önizleme aracı.", IsSafe = true },
                new() { AppId = "Microsoft.MicrosoftOfficeHub", DisplayName = "Office Hub", Description = "Office yönlendirme aracı.", IsSafe = true },
                new() { AppId = "Microsoft.PowerAutomateDesktop", DisplayName = "Power Automate", Description = "İş akışı otomasyon aracı.", IsSafe = true },
                new() { AppId = "Microsoft.WindowsMaps", DisplayName = "Windows Haritalar", Description = "Yerel harita ve yol tarifi.", IsSafe = true },
                new() { AppId = "Microsoft.Todos", DisplayName = "Microsoft To Do", Description = "Görev ve yapılacaklar listesi.", IsSafe = true },
                new() { AppId = "SpotifyAB.SpotifyMusic", DisplayName = "Spotify Müzik", Description = "Önerilen müzik uygulaması.", IsSafe = false },
                new() { AppId = "Microsoft.WindowsSoundRecorder", DisplayName = "Ses Kaydedici", Description = "Dahili ses kayıt aracı.", IsSafe = false },
                new() { AppId = "Microsoft.ZuneMusic", DisplayName = "Medya Oynatıcı", Description = "Dahili medya oynatıcı.", IsSafe = false },
                new() { AppId = "Microsoft.XboxApp", DisplayName = "Xbox Konsol Uygulaması", Description = "Xbox arka plan servisleri.", IsSafe = false },
                new() { AppId = "Microsoft.YourPhone", DisplayName = "Telefon Bağlantısı", Description = "Telefon eşleme aracı.", IsSafe = false }
            };
        }
    }
}
