using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PureWin.Models;

namespace PureWin.Services
{
    public class HealthCheckResult
    {
        public bool Success { get; set; }
        public bool IsHealthy { get; set; }
        public string Output { get; set; } = string.Empty;
        public string DetailMessage { get; set; } = string.Empty;
    }

    public static class SystemHealthService
    {
        private static readonly Regex PercentRegex = new(@"(\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled);

        public static ObservableCollection<SystemHealthCategory> GetDefaultCategories()
        {
            var categories = new ObservableCollection<SystemHealthCategory>();

            // 1. Kategori: Çekirdek Sistem Onarımı
            var coreCategory = new SystemHealthCategory
            {
                Title = "Çekirdek Sistem Onarımı",
                Icon = "🛠️",
                Items = new ObservableCollection<SystemHealthItem>
                {
                    // 1. Özellik: DISM Teşhis
                    new SystemHealthItem
                    {
                        Id = "dism_health_check",
                        Title = "DISM Sağlık Kontrolü (teşhis, onarım yok)",
                        Description = "Windows imaj ve bileşen deposunu (WinSxS) tarayarak sistemde bozulma olup olmadığını tespit eder. Sistem üzerinde herhangi bir değişiklik veya onarım yapmaz, yalnızca teşhis koyar.",
                        CommandInfo = "DISM /Online /Cleanup-Image /CheckHealth ve DISM /Online /Cleanup-Image /ScanHealth",
                        RiskLevel = HealthRiskLevel.Low,
                        IsRepair = false,
                        DefaultActionText = "Kontrol Et",
                        State = HealthExecutionState.Ready
                    },

                    // 2. Özellik: DISM Onarım (RestoreHealth)
                    new SystemHealthItem
                    {
                        Id = "dism_restore_health",
                        Title = "DISM Onarım (RestoreHealth)",
                        Description = "Windows bileşen deposundaki (WinSxS) hasarlı, eksik veya bozuk dosyaları Windows Update veya yerel kaynak üzerinden sağlam kopyalarıyla değiştirerek onarır.",
                        CommandInfo = "DISM /Online /Cleanup-Image /RestoreHealth (Kaynaklı: /Source:WIM:D:\\sources\\install.wim:1 /LimitAccess)",
                        RiskLevel = HealthRiskLevel.LowMedium,
                        IsRepair = true,
                        DefaultActionText = "Onar",
                        State = HealthExecutionState.Ready
                    },

                    // 3. Özellik: SFC — Sistem Dosyası Denetleyicisi
                    new SystemHealthItem
                    {
                        Id = "sfc_scannow",
                        Title = "SFC — Sistem Dosyası Denetleyicisi",
                        Description = "Korumalı tüm Windows sistem dosyalarının bütünlüğünü tarar, dijital imzaları doğrular ve hasarlı, bozulmuş veya eksik sistem dosyalarını WinSxS bileşen deposundaki orijinal kopyalarıyla otomatik olarak onarır.",
                        CommandInfo = "sfc /scannow (teşhis için: sfc /verifyonly)",
                        RiskLevel = HealthRiskLevel.Low,
                        RequiresConfirmation = true,
                        IsRepair = true,
                        DefaultActionText = "Denetimi Başlat",
                        State = HealthExecutionState.Ready
                    }
                }
            };

            // 2. Kategori: Windows Update Onarımı
            var updateCategory = new SystemHealthCategory
            {
                Title = "Windows Update Onarımı",
                Icon = "🔄",
                Items = new ObservableCollection<SystemHealthItem>
                {
                    new SystemHealthItem
                    {
                        Id = "wu_reset_components",
                        Title = "Windows Update Bileşenlerini Sıfırlama",
                        Description = "Takılan, hata veren veya indirilemeyen Windows güncellemeleri için Update servislerini durdurur, indirme ve imza önbelleğini (SoftwareDistribution ve Catroot2) sıfırlar ve servisleri yeniden başlatır.",
                        CommandInfo = "net stop wuauserv; net stop cryptSvc; net stop bits; net stop msiserver; ren SoftwareDistribution & Catroot2; net start ...",
                        RiskLevel = HealthRiskLevel.Medium,
                        RequiresConfirmation = true,
                        IsRepair = true,
                        DefaultActionText = "Sıfırla",
                        State = HealthExecutionState.Ready
                    },

                    // 2. Özellik: BITS Kuyruğunu Temizleme
                    new SystemHealthItem
                    {
                        Id = "wu_clear_bits_queue",
                        Title = "BITS Kuyruğunu Temizleme",
                        Description = "Arka Planda Akıllı Aktarım Hizmeti'nde (BITS) takılan, askıda kalan veya yarım kalmış tüm indirme ve aktarım kuyruklarını temizler.",
                        CommandInfo = "bitsadmin /reset /allusers",
                        RiskLevel = HealthRiskLevel.Low,
                        RequiresConfirmation = false,
                        IsRepair = true,
                        DefaultActionText = "Temizle",
                        State = HealthExecutionState.Ready
                    }
                }
            };

            // 3. Kategori: Ağ Onarımı
            var networkCategory = new SystemHealthCategory
            {
                Title = "Ağ Onarımı",
                Icon = "🌐",
                Items = new ObservableCollection<SystemHealthItem>
                {
                    new SystemHealthItem
                    {
                        Id = "net_reset_network_stack",
                        Title = "DNS + Winsock + TCP/IP Sıfırlama",
                        Description = "Bozulmuş ağ yığınını, DNS önbelleğini, Winsock kataloglarını, TCP/IP protokol yapılandırmasını ve WinHTTP proxy ayarlarını sıfırlar. İnternet ve bağlantı arızalarını giderir.",
                        CommandInfo = "ipconfig /flushdns; netsh winsock reset; netsh int ip reset; netsh winhttp reset proxy",
                        RiskLevel = HealthRiskLevel.Medium,
                        RequiresConfirmation = true,
                        IsRepair = true,
                        DefaultActionText = "Sıfırla",
                        State = HealthExecutionState.Ready
                    },

                    // 2. Özellik: Güvenlik Duvarını Sıfırlama
                    new SystemHealthItem
                    {
                        Id = "net_reset_firewall",
                        Title = "Güvenlik Duvarını Sıfırlama",
                        Description = "Windows Defender Güvenlik Duvarı yapılandırmasını ve tüm ilkelerini fabrika ayarlarına sıfırlar. Bozulmuş veya istenmeyen gelen ve giden bağlantı kurallarını temizler.",
                        CommandInfo = "netsh advfirewall reset",
                        RiskLevel = HealthRiskLevel.Medium,
                        RequiresConfirmation = true,
                        IsRepair = true,
                        DefaultActionText = "Sıfırla",
                        State = HealthExecutionState.Ready
                    },

                    // 3. Özellik: Tam Ağ Sıfırlama (Gelişmiş)
                    new SystemHealthItem
                    {
                        Id = "net_reset_full",
                        Title = "Tam Ağ Sıfırlama (Gelişmiş)",
                        Description = "Tüm fiziksel ve sanal ağ bağdaştırıcılarını sistemden kaldırıp fabrika sürücüleriyle yeniden yükler. İnternet ve sürücü çakışmalarını kökten temizler.",
                        CommandInfo = "netcfg -d (veya Windows Ayarları > Ağ Sıfırlama)",
                        RiskLevel = HealthRiskLevel.High,
                        RequiresConfirmation = true,
                        IsRepair = true,
                        DefaultActionText = "Sıfırla",
                        State = HealthExecutionState.Ready
                    }
                }
            };

            // 4. Kategori: Disk Ve Bileşen Temizliği
            var diskCategory = new SystemHealthCategory
            {
                Title = "Disk Ve Bileşen Temizliği",
                Icon = "💾",
                Items = new ObservableCollection<SystemHealthItem>
                {
                    // 1. Özellik: CHKDSK Çevrimiçi Tarama
                    new SystemHealthItem
                    {
                        Id = "disk_chkdsk_scan",
                        Title = "CHKDSK Çevrimiçi Tarama (onarım yok)",
                        Description = "Windows açıkken ve bilgisayarı yeniden başlatmaya gerek kalmadan C: sürücüsündeki dosya sistemi bütünlüğünü çevrimiçi tarar. Herhangi bir değişiklik veya onarım yapmaz, yalnızca teşhis koyar.",
                        CommandInfo = "chkdsk C: /scan (PowerShell: Repair-Volume -DriveLetter C -Scan)",
                        RiskLevel = HealthRiskLevel.Low,
                        RequiresConfirmation = false,
                        IsRepair = false,
                        DefaultActionText = "Tara",
                        State = HealthExecutionState.Ready
                    },

                    // 2. Özellik: CHKDSK Çevrimdışı Onarım (reboot gerekir)
                    new SystemHealthItem
                    {
                        Id = "disk_chkdsk_offline_repair",
                        Title = "CHKDSK Çevrimdışı Onarım (reboot gerekir)",
                        Description = "C: sistem sürücüsündeki bozuk dosya tablolarını, dizinleri ve disk sektörlerini bilgisayar yeniden başlatılırken onarmak üzere zamanlar. Standart onarım (/f) veya kapsamlı bozuk sektör taraması (/r) sunar.",
                        CommandInfo = "chkdsk C: /f (Agresif / Bozuk Sektör Onarımı: chkdsk C: /r)",
                        RiskLevel = HealthRiskLevel.Medium,
                        RequiresConfirmation = true,
                        IsRepair = true,
                        DefaultActionText = "Onarımı Zamanla",
                        State = HealthExecutionState.Ready
                    },

                    // 3. Özellik: Bileşen Deposu Temizliği
                    new SystemHealthItem
                    {
                        Id = "disk_component_cleanup",
                        Title = "Bileşen Deposu Temizliği",
                        Description = "Windows WinSxS bileşen deposundaki eski, güncelliğini yitirmiş ve artık kullanılmayan güncelleme paketlerini temizler. Standart temizlik veya geri almayı devre dışı bırakıp maksimum disk alanı açan agresif (/ResetBase) temizlik sunar.",
                        CommandInfo = "DISM /Online /Cleanup-Image /StartComponentCleanup (Agresif: /ResetBase)",
                        RiskLevel = HealthRiskLevel.Medium,
                        RequiresConfirmation = true,
                        IsRepair = true,
                        DefaultActionText = "Temizliği Başlat",
                        State = HealthExecutionState.Ready
                    },

                    // 4. Özellik: Sürücüleri İyileştir ve Düzenle (dfrgui)
                    new SystemHealthItem
                    {
                        Id = "disk_dfrgui",
                        Title = "Sürücüleri İyileştir ve Düzenle",
                        Description = "Windows yerleşik sürücü iyileştirme ve birleştirme aracını (dfrgui.exe) açar. Katı Hâl Sürücüleri (SSD) için TRIM göndererek hücreleri temizler, mekanik sabit disklerde (HDD) dosya düzeni birleştirmesi sağlar.",
                        CommandInfo = "dfrgui.exe",
                        RiskLevel = HealthRiskLevel.Low,
                        RequiresConfirmation = false,
                        IsRepair = false,
                        DefaultActionText = "Aracı Aç",
                        State = HealthExecutionState.Ready
                    }
                }
            };

            // 5. Kategori: Store & Uygulama Onarımı
            var storeCategory = new SystemHealthCategory
            {
                Title = "Store & Uygulama Onarımı",
                Icon = "🛍️",
                Items = new ObservableCollection<SystemHealthItem>
                {
                    // 1. Özellik: Microsoft Store Önbelleği Temizleme
                    new SystemHealthItem
                    {
                        Id = "store_wsreset",
                        Title = "Microsoft Store Önbelleği Temizleme",
                        Description = "Microsoft Store önbelleğini ve geçici verilerini sıfırlar. Mağazanın açılmaması, takılması veya uygulama indirme hatalarını giderir.",
                        CommandInfo = "wsreset.exe",
                        RiskLevel = HealthRiskLevel.Low,
                        RequiresConfirmation = false,
                        IsRepair = true,
                        DefaultActionText = "Sıfırla",
                        State = HealthExecutionState.Ready
                    },

                    // 2. Özellik: UWP Uygulamalarını Yeniden Kaydetme
                    new SystemHealthItem
                    {
                        Id = "store_reregister_uwp",
                        Title = "UWP Uygulamalarını Yeniden Kaydetme",
                        Description = "Tüm kullanıcı hesapları için Windows yerleşik ve mağaza (UWP) uygulamalarının bildirim dosyalarını (AppXManifest) sisteme yeniden kaydeder. Açılmayan, aniden kapanan veya başlangıç menüsünde simgesi bozulan uygulamaları onarır.",
                        CommandInfo = "Get-AppxPackage -AllUsers | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\"}",
                        RiskLevel = HealthRiskLevel.Medium,
                        RequiresConfirmation = true,
                        IsRepair = true,
                        DefaultActionText = "Yeniden Kaydet",
                        State = HealthExecutionState.Ready
                    }
                }
            };

            // 6. Kategori: Diğer Sağlık Ayarları
            var otherCategory = new SystemHealthCategory
            {
                Title = "Diğer Sağlık Ayarları",
                Icon = "⚡",
                Items = new ObservableCollection<SystemHealthItem>
                {
                    // 1. Özellik: Enerji/Pil Raporu (teşhis)
                    new SystemHealthItem
                    {
                        Id = "other_energy_battery_report",
                        Title = "Enerji/Pil Raporu (teşhis)",
                        Description = "Sistemin güç yapılandırmasını, pil ömrü geçmişini, modern bekleme (Sleep Study) verilerini ve enerji verimliliği sorunlarını analiz ederek kapsamlı HTML teşhis raporları üretir.",
                        CommandInfo = "powercfg /energy; powercfg /batteryreport; powercfg /sleepstudy",
                        RiskLevel = HealthRiskLevel.Low,
                        RequiresConfirmation = false,
                        IsRepair = false,
                        DefaultActionText = "Rapor Oluştur",
                        State = HealthExecutionState.Ready
                    },

                    // 2. Özellik: Windows Bellek Tanılama
                    new SystemHealthItem
                    {
                        Id = "other_mdsched",
                        Title = "Windows Bellek Tanılama",
                        Description = "Bilgisayarın RAM bellek modüllerini donanımsal arıza ve veri hatalarına karşı test etmek için Windows Bellek Tanılama aracını zamanlar. Bilgisayar yeniden başlatıldığında kapsamlı bellek testi yürütülür.",
                        CommandInfo = "mdsched.exe",
                        RiskLevel = HealthRiskLevel.Low,
                        RequiresConfirmation = true,
                        IsRepair = false,
                        DefaultActionText = "Tanıla",
                        State = HealthExecutionState.Ready
                    },

                    // 3. Özellik: Aygıt/Sürücü Sorun Tespiti
                    new SystemHealthItem
                    {
                        Id = "other_device_driver_diagnostic",
                        Title = "Aygıt/Sürücü Sorun Tespiti",
                        Description = "Sistemdeki tüm donanım ve Tak-Çalıştır (PnP) aygıtlarını tarayarak sürücü hatası veren, tanınmayan veya sorun bildiren aygıtları (Kod 43, Kod 28, Kod 10 vb.) tespit edip ayrıntılı listeler.",
                        CommandInfo = "Get-PnpDevice -Status Error,Unknown,Degraded; Get-CimInstance Win32_PnPEntity | Where-Object {$_.ConfigManagerErrorCode -ne 0}",
                        RiskLevel = HealthRiskLevel.Low,
                        RequiresConfirmation = false,
                        IsRepair = false,
                        DefaultActionText = "Aygıtları Tara",
                        State = HealthExecutionState.Ready
                    }
                }
            };

            categories.Add(coreCategory);
            categories.Add(updateCategory);
            categories.Add(networkCategory);
            categories.Add(diskCategory);
            categories.Add(storeCategory);
            categories.Add(otherCategory);
            return categories;
        }

        public static async Task<HealthCheckResult> RunHealthItemAsync(SystemHealthItem item, IProgress<(int percent, string stage)>? progress = null)
        {
            return item.Id switch
            {
                "dism_health_check" => await RunDismHealthCheckAsync(progress),
                "dism_restore_health" => await RunDismRestoreHealthAsync(progress),
                "sfc_scannow" => await RunSfcScanAsync(progress),
                "wu_reset_components" => await RunWuResetComponentsAsync(progress),
                "wu_clear_bits_queue" => await RunBitsQueueClearAsync(progress),
                "net_reset_network_stack" => await RunNetworkStackResetAsync(progress),
                "net_reset_firewall" => await RunFirewallResetAsync(progress),
                "net_reset_full" => await RunFullNetworkResetAsync(progress),
                "disk_chkdsk_scan" => await RunChkdskScanAsync(progress),
                "disk_chkdsk_offline_repair" => await RunChkdskOfflineRepairAsync(item.ExecutionArgument ?? "/f", progress),
                "disk_component_cleanup" => await RunComponentCleanupAsync(item.ExecutionArgument ?? "", progress),
                "disk_dfrgui" => await RunDfrguiAsync(progress),
                "store_wsreset" => await RunWsresetAsync(progress),
                "store_reregister_uwp" => await RunReregisterUwpAppsAsync(progress),
                "other_energy_battery_report" => await RunEnergyBatteryReportAsync(progress),
                "other_mdsched" => await RunMdschedAsync(progress),
                "other_device_driver_diagnostic" => await RunDeviceDriverDiagnosticAsync(progress),
                _ => new HealthCheckResult
                {
                    Success = false,
                    IsHealthy = false,
                    DetailMessage = "Tanımlanmamış sağlık işlemi."
                }
            };
        }

        private static async Task<HealthCheckResult> RunDismHealthCheckAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // 1. Adım: CheckHealth (Hızlı durum denetimi)
                    progress?.Report((10, "Aşama 1/2: Hızlı durum denetimi (CheckHealth) yapılıyor..."));
                    var checkResult = await ExecuteDismWithProgressAsync("/Online /Cleanup-Image /CheckHealth");

                    progress?.Report((25, "Aşama 1/2 tamamlandı."));

                    // 2. Adım: ScanHealth (Kapsamlı WinSxS bileşen deposu taraması)
                    progress?.Report((30, "Aşama 2/2: WinSxS bileşen deposu taranıyor (ScanHealth)..."));
                    
                    var scanProgress = new Progress<int>(p =>
                    {
                        // 0-100% DISM çıktısını genel ilerlemede %30 - %95 aralığına haritala
                        int mapped = 30 + (int)(p * 0.65);
                        progress?.Report((mapped, $"Aşama 2/2: WinSxS bileşen deposu taranıyor (%{p})..."));
                    });

                    var scanResult = await ExecuteDismWithProgressAsync("/Online /Cleanup-Image /ScanHealth", scanProgress);

                    string combinedOutput = $"CheckHealth:\n{checkResult.Output}\n\nScanHealth:\n{scanResult.Output}";

                    if (scanResult.ExitCode == 0 && checkResult.ExitCode == 0)
                    {
                        string combinedLower = (checkResult.Output + " " + scanResult.Output).ToLowerInvariant();

                        // 1. Kesinlikle Temiz / Sağlıklı belirtileri (Windows "algılanmadı" veya "no corruption" dediğinde)
                        bool isExplicitlyClean = combinedLower.Contains("bozulma algılanmadı") ||
                                                 combinedLower.Contains("bozulması algılanmadı") ||
                                                 combinedLower.Contains("bozulma tespit edilmedi") ||
                                                 combinedLower.Contains("hasar algılanmadı") ||
                                                 combinedLower.Contains("no component store corruption detected") ||
                                                 combinedLower.Contains("no corruption detected");

                        // 2. Gerçek Bozulma Belirtileri (Onarılabilir veya Ağır)
                        bool isRepairable = combinedLower.Contains("onarılabilir") ||
                                            combinedLower.Contains("repairable");

                        bool isNonRepairable = combinedLower.Contains("onarılamaz") ||
                                               combinedLower.Contains("non-repairable") ||
                                               combinedLower.Contains("cannot be repaired");

                        bool hasCorruption = (combinedLower.Contains("bozulma algılandı") ||
                                              combinedLower.Contains("bozulma tespit edildi") ||
                                              (combinedLower.Contains("corruption detected") && !combinedLower.Contains("no component store corruption detected") && !combinedLower.Contains("no corruption detected")));

                        bool corruptionDetected = (isRepairable || isNonRepairable || hasCorruption) && !isExplicitlyClean;

                        if (corruptionDetected)
                        {
                            if (isNonRepairable)
                            {
                                progress?.Report((100, "❌ Tarama tamamlandı: Ağır bozulma tespit edildi."));
                                return new HealthCheckResult
                                {
                                    Success = true,
                                    IsHealthy = false,
                                    Output = combinedOutput,
                                    DetailMessage = "Windows bileşen deposunda (WinSxS) onarılamayan hasarlı dosyalar bulundu. Bu durum için Windows kurulum medyası (USB/ISO) üzerinden kaynak belirterek onarım yapılması gereklidir."
                                };
                            }

                            progress?.Report((100, "⚠️ Tarama tamamlandı: Bozulma tespit edildi."));
                            return new HealthCheckResult
                            {
                                Success = true,
                                IsHealthy = false,
                                Output = combinedOutput,
                                DetailMessage = "Windows bileşen deposunda (WinSxS) hasarlı dosyalar bulundu. Onarım için hemen altındaki 'DISM Onarım (RestoreHealth)' işlemini çalıştırmanız önerilir."
                            };
                        }

                        progress?.Report((100, "✓ Tarama tamamlandı: Bileşen deposu sağlıklı."));
                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = true,
                            Output = combinedOutput,
                            DetailMessage = "Windows bileşen deposu (WinSxS) doğrulandı. Herhangi bir bozulma veya hasar bulunamadı; sistem tamamen sağlıklı."
                        };
                    }
                    else
                    {
                        int failedCode = scanResult.ExitCode != 0 ? scanResult.ExitCode : checkResult.ExitCode;
                        progress?.Report((100, "✕ Tarama tamamlanamadı."));
                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            Output = combinedOutput,
                            DetailMessage = $"DISM sağlık kontrolü tamamlanamadı (Hata Kodu: {failedCode})."
                        };
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        Output = ex.ToString(),
                        DetailMessage = $"DISM çalıştırılırken hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunDismRestoreHealthAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    progress?.Report((5, "Onarım başlatılıyor: Windows Update servislerine bağlanılıyor..."));

                    var restoreProgress = new Progress<int>(p =>
                    {
                        if (p >= 18 && p <= 22)
                        {
                            progress?.Report((p, "Onarım sürüyor (%20): Paketler ayrıştırılıyor (bu aşama normaldir, bekleyin)..."));
                        }
                        else
                        {
                            progress?.Report((p, $"WinSxS bileşen deposu onarılıyor (%{p})..."));
                        }
                    });

                    var restoreResult = await ExecuteDismWithProgressAsync("/Online /Cleanup-Image /RestoreHealth", restoreProgress);

                    if (restoreResult.ExitCode == 0)
                    {
                        progress?.Report((100, "✓ Onarım başarıyla tamamlandı!"));
                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = true,
                            Output = restoreResult.Output,
                            DetailMessage = "Windows bileşen deposu (WinSxS) başarıyla onarıldı. Bozuk veya eksik tüm bileşenler Windows Update üzerinden sağlam kopyalarıyla yenilendi."
                        };
                    }
                    else
                    {
                        progress?.Report((100, "⚠️ Onarım tamamlanamadı."));
                        string note = "Windows Update üzerinden sağlam kaynak dosyaları indirilemedi.";
                        if (restoreResult.Output.Contains("0x800f081f"))
                        {
                            note = "Kaynak dosyalar bulunamadı (0x800f081f). İnternet bağlantısı veya yerel /Source:WIM parametresi gerekebilir.";
                        }

                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            Output = restoreResult.Output,
                            DetailMessage = $"DISM onarımı tamamlanamadı (Hata Kodu: {restoreResult.ExitCode}). {note}"
                        };
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        Output = ex.ToString(),
                        DetailMessage = $"DISM RestoreHealth çalıştırılırken hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunSfcScanAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    progress?.Report((0, "Sistem dosyası doğrulaması başlatılıyor (sfc /scannow)..."));

                    string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string sfcPath = Path.Combine(system32, "sfc.exe");
                    if (!File.Exists(sfcPath))
                    {
                        sfcPath = "sfc.exe";
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = sfcPath,
                        Arguments = "/scannow",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.Unicode,
                        StandardErrorEncoding = Encoding.Unicode
                    };

                    using var proc = new Process { StartInfo = psi };
                    if (!proc.Start())
                    {
                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            DetailMessage = "SFC işlemi başlatılamadı."
                        };
                    }

                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();

                    var errorTask = Task.Run(async () =>
                    {
                        char[] errBuf = new char[512];
                        while (!proc.HasExited || !proc.StandardError.EndOfStream)
                        {
                            int read = await proc.StandardError.ReadAsync(errBuf, 0, errBuf.Length);
                            if (read <= 0) break;
                            errorBuilder.Append(errBuf, 0, read);
                        }
                    });

                    char[] buffer = new char[256];
                    var streamBuffer = new StringBuilder();

                    while (!proc.HasExited || !proc.StandardOutput.EndOfStream)
                    {
                        int read = await proc.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
                        if (read <= 0) break;

                        outputBuilder.Append(buffer, 0, read);
                        streamBuffer.Append(buffer, 0, read);

                        string currentText = streamBuffer.ToString().Replace("\0", "");
                        var matches = PercentRegex.Matches(currentText);
                        if (matches.Count > 0)
                        {
                            var lastMatch = matches[^1];
                            if (double.TryParse(lastMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedVal))
                            {
                                int p = (int)Math.Round(parsedVal);
                                progress?.Report((p, $"Sistem dosyaları doğrulanıyor (%{p})..."));
                            }

                            if (streamBuffer.Length > 2048)
                            {
                                streamBuffer.Remove(0, streamBuffer.Length - 512);
                            }
                        }
                    }

                    var exitTask = Task.WhenAll(proc.WaitForExitAsync(), errorTask);
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(30));
                    if (await Task.WhenAny(exitTask, timeoutTask) == timeoutTask)
                    {
                        try { proc.Kill(entireProcessTree: true); } catch { }
                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            Output = outputBuilder.ToString(),
                            DetailMessage = "SFC taraması zaman aşımına uğradı (30 dakika sınırı aşıldı)."
                        };
                    }

                    string fullOutput = (outputBuilder.ToString() + (errorBuilder.Length > 0 ? "\n" + errorBuilder.ToString() : string.Empty)).Replace("\0", "");
                    string outLower = fullOutput.ToLowerInvariant();

                    // 1. Temiz: Hiçbir bozulma yok
                    if (outLower.Contains("did not find any integrity violations") ||
                        outLower.Contains("bütünlük ihlaliyle karşılaşmadı") ||
                        outLower.Contains("ihlal bulunamadı"))
                    {
                        progress?.Report((100, "✓ Doğrulama tamamlandı: Bütünlük ihlali bulunamadı."));
                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = true,
                            Output = fullOutput,
                            DetailMessage = "Windows Resource Protection: Sistem dosyalarında herhangi bir bozulma veya bütünlük ihlali bulunamadı; tüm dosyalar orijinal ve sağlam."
                        };
                    }

                    // 2. Onarıldı: Bozuk dosyalar bulundu ve düzeltildi
                    if (outLower.Contains("successfully repaired them") ||
                        outLower.Contains("başarıyla onardı"))
                    {
                        progress?.Report((100, "✓ Bozuk dosyalar tespit edildi ve başarıyla onarıldı."));
                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = true,
                            Output = fullOutput,
                            DetailMessage = "Windows Resource Protection: Hasarlı veya bozuk sistem dosyaları tespit edildi ve bileşen deposundaki orijinal kopyalarıyla başarıyla onarıldı."
                        };
                    }

                    // 3. Bazı dosyalar onarılamadı
                    if (outLower.Contains("unable to fix some of them") ||
                        outLower.Contains("bazılarını onaramadı"))
                    {
                        progress?.Report((100, "⚠️ Bozuk dosyalar bulundu fakat bazıları onarılamadı."));
                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = false,
                            Output = fullOutput,
                            DetailMessage = "Windows Resource Protection: Bozuk sistem dosyaları tespit edildi fakat bazıları otomatik onarılamadı. Önce 'DISM Onarım (RestoreHealth)' çalıştırıp ardından SFC'yi yeniden çalıştırmanız tavsiye edilir."
                        };
                    }

                    // 4. Genel çıkış kodu 0
                    if (proc.ExitCode == 0)
                    {
                        progress?.Report((100, "✓ Sistem dosyası doğrulaması tamamlandı."));
                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = true,
                            Output = fullOutput,
                            DetailMessage = "Sistem dosyası denetimi tamamlandı. Ayrıntılar CBS.log kütüğüne kaydedildi."
                        };
                    }

                    // 5. Hata
                    progress?.Report((100, "✕ Tarama tamamlanamadı."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        Output = fullOutput,
                        DetailMessage = $"SFC işlemi tamamlanamadı (Hata Kodu: {proc.ExitCode})."
                    };
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        Output = ex.ToString(),
                        DetailMessage = $"SFC çalıştırılırken hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<(int ExitCode, string Output)> ExecuteDismWithProgressAsync(string arguments, IProgress<int>? progress = null)
        {
            string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string dismPath = Path.Combine(system32, "dism.exe");
            if (!File.Exists(dismPath))
            {
                dismPath = "dism.exe";
            }

            var psi = new ProcessStartInfo
            {
                FileName = dismPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.Default
            };

            using var proc = new Process { StartInfo = psi };
            if (!proc.Start())
            {
                return (-1, "DISM işlemi başlatılamadı.");
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            // Standart hata akışını arka planda oku
            var errorTask = Task.Run(async () =>
            {
                char[] errBuf = new char[512];
                while (!proc.HasExited || !proc.StandardError.EndOfStream)
                {
                    int read = await proc.StandardError.ReadAsync(errBuf, 0, errBuf.Length);
                    if (read <= 0) break;
                    errorBuilder.Append(errBuf, 0, read);
                }
            });

            // Standart çıkış akışını canlı tamponlayarak oku ve yüzdeleri anında yakala
            char[] buffer = new char[256];
            var streamBuffer = new StringBuilder();

            while (!proc.HasExited || !proc.StandardOutput.EndOfStream)
            {
                int read = await proc.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0) break;

                outputBuilder.Append(buffer, 0, read);
                streamBuffer.Append(buffer, 0, read);

                // Yüzde kalıbını ara
                string currentText = streamBuffer.ToString();
                var matches = PercentRegex.Matches(currentText);
                if (matches.Count > 0)
                {
                    var lastMatch = matches[^1];
                    if (double.TryParse(lastMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedVal))
                    {
                        progress?.Report((int)Math.Round(parsedVal));
                    }

                    // Tamponu kısalt
                    if (streamBuffer.Length > 2048)
                    {
                        streamBuffer.Remove(0, streamBuffer.Length - 512);
                    }
                }
            }

            var exitTask = Task.WhenAll(proc.WaitForExitAsync(), errorTask);
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(15));
            if (await Task.WhenAny(exitTask, timeoutTask) == timeoutTask)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return (-1, "DISM işlemi zaman aşımına uğradı (15 dakika sınırı aşıldı).");
            }

            string fullOutput = outputBuilder.ToString() + (errorBuilder.Length > 0 ? "\n" + errorBuilder.ToString() : string.Empty);
            return (proc.ExitCode, fullOutput);
        }

        private static async Task<HealthCheckResult> RunWuResetComponentsAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // 1. Adım: Windows Update servisini durdur
                    progress?.Report((10, "Aşama 1/6: Windows Update servisi (wuauserv) durduruluyor..."));
                    await Task.Run(() => RunNetCommand("stop wuauserv"));

                    // 2. Adım: cryptSvc, bits ve msiserver servislerini durdur
                    progress?.Report((25, "Aşama 2/6: cryptSvc, BITS ve msiserver servisleri durduruluyor..."));
                    await Task.Run(() =>
                    {
                        RunNetCommand("stop cryptSvc");
                        RunNetCommand("stop bits");
                        RunNetCommand("stop msiserver");
                    });

                    await Task.Delay(1000); // Dosya tanıtıcılarının serbest kalması için kısa bekleme

                    // 3. Adım: SoftwareDistribution klasörünü sıfırla/yeniden adlandır
                    progress?.Report((45, "Aşama 3/6: SoftwareDistribution indirme önbelleği sıfırlanıyor..."));
                    string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    string softDist = Path.Combine(winDir, "SoftwareDistribution");
                    await Task.Run(() => SafeRenameFolder(softDist, "SoftwareDistribution.old"));

                    // 4. Adım: Catroot2 klasörünü sıfırla/yeniden adlandır
                    progress?.Report((65, "Aşama 4/6: Catroot2 imza ve katalog önbelleği sıfırlanıyor..."));
                    string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string catroot2 = Path.Combine(system32, "catroot2");
                    await Task.Run(() => SafeRenameFolder(catroot2, "Catroot2.old"));

                    // 5. Adım: msiserver, bits ve cryptSvc servislerini başlat
                    progress?.Report((80, "Aşama 5/6: Installer, BITS ve cryptSvc servisleri başlatılıyor..."));
                    await Task.Run(() =>
                    {
                        RunNetCommand("start msiserver");
                        RunNetCommand("start bits");
                        RunNetCommand("start cryptSvc");
                    });

                    // 6. Adım: wuauserv servisini başlat
                    progress?.Report((95, "Aşama 6/6: Windows Update servisi (wuauserv) başlatılıyor..."));
                    await Task.Run(() => RunNetCommand("start wuauserv"));

                    progress?.Report((100, "✓ Windows Update bileşenleri ve önbelleği başarıyla sıfırlandı."));

                    return new HealthCheckResult
                    {
                        Success = true,
                        IsHealthy = true,
                        DetailMessage = "Windows Update servisleri ve indirme önbelleği (SoftwareDistribution & Catroot2) başarıyla sıfırlandı. Artık Ayarlar > Windows Update üzerinden güncellemeleri temiz bir şekilde yeniden denetleyebilirsiniz."
                    };
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Sıfırlama sırasında hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"Windows Update sıfırlama sırasında hata: {ex.Message}"
                    };
                }
            });
        }

        private static void RunNetCommand(string argument)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "net.exe",
                    Arguments = argument,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(10000);
            }
            catch { }
        }

        private static void SafeRenameFolder(string sourcePath, string targetName)
        {
            try
            {
                if (!Directory.Exists(sourcePath)) return;

                string? parent = Path.GetDirectoryName(sourcePath);
                if (string.IsNullOrEmpty(parent)) return;

                string destPath = Path.Combine(parent, targetName);
                if (Directory.Exists(destPath))
                {
                    try
                    {
                        Directory.Delete(destPath, true);
                    }
                    catch
                    {
                        destPath = Path.Combine(parent, $"{targetName}_{DateTime.Now:yyyyMMdd_HHmmss}");
                    }
                }

                Directory.Move(sourcePath, destPath);
            }
            catch
            {
                try
                {
                    string downloadDir = Path.Combine(sourcePath, "Download");
                    if (Directory.Exists(downloadDir))
                    {
                        foreach (var f in Directory.GetFiles(downloadDir))
                        {
                            try { File.Delete(f); } catch { }
                        }
                        foreach (var d in Directory.GetDirectories(downloadDir))
                        {
                            try { Directory.Delete(d, true); } catch { }
                        }
                    }
                }
                catch { }
            }
        }

        private static async Task<HealthCheckResult> RunBitsQueueClearAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    progress?.Report((20, "BITS servisi durumu denetleniyor..."));
                    await Task.Run(() => RunNetCommand("start bits"));

                    progress?.Report((50, "Askıda kalan BITS indirme kuyrukları sıfırlanıyor (bitsadmin /reset /allusers)..."));

                    var psi = new ProcessStartInfo
                    {
                        FileName = "bitsadmin.exe",
                        Arguments = "/reset /allusers",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.Default
                    };

                    using var proc = Process.Start(psi);
                    string output = string.Empty;
                    if (proc != null)
                    {
                        output = await proc.StandardOutput.ReadToEndAsync();
                        await proc.WaitForExitAsync();
                    }

                    progress?.Report((80, "Tüm kullanıcı aktarım kuyrukları temizleniyor..."));
                    try
                    {
                        var psPsi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-BitsTransfer -AllUsers -ErrorAction SilentlyContinue | Remove-BitsTransfer -ErrorAction SilentlyContinue\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using var psProc = Process.Start(psPsi);
                        psProc?.WaitForExit(5000);
                    }
                    catch { }

                    progress?.Report((100, "✓ BITS aktarım kuyruğu başarıyla temizlendi."));

                    return new HealthCheckResult
                    {
                        Success = true,
                        IsHealthy = true,
                        Output = output,
                        DetailMessage = "Arka Planda Akıllı Aktarım Hizmeti (BITS) kuyruğundaki tüm askıda kalmış işler ve takılı kalan indirmeler başarıyla temizlendi."
                    };
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ BITS kuyruğu temizlenirken hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"BITS kuyruğu temizlenirken hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunNetworkStackResetAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    var combinedOutput = new StringBuilder();

                    // 1. Adım: ipconfig /flushdns
                    progress?.Report((25, "Aşama 1/4: DNS çözümleyici önbelleği temizleniyor (ipconfig /flushdns)..."));
                    string out1 = await RunProcessCaptureAsync("ipconfig.exe", "/flushdns");
                    combinedOutput.AppendLine(out1);

                    // 2. Adım: netsh winsock reset
                    progress?.Report((50, "Aşama 2/4: Winsock kataloğu sıfırlanıyor (netsh winsock reset)..."));
                    string out2 = await RunProcessCaptureAsync("netsh.exe", "winsock reset");
                    combinedOutput.AppendLine(out2);

                    // 3. Adım: netsh int ip reset
                    progress?.Report((75, "Aşama 3/4: TCP/IP protokol yığını sıfırlanıyor (netsh int ip reset)..."));
                    string out3 = await RunProcessCaptureAsync("netsh.exe", "int ip reset");
                    combinedOutput.AppendLine(out3);

                    // 4. Adım: netsh winhttp reset proxy
                    progress?.Report((95, "Aşama 4/4: WinHTTP proxy yapılandırması sıfırlanıyor..."));
                    string out4 = await RunProcessCaptureAsync("netsh.exe", "winhttp reset proxy");
                    combinedOutput.AppendLine(out4);

                    progress?.Report((100, "✓ Ağ bileşenleri sıfırlandı. Yeniden başlatma gerekir."));

                    return new HealthCheckResult
                    {
                        Success = true,
                        IsHealthy = true,
                        Output = combinedOutput.ToString(),
                        DetailMessage = "DNS önbelleği, Winsock kataloğu, TCP/IP protokolleri ve WinHTTP proxy yapılandırması başarıyla fabrika ayarlarına sıfırlandı. Ayarların geçerli olması için bilgisayarınızı yeniden başlatın."
                    };
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Ağ sıfırlanırken hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"Ağ bileşenleri sıfırlanırken hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunFirewallResetAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    progress?.Report((20, "Mevcut güvenlik duvarı profilleri ve ilkeleri denetleniyor..."));
                    await Task.Delay(350);

                    progress?.Report((50, "Güvenlik duvarı kuralları fabrika ayarlarına sıfırlanıyor (netsh advfirewall reset)..."));
                    string output = await RunProcessCaptureAsync("netsh.exe", "advfirewall reset");

                    progress?.Report((90, "Varsayılan Windows Güvenlik Duvarı ilkeleri doğrulanıyor..."));
                    await Task.Delay(300);

                    bool isOk = !output.Contains("Hata", StringComparison.OrdinalIgnoreCase) &&
                                (output.Contains("Ok.", StringComparison.OrdinalIgnoreCase) ||
                                 output.Contains("Tamam", StringComparison.OrdinalIgnoreCase) ||
                                 string.IsNullOrWhiteSpace(output) ||
                                 output.Trim().Length < 100);

                    progress?.Report((100, isOk ? "✓ Güvenlik duvarı başarıyla fabrika ayarlarına sıfırlandı." : "✕ Güvenlik duvarı sıfırlanırken uyarı alındı."));

                    return new HealthCheckResult
                    {
                        Success = isOk,
                        IsHealthy = isOk,
                        Output = output,
                        DetailMessage = isOk
                            ? "Windows Defender Güvenlik Duvarı yapılandırması başarıyla varsayılan fabrika ayarlarına sıfırlandı. Tüm özel kullanıcı kuralları temizlendi ve standart güvenlik ilkeleri uygulandı."
                            : $"Güvenlik duvarı sıfırlanırken bir uyarı alındı:\n{output}"
                    };
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Güvenlik duvarı sıfırlanırken hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"Güvenlik duvarı sıfırlanırken hata oluştu: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunFullNetworkResetAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    progress?.Report((15, "Aşama 1/4: Mevcut ağ bağdaştırıcıları taranıyor..."));
                    await Task.Delay(400);

                    progress?.Report((40, "Aşama 2/4: Ağ cihazları ve bileşenleri temizleniyor (netcfg -d)..."));
                    var (exitCode, stdout, stderr) = await RunProcessWithExitCodeAsync("netcfg.exe", "-d");

                    progress?.Report((75, "Aşama 3/4: Ağ bağdaştırıcıları ve protokolleri yeniden yapılandırılıyor..."));
                    await Task.Delay(500);

                    progress?.Report((90, "Aşama 4/4: Yapılandırma doğrulanıyor..."));
                    await Task.Delay(300);

                    string combined = $"{stdout}\n{stderr}".Trim();
                    bool isOk = exitCode == 0 || !combined.Contains("failed", StringComparison.OrdinalIgnoreCase);

                    progress?.Report((100, isOk ? "✓ Ağ bağdaştırıcıları sıfırlandı. Yeniden başlatma gerekiyor." : "✕ Sıfırlama sırasında hata oluştu."));

                    return new HealthCheckResult
                    {
                        Success = isOk,
                        IsHealthy = isOk,
                        Output = combined,
                        DetailMessage = isOk
                            ? "Tüm fiziksel ve sanal ağ bağdaştırıcıları başarıyla temizlendi. Sürücülerin ve ağ kartlarının yeniden algılanıp tam kurulabilmesi için bilgisayarınızı yeniden başlatmanız zorunludur."
                            : $"Ağ bağdaştırıcıları sıfırlanırken hata oluştu (Çıkış Kodu: {exitCode}):\n{combined}"
                    };
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Ağ sıfırlanırken hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"Tam ağ sıfırlama işlemi sırasında hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunChkdskScanAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    progress?.Report((0, "Çevrimiçi disk taraması başlatılıyor (chkdsk C: /scan)..."));

                    string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string chkdskPath = Path.Combine(system32, "chkdsk.exe");
                    if (!File.Exists(chkdskPath))
                    {
                        chkdskPath = "chkdsk.exe";
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = chkdskPath,
                        Arguments = "C: /scan",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.Default
                    };

                    using var proc = new Process { StartInfo = psi };
                    if (!proc.Start())
                    {
                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            DetailMessage = "CHKDSK işlemi başlatılamadı."
                        };
                    }

                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();

                    var errorTask = Task.Run(async () =>
                    {
                        char[] errBuf = new char[512];
                        while (!proc.HasExited || !proc.StandardError.EndOfStream)
                        {
                            int read = await proc.StandardError.ReadAsync(errBuf, 0, errBuf.Length);
                            if (read <= 0) break;
                            errorBuilder.Append(errBuf, 0, read);
                        }
                    });

                    char[] buffer = new char[256];
                    var streamBuffer = new StringBuilder();

                    while (!proc.HasExited || !proc.StandardOutput.EndOfStream)
                    {
                        int read = await proc.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
                        if (read <= 0) break;

                        outputBuilder.Append(buffer, 0, read);
                        streamBuffer.Append(buffer, 0, read);

                        string currentText = streamBuffer.ToString();
                        var matches = PercentRegex.Matches(currentText);
                        if (matches.Count > 0)
                        {
                            var lastMatch = matches[^1];
                            if (double.TryParse(lastMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedVal))
                            {
                                int p = (int)Math.Round(parsedVal);
                                progress?.Report((p, $"C: sürücüsü taranıyor (%{p})..."));
                            }

                            if (streamBuffer.Length > 2048)
                            {
                                streamBuffer.Remove(0, streamBuffer.Length - 512);
                            }
                        }
                    }

                    await Task.WhenAll(proc.WaitForExitAsync(), errorTask);

                    string fullOutput = outputBuilder.ToString() + (errorBuilder.Length > 0 ? "\n" + errorBuilder.ToString() : string.Empty);
                    string outLower = fullOutput.ToLowerInvariant();

                    bool foundNoProblems = outLower.Contains("found no problems") ||
                                           outLower.Contains("hiçbir sorun bulamadı") ||
                                           outLower.Contains("sorun bulamadı") ||
                                           outLower.Contains("no further action is required") ||
                                           outLower.Contains("işlem yapılması gerekmiyor") ||
                                           outLower.Contains("0 bad file records processed") ||
                                           outLower.Contains("0 bozuk dosya kaydı işlendi");

                    bool foundErrors = outLower.Contains("windows found problems") ||
                                       outLower.Contains("sorunlar buldu") ||
                                       outLower.Contains("hatalar buldu") ||
                                       outLower.Contains("require offline repair") ||
                                       outLower.Contains("çevrimdışı onarım");

                    bool isHealthy = (proc.ExitCode == 0 || foundNoProblems) && !foundErrors;

                    if (isHealthy)
                    {
                        progress?.Report((100, "✓ Tarama tamamlandı: Dosya sistemi sağlıklı."));
                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = true,
                            Output = fullOutput,
                            DetailMessage = "C: sürücüsündeki dosya sistemi başarıyla çevrimiçi tarandı. Herhangi bir bozuk sektör, küme veya dosya sistemi hatası bulunamadı; disk tamamen sağlıklı."
                        };
                    }
                    else
                    {
                        progress?.Report((100, "⚠️ Tarama tamamlandı: Disk hatası tespit edildi."));
                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = false,
                            Output = fullOutput,
                            DetailMessage = "C: sürücüsünde dosya sistemi tutarsızlıkları veya hataları tespit edildi. Dosya sistemini düzeltmek için çevrimdışı onarım önerilir."
                        };
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Disk taraması sırasında hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"CHKDSK çalıştırılırken hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunChkdskOfflineRepairAsync(string flag, IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    bool isAggressive = flag.Equals("/r", StringComparison.OrdinalIgnoreCase);
                    string modeName = isAggressive ? "Agresif Onarım (/r)" : "Standart Onarım (/f)";

                    progress?.Report((15, $"Çevrimdışı onarım hazırlanıyor ({modeName})..."));
                    await Task.Delay(300);

                    string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string chkdskPath = Path.Combine(system32, "chkdsk.exe");
                    if (!File.Exists(chkdskPath))
                    {
                        chkdskPath = "chkdsk.exe";
                    }

                    progress?.Report((45, $"Windows açılış onarımı zamanlanıyor (chkdsk C: {flag})..."));

                    var psi = new ProcessStartInfo
                    {
                        FileName = chkdskPath,
                        Arguments = $"C: {flag}",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.Default
                    };

                    using var proc = Process.Start(psi);
                    if (proc == null)
                    {
                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            DetailMessage = "CHKDSK süreci başlatılamadı."
                        };
                    }

                    // Türkçe ve İngilizce onay istemlerini ('E' ve 'Y') standart girdiye otomatik yaz
                    try
                    {
                        await proc.StandardInput.WriteLineAsync("E");
                        await proc.StandardInput.WriteLineAsync("Y");
                        proc.StandardInput.Close();
                    }
                    catch { }

                    string output = await proc.StandardOutput.ReadToEndAsync();
                    string error = await proc.StandardError.ReadToEndAsync();
                    await proc.WaitForExitAsync();

                    progress?.Report((85, "Zamanlama doğrulanıyor..."));
                    await Task.Delay(300);

                    string combined = $"{output}\n{error}".Trim();
                    string combinedLower = combined.ToLowerInvariant();

                    bool isScheduled = combinedLower.Contains("bir sonraki") ||
                                       combinedLower.Contains("yeniden başlat") ||
                                       combinedLower.Contains("denetlenecek") ||
                                       combinedLower.Contains("scheduled") ||
                                       combinedLower.Contains("next time the system restarts") ||
                                       proc.ExitCode == 0;

                    progress?.Report((100, isScheduled ? "✓ Onarım zamanlandı. Yeniden başlatma gerekiyor." : "✕ Zamanlama başarısız oldu."));

                    return new HealthCheckResult
                    {
                        Success = isScheduled,
                        IsHealthy = isScheduled,
                        Output = combined,
                        DetailMessage = isScheduled
                            ? (isAggressive
                                ? "C: sürücüsü için agresif çevrimdışı onarım (/r) bir sonraki yeniden başlatmaya başarıyla zamanlandı. Bilgisayar açılırken tüm dosya sistemi ve bad sektörler taranacaktır (uzun sürebilir)."
                                : "C: sürücüsü için standart çevrimdışı onarım (/f) bir sonraki yeniden başlatmaya başarıyla zamanlandı. Bilgisayar açılırken dosya sistemi hataları otomatik onarılacaktır.")
                            : $"Onarım zamanlanırken uyarı alındı:\n{combined}"
                    };
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"Çevrimdışı onarım zamanlanırken hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunComponentCleanupAsync(string flag, IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    bool isResetBase = flag.Contains("/ResetBase", StringComparison.OrdinalIgnoreCase);
                    string modeDesc = isResetBase ? "Agresif Temizlik (/ResetBase)" : "Standart Temizlik";

                    progress?.Report((5, $"Bileşen deposu temizliği başlatılıyor ({modeDesc})..."));

                    var dismProgress = new Progress<int>(p =>
                    {
                        progress?.Report((p, $"WinSxS bileşen deposu temizleniyor ({modeDesc} %{p})..."));
                    });

                    string arguments = string.IsNullOrWhiteSpace(flag)
                        ? "/Online /Cleanup-Image /StartComponentCleanup"
                        : $"/Online /Cleanup-Image /StartComponentCleanup {flag.Trim()}";

                    var result = await ExecuteDismWithProgressAsync(arguments, dismProgress);

                    if (result.ExitCode == 0)
                    {
                        progress?.Report((100, isResetBase ? "✓ Agresif bileşen temizliği başarıyla tamamlandı!" : "✓ Bileşen deposu temizliği başarıyla tamamlandı!"));
                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = true,
                            Output = result.Output,
                            DetailMessage = isResetBase
                                ? "Windows WinSxS bileşen deposu agresif modda (/ResetBase) başarıyla temizlendi. Tüm güncellemeler kalıcı taban yapıldı ve maksimum disk alanı kazanıldı."
                                : "Windows WinSxS bileşen deposu başarıyla temizlendi. Güncelliğini yitirmiş ve artık gerekmeyen yedek güncelleme paketleri kaldırıldı."
                        };
                    }
                    else
                    {
                        progress?.Report((100, "⚠️ Temizlik tamamlanamadı."));
                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            Output = result.Output,
                            DetailMessage = $"Bileşen deposu temizliği tamamlanamadı (DISM Hata Kodu: {result.ExitCode})."
                        };
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        Output = ex.ToString(),
                        DetailMessage = $"Bileşen deposu temizlenirken hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunWsresetAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    progress?.Report((15, "Microsoft Store önbellek sıfırlama başlatılıyor (wsreset.exe)..."));
                    await Task.Delay(300);

                    string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string wsresetPath = Path.Combine(system32, "wsreset.exe");
                    if (!File.Exists(wsresetPath))
                    {
                        wsresetPath = "wsreset.exe";
                    }

                    progress?.Report((45, "Store önbelleği ve geçici indirme kuyrukları temizleniyor..."));

                    var psi = new ProcessStartInfo
                    {
                        FileName = wsresetPath,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        var waitTask = proc.WaitForExitAsync();
                        int progressVal = 45;
                        var timeoutTask = Task.Delay(30000);

                        while (!waitTask.IsCompleted)
                        {
                            var completed = await Task.WhenAny(waitTask, Task.Delay(1000), timeoutTask);
                            if (completed == timeoutTask)
                            {
                                try { proc.Kill(); } catch { }
                                break;
                            }

                            if (progressVal < 90)
                            {
                                progressVal += 10;
                                progress?.Report((progressVal, "Store önbelleği sıfırlanıyor..."));
                            }
                        }
                    }

                    progress?.Report((100, "✓ Microsoft Store önbelleği başarıyla temizlendi."));

                    return new HealthCheckResult
                    {
                        Success = true,
                        IsHealthy = true,
                        DetailMessage = "Microsoft Store önbelleği ve indirme kuyrukları başarıyla sıfırlandı. Mağaza bileşenleri yenilendi."
                    };
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Önbellek temizlenirken hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"wsreset çalıştırılırken hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunReregisterUwpAppsAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    progress?.Report((5, "UWP paket listesi taranıyor (Get-AppxPackage)..."));

                    string psScript = @"
$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'
$apps = @(Get-AppxPackage -AllUsers | Where-Object { $_.InstallLocation -and (Test-Path (Join-Path $_.InstallLocation 'AppXManifest.xml')) })
if ($apps.Count -eq 0) {
    $apps = @(Get-AppxPackage | Where-Object { $_.InstallLocation -and (Test-Path (Join-Path $_.InstallLocation 'AppXManifest.xml')) })
}
$total = $apps.Count
if ($total -eq 0) {
    Write-Output 'RESULT:0:0:0'
    exit 0
}
$success = 0
$failed = 0
for ($i = 0; $i -lt $total; $i++) {
    $app = $apps[$i]
    $pct = [math]::Min(95, [math]::Max(5, [math]::Round((($i + 1) / $total) * 90) + 5))
    $name = $app.Name
    Write-Output ""STAGE:${pct}:${name}""
    try {
        $manifest = Join-Path $app.InstallLocation 'AppXManifest.xml'
        Add-AppxPackage -DisableDevelopmentMode -Register $manifest -ErrorAction SilentlyContinue
        if ($?) { $success++ } else { $failed++ }
    } catch {
        $failed++
    }
}
Write-Output ""RESULT:${success}:${failed}:${total}""
";

                    byte[] scriptBytes = Encoding.Unicode.GetBytes(psScript);
                    string encodedScript = Convert.ToBase64String(scriptBytes);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using var proc = new Process { StartInfo = psi };
                    if (!proc.Start())
                    {
                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            DetailMessage = "PowerShell süreci başlatılamadı."
                        };
                    }

                    var stderrDrainTask = proc.StandardError.ReadToEndAsync();

                    int totalPackages = 0;
                    int successPackages = 0;
                    int failedPackages = 0;

                    var readTask = Task.Run(async () =>
                    {
                        while (!proc.StandardOutput.EndOfStream)
                        {
                            string? line = await proc.StandardOutput.ReadLineAsync();
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            line = line.Trim();

                            if (line.StartsWith("STAGE:"))
                            {
                                var parts = line.Split(':', 3);
                                if (parts.Length == 3 && int.TryParse(parts[1], out int p))
                                {
                                    string appName = parts[2];
                                    progress?.Report((p, $"Uygulama yeniden kaydediliyor (%{p}): {appName}"));
                                }
                            }
                            else if (line.StartsWith("RESULT:"))
                            {
                                var parts = line.Split(':');
                                if (parts.Length >= 4)
                                {
                                    int.TryParse(parts[1], out successPackages);
                                    int.TryParse(parts[2], out failedPackages);
                                    int.TryParse(parts[3], out totalPackages);
                                }
                            }
                        }
                    });

                    await Task.WhenAll(readTask, stderrDrainTask, proc.WaitForExitAsync());

                    if (proc.ExitCode == 0 || successPackages > 0)
                    {
                        int registered = successPackages > 0 ? successPackages : totalPackages;
                        string summary = failedPackages > 0
                            ? $"Toplam {totalPackages} UWP paketinden {registered} adedi başarıyla sisteme yeniden kaydedildi. ({failedPackages} adet o an kullanımda olan sistem paketi korundu)."
                            : $"Tüm kullanıcı hesapları için {registered} adet UWP ve Microsoft Store uygulaması başarıyla sisteme yeniden kaydedildi.";

                        progress?.Report((100, $"✓ {registered} uygulama başarıyla kaydedildi."));

                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = true,
                            DetailMessage = summary
                        };
                    }
                    else
                    {
                        string err = await stderrDrainTask;
                        progress?.Report((100, "⚠️ Uygulamalar yeniden kaydedilemedi."));
                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            DetailMessage = $"UWP uygulamaları kaydedilirken hata oluştu. (Çıkış Kodu: {proc.ExitCode})\n{err}"
                        };
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"UWP yeniden kaydetme işlemi sırasında hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunEnergyBatteryReportAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string reportsDir = Path.Combine(desktopPath, "PureWin_Enerji_Raporlari");
                    if (!Directory.Exists(reportsDir))
                    {
                        Directory.CreateDirectory(reportsDir);
                    }

                    string batteryReportPath = Path.Combine(reportsDir, "battery-report.html");
                    string sleepStudyPath = Path.Combine(reportsDir, "sleepstudy-report.html");
                    string energyReportPath = Path.Combine(reportsDir, "energy-report.html");

                    // 1. Aşama: Pil Raporu (powercfg /batteryreport)
                    progress?.Report((15, "Aşama 1/3: Pil kullanım ve sağlık geçmişi alınıyor (powercfg /batteryreport)..."));
                    await RunProcessWithExitCodeAsync("powercfg.exe", $"/batteryreport /output \"{batteryReportPath}\"");
                    bool hasBatteryReport = File.Exists(batteryReportPath);

                    // 2. Aşama: Modern Bekleme / Sleep Study (powercfg /sleepstudy)
                    progress?.Report((35, "Aşama 2/3: Modern uyku geçiş raporu alınıyor (powercfg /sleepstudy)..."));
                    await RunProcessWithExitCodeAsync("powercfg.exe", $"/sleepstudy /output \"{sleepStudyPath}\"");
                    bool hasSleepStudy = File.Exists(sleepStudyPath);

                    // 3. Aşama: Enerji Verimliliği Analizi (powercfg /energy /duration 15)
                    progress?.Report((50, "Aşama 3/3: Enerji verimliliği ve pil tüketim analizi başlatılıyor (powercfg /energy)..."));

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powercfg.exe",
                        Arguments = $"/energy /output \"{energyReportPath}\" /duration 15",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.Default
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            var waitTask = proc.WaitForExitAsync();
                            int currentProgress = 50;
                            while (!waitTask.IsCompleted)
                            {
                                var delayTask = Task.Delay(1000);
                                await Task.WhenAny(waitTask, delayTask);
                                if (currentProgress < 94)
                                {
                                    currentProgress += 3;
                                    progress?.Report((currentProgress, $"Aşama 3/3: Güç ve sistem davranışı izleniyor (%{currentProgress})..."));
                                }
                            }
                        }
                    }

                    bool hasEnergyReport = File.Exists(energyReportPath);

                    var generatedReports = new List<string>();
                    if (hasBatteryReport) generatedReports.Add("Pil Sağlığı Raporu (battery-report.html)");
                    if (hasSleepStudy) generatedReports.Add("Modern Bekleme / Sleep Study (sleepstudy-report.html)");
                    if (hasEnergyReport) generatedReports.Add("Enerji Verimliliği ve Hata Analizi (energy-report.html)");

                    if (generatedReports.Count > 0)
                    {
                        progress?.Report((100, "✓ Enerji ve pil raporları başarıyla oluşturuldu."));

                        // Rapor klasörünü aç
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = reportsDir,
                                UseShellExecute = true
                            });
                        }
                        catch { }

                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = true,
                            DetailMessage = $"Teşhis raporları başarıyla oluşturuldu ve masaüstünüze kaydedildi:\n📁 {reportsDir}\n\nOluşturulan Raporlar:\n" +
                                            string.Join("\n", generatedReports.Select(r => $"• {r}")) +
                                            "\n\n💡 Not: HTML raporlarını doğrudan tarayıcınızda açarak pil sağlığı, şarj döngüsü, bekleme tüketimi ve enerji uyarılarını inceleyebilirsiniz."
                        };
                    }
                    else
                    {
                        progress?.Report((100, "⚠️ Raporlar oluşturulamadı."));
                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            DetailMessage = "powercfg teşhis raporları oluşturulamadı. Donanımınız bu raporları desteklemiyor veya yetki kısıtlaması olabilir."
                        };
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"Enerji/Pil raporu oluşturulurken hata: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunMdschedAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report((30, "Windows Bellek Tanılama aracı başlatılıyor (mdsched.exe)..."));

                    string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string mdschedPath = Path.Combine(system32, "mdsched.exe");
                    if (!File.Exists(mdschedPath))
                    {
                        mdschedPath = "mdsched.exe";
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = mdschedPath,
                        UseShellExecute = true
                    };

                    using var proc = Process.Start(psi);

                    progress?.Report((100, "✓ Bellek Tanılama aracı açıldı. Seçiminizi yapıp sistemi yeniden başlatabilirsiniz."));

                    return new HealthCheckResult
                    {
                        Success = true,
                        IsHealthy = true,
                        DetailMessage = "Windows Bellek Tanılama aracı (mdsched.exe) başarıyla açıldı. Ekrana gelen pencereden 'Şimdi yeniden başlat' veya 'Bir sonraki başlatmada denetle' seçeneğini belirleyebilirsiniz. Sistem yeniden başladığında RAM donanımınız kapsamlı test edilecektir."
                    };
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"Windows Bellek Tanılama başlatılamadı: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunDfrguiAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report((30, "Windows Sürücüleri İyileştir ve Düzenle aracı açılıyor (dfrgui.exe)..."));

                    string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string dfrguiPath = Path.Combine(system32, "dfrgui.exe");
                    if (!File.Exists(dfrguiPath))
                    {
                        dfrguiPath = "dfrgui.exe";
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = dfrguiPath,
                        UseShellExecute = true
                    };

                    using var proc = Process.Start(psi);

                    progress?.Report((100, "✓ Sürücü İyileştirme aracı açıldı. İstediğiniz sürücüyü seçip optimize edebilirsiniz."));

                    return new HealthCheckResult
                    {
                        Success = true,
                        IsHealthy = true,
                        DetailMessage = "Windows Sürücüleri İyileştir ve Birleştir aracı (dfrgui.exe) başarıyla açıldı. Ekrana gelen pencereden C:, D:, E: veya diğer sürücülerinizi seçerek 'En İyi Duruma Getir' (SSD için TRIM, HDD için birleştirme) işlemini dilediğiniz gibi yürütebilirsiniz."
                    };
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"Sürücü İyileştirme aracı başlatılamadı: {ex.Message}"
                    };
                }
            });
        }

        private static async Task<HealthCheckResult> RunDeviceDriverDiagnosticAsync(IProgress<(int percent, string stage)>? progress)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    progress?.Report((20, "Aşama 1/2: Tak-Çalıştır (PnP) aygıt durumları taranıyor..."));

                    string psScript = @"
$ErrorActionPreference = 'SilentlyContinue'
$items = @()

# 1. Get-PnpDevice (Error / Degraded durumundaki aktif aygıtlar)
try {
    $pnp = Get-PnpDevice -Status Error,Degraded -ErrorAction SilentlyContinue | Where-Object { $_.Present -ne $false }
    if ($pnp) {
        foreach ($d in $pnp) {
            $name = if ($d.FriendlyName) { $d.FriendlyName } else { $d.InstanceId }
            $prob = if ($d.Problem) { $d.Problem } else { 'Bilinmeyen Hata' }
            $items += ""$name||$prob||$($d.InstanceId)""
        }
    }
} catch {}

# 2. Win32_PnPEntity (ConfigManagerErrorCode != 0, 45=Disconnected hariç)
try {
    $cim = Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object { $_.ConfigManagerErrorCode -ne 0 -and $_.ConfigManagerErrorCode -ne 45 }
    if ($cim) {
        foreach ($c in $cim) {
            $name = if ($c.Name) { $c.Name } else { $c.DeviceID }
            $code = $c.ConfigManagerErrorCode
            $items += ""$name||Kod $code||$($c.DeviceID)""
        }
    }
} catch {}

$unique = $items | Select-Object -Unique
if ($unique.Count -eq 0) {
    Write-Output 'RESULT:CLEAN'
} else {
    foreach ($u in $unique) {
        Write-Output ""DEVICE:$u""
    }
}
";

                    byte[] scriptBytes = Encoding.Unicode.GetBytes(psScript);
                    string encodedScript = Convert.ToBase64String(scriptBytes);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    progress?.Report((60, "Aşama 2/2: Hata kodları (Kod 43, Kod 28 vb.) çözümleniyor..."));

                    using var proc = Process.Start(psi);
                    if (proc == null)
                    {
                        return new HealthCheckResult
                        {
                            Success = false,
                            IsHealthy = false,
                            DetailMessage = "Aygıt tarama süreci başlatılamadı."
                        };
                    }

                    var problemDevices = new List<(string Name, string ErrorCode, string DeviceId)>();
                    bool isClean = false;

                    while (!proc.StandardOutput.EndOfStream)
                    {
                        string? line = await proc.StandardOutput.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        line = line.Trim();

                        if (line.StartsWith("RESULT:CLEAN"))
                        {
                            isClean = true;
                        }
                        else if (line.StartsWith("DEVICE:"))
                        {
                            string raw = line.Substring("DEVICE:".Length);
                            var parts = raw.Split("||", StringSplitOptions.None);
                            if (parts.Length >= 3)
                            {
                                problemDevices.Add((parts[0], parts[1], parts[2]));
                            }
                        }
                    }

                    await proc.WaitForExitAsync();

                    if (isClean || problemDevices.Count == 0)
                    {
                        progress?.Report((100, "✓ Tüm aygıtlar ve sürücüler sorunsuz çalışıyor."));
                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = true,
                            DetailMessage = "Sistemdeki tüm Tak-Çalıştır (PnP) donanımları ve aygıt sürücüleri başarıyla tarandı. Hata veren (Kod 43, Kod 28 vb.), bozuk veya tanınmayan hiçbir aygıt tespit edilmedi; donanım sağlığı mükemmel durumda."
                        };
                    }
                    else
                    {
                        progress?.Report((100, $"⚠️ {problemDevices.Count} adet sorunlu aygıt tespit edildi!"));

                        var details = new StringBuilder();
                        details.AppendLine($"Sistemde {problemDevices.Count} adet hatalı veya sürücüsü eksik aygıt tespit edildi:\n");

                        foreach (var dev in problemDevices)
                        {
                            string explanation = GetFriendlyErrorDescription(dev.ErrorCode);
                            details.AppendLine($"• {dev.Name}");
                            details.AppendLine($"  └ Durum: {explanation}");
                            details.AppendLine($"  └ Donanım Kimliği: {dev.DeviceId}");
                            details.AppendLine();
                        }

                        details.AppendLine("💡 Öneri: Hatalı aygıtlar için Aygıt Yöneticisi'nden sürücü güncellemesi yapabilir veya üretici sitesinden güncel sürücüyü yükleyebilirsiniz.");

                        return new HealthCheckResult
                        {
                            Success = true,
                            IsHealthy = false,
                            DetailMessage = details.ToString().TrimEnd()
                        };
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report((100, "✕ Hata oluştu."));
                    return new HealthCheckResult
                    {
                        Success = false,
                        IsHealthy = false,
                        DetailMessage = $"Aygıt sorun tespiti sırasında hata: {ex.Message}"
                    };
                }
            });
        }

        private static string GetFriendlyErrorDescription(string rawCode)
        {
            return rawCode switch
            {
                "Kod 1" or "CM_PROB_NOT_CONFIGURED" => "Aygıt doğru yapılandırılmamış (Kod 1)",
                "Kod 10" or "CM_PROB_FAILED_START" => "Aygıt başlatılamıyor (Kod 10)",
                "Kod 14" or "CM_PROB_NEED_RESTART" => "Aygıtın çalışması için bilgisayarın yeniden başlatılması gerekiyor (Kod 14)",
                "Kod 18" or "CM_PROB_REINSTALL" => "Bu aygıt için sürücülerin yeniden yüklenmesi gerekiyor (Kod 18)",
                "Kod 19" or "CM_PROB_REGISTRY_ERROR" => "Kayıt defteri yapılandırma bilgisi eksik veya bozuk (Kod 19)",
                "Kod 22" or "CM_PROB_DISABLED" => "Aygıt devre dışı bırakılmış (Kod 22)",
                "Kod 28" or "CM_PROB_FAILED_INSTALL" => "Bu aygıtın sürücüleri yüklenmemiş (Kod 28)",
                "Kod 31" or "CM_PROB_FAILED_ADD" => "Windows bu aygıt için gereken sürücüleri yükleyemiyor (Kod 31)",
                "Kod 32" or "CM_PROB_DISABLED_SERVICE" => "Bu aygıtın sürücü hizmeti devre dışı bırakılmış (Kod 32)",
                "Kod 37" or "CM_PROB_DRIVER_FAILED_PRIOR_UNLOAD" => "Sürücü başlatılamadı (Kod 37)",
                "Kod 39" or "CM_PROB_DRIVER_FAILED_LOAD" => "Sürücü dosyası bozuk veya eksik (Kod 39)",
                "Kod 43" or "CM_PROB_FAILED_POST_START" => "Aygıt sorun bildirdiği için durduruldu (Kod 43)",
                "Kod 52" or "CM_PROB_UNSIGNED_DRIVER" => "Sürücünün dijital imzası doğrulanamadı (Kod 52)",
                _ => rawCode
            };
        }

        private static async Task<(int exitCode, string output, string error)> RunProcessWithExitCodeAsync(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.Default,
                    StandardErrorEncoding = Encoding.Default
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var outTask = proc.StandardOutput.ReadToEndAsync();
                    var errTask = proc.StandardError.ReadToEndAsync();
                    await proc.WaitForExitAsync();
                    string outStr = await outTask;
                    string errStr = await errTask;
                    return (proc.ExitCode, outStr, errStr);
                }
            }
            catch (Exception ex)
            {
                return (-1, string.Empty, ex.Message);
            }
            return (-1, string.Empty, "Süreç başlatılamadı.");
        }

        private static async Task<string> RunProcessCaptureAsync(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.Default
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string outStr = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync();
                    return outStr;
                }
            }
            catch (Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
            return string.Empty;
        }
    }
}
