using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using PureWin.Helpers;
using PureWin.Models;

namespace PureWin.Services
{
    public static class CleanupService
    {
        public static List<CleanupGroupItem> GetDefaultGroups()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            var systemTrashGroup = new CleanupGroupItem
            {
                Id = "SystemTrash",
                Title = "Sistem Çöpü",
                Icon = "🗑️",
                IsExpanded = false // Kullanıcı kuralı 1: Varsayılan olarak kapalı!
            };

            var items = new List<CleanupSubItem>
            {
                new CleanupSubItem
                {
                    Id = "UserTemp",
                    Title = "Geçici dosyalar (%TEMP%, C:\\Windows\\Temp)",
                    Description = "Uygulama ve sistem geçici çalışma dosyaları.",
                    ParentGroup = systemTrashGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "System_RecycleBin",
                    Title = "Geri Dönüşüm Kutusu",
                    Description = "Tüm sabit ve harici sürücülerdeki silinmiş ve çöp kutusunda bekleyen dosyalar ($Recycle.Bin).",
                    ParentGroup = systemTrashGroup,
                    IsRisky = false,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "MemoryDumps",
                    Title = "Bellek dökümü dosyaları (.dmp)",
                    Description = "Mavi ekran ve çekirdek çökme kayıtları (MEMORY.DMP, Minidump).",
                    ParentGroup = systemTrashGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "WERReports",
                    Title = "Windows hata raporlama dosyaları (WER)",
                    Description = "Microsoft Hata Raporlama kuyruk ve arşiv dosyaları.",
                    ParentGroup = systemTrashGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "AppCrashReports",
                    Title = "Uygulama çöküş raporları",
                    Description = "Masaüstü uygulamalarının çökme dökümleri ve hata teşhis kayıtları.",
                    ParentGroup = systemTrashGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "SetupLogs",
                    Title = "Setup log dosyaları",
                    Description = "Tamamlanmış Windows ve sürücü kurulumlarının metin ve xml günlükleri (Panther, MoSetup).",
                    ParentGroup = systemTrashGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "UpdateLogs",
                    Title = "Windows Update log dosyaları",
                    Description = "Bileşen hizmeti (CBS), DISM ve güncelleme işlem günlükleri.",
                    ParentGroup = systemTrashGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "TaskSchedulerLogs",
                    Title = "Görev Zamanlayıcı geçmiş kayıtları",
                    Description = "Zamanlanmış görev operasyonel geçmiş logları ve eski görev artıkları.",
                    ParentGroup = systemTrashGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "PowerShellCache",
                    Title = "PowerShell geçici modül dosyaları",
                    Description = "PowerShell script ve modül analiz önbellekleri.",
                    ParentGroup = systemTrashGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "ComDcomTemp",
                    Title = "COM/DCOM kayıt geçici dosyaları",
                    Description = "LocalService ve NetworkService COM+ geçici dosyaları ile CRM işlem günlükleri.",
                    ParentGroup = systemTrashGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Clean_ChkdskArtifacts",
                    Title = "Chkdsk disk kurtarma artıkları (FOUND.000)",
                    Description = "Disk denetleme (chkdsk) sonrasında sürücü kök dizininde bırakılan hasarlı dosya parçaları (FOUND.000, *.CHK).",
                    ParentGroup = systemTrashGroup,
                    IsRisky = false,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Clean_DownloadedProgramFiles",
                    Title = "İndirilen program geçici dosyaları (Downloaded Program Files)",
                    Description = "C:\\Windows\\Downloaded Program Files altındaki eski web ve uygulama indirme artıkları.",
                    ParentGroup = systemTrashGroup,
                    IsRisky = false,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Clean_OldRestorePoints",
                    Title = "Eski sistem geri yükleme noktaları (En sonuncusu hariç)",
                    Description = "DİKKAT: En son çalışan geri yükleme noktası sistem güvenliği için korunur; önceki aylardan kalan tüm eski gölge kopyalar (VSS) silinerek 10-30 GB alan açılır.",
                    ParentGroup = systemTrashGroup,
                    IsRisky = true,
                    IsSelected = false // Kullanıcı kuralı: Riskli kalemler varsayılan kapalı!
                },
                new CleanupSubItem
                {
                    Id = "System_RegistryBackups",
                    Title = "Kayıt Defteri Yedekleri (Eski .reg ve Sistem Yedekleri)",
                    Description = "Sistemdeki tüm eski kayıt defteri yedeklerini temizler; güvenlik için yalnızca en son alınan tek bir güncel yedeği korur.",
                    ParentGroup = systemTrashGroup,
                    IsRisky = false,
                    IsSelected = true
                }
            };

            foreach (var item in items)
            {
                systemTrashGroup.Items.Add(item);
            }

            systemTrashGroup.OnSubItemSelectionChanged();

            // 2. GRUP: ÖNBELLEK TEMİZLİĞİ
            var cacheCleanGroup = new CleanupGroupItem
            {
                Id = "CacheClean",
                Title = "Önbellek Temizliği",
                Icon = "⚡",
                IsExpanded = false
            };

            var cacheItems = new List<CleanupSubItem>
            {
                new CleanupSubItem
                {
                    Id = "ThumbCache",
                    Title = "Küçük resim önbelleği (thumbnail cache)",
                    Description = "Klasör ve dosya gezgini küçük resim (thumbnail) veri tabanları.",
                    ParentGroup = cacheCleanGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "IconCache",
                    Title = "Sistem simge önbelleği (IconCache.db)",
                    Description = "Uygulama, kısayol ve dosya simgelerinin Windows simge önbelleği.",
                    ParentGroup = cacheCleanGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "DirectXShaderCache",
                    Title = "DirectX shader önbelleği",
                    Description = "Grafik kartı ve DirectX gölgelendirici önbelleği (D3DSCache, NVIDIA, AMD).",
                    ParentGroup = cacheCleanGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "FontCache",
                    Title = "Yazı tipi önbelleği (font cache)",
                    Description = "Windows yazı tipi önbellek dosyaları (FontCache ve FNTCACHE.DAT).",
                    ParentGroup = cacheCleanGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "DotNetCache",
                    Title = "Kullanılmayan .NET framework önbelleği",
                    Description = "Geçici .NET derleme ve uygulama çalışma önbellekleri (%TEMP%\\.net, CLR).",
                    ParentGroup = cacheCleanGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "MediaPlayerCache",
                    Title = "Windows Media Player önbelleği",
                    Description = "Medya yürütücüsü albüm kapakları ve yerel medya önbelleği.",
                    ParentGroup = cacheCleanGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "SearchIndexTemp",
                    Title = "Arama indeksi geçici dosyaları",
                    Description = "Windows Arama dizinleyicisinin geçici çalışma verileri.",
                    ParentGroup = cacheCleanGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "PrefetchCache",
                    Title = "Windows Prefetch önbelleği",
                    Description = "Uygulama ön yükleme izleri ve kaldırılmış programların .pf kayıtları (C:\\Windows\\Prefetch).",
                    ParentGroup = cacheCleanGroup,
                    IsRisky = false,
                    IsSelected = false // Kullanıcı isteğe bağlı temizlesin
                },
                new CleanupSubItem
                {
                    Id = "Cache_INetCache",
                    Title = "Geçici internet ve web önbelleği (INetCache & WebCache)",
                    Description = "Windows bileşenleri, Microsoft Store ve WebView uygulamalarının geçici web indirmeleri ve veritabanı artıkları.",
                    ParentGroup = cacheCleanGroup,
                    IsRisky = false,
                    IsSelected = true
                }
            };

            foreach (var item in cacheItems)
            {
                cacheCleanGroup.Items.Add(item);
            }
            cacheCleanGroup.OnSubItemSelectionChanged();

            // 3. GRUP: TARAYICI TEMİZLİĞİ
            var browserCleanGroup = new CleanupGroupItem
            {
                Id = "BrowserClean",
                Title = "Tarayıcı Temizliği",
                Icon = "🌐",
                IsExpanded = false
            };

            if (IsBrowserInstalled("Browser_Chrome"))
            {
                var chromeExe = AppIconHelper.FindExecutable(
                    @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                    @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe");
                var chromeItem = new CleanupSubItem
                {
                    Id = "Browser_Chrome",
                    Title = "Google Chrome",
                    Description = "Önbellek (Cache, GPU, Shader, Code Cache, CRX Cache), Crash raporları. (Yer imleri, açık sekmeler ve geçmiş korunur).",
                    ParentGroup = browserCleanGroup,
                    IsSelected = true
                };
                if (chromeExe != null) chromeItem.IconImage = AppIconHelper.GetIcon(chromeExe);
                browserCleanGroup.Items.Add(chromeItem);
            }

            if (IsBrowserInstalled("Browser_Edge"))
            {
                var edgeExe = AppIconHelper.FindExecutable(
                    @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                    @"C:\Program Files\Microsoft\Edge\Application\msedge.exe");
                var edgeItem = new CleanupSubItem
                {
                    Id = "Browser_Edge",
                    Title = "Microsoft Edge",
                    Description = "Önbellek (Cache, GPU, Shader, Code Cache, CRX Cache), Crash raporları. (Yer imleri, açık sekmeler ve geçmiş korunur).",
                    ParentGroup = browserCleanGroup,
                    IsSelected = true
                };
                if (edgeExe != null) edgeItem.IconImage = AppIconHelper.GetIcon(edgeExe);
                browserCleanGroup.Items.Add(edgeItem);
            }

            if (IsBrowserInstalled("Browser_Brave"))
            {
                var braveExe = AppIconHelper.FindExecutable(
                    @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
                    @"C:\Program Files (x86)\BraveSoftware\Brave-Browser\Application\brave.exe");
                var braveItem = new CleanupSubItem
                {
                    Id = "Browser_Brave",
                    Title = "Brave",
                    Description = "Önbellek (Cache, GPU, Shader, Code Cache, CRX Cache), Crash raporları. (Yer imleri, açık sekmeler ve geçmiş korunur).",
                    ParentGroup = browserCleanGroup,
                    IsSelected = true
                };
                if (braveExe != null) braveItem.IconImage = AppIconHelper.GetIcon(braveExe);
                browserCleanGroup.Items.Add(braveItem);
            }

            if (IsBrowserInstalled("Browser_OperaGX"))
            {
                var operaExe = AppIconHelper.FindExecutable(
                    Path.Combine(localAppData, @"Programs\Opera GX\opera.exe"),
                    Path.Combine(localAppData, @"Programs\Opera GX\launcher.exe"),
                    Path.Combine(localAppData, @"Programs\Opera\opera.exe"),
                    Path.Combine(localAppData, @"Programs\Opera\launcher.exe"),
                    Path.Combine(appData, @"Programs\Opera GX\opera.exe"),
                    Path.Combine(appData, @"Opera Software\Opera GX Stable\opera.exe"),
                    Path.Combine(appData, @"Opera Software\Opera GX Stable\launcher.exe"),
                    Path.Combine(appData, @"Opera Software\Opera Stable\opera.exe"),
                    Path.Combine(appData, @"Opera Software\Opera Stable\launcher.exe"),
                    @"C:\Program Files\Opera GX\opera.exe",
                    @"C:\Program Files\Opera GX\launcher.exe",
                    @"C:\Program Files\Opera\opera.exe",
                    @"C:\Program Files\Opera\launcher.exe",
                    @"C:\Program Files (x86)\Opera GX\opera.exe",
                    @"C:\Program Files (x86)\Opera\opera.exe");
                var operaItem = new CleanupSubItem
                {
                    Id = "Browser_OperaGX",
                    Title = "Opera GX / Opera",
                    Description = "Önbellek (Cache, GPU, Shader, Code Cache, CRX Cache), Crash raporları. (Yer imleri, açık sekmeler ve geçmiş korunur).",
                    ParentGroup = browserCleanGroup,
                    IsSelected = true
                };
                if (operaExe != null) operaItem.IconImage = AppIconHelper.GetIcon(operaExe);
                browserCleanGroup.Items.Add(operaItem);
            }

            if (IsBrowserInstalled("Browser_Firefox"))
            {
                var ffExe = AppIconHelper.FindExecutable(
                    @"C:\Program Files\Mozilla Firefox\firefox.exe",
                    @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe");
                var ffItem = new CleanupSubItem
                {
                    Id = "Browser_Firefox",
                    Title = "Mozilla Firefox",
                    Description = "Önbellek (cache2, startupCache, shader-cache, jumpListCache, thumbnails), Crash raporları. (Yer imleri ve açık sekmeler korunur).",
                    ParentGroup = browserCleanGroup,
                    IsSelected = true
                };
                if (ffExe != null) ffItem.IconImage = AppIconHelper.GetIcon(ffExe);
                browserCleanGroup.Items.Add(ffItem);
            }

            if (browserCleanGroup.Items.Count > 0)
            {
                var historyItem = new CleanupSubItem
                {
                    Id = "Browser_History",
                    Title = "Tarayıcı Gezinti ve İndirme Geçmişi",
                    Description = "Ziyaret edilen web sayfaları listesi, adres çubuğu arama geçmişi ve indirme kayıtları temizlenir. (Yer imleri, açık sekmeler ve şifreler korunur).",
                    ParentGroup = browserCleanGroup,
                    IsRisky = false,
                    IsSelected = false // Varsayılan kapalı! Kullanıcı bilinçli olarak seçmeli
                };
                browserCleanGroup.Items.Add(historyItem);

                var cookiesItem = new CleanupSubItem
                {
                    Id = "Browser_CookiesAndPasswords",
                    Title = "Tarayıcı Çerezler ve Kayıtlı Şifreler Temizliği",
                    Description = "DİKKAT: Web sitelerindeki açık oturumlar kapanır. Çerezler, kayıtlı şifreler, form geçmişi, site izinleri ve ödeme verileri kalıcı olarak silinir.",
                    ParentGroup = browserCleanGroup,
                    IsRisky = true,
                    IsSelected = false // Kullanıcı kuralı: Varsayılan kapalı!
                };
                browserCleanGroup.Items.Add(cookiesItem);
            }
            browserCleanGroup.OnSubItemSelectionChanged();

            // 4. Grup: Windows Güncelleme Artıkları
            var updateCleanGroup = new CleanupGroupItem
            {
                Id = "Group_WindowsUpdate",
                Title = "Windows Güncelleme Artıkları",
                Icon = "🔄",
                IsExpanded = false
            };

            var updateItems = new List<CleanupSubItem>
            {
                new CleanupSubItem
                {
                    Id = "Update_SoftwareDistribution",
                    Title = "Windows güncelleme önbelleği (SoftwareDistribution\\Download)",
                    Description = "İndirilip kurulmuş Windows güncelleme yükleme paketleri.",
                    ParentGroup = updateCleanGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Update_DeliveryOptimization",
                    Title = "Teslimat optimizasyonu dosyaları",
                    Description = "Yerel ağ veya internet üzerinden paylaşılan teslimat optimizasyonu güncelleme önbelleği.",
                    ParentGroup = updateCleanGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Update_WindowsOld",
                    Title = "Eski Windows kurulum dosyaları (Windows.old)",
                    Description = "DİKKAT: Önceki Windows sürümüne geri dönme (Rollback) imkanı kalıcı olarak iptal olur. Windows.old, $WINDOWS.~BT ve $WINDOWS.~WS arşivleri kalıcı olarak silinir.",
                    ParentGroup = updateCleanGroup,
                    IsRisky = true,
                    IsSelected = false // Kullanıcı kuralı: Riskli kalemler varsayılan kapalı!
                },
                new CleanupSubItem
                {
                    Id = "Update_WinSxS",
                    Title = "Sistem güncelleme yedek dosyaları (WinSxS)",
                    Description = "DİKKAT: Daha önce kurulmuş olan güncellemeleri 'Güncellemeleri Kaldır' diyerek geri alma imkanı kapanabilir. WinSxS geçici silme kuyruğu ve bileşen yedekleri temizlenir.",
                    ParentGroup = updateCleanGroup,
                    IsRisky = true,
                    IsSelected = false // Kullanıcı kuralı: Riskli kalemler varsayılan kapalı!
                },
                new CleanupSubItem
                {
                    Id = "Update_InstallerCache",
                    Title = "Windows Installer önbelleği (.msi artıkları)",
                    Description = "Windows Installer geçici dosyaları, $PatchCache$ önbelleği ve tamamlanmış kurulum artıkları.",
                    ParentGroup = updateCleanGroup,
                    IsSelected = true
                }
            };

            foreach (var item in updateItems)
            {
                updateCleanGroup.Items.Add(item);
            }
            updateCleanGroup.OnSubItemSelectionChanged();

            // 5. Grup: Sistem Geçmişi & Loglar
            var systemHistoryGroup = new CleanupGroupItem
            {
                Id = "Group_SystemHistory",
                Title = "Sistem Geçmişi & Loglar",
                Icon = "📜",
                IsExpanded = false
            };

            var historyItems = new List<CleanupSubItem>
            {
                new CleanupSubItem
                {
                    Id = "Log_SystemLogs",
                    Title = "Sistem günlükleri (log dosyaları)",
                    Description = "Windows sistem, kurulum ve servis operasyonel log dosyaları (Logs, LogFiles, CBS).",
                    ParentGroup = systemHistoryGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Log_DnsCache",
                    Title = "DNS önbelleği",
                    Description = "Ziyaret edilen web sitelerinin IP adresi çözümleme kayıtları (DNS istemci önbelleği).",
                    ParentGroup = systemHistoryGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Log_ClipboardHistory",
                    Title = "Clipboard (pano) geçmişi",
                    Description = "Windows panosuna kopyalanan metin, görsel ve veri geçmişi kayıtları.",
                    ParentGroup = systemHistoryGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Log_RdpHistory",
                    Title = "Uzak Masaüstü (RDP) bağlantı geçmişi",
                    Description = "Uzak Masaüstü istemcisinde saklanan sunucu IP adresleri, bağlantı geçmişi ve ekran bitmap önbelleği.",
                    ParentGroup = systemHistoryGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Log_NetCache",
                    Title = "Ağ paylaşım geçici dosyaları (NetCache)",
                    Description = "Yerel ağ paylaşımları ve çevrimdışı dosyalar için tutulan istemci tarafı geçici önbellek (CSC).",
                    ParentGroup = systemHistoryGroup,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Log_EventLogs",
                    Title = "Windows Olay Günlükleri (Event Viewer)",
                    Description = "Windows sistem, uygulama, güvenlik ve servis olay izleme günlükleri (Application, System, Setup vb. winevt\\.evtx kayıtları).",
                    ParentGroup = systemHistoryGroup,
                    IsSelected = true
                }
            };

            foreach (var item in historyItems)
            {
                systemHistoryGroup.Items.Add(item);
            }
            systemHistoryGroup.OnSubItemSelectionChanged();

            // 6. Grup: Donanım & Sürücü Artıkları
            var hardwareCleanGroup = new CleanupGroupItem
            {
                Id = "Group_HardwareDrivers",
                Title = "Donanım & Sürücü Artıkları",
                Icon = "🔌",
                IsExpanded = false
            };

            var hardwareItems = new List<CleanupSubItem>
            {
                new CleanupSubItem
                {
                    Id = "Driver_UnusedPackages",
                    Title = "Kullanılmayan aygıt sürücü paketleri (DriverStore)",
                    Description = "DİKKAT: Eski ekran kartı, yonga seti veya ses sürücülerine geri dönme (Rollback) imkanı kalkar. DriverStore FileRepository'deki eski oem*.inf sürücü yedekleri ve geçici kurulum havuzu temizlenir.",
                    ParentGroup = hardwareCleanGroup,
                    IsRisky = true,
                    IsSelected = false // Kullanıcı kuralı: Riskli kalemler varsayılan kapalı!
                },
                new CleanupSubItem
                {
                    Id = "Driver_InfSetupLogs",
                    Title = "Eski program ve donanım kurulum logları (INF)",
                    Description = "C:\\Windows\\INF altındaki eski donanım yükleme günlükleri (setupapi.dev.log, setupapi.setup.log) ve kurulum raporları.",
                    ParentGroup = hardwareCleanGroup,
                    IsRisky = false,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Driver_PrintSpooler",
                    Title = "Yazıcı kuyruğu (Print Spooler)",
                    Description = "Yazdırma hizmetinde takılı kalan veya tamamlanmış yazdırma işleri (C:\\Windows\\System32\\spool\\PRINTERS).",
                    ParentGroup = hardwareCleanGroup,
                    IsRisky = false,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Driver_DefenderCache",
                    Title = "Microsoft Defender tanılama ve tarama önbelleği",
                    Description = "Defender geçici destek günlükleri, hata dökümleri ve analiz önbelleği (Etkin virüs imzaları kesinlikle korunur).",
                    ParentGroup = hardwareCleanGroup,
                    IsRisky = false,
                    IsSelected = true
                },
                new CleanupSubItem
                {
                    Id = "Driver_GpuSetupCache",
                    Title = "Ekran kartı & yonga seti kurulum klasörleri",
                    Description = "NVIDIA, AMD ve Intel sürücü kurulumu sonrası diskte bırakılan geçici paket açma klasörleri (C:\\NVIDIA, C:\\AMD, C:\\Intel).",
                    ParentGroup = hardwareCleanGroup,
                    IsRisky = false,
                    IsSelected = true
                }
            };

            foreach (var item in hardwareItems)
            {
                hardwareCleanGroup.Items.Add(item);
            }
            hardwareCleanGroup.OnSubItemSelectionChanged();

            // 7. GRUP: DİĞER UYGULAMA ÖĞELERİ (Dinamik Liste - Sadece Bilgisayarda Yüklü Olanlar)
            var otherAppsGroup = new CleanupGroupItem
            {
                Id = "Group_OtherApps",
                Title = "Diğer Uygulama Öğeleri",
                Icon = "🧩",
                IsExpanded = false
            };

            // 1. Node.js
            string? nodeExe = AppIconHelper.FindExecutable(@"C:\Program Files\nodejs\node.exe", @"C:\Program Files (x86)\nodejs\node.exe")
                              ?? AppIconHelper.FindOnPath("node.exe");
            bool hasNode = nodeExe != null || Directory.Exists(Path.Combine(localAppData, "npm-cache")) || Directory.Exists(Path.Combine(appData, "npm-cache")) || Directory.Exists(Path.Combine(userProfile, ".npm"));
            if (hasNode)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_NodeJs",
                    Title = "Node.js (npm önbelleği)",
                    Description = "npm paket yöneticisinin geçici indirme ve tarball paket önbelleği (%LocalAppData%\\npm-cache).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (nodeExe != null) item.IconImage = AppIconHelper.GetIcon(nodeExe);
                otherAppsGroup.Items.Add(item);
            }

            // 2. NuGet (.NET paket önbelleği)
            string? dotnetExe = AppIconHelper.FindExecutable(@"C:\Program Files\dotnet\dotnet.exe", @"C:\Program Files (x86)\dotnet\dotnet.exe")
                                ?? AppIconHelper.FindOnPath("dotnet.exe");
            bool hasNuGet = dotnetExe != null || Directory.Exists(Path.Combine(localAppData, @"NuGet\v3-cache")) || Directory.Exists(Path.Combine(userProfile, @".nuget\packages"));
            if (hasNuGet)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_NuGet",
                    Title = "NuGet (.NET paket önbelleği)",
                    Description = "Visual Studio ve .NET paket indirme ve HTTP yanıt önbellekleri (%LocalAppData%\\NuGet\\v3-cache).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (dotnetExe != null) item.IconImage = AppIconHelper.GetIcon(dotnetExe);
                otherAppsGroup.Items.Add(item);
            }

            // 3. Visual Studio Code
            string? vsCodeExe = AppIconHelper.FindExecutable(
                Path.Combine(localAppData, @"Programs\Microsoft VS Code\Code.exe"),
                @"C:\Program Files\Microsoft VS Code\Code.exe",
                @"C:\Program Files (x86)\Microsoft VS Code\Code.exe")
                ?? AppIconHelper.FindOnPath("Code.exe");
            bool hasVSCode = vsCodeExe != null || Directory.Exists(Path.Combine(appData, "Code"));
            if (hasVSCode)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_VSCode",
                    Title = "Visual Studio Code",
                    Description = "VS Code arayüz, GPU, eklenti (VSIX) ve oturum çalışma önbellekleri (%AppData%\\Code).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (vsCodeExe != null) item.IconImage = AppIconHelper.GetIcon(vsCodeExe);
                otherAppsGroup.Items.Add(item);
            }

            // 4. Discord
            string? discordExe = AppIconHelper.FindExecutable(
                Path.Combine(localAppData, @"Discord\Update.exe"))
                ?? AppIconHelper.FindOnPath("Discord.exe");
            bool hasDiscord = discordExe != null || Directory.Exists(Path.Combine(appData, "discord")) || Directory.Exists(Path.Combine(localAppData, "Discord"));
            if (hasDiscord)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_Discord",
                    Title = "Discord",
                    Description = "Sohbet görsel, medya ve GPU donanım önbellekleri (%AppData%\\discord\\Cache).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (discordExe != null) item.IconImage = AppIconHelper.GetIcon(discordExe);
                otherAppsGroup.Items.Add(item);
            }

            // 5. Spotify
            string? spotifyExe = AppIconHelper.FindExecutable(
                Path.Combine(appData, @"Spotify\Spotify.exe"),
                Path.Combine(localAppData, @"Microsoft\WindowsApps\Spotify.exe"));
            bool hasSpotify = spotifyExe != null || Directory.Exists(Path.Combine(localAppData, "Spotify")) || Directory.Exists(Path.Combine(appData, "Spotify"));
            if (hasSpotify)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_Spotify",
                    Title = "Spotify",
                    Description = "Çevrimdışı ve akış şarkılarının geçici yerel veri önbelleği (%LocalAppData%\\Spotify).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (spotifyExe != null) item.IconImage = AppIconHelper.GetIcon(spotifyExe);
                otherAppsGroup.Items.Add(item);
            }

            // 6. Steam
            string? steamExe = AppIconHelper.FindExecutable(
                @"C:\Program Files (x86)\Steam\steam.exe",
                @"C:\Program Files\Steam\steam.exe");
            bool hasSteam = steamExe != null || Directory.Exists(@"C:\Program Files (x86)\Steam") || Directory.Exists(Path.Combine(localAppData, "Steam"));
            if (hasSteam)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_Steam",
                    Title = "Steam",
                    Description = "Steam istemcisi web, HTML ve arayüz indirme önbelleği (htmlcache, appcache).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (steamExe != null) item.IconImage = AppIconHelper.GetIcon(steamExe);
                otherAppsGroup.Items.Add(item);
            }

            // 7. Telegram Desktop
            string? telegramExe = AppIconHelper.FindExecutable(
                Path.Combine(appData, @"Telegram Desktop\Telegram.exe"),
                Path.Combine(localAppData, @"Programs\Telegram Desktop\Telegram.exe"),
                @"C:\Program Files\Telegram Desktop\Telegram.exe");
            bool hasTelegram = telegramExe != null || Directory.Exists(Path.Combine(appData, @"Telegram Desktop"));
            if (hasTelegram)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_Telegram",
                    Title = "Telegram Desktop",
                    Description = "İndirilen geçici medya, çıkartma ve ses önbellekleri (%AppData%\\Telegram Desktop\\tdata\\temp).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (telegramExe != null) item.IconImage = AppIconHelper.GetIcon(telegramExe);
                otherAppsGroup.Items.Add(item);
            }

            // 8. VLC Media Player
            string? vlcExe = AppIconHelper.FindExecutable(
                @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe");
            bool hasVlc = vlcExe != null || Directory.Exists(Path.Combine(appData, "vlc"));
            if (hasVlc)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_Vlc",
                    Title = "VLC Media Player",
                    Description = "VLC albüm kapakları, önizleme ve geçici medya önbellekleri (%AppData%\\vlc\\art).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (vlcExe != null) item.IconImage = AppIconHelper.GetIcon(vlcExe);
                otherAppsGroup.Items.Add(item);
            }

            // 9. Epic Games Launcher
            string? epicExe = AppIconHelper.FindExecutable(
                @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
                @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
                @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe");
            bool hasEpic = epicExe != null || Directory.Exists(Path.Combine(localAppData, "EpicGamesLauncher")) || Directory.Exists(@"C:\Program Files (x86)\Epic Games");
            if (hasEpic)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_EpicGames",
                    Title = "Epic Games Launcher",
                    Description = "Epic Games istemcisi Chromium web arayüzü, mağaza önbelleği ve log dosyaları (%LocalAppData%\\EpicGamesLauncher\\Saved\\webcache).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (epicExe != null) item.IconImage = AppIconHelper.GetIcon(epicExe);
                otherAppsGroup.Items.Add(item);
            }

            // 10. EA App (Electronic Arts)
            string? eaExe = AppIconHelper.FindExecutable(
                @"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe",
                @"C:\Program Files (x86)\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe");
            bool hasEa = eaExe != null || Directory.Exists(Path.Combine(localAppData, @"Electronic Arts\EA Desktop")) || Directory.Exists(@"C:\Program Files\Electronic Arts\EA Desktop");
            if (hasEa)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_EaApp",
                    Title = "EA App",
                    Description = "EA Desktop istemcisi Chromium arayüzü, oyun içi arayüz (IGO) ve log önbelleği (%LocalAppData%\\Electronic Arts\\EA Desktop).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (eaExe != null) item.IconImage = AppIconHelper.GetIcon(eaExe);
                otherAppsGroup.Items.Add(item);
            }

            // 11. Ubisoft Connect
            string? ubiExe = AppIconHelper.FindExecutable(
                @"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe",
                @"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\upc.exe",
                @"C:\Program Files\Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe");
            bool hasUbi = ubiExe != null || Directory.Exists(Path.Combine(localAppData, "Ubisoft Game Launcher")) || Directory.Exists(@"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher");
            if (hasUbi)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_Ubisoft",
                    Title = "Ubisoft Connect",
                    Description = "Ubisoft Connect istemcisi web arayüz önbelleği, geçici indirmeler ve günlükler (%LocalAppData%\\Ubisoft Game Launcher).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (ubiExe != null) item.IconImage = AppIconHelper.GetIcon(ubiExe);
                otherAppsGroup.Items.Add(item);
            }

            // 12. Battle.net
            string? bnetExe = AppIconHelper.FindExecutable(
                @"C:\Program Files (x86)\Battle.net\Battle.net.exe",
                @"C:\Program Files\Battle.net\Battle.net.exe");
            bool hasBnet = bnetExe != null || Directory.Exists(Path.Combine(localAppData, "Battle.net")) || Directory.Exists(Path.Combine(progData, "Battle.net"));
            if (hasBnet)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_BattleNet",
                    Title = "Battle.net",
                    Description = "Blizzard Battle.net istemcisi web arayüzü, HTML önbelleği ve log kayıtları (%LocalAppData%\\Battle.net).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (bnetExe != null) item.IconImage = AppIconHelper.GetIcon(bnetExe);
                otherAppsGroup.Items.Add(item);
            }

            // 13. GOG Galaxy
            string? gogExe = AppIconHelper.FindExecutable(
                @"C:\Program Files (x86)\GOG Galaxy\GalaxyClient.exe",
                @"C:\Program Files\GOG Galaxy\GalaxyClient.exe");
            bool hasGog = gogExe != null || Directory.Exists(Path.Combine(progData, @"GOG.com\Galaxy")) || Directory.Exists(Path.Combine(localAppData, @"GOG.com\Galaxy"));
            if (hasGog)
            {
                var item = new CleanupSubItem
                {
                    Id = "App_GogGalaxy",
                    Title = "GOG Galaxy",
                    Description = "GOG Galaxy istemcisi web arayüzü, iletişim ve geçici önbellek dosyaları (%ProgramData%\\GOG.com\\Galaxy).",
                    ParentGroup = otherAppsGroup,
                    IsRisky = false,
                    IsSelected = true
                };
                if (gogExe != null) item.IconImage = AppIconHelper.GetIcon(gogExe);
                otherAppsGroup.Items.Add(item);
            }

            otherAppsGroup.OnSubItemSelectionChanged();

            var groups = new List<CleanupGroupItem> { systemTrashGroup, cacheCleanGroup, browserCleanGroup, updateCleanGroup, systemHistoryGroup, hardwareCleanGroup };
            if (otherAppsGroup.Items.Count > 0)
            {
                groups.Add(otherAppsGroup);
            }

            return groups;
        }

        #region Tarama İşlemleri
        public static Task<long> ScanSubItemAsync(CleanupSubItem item)
        {
            return Task.Run(() =>
            {
                try
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                    return item.Id switch
                    {
                        "UserTemp" =>
                            CalculateDirectorySize(Path.GetTempPath()) +
                            CalculateDirectorySize(@"C:\Windows\Temp") +
                            CalculateDirectorySize(@"C:\Windows\SystemTemp"),

                        "System_RecycleBin" =>
                            GetRecycleBinSize(),

                        "MemoryDumps" =>
                            GetFileSizeSafe(@"C:\Windows\MEMORY.DMP") +
                            CalculateDirectorySize(@"C:\Windows\Minidump") +
                            CalculateDirectorySize(@"C:\Windows\LiveKernelReports"),

                        "WERReports" =>
                            CalculateDirectorySize(Path.Combine(progData, @"Microsoft\Windows\WER\ReportArchive")) +
                            CalculateDirectorySize(Path.Combine(progData, @"Microsoft\Windows\WER\ReportQueue")) +
                            CalculateDirectorySize(Path.Combine(progData, @"Microsoft\Windows\WER\Temp")) +
                            CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\Windows\WER\ReportArchive")) +
                            CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\Windows\WER\ReportQueue")) +
                            CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\Windows\WER\Temp")),

                        "AppCrashReports" =>
                            CalculateDirectorySize(Path.Combine(progData, @"Microsoft\Windows\WER\ERC")) +
                            CalculateDirectorySize(Path.Combine(localAppData, "CrashDumps")) +
                            CalculateDirectorySize(Path.Combine(appData, @"Microsoft\Windows\WER")),

                        "SetupLogs" =>
                            CalculateDirectorySize(@"C:\Windows\Panther") +
                            CalculateDirectorySize(@"C:\Windows\Logs\Setup") +
                            CalculateDirectorySize(@"C:\Windows\Logs\MoSetup"),

                        "UpdateLogs" =>
                            GetDirectoryFilesSizeMatching(@"C:\Windows\Logs\CBS", "CbsPersist_*.*") +
                            CalculateDirectorySize(@"C:\Windows\Logs\DISM") +
                            GetFileSizeSafe(@"C:\Windows\SoftwareDistribution\ReportingEvents.log") +
                            CalculateDirectorySize(@"C:\Windows\Logs\WindowsUpdate"),

                        "TaskSchedulerLogs" =>
                            GetEvtxCleanableBytes(@"C:\Windows\System32\winevt\Logs\Microsoft-Windows-TaskScheduler%4Operational.evtx") +
                            GetEvtxCleanableBytes(@"C:\Windows\System32\winevt\Logs\Microsoft-Windows-TaskScheduler%4Maintenance.evtx") +
                            CalculateDirectorySize(@"C:\Windows\Tasks"),

                        "PowerShellCache" =>
                            CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\PowerShell\ModuleAnalysisCache")) +
                            CalculateDirectorySize(Path.Combine(localAppData, @"PowerShell\ScriptAnalysisCache")) +
                            CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\Windows\PowerShell\CommandAnalysisCache")) +
                            GetDirectoryFilesSizeMatching(Path.Combine(localAppData, @"Microsoft\PowerShell"), "StartupProfileData-*") +
                            GetDirectoryFilesSizeMatching(Path.GetTempPath(), "PowerShell_ISE*"),

                        "ComDcomTemp" =>
                            CalculateDirectorySize(@"C:\Windows\ServiceProfiles\LocalService\AppData\Local\Temp") +
                            CalculateDirectorySize(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Temp") +
                            CalculateDirectorySize(@"C:\Windows\Registration\CRMLog"),

                        // 2. Grup: Önbellek Temizliği
                        "ThumbCache" =>
                            GetDirectoryFilesSizeMatching(Path.Combine(localAppData, @"Microsoft\Windows\Explorer"), "thumbcache_*.db"),

                        "IconCache" =>
                            GetFileSizeSafe(Path.Combine(localAppData, "IconCache.db")) +
                            GetDirectoryFilesSizeMatching(Path.Combine(localAppData, @"Microsoft\Windows\Explorer"), "iconcache_*.db"),

                        "DirectXShaderCache" =>
                            CalculateDirectorySize(Path.Combine(localAppData, "D3DSCache")) +
                            CalculateDirectorySize(Path.Combine(localAppData, @"NVIDIA\DXCache")) +
                            CalculateDirectorySize(Path.Combine(localAppData, @"NVIDIA\GLCache")) +
                            CalculateDirectorySize(Path.Combine(localAppData, @"AMD\DxCache")) +
                            CalculateDirectorySize(Path.Combine(localAppData, @"Intel\ShaderCache")) +
                            CalculateDirectorySize(@"C:\Windows\System32\DriverData\D3DSCache"),

                        "FontCache" =>
                            GetFontCacheSize(localAppData),

                        "DotNetCache" =>
                            CalculateDotNetTempCacheSize() +
                            CalculateDirectorySize(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\Temporary ASP.NET Files") +
                            CalculateDirectorySize(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files") +
                            CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\CLR_v4.0")),

                        "MediaPlayerCache" =>
                            CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\Media Player")) +
                            CalculateDirectorySize(Path.Combine(appData, @"Microsoft\Media Player")),

                        "SearchIndexTemp" =>
                            GetSearchIndexTempSize(),

                        "PrefetchCache" =>
                            CalculateDirectorySize(@"C:\Windows\Prefetch"),

                        "Cache_INetCache" =>
                            GetINetCacheSize(localAppData),

                        // 3. Grup: Tarayıcı Temizliği
                        "Browser_Chrome" =>
                            ScanChromiumBrowser(Path.Combine(localAppData, @"Google\Chrome\User Data")),

                        "Browser_Edge" =>
                            ScanChromiumBrowser(Path.Combine(localAppData, @"Microsoft\Edge\User Data")),

                        "Browser_Brave" =>
                            ScanChromiumBrowser(Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data")),

                        "Browser_OperaGX" =>
                            ScanOperaGX(localAppData, appData),

                        "Browser_Firefox" =>
                            ScanFirefox(localAppData, appData),

                        "Browser_CookiesAndPasswords" =>
                            ScanBrowserCookiesAndPasswords(localAppData, appData),

                        "Browser_History" =>
                            ScanBrowserHistory(localAppData, appData),

                        // 4. Grup: Windows Güncelleme Artıkları
                        "Update_SoftwareDistribution" =>
                            CalculateDirectorySize(@"C:\Windows\SoftwareDistribution\Download"),

                        "Update_DeliveryOptimization" =>
                            GetDeliveryOptimizationSize(),

                        "Update_WindowsOld" =>
                            CalculateDirectorySize(@"C:\Windows.old") +
                            CalculateDirectorySize(@"C:\$WINDOWS.~BT") +
                            CalculateDirectorySize(@"C:\$WINDOWS.~WS"),

                        "Update_WinSxS" =>
                            CalculateDirectorySize(@"C:\Windows\WinSxS\Temp\PendingDeletes") +
                            CalculateDirectorySize(@"C:\Windows\WinSxS\ManifestCache"),

                        "Update_InstallerCache" =>
                            CalculateDirectorySize(@"C:\Windows\Installer\$PatchCache$") +
                            GetDirectoryFilesSizeMatching(@"C:\Windows\Installer", "*.tmp") +
                            GetDirectoryFilesSizeMatching(@"C:\Windows\Installer", "*.log") +
                            CalculateDirectorySize(@"C:\Config.Msi") +
                            CalculateDirectorySize(@"C:\Windows\Installer\Config.Msi") +
                            CalculateDirectorySize(Path.Combine(Path.GetTempPath(), "Config.Msi")),

                        // 5. Grup: Sistem Geçmişi & Loglar
                        "Log_SystemLogs" =>
                            CalculateSystemLogsSize(),

                        "Log_DnsCache" =>
                            0L,

                        "Log_ClipboardHistory" =>
                            CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\Windows\Clipboard")) +
                            CalculateDirectorySize(Path.Combine(localAppData, "ConnectedDevicesPlatform")),

                        "Log_RdpHistory" =>
                            CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\Terminal Server Client\Cache")) +
                            GetFileSizeSafe(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Default.rdp")),

                        "Log_NetCache" =>
                            GetNetCacheSize(localAppData),

                        "Log_EventLogs" =>
                            GetWindowsEventLogsSize(),

                        // 6. Grup: Donanım & Sürücü Artıkları
                        "Driver_UnusedPackages" =>
                            GetDriverStoreLeftoversSize(),

                        "Driver_InfSetupLogs" =>
                            GetDirectoryFilesSizeMatching(@"C:\Windows\INF", "*.log") +
                            GetDirectoryFilesSizeMatching(@"C:\Windows\INF", "*.bak"),

                        "Driver_PrintSpooler" =>
                            GetPrintSpoolerSize(),

                        "Driver_DefenderCache" =>
                            GetDefenderCacheSize(),

                        "Driver_GpuSetupCache" =>
                            GetGpuSetupCacheSize(),

                        "Clean_ChkdskArtifacts" =>
                            GetChkdskArtifactsSize(),

                        "Clean_DownloadedProgramFiles" =>
                            CalculateDirectorySize(@"C:\Windows\Downloaded Program Files"),

                        "Clean_OldRestorePoints" =>
                            GetOldRestorePointsInfo().OldBytes,

                        "System_RegistryBackups" =>
                            GetRegistryBackupsSize(),

                        // 7. Grup: Diğer Uygulama Öğeleri
                        "App_NodeJs" => GetNodeJsCacheSize(localAppData, appData),
                        "App_NuGet" => GetNuGetCacheSize(localAppData),
                        "App_VSCode" => GetVSCodeCacheSize(appData),
                        "App_Discord" => GetDiscordCacheSize(appData),
                        "App_Spotify" => GetSpotifyCacheSize(localAppData, appData),
                        "App_Telegram" => GetTelegramCacheSize(appData),
                        "App_Vlc" => GetVlcCacheSize(appData),
                        "App_Steam" => GetSteamCacheSize(localAppData),
                        "App_EpicGames" => GetEpicGamesCacheSize(localAppData),
                        "App_EaApp" => GetEaAppCacheSize(localAppData, progData),
                        "App_Ubisoft" => GetUbisoftCacheSize(localAppData),
                        "App_BattleNet" => GetBattleNetCacheSize(localAppData, progData),
                        "App_GogGalaxy" => GetGogGalaxyCacheSize(progData, localAppData),

                        _ => 0L
                    };
                }
                catch
                {
                    return 0L;
                }
            });
        }
        #endregion

        #region Temizleme İşlemleri
        public static Task<long> CleanSubItemAsync(CleanupSubItem item)
        {
            return Task.Run(() =>
            {
                try
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                    return item.Id switch
                    {
                        "UserTemp" =>
                            CleanDirectorySafe(Path.GetTempPath()) +
                            CleanDirectorySafe(@"C:\Windows\Temp") +
                            CleanDirectorySafe(@"C:\Windows\SystemTemp"),

                        "System_RecycleBin" => CleanRecycleBin(),

                        "MemoryDumps" =>
                            DeleteFileSafe(@"C:\Windows\MEMORY.DMP") +
                            CleanDirectorySafe(@"C:\Windows\Minidump") +
                            CleanDirectorySafe(@"C:\Windows\LiveKernelReports"),

                        "WERReports" =>
                            CleanDirectorySafe(Path.Combine(progData, @"Microsoft\Windows\WER\ReportArchive")) +
                            CleanDirectorySafe(Path.Combine(progData, @"Microsoft\Windows\WER\ReportQueue")) +
                            CleanDirectorySafe(Path.Combine(progData, @"Microsoft\Windows\WER\Temp")) +
                            CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Windows\WER\ReportArchive")) +
                            CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Windows\WER\ReportQueue")) +
                            CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Windows\WER\Temp")),

                        "AppCrashReports" =>
                            CleanDirectorySafe(Path.Combine(progData, @"Microsoft\Windows\WER\ERC")) +
                            CleanDirectorySafe(Path.Combine(localAppData, "CrashDumps")) +
                            CleanDirectorySafe(Path.Combine(appData, @"Microsoft\Windows\WER")),

                        "SetupLogs" =>
                            CleanDirectorySafe(@"C:\Windows\Panther") +
                            CleanDirectorySafe(@"C:\Windows\Logs\Setup") +
                            CleanDirectorySafe(@"C:\Windows\Logs\MoSetup"),

                        "UpdateLogs" =>
                            CleanDirectorySafe(@"C:\Windows\Logs\CBS") +
                            CleanDirectorySafe(@"C:\Windows\Logs\DISM") +
                            DeleteFileSafe(@"C:\Windows\SoftwareDistribution\ReportingEvents.log") +
                            CleanDirectorySafe(@"C:\Windows\Logs\WindowsUpdate"),

                        "TaskSchedulerLogs" =>
                            ClearTaskSchedulerLogs(),

                        "PowerShellCache" =>
                            CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\PowerShell\ModuleAnalysisCache")) +
                            CleanDirectorySafe(Path.Combine(localAppData, @"PowerShell\ScriptAnalysisCache")) +
                            CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Windows\PowerShell\CommandAnalysisCache")) +
                            CleanDirectoryFilesMatching(Path.Combine(localAppData, @"Microsoft\PowerShell"), "StartupProfileData-*") +
                            CleanDirectoryFilesMatching(Path.GetTempPath(), "PowerShell_ISE*"),

                        "ComDcomTemp" =>
                            CleanDirectorySafe(@"C:\Windows\ServiceProfiles\LocalService\AppData\Local\Temp") +
                            CleanDirectorySafe(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Temp") +
                            CleanDirectorySafe(@"C:\Windows\Registration\CRMLog"),

                        // 2. Grup: Önbellek Temizliği
                        "ThumbCache" =>
                            CleanDirectoryFilesMatching(Path.Combine(localAppData, @"Microsoft\Windows\Explorer"), "thumbcache_*.db"),

                        "IconCache" =>
                            DeleteFileSafe(Path.Combine(localAppData, "IconCache.db")) +
                            CleanDirectoryFilesMatching(Path.Combine(localAppData, @"Microsoft\Windows\Explorer"), "iconcache_*.db"),

                        "DirectXShaderCache" =>
                            CleanDirectorySafe(Path.Combine(localAppData, "D3DSCache")) +
                            CleanDirectorySafe(Path.Combine(localAppData, @"NVIDIA\DXCache")) +
                            CleanDirectorySafe(Path.Combine(localAppData, @"NVIDIA\GLCache")) +
                            CleanDirectorySafe(Path.Combine(localAppData, @"AMD\DxCache")) +
                            CleanDirectorySafe(Path.Combine(localAppData, @"Intel\ShaderCache")) +
                            CleanDirectorySafe(@"C:\Windows\System32\DriverData\D3DSCache"),

                        "FontCache" =>
                            CleanFontCache(localAppData),

                        "DotNetCache" =>
                            CleanDotNetTempCache() +
                            CleanDirectorySafe(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\Temporary ASP.NET Files") +
                            CleanDirectorySafe(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files") +
                            CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\CLR_v4.0")),

                        "MediaPlayerCache" =>
                            CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Media Player")) +
                            CleanDirectorySafe(Path.Combine(appData, @"Microsoft\Media Player")),

                        "SearchIndexTemp" =>
                            CleanSearchIndexTemp(),

                        "PrefetchCache" => CleanDirectorySafe(@"C:\Windows\Prefetch"),
                        "Cache_INetCache" => CleanINetCache(localAppData),

                        // 3. Grup: Tarayıcı Temizliği
                        "Browser_Chrome" =>
                            CleanChromiumBrowser(Path.Combine(localAppData, @"Google\Chrome\User Data")),

                        "Browser_Edge" =>
                            CleanChromiumBrowser(Path.Combine(localAppData, @"Microsoft\Edge\User Data")),

                        "Browser_Brave" =>
                            CleanChromiumBrowser(Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data")),

                        "Browser_OperaGX" =>
                            CleanOperaGX(localAppData, appData),

                        "Browser_Firefox" =>
                            CleanFirefox(localAppData, appData),

                        "Browser_CookiesAndPasswords" =>
                            CleanBrowserCookiesAndPasswords(localAppData, appData),

                        "Browser_History" =>
                            CleanBrowserHistory(localAppData, appData),

                        // 4. Grup: Windows Güncelleme Artıkları
                        "Update_SoftwareDistribution" =>
                            CleanSoftwareDistribution(),

                        "Update_DeliveryOptimization" =>
                            CleanDeliveryOptimization(),

                        "Update_WindowsOld" =>
                            CleanWindowsOld(),

                        "Update_WinSxS" =>
                            CleanWinSxS(),

                        "Update_InstallerCache" =>
                            CleanInstallerCache(),

                        // 5. Grup: Sistem Geçmişi & Loglar
                        "Log_SystemLogs" => CleanSystemLogs(),
                        "Log_DnsCache" => CleanDnsCache(),
                        "Log_ClipboardHistory" => CleanClipboardHistory(localAppData),
                        "Log_RdpHistory" => CleanRdpHistory(localAppData),
                        "Log_NetCache" => CleanNetCache(localAppData),
                        "Log_EventLogs" => CleanWindowsEventLogs(),

                        // 6. Grup: Donanım & Sürücü Artıkları
                        "Driver_UnusedPackages" => CleanUnusedDriverPackages(),
                        "Driver_InfSetupLogs" => CleanInfSetupLogs(),
                        "Driver_PrintSpooler" => CleanPrintSpooler(),
                        "Driver_DefenderCache" => CleanDefenderCache(),
                        "Driver_GpuSetupCache" => CleanGpuSetupCache(),

                        "Clean_ChkdskArtifacts" => CleanChkdskArtifacts(),
                        "Clean_DownloadedProgramFiles" => CleanDirectorySafe(@"C:\Windows\Downloaded Program Files"),
                        "Clean_OldRestorePoints" => CleanOldRestorePoints(),
                        "System_RegistryBackups" => CleanRegistryBackups(),

                        // 7. Grup: Diğer Uygulama Öğeleri
                        "App_NodeJs" => CleanNodeJsCache(localAppData, appData),
                        "App_NuGet" => CleanNuGetCache(localAppData),
                        "App_VSCode" => CleanVSCodeCache(appData),
                        "App_Discord" => CleanDiscordCache(appData),
                        "App_Spotify" => CleanSpotifyCache(localAppData, appData),
                        "App_Telegram" => CleanTelegramCache(appData),
                        "App_Vlc" => CleanVlcCache(appData),
                        "App_Steam" => CleanSteamCache(localAppData),
                        "App_EpicGames" => CleanEpicGamesCache(localAppData),
                        "App_EaApp" => CleanEaAppCache(localAppData, progData),
                        "App_Ubisoft" => CleanUbisoftCache(localAppData),
                        "App_BattleNet" => CleanBattleNetCache(localAppData, progData),
                        "App_GogGalaxy" => CleanGogGalaxyCache(progData, localAppData),

                        _ => 0L
                    };
                }
                catch
                {
                    return 0L;
                }
            });
        }
        #endregion

        #region Geri Dönüşüm Kutusu (Recycle Bin)
        [StructLayout(LayoutKind.Sequential)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private static long GetRecycleBinSize()
        {
            long total = 0;
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable))
                    {
                        try
                        {
                            var rb = new SHQUERYRBINFO();
                            rb.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                            if (SHQueryRecycleBin(drive.Name, ref rb) == 0 && rb.i64Size > 0)
                            {
                                total += rb.i64Size;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return total;
        }

        private static long CleanRecycleBin()
        {
            long before = GetRecycleBinSize();
            try
            {
                // 7 = SHERB_NOCONFIRMATION (1) | SHERB_NOPROGRESSUI (2) | SHERB_NOSOUND (4)
                SHEmptyRecycleBin(IntPtr.Zero, null, 7);
            }
            catch { }

            long after = GetRecycleBinSize();
            long freed = Math.Max(0, before - after);
            if (freed == 0 && before > 0)
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable))
                    {
                        try
                        {
                            SHEmptyRecycleBin(IntPtr.Zero, drive.Name, 7);
                        }
                        catch { }
                    }
                }
                after = GetRecycleBinSize();
                freed = Math.Max(0, before - after);
            }
            return freed;
        }
        #endregion

        #region Özel Görev Zamanlayıcı Temizliği
        private static long GetEvtxCleanableBytes(string path)
        {
            long size = GetFileSizeSafe(path);
            // 69632 bayt (68 KB) Windows EVTX dosyasının boş kayıt başlık (header) boyutudur.
            return size > 69632 ? size - 69632 : 0L;
        }

        private static long ClearTaskSchedulerLogs()
        {
            long freed = 0;
            string opEvtx = @"C:\Windows\System32\winevt\Logs\Microsoft-Windows-TaskScheduler%4Operational.evtx";
            string maintEvtx = @"C:\Windows\System32\winevt\Logs\Microsoft-Windows-TaskScheduler%4Maintenance.evtx";
            long opBefore = GetFileSizeSafe(opEvtx);
            long maintBefore = GetFileSizeSafe(maintEvtx);

            freed += CleanDirectorySafe(@"C:\Windows\Tasks");

            try
            {
                using var p1 = Process.Start(new ProcessStartInfo
                {
                    FileName = "wevtutil.exe",
                    Arguments = "cl \"Microsoft-Windows-TaskScheduler/Operational\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p1?.WaitForExit(2500);
            }
            catch { }

            try
            {
                using var p2 = Process.Start(new ProcessStartInfo
                {
                    FileName = "wevtutil.exe",
                    Arguments = "cl \"Microsoft-Windows-TaskScheduler/Maintenance\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p2?.WaitForExit(2500);
            }
            catch { }

            long opAfter = GetFileSizeSafe(opEvtx);
            long maintAfter = GetFileSizeSafe(maintEvtx);

            if (opBefore > opAfter) freed += (opBefore - opAfter);
            if (maintBefore > maintAfter) freed += (maintBefore - maintAfter);

            return freed;
        }

        private static void EnsureFontCacheAccess()
        {
            try
            {
                string path = @"C:\Windows\ServiceProfiles\LocalService\AppData\Local\FontCache";
                if (Directory.Exists(path))
                {
                    TakeOwnershipSafe(path, 3000);
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "icacls.exe",
                        Arguments = $"\"{path}\" /grant \"*S-1-5-32-544:(OI)(CI)F\" /Q",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit(1200);
                }
            }
            catch { }
        }

        private static long GetFontCacheSize(string localAppData)
        {
            EnsureFontCacheAccess();
            long size = CalculateDirectorySize(@"C:\Windows\ServiceProfiles\LocalService\AppData\Local\FontCache");
            size += GetFileSizeSafe(@"C:\Windows\System32\FNTCACHE.DAT");
            size += CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\FontCache"));
            size += CalculateDirectorySize(@"C:\Windows\System32\DirectWriteFontCache");
            return size;
        }

        private static long CleanFontCache(string localAppData)
        {
            EnsureFontCacheAccess();
            bool stoppedService = false;
            try
            {
                using var stopProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "net.exe",
                    Arguments = "stop FontCache /y",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                stopProc?.WaitForExit(3000);
                stoppedService = true;
            }
            catch { }

            long freed = 0;
            try
            {
                freed += CleanDirectorySafe(@"C:\Windows\ServiceProfiles\LocalService\AppData\Local\FontCache");
                freed += DeleteFileSafe(@"C:\Windows\System32\FNTCACHE.DAT");
                freed += CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\FontCache"));
                freed += CleanDirectorySafe(@"C:\Windows\System32\DirectWriteFontCache");
            }
            finally
            {
                if (stoppedService)
                {
                    try
                    {
                        using var startProc = Process.Start(new ProcessStartInfo
                        {
                            FileName = "net.exe",
                            Arguments = "start FontCache",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        startProc?.WaitForExit(3000);
                    }
                    catch { }
                }
            }
            return freed;
        }

        private static void EnsureSearchIndexAccess()
        {
            try
            {
                string path = @"C:\ProgramData\Microsoft\Search\Data\Temp";
                if (Directory.Exists(path))
                {
                    TakeOwnershipSafe(path, 3000);
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "icacls.exe",
                        Arguments = $"\"{path}\" /grant \"*S-1-5-32-544:(OI)(CI)F\" /Q",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit(1200);
                }
            }
            catch { }
        }

        private static long GetSearchIndexTempSize()
        {
            EnsureSearchIndexAccess();
            return CalculateDirectorySize(@"C:\ProgramData\Microsoft\Search\Data\Temp");
        }

        private static long CleanSearchIndexTemp()
        {
            EnsureSearchIndexAccess();
            string path = @"C:\ProgramData\Microsoft\Search\Data\Temp";
            if (!Directory.Exists(path)) return 0;

            bool stoppedService = false;
            try
            {
                using var stopProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "net.exe",
                    Arguments = "stop wsearch /y",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                stopProc?.WaitForExit(3000);
                stoppedService = true;
            }
            catch { }

            long freed = 0;
            try
            {
                freed = CleanDirectorySafe(path);
            }
            finally
            {
                if (stoppedService)
                {
                    try
                    {
                        using var startProc = Process.Start(new ProcessStartInfo
                        {
                            FileName = "net.exe",
                            Arguments = "start wsearch",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        startProc?.WaitForExit(3000);
                    }
                    catch { }
                }
            }
            return freed;
        }

        private static long CalculateDotNetTempCacheSize()
        {
            long size = 0;
            string dotNetDir = Path.Combine(Path.GetTempPath(), ".net");
            if (!Directory.Exists(dotNetDir)) return 0;

            string currentAppName = Process.GetCurrentProcess().ProcessName;
            try
            {
                foreach (var dir in Directory.GetDirectories(dotNetDir))
                {
                    string dirName = Path.GetFileName(dir);
                    if (string.Equals(dirName, currentAppName, StringComparison.OrdinalIgnoreCase))
                        continue; // PureWin kendi çalışma dosyalarını saymaz

                    size += CalculateDirectorySize(dir);
                }

                foreach (var file in Directory.GetFiles(dotNetDir))
                {
                    try { size += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }
            return size;
        }

        private static long CleanDotNetTempCache()
        {
            long freed = 0;
            string dotNetDir = Path.Combine(Path.GetTempPath(), ".net");
            if (!Directory.Exists(dotNetDir)) return 0;

            string currentAppName = Process.GetCurrentProcess().ProcessName;
            try
            {
                foreach (var dir in Directory.GetDirectories(dotNetDir))
                {
                    string dirName = Path.GetFileName(dir);
                    if (string.Equals(dirName, currentAppName, StringComparison.OrdinalIgnoreCase))
                        continue; // PureWin kendi çalışma dosyalarını ASLA silmez!

                    freed += CleanDirectorySafe(dir);
                    try { Directory.Delete(dir, true); } catch { }
                }

                foreach (var file in Directory.GetFiles(dotNetDir))
                {
                    try
                    {
                        long len = new FileInfo(file).Length;
                        File.Delete(file);
                        freed += len;
                    }
                    catch { }
                }
            }
            catch { }
            return freed;
        }

        #region INetCache & WebCache Yardımcıları
        private static long GetWebCacheCleanableSize(string webCacheDir)
        {
            long size = 0;
            if (!Directory.Exists(webCacheDir)) return 0;
            try
            {
                foreach (var f in Directory.GetFiles(webCacheDir))
                {
                    try
                    {
                        using var fs = File.Open(f, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        size += new FileInfo(f).Length;
                    }
                    catch { }
                }
            }
            catch { }
            return size;
        }

        private static long GetINetCacheSize(string localAppData)
        {
            long size = 0;
            try
            {
                size += CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\Windows\INetCache"));
                size += CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\Windows\INetCookies"));
                size += GetWebCacheCleanableSize(Path.Combine(localAppData, @"Microsoft\Windows\WebCache"));
                size += CalculateDirectorySize(@"C:\Windows\System32\config\systemprofile\AppData\Local\Microsoft\Windows\INetCache");
                size += CalculateDirectorySize(@"C:\Windows\ServiceProfiles\LocalService\AppData\Local\Microsoft\Windows\INetCache");
                size += CalculateDirectorySize(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\INetCache");
            }
            catch { }
            return size;
        }

        private static long CleanINetCache(string localAppData)
        {
            long freed = 0;

            // 1. Resmi Windows Internet Seçenekleri API temizliği (Geçici Internet Dosyaları flag 8)
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = "inetcpl.cpl,ClearMyTracksByProcess 8",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                proc?.WaitForExit(3000);
            }
            catch { }

            // 2. INetCache ve WebCache dizinlerini temizle
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Windows\INetCache"));
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Windows\INetCookies"));
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Windows\WebCache"));

            // 3. Sistem profilleri INetCache temizliği
            freed += CleanDirectorySafe(@"C:\Windows\System32\config\systemprofile\AppData\Local\Microsoft\Windows\INetCache");
            freed += CleanDirectorySafe(@"C:\Windows\ServiceProfiles\LocalService\AppData\Local\Microsoft\Windows\INetCache");
            freed += CleanDirectorySafe(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\INetCache");

            return freed;
        }
        #endregion

        #region Diğer Uygulama Öğeleri Yardımcıları
        private static long GetNodeJsCacheSize(string localAppData, string appData)
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            long size = 0;
            size += CalculateDirectorySize(Path.Combine(localAppData, "npm-cache"));
            size += CalculateDirectorySize(Path.Combine(appData, "npm-cache"));
            size += CalculateDirectorySize(Path.Combine(userProfile, ".npm"));
            size += CalculateDirectorySize(Path.Combine(localAppData, @"node-gyp\Cache"));
            return size;
        }

        private static long CleanNodeJsCache(string localAppData, string appData)
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            long freed = 0;
            freed += CleanDirectorySafe(Path.Combine(localAppData, "npm-cache"));
            freed += CleanDirectorySafe(Path.Combine(appData, "npm-cache"));
            freed += CleanDirectorySafe(Path.Combine(userProfile, ".npm"));
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"node-gyp\Cache"));
            return freed;
        }

        private static long GetNuGetCacheSize(string localAppData)
        {
            long size = 0;
            size += CalculateDirectorySize(Path.Combine(localAppData, @"NuGet\v3-cache"));
            size += CalculateDirectorySize(Path.Combine(localAppData, @"NuGet\http-cache"));
            size += CalculateDirectorySize(Path.Combine(localAppData, @"NuGet\plugins-cache"));
            return size;
        }

        private static long CleanNuGetCache(string localAppData)
        {
            long freed = 0;
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"NuGet\v3-cache"));
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"NuGet\http-cache"));
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"NuGet\plugins-cache"));
            return freed;
        }

        private static long GetVSCodeCacheSize(string appData)
        {
            long size = 0;
            string codeDir = Path.Combine(appData, "Code");
            string[] subDirs = new[]
            {
                "Cache",
                "CachedData",
                "CachedExtensionVSIXs",
                "CachedProfilesData",
                "Code Cache",
                "GPUCache",
                "logs",
                @"Crashpad\completed"
            };
            foreach (var sub in subDirs)
            {
                size += CalculateDirectorySize(Path.Combine(codeDir, sub));
            }
            return size;
        }

        private static long CleanVSCodeCache(string appData)
        {
            long freed = 0;
            string codeDir = Path.Combine(appData, "Code");
            string[] subDirs = new[]
            {
                "Cache",
                "CachedData",
                "CachedExtensionVSIXs",
                "CachedProfilesData",
                "Code Cache",
                "GPUCache",
                "logs",
                @"Crashpad\completed"
            };
            foreach (var sub in subDirs)
            {
                freed += CleanDirectorySafe(Path.Combine(codeDir, sub));
            }
            return freed;
        }

        private static long GetDiscordCacheSize(string appData)
        {
            long size = 0;
            string discordDir = Path.Combine(appData, "discord");
            string[] subDirs = new[] { "Cache", "Code Cache", "GPUCache", @"Crashpad\completed" };
            foreach (var sub in subDirs)
            {
                size += CalculateDirectorySize(Path.Combine(discordDir, sub));
            }
            return size;
        }

        private static long CleanDiscordCache(string appData)
        {
            long freed = 0;
            string discordDir = Path.Combine(appData, "discord");
            string[] subDirs = new[] { "Cache", "Code Cache", "GPUCache", @"Crashpad\completed" };
            foreach (var sub in subDirs)
            {
                freed += CleanDirectorySafe(Path.Combine(discordDir, sub));
            }
            return freed;
        }

        private static long GetSpotifyCacheSize(string localAppData, string appData)
        {
            long size = 0;
            size += CalculateDirectorySize(Path.Combine(localAppData, @"Spotify\Data"));
            size += CalculateDirectorySize(Path.Combine(localAppData, @"Spotify\Storage"));
            size += CalculateDirectorySize(Path.Combine(appData, @"Spotify\PersistentCache"));
            return size;
        }

        private static long CleanSpotifyCache(string localAppData, string appData)
        {
            long freed = 0;
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Spotify\Data"));
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Spotify\Storage"));
            freed += CleanDirectorySafe(Path.Combine(appData, @"Spotify\PersistentCache"));
            return freed;
        }

        private static long GetSteamCacheSize(string localAppData)
        {
            long size = 0;
            size += CalculateDirectorySize(Path.Combine(localAppData, @"Steam\htmlcache"));
            size += CalculateDirectorySize(@"C:\Program Files (x86)\Steam\appcache\httpcache");
            size += CalculateDirectorySize(@"C:\Program Files (x86)\Steam\logs");
            size += CalculateDirectorySize(@"C:\Program Files\Steam\appcache\httpcache");
            size += CalculateDirectorySize(@"C:\Program Files\Steam\logs");
            return size;
        }

        private static long CleanSteamCache(string localAppData)
        {
            long freed = 0;
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Steam\htmlcache"));
            freed += CleanDirectorySafe(@"C:\Program Files (x86)\Steam\appcache\httpcache");
            freed += CleanDirectorySafe(@"C:\Program Files (x86)\Steam\logs");
            freed += CleanDirectorySafe(@"C:\Program Files\Steam\appcache\httpcache");
            freed += CleanDirectorySafe(@"C:\Program Files\Steam\logs");
            return freed;
        }

        private static long GetTelegramCacheSize(string appData)
        {
            long size = 0;
            string tdir = Path.Combine(appData, @"Telegram Desktop\tdata");
            size += CalculateDirectorySize(Path.Combine(tdir, "temp"));
            size += CalculateDirectorySize(Path.Combine(tdir, @"user_data\cache"));
            size += CalculateDirectorySize(Path.Combine(tdir, @"user_data\media_cache"));
            return size;
        }

        private static long CleanTelegramCache(string appData)
        {
            long freed = 0;
            string tdir = Path.Combine(appData, @"Telegram Desktop\tdata");
            freed += CleanDirectorySafe(Path.Combine(tdir, "temp"));
            freed += CleanDirectorySafe(Path.Combine(tdir, @"user_data\cache"));
            freed += CleanDirectorySafe(Path.Combine(tdir, @"user_data\media_cache"));
            return freed;
        }

        private static long GetVlcCacheSize(string appData)
        {
            long size = 0;
            size += CalculateDirectorySize(Path.Combine(appData, @"vlc\art"));
            size += CalculateDirectorySize(Path.Combine(appData, @"vlc\crashdumps"));
            return size;
        }

        private static long CleanVlcCache(string appData)
        {
            long freed = 0;
            freed += CleanDirectorySafe(Path.Combine(appData, @"vlc\art"));
            freed += CleanDirectorySafe(Path.Combine(appData, @"vlc\crashdumps"));
            return freed;
        }

        private static long GetEpicGamesCacheSize(string localAppData)
        {
            long size = 0;
            string epicSaved = Path.Combine(localAppData, @"EpicGamesLauncher\Saved");
            size += CalculateDirectorySize(Path.Combine(epicSaved, "webcache"));
            size += CalculateDirectorySize(Path.Combine(epicSaved, "webcache_4147"));
            size += CalculateDirectorySize(Path.Combine(epicSaved, "webcache_4430"));
            size += CalculateDirectorySize(Path.Combine(epicSaved, "Logs"));
            size += CalculateDirectorySize(Path.Combine(epicSaved, "CrashReportClient"));
            return size;
        }

        private static long CleanEpicGamesCache(string localAppData)
        {
            long freed = 0;
            string epicSaved = Path.Combine(localAppData, @"EpicGamesLauncher\Saved");
            freed += CleanDirectorySafe(Path.Combine(epicSaved, "webcache"));
            freed += CleanDirectorySafe(Path.Combine(epicSaved, "webcache_4147"));
            freed += CleanDirectorySafe(Path.Combine(epicSaved, "webcache_4430"));
            freed += CleanDirectorySafe(Path.Combine(epicSaved, "Logs"));
            freed += CleanDirectorySafe(Path.Combine(epicSaved, "CrashReportClient"));
            return freed;
        }

        private static long GetEaAppCacheSize(string localAppData, string progData)
        {
            long size = 0;
            string eaLocal = Path.Combine(localAppData, @"Electronic Arts\EA Desktop");
            size += CalculateDirectorySize(Path.Combine(eaLocal, "cef_cache"));
            size += CalculateDirectorySize(Path.Combine(eaLocal, "IGOWebCache"));
            size += CalculateDirectorySize(Path.Combine(eaLocal, "Logs"));
            size += CalculateDirectorySize(Path.Combine(eaLocal, "CrashDumps"));
            size += CalculateDirectorySize(Path.Combine(progData, @"Electronic Arts\EA Desktop\Logs"));
            return size;
        }

        private static long CleanEaAppCache(string localAppData, string progData)
        {
            long freed = 0;
            string eaLocal = Path.Combine(localAppData, @"Electronic Arts\EA Desktop");
            freed += CleanDirectorySafe(Path.Combine(eaLocal, "cef_cache"));
            freed += CleanDirectorySafe(Path.Combine(eaLocal, "IGOWebCache"));
            freed += CleanDirectorySafe(Path.Combine(eaLocal, "Logs"));
            freed += CleanDirectorySafe(Path.Combine(eaLocal, "CrashDumps"));
            freed += CleanDirectorySafe(Path.Combine(progData, @"Electronic Arts\EA Desktop\Logs"));
            return freed;
        }

        private static long GetUbisoftCacheSize(string localAppData)
        {
            long size = 0;
            string ubiLocal = Path.Combine(localAppData, "Ubisoft Game Launcher");
            size += CalculateDirectorySize(Path.Combine(ubiLocal, "cache"));
            size += CalculateDirectorySize(Path.Combine(ubiLocal, "spool"));
            size += CalculateDirectorySize(Path.Combine(ubiLocal, "logs"));
            return size;
        }

        private static long CleanUbisoftCache(string localAppData)
        {
            long freed = 0;
            string ubiLocal = Path.Combine(localAppData, "Ubisoft Game Launcher");
            freed += CleanDirectorySafe(Path.Combine(ubiLocal, "cache"));
            freed += CleanDirectorySafe(Path.Combine(ubiLocal, "spool"));
            freed += CleanDirectorySafe(Path.Combine(ubiLocal, "logs"));
            return freed;
        }

        private static long GetBattleNetCacheSize(string localAppData, string progData)
        {
            long size = 0;
            size += CalculateDirectorySize(Path.Combine(localAppData, @"Battle.net\Browser\Cache"));
            size += CalculateDirectorySize(Path.Combine(localAppData, @"Battle.net\Browser\GPUCache"));
            size += CalculateDirectorySize(Path.Combine(localAppData, @"Battle.net\Logs"));
            size += CalculateDirectorySize(Path.Combine(progData, @"Battle.net\Cache"));
            return size;
        }

        private static long CleanBattleNetCache(string localAppData, string progData)
        {
            long freed = 0;
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Battle.net\Browser\Cache"));
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Battle.net\Browser\GPUCache"));
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Battle.net\Logs"));
            freed += CleanDirectorySafe(Path.Combine(progData, @"Battle.net\Cache"));
            return freed;
        }

        private static long GetGogGalaxyCacheSize(string progData, string localAppData)
        {
            long size = 0;
            size += CalculateDirectorySize(Path.Combine(progData, @"GOG.com\Galaxy\webcache"));
            size += CalculateDirectorySize(Path.Combine(localAppData, @"GOG.com\Galaxy\webcache"));
            size += CalculateDirectorySize(Path.Combine(progData, @"GOG.com\Galaxy\logs"));
            return size;
        }

        private static long CleanGogGalaxyCache(string progData, string localAppData)
        {
            long freed = 0;
            freed += CleanDirectorySafe(Path.Combine(progData, @"GOG.com\Galaxy\webcache"));
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"GOG.com\Galaxy\webcache"));
            freed += CleanDirectorySafe(Path.Combine(progData, @"GOG.com\Galaxy\logs"));
            return freed;
        }
        #endregion

        #region Özel Tarayıcı Temizliği Yardımcıları
        public static bool IsBrowserInstalled(string id)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            return id switch
            {
                "Browser_Chrome" => Directory.Exists(Path.Combine(local, @"Google\Chrome\User Data")),
                "Browser_Edge" => Directory.Exists(Path.Combine(local, @"Microsoft\Edge\User Data")),
                "Browser_Brave" => Directory.Exists(Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data")),
                "Browser_OperaGX" => Directory.Exists(Path.Combine(local, @"Opera Software\Opera GX Stable")) || 
                                     Directory.Exists(Path.Combine(appData, @"Opera Software\Opera GX Stable")) ||
                                     Directory.Exists(Path.Combine(local, @"Opera Software\Opera Stable")) || 
                                     Directory.Exists(Path.Combine(appData, @"Opera Software\Opera Stable")),
                "Browser_Firefox" => Directory.Exists(Path.Combine(local, @"Mozilla\Firefox")) || Directory.Exists(Path.Combine(appData, @"Mozilla\Firefox")),
                "Browser_CookiesAndPasswords" => 
                    IsBrowserInstalled("Browser_Chrome") || 
                    IsBrowserInstalled("Browser_Edge") || 
                    IsBrowserInstalled("Browser_Brave") || 
                    IsBrowserInstalled("Browser_OperaGX") || 
                    IsBrowserInstalled("Browser_Firefox"),
                "Browser_History" =>
                    IsBrowserInstalled("Browser_CookiesAndPasswords"),
                _ => true
            };
        }

        private static long ScanChromiumBrowser(string userDataDir)
        {
            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
                return 0;

            long total = 0;
            try
            {
                // Ana dizindeki Shader, GrShader, Metrik, CRX önbellekleri ve Crashpad raporları
                total += CalculateDirectorySize(Path.Combine(userDataDir, "ShaderCache"));
                total += CalculateDirectorySize(Path.Combine(userDataDir, "GrShaderCache"));
                total += CalculateDirectorySize(Path.Combine(userDataDir, "GPUPersistentCache"));
                total += CalculateDirectorySize(Path.Combine(userDataDir, "Crash Reports"));
                total += CalculateDirectorySize(Path.Combine(userDataDir, "BrowserMetrics"));
                total += CalculateDirectorySize(Path.Combine(userDataDir, "DeferredBrowserMetrics"));
                total += CalculateDirectorySize(Path.Combine(userDataDir, @"Crashpad\reports"));
                total += CalculateDirectorySize(Path.Combine(userDataDir, @"Crashpad\completed"));
                total += CalculateDirectorySize(Path.Combine(userDataDir, @"Crashpad\pending"));
                total += CalculateDirectorySize(Path.Combine(userDataDir, "component_crx_cache"));
                total += CalculateDirectorySize(Path.Combine(userDataDir, "extensions_crx_cache"));

                // Default, Profile *, System Profile ve Guest Profile dizinlerini tara
                var dirInfo = new DirectoryInfo(userDataDir);
                foreach (var profileDir in dirInfo.EnumerateDirectories())
                {
                    if (profileDir.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("System Profile", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase))
                    {
                        string pPath = profileDir.FullName;
                        total += CalculateDirectorySize(Path.Combine(pPath, "Cache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "System Cache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "GPUCache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "DawnCache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "DawnGraphiteCache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "DawnWebGPUCache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "Code Cache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, @"Service Worker\CacheStorage"));
                        total += CalculateDirectorySize(Path.Combine(pPath, @"Service Worker\ScriptCache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "Media Cache"));
                        // Not: Yer imleri (Bookmarks, Favicons), açık oturum sekmeleri (Sessions) ve geçmiş (History) burada bilerek KORUNUR!
                    }
                }
            }
            catch { }
            return total;
        }

        private static long CleanChromiumBrowser(string userDataDir)
        {
            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
                return 0;

            long freed = 0;
            try
            {
                freed += CleanDirectorySafe(Path.Combine(userDataDir, "ShaderCache"));
                freed += CleanDirectorySafe(Path.Combine(userDataDir, "GrShaderCache"));
                freed += CleanDirectorySafe(Path.Combine(userDataDir, "GPUPersistentCache"));
                freed += CleanDirectorySafe(Path.Combine(userDataDir, "Crash Reports"));
                freed += CleanDirectorySafe(Path.Combine(userDataDir, "BrowserMetrics"));
                freed += CleanDirectorySafe(Path.Combine(userDataDir, "DeferredBrowserMetrics"));
                freed += CleanDirectorySafe(Path.Combine(userDataDir, @"Crashpad\reports"));
                freed += CleanDirectorySafe(Path.Combine(userDataDir, @"Crashpad\completed"));
                freed += CleanDirectorySafe(Path.Combine(userDataDir, @"Crashpad\pending"));
                freed += CleanDirectorySafe(Path.Combine(userDataDir, "component_crx_cache"));
                freed += CleanDirectorySafe(Path.Combine(userDataDir, "extensions_crx_cache"));

                var dirInfo = new DirectoryInfo(userDataDir);
                foreach (var profileDir in dirInfo.EnumerateDirectories())
                {
                    if (profileDir.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("System Profile", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase))
                    {
                        string pPath = profileDir.FullName;
                        freed += CleanDirectorySafe(Path.Combine(pPath, "Cache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "System Cache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "GPUCache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "DawnCache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "DawnGraphiteCache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "DawnWebGPUCache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "Code Cache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, @"Service Worker\CacheStorage"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, @"Service Worker\ScriptCache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "Media Cache"));
                        // Not: Yer imleri (Bookmarks, Favicons), açık oturum sekmeleri (Sessions) ve geçmiş (History) burada bilerek KORUNUR!
                    }
                }
            }
            catch { }
            return freed;
        }

        private static long ScanOperaDirectory(string baseDir)
        {
            if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                return 0;

            long total = 0;
            try
            {
                // Kök dizindeki önbellekler & crash raporları
                total += CalculateDirectorySize(Path.Combine(baseDir, "ShaderCache"));
                total += CalculateDirectorySize(Path.Combine(baseDir, "GrShaderCache"));
                total += CalculateDirectorySize(Path.Combine(baseDir, "GPUPersistentCache"));
                total += CalculateDirectorySize(Path.Combine(baseDir, "Crash Reports"));
                total += CalculateDirectorySize(Path.Combine(baseDir, @"Crashpad\reports"));
                total += CalculateDirectorySize(Path.Combine(baseDir, @"Crashpad\completed"));
                total += CalculateDirectorySize(Path.Combine(baseDir, @"Crashpad\pending"));
                total += CalculateDirectorySize(Path.Combine(baseDir, "component_crx_cache"));
                total += CalculateDirectorySize(Path.Combine(baseDir, "extensions_crx_cache"));

                // Profiller: Default, Profile *, System Profile veya Guest Profile
                var dirInfo = new DirectoryInfo(baseDir);
                foreach (var profileDir in dirInfo.EnumerateDirectories())
                {
                    if (profileDir.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("System Profile", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase))
                    {
                        string pPath = profileDir.FullName;
                        total += CalculateDirectorySize(Path.Combine(pPath, "Cache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "System Cache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "GPUCache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "DawnCache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "DawnGraphiteCache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "DawnWebGPUCache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "Code Cache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, @"Service Worker\CacheStorage"));
                        total += CalculateDirectorySize(Path.Combine(pPath, @"Service Worker\ScriptCache"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "Media Cache"));
                        // Not: Yer imleri, açık oturum sekmeleri ve geçmiş burada bilerek KORUNUR!
                    }
                }
            }
            catch { }
            return total;
        }

        private static long CleanOperaDirectory(string baseDir)
        {
            if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                return 0;

            long freed = 0;
            try
            {
                freed += CleanDirectorySafe(Path.Combine(baseDir, "ShaderCache"));
                freed += CleanDirectorySafe(Path.Combine(baseDir, "GrShaderCache"));
                freed += CleanDirectorySafe(Path.Combine(baseDir, "GPUPersistentCache"));
                freed += CleanDirectorySafe(Path.Combine(baseDir, "Crash Reports"));
                freed += CleanDirectorySafe(Path.Combine(baseDir, @"Crashpad\reports"));
                freed += CleanDirectorySafe(Path.Combine(baseDir, @"Crashpad\completed"));
                freed += CleanDirectorySafe(Path.Combine(baseDir, @"Crashpad\pending"));
                freed += CleanDirectorySafe(Path.Combine(baseDir, "component_crx_cache"));
                freed += CleanDirectorySafe(Path.Combine(baseDir, "extensions_crx_cache"));

                var dirInfo = new DirectoryInfo(baseDir);
                foreach (var profileDir in dirInfo.EnumerateDirectories())
                {
                    if (profileDir.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("System Profile", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase))
                    {
                        string pPath = profileDir.FullName;
                        freed += CleanDirectorySafe(Path.Combine(pPath, "Cache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "System Cache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "GPUCache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "DawnCache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "DawnGraphiteCache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "DawnWebGPUCache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "Code Cache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, @"Service Worker\CacheStorage"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, @"Service Worker\ScriptCache"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "Media Cache"));
                        // Not: Yer imleri, açık oturum sekmeleri ve geçmiş burada bilerek KORUNUR!
                    }
                }
            }
            catch { }
            return freed;
        }

        private static long ScanOperaGX(string localAppData, string appData)
        {
            long total = 0;
            string[] operaProfiles = { "Opera GX Stable", "Opera Stable" };
            foreach (var profile in operaProfiles)
            {
                total += ScanOperaDirectory(Path.Combine(localAppData, @"Opera Software", profile));
                total += ScanOperaDirectory(Path.Combine(appData, @"Opera Software", profile));
            }
            return total;
        }

        private static long CleanOperaGX(string localAppData, string appData)
        {
            long freed = 0;
            string[] operaProfiles = { "Opera GX Stable", "Opera Stable" };
            foreach (var profile in operaProfiles)
            {
                freed += CleanOperaDirectory(Path.Combine(localAppData, @"Opera Software", profile));
                freed += CleanOperaDirectory(Path.Combine(appData, @"Opera Software", profile));
            }
            return freed;
        }

        private static long ScanFirefox(string localAppData, string appData)
        {
            long total = 0;
            string localProfiles = Path.Combine(localAppData, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(localProfiles))
            {
                var dirInfo = new DirectoryInfo(localProfiles);
                foreach (var profile in dirInfo.EnumerateDirectories())
                {
                    total += CalculateDirectorySize(Path.Combine(profile.FullName, "cache2"));
                    total += CalculateDirectorySize(Path.Combine(profile.FullName, "startupCache"));
                    total += CalculateDirectorySize(Path.Combine(profile.FullName, "shader-cache"));
                    total += CalculateDirectorySize(Path.Combine(profile.FullName, "jumpListCache"));
                    total += CalculateDirectorySize(Path.Combine(profile.FullName, "thumbnails"));
                }
            }

            string roamMozilla = Path.Combine(appData, @"Mozilla\Firefox");
            if (Directory.Exists(roamMozilla))
            {
                total += CalculateDirectorySize(Path.Combine(roamMozilla, "Crash Reports"));
                string roamProfiles = Path.Combine(roamMozilla, "Profiles");
                if (Directory.Exists(roamProfiles))
                {
                    var dirInfo = new DirectoryInfo(roamProfiles);
                    foreach (var profile in dirInfo.EnumerateDirectories())
                    {
                        total += CalculateDirectorySize(Path.Combine(profile.FullName, "thumbnails"));
                        // Not: Yer imleri (places.sqlite), oturum kurtarma (sessionstore*) ve formhistory burada bilerek KORUNUR!
                    }
                }
            }
            return total;
        }

        private static long CleanFirefox(string localAppData, string appData)
        {
            long freed = 0;
            string localProfiles = Path.Combine(localAppData, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(localProfiles))
            {
                var dirInfo = new DirectoryInfo(localProfiles);
                foreach (var profile in dirInfo.EnumerateDirectories())
                {
                    freed += CleanDirectorySafe(Path.Combine(profile.FullName, "cache2"));
                    freed += CleanDirectorySafe(Path.Combine(profile.FullName, "startupCache"));
                    freed += CleanDirectorySafe(Path.Combine(profile.FullName, "shader-cache"));
                    freed += CleanDirectorySafe(Path.Combine(profile.FullName, "jumpListCache"));
                    freed += CleanDirectorySafe(Path.Combine(profile.FullName, "thumbnails"));
                }
            }

            string roamMozilla = Path.Combine(appData, @"Mozilla\Firefox");
            if (Directory.Exists(roamMozilla))
            {
                freed += CleanDirectorySafe(Path.Combine(roamMozilla, "Crash Reports"));
                string roamProfiles = Path.Combine(roamMozilla, "Profiles");
                if (Directory.Exists(roamProfiles))
                {
                    var dirInfo = new DirectoryInfo(roamProfiles);
                    foreach (var profile in dirInfo.EnumerateDirectories())
                    {
                        freed += CleanDirectorySafe(Path.Combine(profile.FullName, "thumbnails"));
                        // Not: Yer imleri (places.sqlite), oturum kurtarma (sessionstore*) ve formhistory burada bilerek KORUNUR!
                    }
                }
            }
            return freed;
        }

        #region Tarayıcı Gezinti ve İndirme Geçmişi Temizliği Yardımcıları
        private static long ScanChromiumHistory(string userDataDir)
        {
            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
                return 0;

            long total = 0;
            try
            {
                var dirInfo = new DirectoryInfo(userDataDir);
                foreach (var profileDir in dirInfo.EnumerateDirectories())
                {
                    if (profileDir.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("System Profile", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase))
                    {
                        string pPath = profileDir.FullName;
                        total += GetFileSizeSafe(Path.Combine(pPath, "History"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "History-journal"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Visited Links"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Shortcuts"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Shortcuts-journal"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Top Sites"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Top Sites-journal"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Network Action Predictor"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Network Action Predictor-journal"));
                    }
                }
            }
            catch { }
            return total;
        }

        private static long CleanChromiumHistory(string userDataDir)
        {
            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
                return 0;

            long freed = 0;
            try
            {
                var dirInfo = new DirectoryInfo(userDataDir);
                foreach (var profileDir in dirInfo.EnumerateDirectories())
                {
                    if (profileDir.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("System Profile", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase))
                    {
                        string pPath = profileDir.FullName;
                        freed += DeleteFileSafe(Path.Combine(pPath, "History"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "History-journal"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Visited Links"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Shortcuts"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Shortcuts-journal"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Top Sites"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Top Sites-journal"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Network Action Predictor"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Network Action Predictor-journal"));
                    }
                }
            }
            catch { }
            return freed;
        }

        private static long ScanBrowserHistory(string localAppData, string appData)
        {
            long total = 0;

            // 1. Chromium Tabanlı: Chrome, Edge, Brave
            string[] chromiumUserDataPaths = {
                Path.Combine(localAppData, @"Google\Chrome\User Data"),
                Path.Combine(localAppData, @"Microsoft\Edge\User Data"),
                Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data")
            };

            foreach (var userData in chromiumUserDataPaths)
            {
                total += ScanChromiumHistory(userData);
            }

            // 2. Opera & Opera GX (Local ve Roaming)
            string[] operaProfiles = { "Opera GX Stable", "Opera Stable" };
            foreach (var prof in operaProfiles)
            {
                total += ScanChromiumHistory(Path.Combine(localAppData, @"Opera Software", prof));
                total += ScanChromiumHistory(Path.Combine(appData, @"Opera Software", prof));
            }

            // 3. Firefox: Arama kutusu ve form geçmişi (Not: places.sqlite yer imlerini barındırdığı için bilerek taranmaz/silinmez)
            string roamMozilla = Path.Combine(appData, @"Mozilla\Firefox");
            if (Directory.Exists(roamMozilla))
            {
                string roamProfiles = Path.Combine(roamMozilla, "Profiles");
                if (Directory.Exists(roamProfiles))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(roamProfiles);
                        foreach (var profile in dirInfo.EnumerateDirectories())
                        {
                            total += GetFileSizeSafe(Path.Combine(profile.FullName, "formhistory.sqlite"));
                        }
                    }
                    catch { }
                }
            }

            return total;
        }

        private static long CleanBrowserHistory(string localAppData, string appData)
        {
            long freed = 0;

            // 1. Chromium Tabanlı: Chrome, Edge, Brave
            string[] chromiumUserDataPaths = {
                Path.Combine(localAppData, @"Google\Chrome\User Data"),
                Path.Combine(localAppData, @"Microsoft\Edge\User Data"),
                Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data")
            };

            foreach (var userData in chromiumUserDataPaths)
            {
                freed += CleanChromiumHistory(userData);
            }

            // 2. Opera & Opera GX (Local ve Roaming)
            string[] operaProfiles = { "Opera GX Stable", "Opera Stable" };
            foreach (var prof in operaProfiles)
            {
                freed += CleanChromiumHistory(Path.Combine(localAppData, @"Opera Software", prof));
                freed += CleanChromiumHistory(Path.Combine(appData, @"Opera Software", prof));
            }

            // 3. Firefox: Arama kutusu ve form geçmişi (Not: places.sqlite yer imlerini barındırdığı için bilerek silinmez)
            string roamMozilla = Path.Combine(appData, @"Mozilla\Firefox");
            if (Directory.Exists(roamMozilla))
            {
                string roamProfiles = Path.Combine(roamMozilla, "Profiles");
                if (Directory.Exists(roamProfiles))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(roamProfiles);
                        foreach (var profileDir in dirInfo.EnumerateDirectories())
                        {
                            freed += DeleteFileSafe(Path.Combine(profileDir.FullName, "formhistory.sqlite"));
                        }
                    }
                    catch { }
                }
            }

            return freed;
        }
        #endregion

        #region Tarayıcı Çerezler ve Kayıtlı Şifreler Temizliği Yardımcıları
        private static long ScanBrowserCookiesAndPasswords(string localAppData, string appData)
        {
            long total = 0;

            // 1. Chromium Tabanlı: Chrome, Edge, Brave
            string[] chromiumUserDataPaths = {
                Path.Combine(localAppData, @"Google\Chrome\User Data"),
                Path.Combine(localAppData, @"Microsoft\Edge\User Data"),
                Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data")
            };

            foreach (var userData in chromiumUserDataPaths)
            {
                total += ScanChromiumCookiesAndPasswords(userData);
            }

            // 2. Opera & Opera GX (Local ve Roaming)
            string[] operaProfiles = { "Opera GX Stable", "Opera Stable" };
            foreach (var prof in operaProfiles)
            {
                total += ScanChromiumCookiesAndPasswords(Path.Combine(localAppData, @"Opera Software", prof));
                total += ScanChromiumCookiesAndPasswords(Path.Combine(appData, @"Opera Software", prof));
            }

            // 3. Mozilla Firefox
            total += ScanFirefoxCookiesAndPasswords(appData);

            return total;
        }

        private static long CleanBrowserCookiesAndPasswords(string localAppData, string appData)
        {
            long freed = 0;

            // 1. Chromium Tabanlı: Chrome, Edge, Brave
            string[] chromiumUserDataPaths = {
                Path.Combine(localAppData, @"Google\Chrome\User Data"),
                Path.Combine(localAppData, @"Microsoft\Edge\User Data"),
                Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data")
            };

            foreach (var userData in chromiumUserDataPaths)
            {
                freed += CleanChromiumCookiesAndPasswords(userData);
            }

            // 2. Opera & Opera GX
            string[] operaProfiles = { "Opera GX Stable", "Opera Stable" };
            foreach (var prof in operaProfiles)
            {
                freed += CleanChromiumCookiesAndPasswords(Path.Combine(localAppData, @"Opera Software", prof));
                freed += CleanChromiumCookiesAndPasswords(Path.Combine(appData, @"Opera Software", prof));
            }

            // 3. Mozilla Firefox
            freed += CleanFirefoxCookiesAndPasswords(appData);

            return freed;
        }

        private static long ScanChromiumCookiesAndPasswords(string userDataDir)
        {
            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
                return 0;

            long total = 0;
            try
            {
                var dirInfo = new DirectoryInfo(userDataDir);
                foreach (var profileDir in dirInfo.EnumerateDirectories())
                {
                    if (profileDir.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                    {
                        string pPath = profileDir.FullName;

                        // Çerezler (Cookies) & Trust Tokens
                        total += GetFileSizeSafe(Path.Combine(pPath, @"Network\Cookies"));
                        total += GetFileSizeSafe(Path.Combine(pPath, @"Network\Cookies-journal"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Cookies"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Cookies-journal"));
                        total += GetFileSizeSafe(Path.Combine(pPath, @"Network\Trust Tokens"));
                        total += GetFileSizeSafe(Path.Combine(pPath, @"Network\Trust Tokens-journal"));

                        // Kayıtlı Şifreler (Login Data)
                        total += GetFileSizeSafe(Path.Combine(pPath, "Login Data"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Login Data-journal"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Login Data For Account"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Login Data For Account-journal"));

                        // Otomatik Doldurma (Web Data & Autofill)
                        total += GetFileSizeSafe(Path.Combine(pPath, "Web Data"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Web Data-journal"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "AutofillStrikeDatabase"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "AutofillStrikeDatabase"));

                        // Site Ayarları / İzinleri / Oturum Depolaması
                        total += GetFileSizeSafe(Path.Combine(pPath, "siteprefs.json"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "DIPS"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "DIPS-journal"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Affiliation Database"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "Affiliation Database-journal"));
                        total += CalculateDirectorySize(Path.Combine(pPath, @"Local Storage\leveldb"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "IndexedDB"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "Session Storage"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "databases"));
                        total += CalculateDirectorySize(Path.Combine(pPath, "blob_storage"));
                    }
                }
            }
            catch { }
            return total;
        }

        private static long CleanChromiumCookiesAndPasswords(string userDataDir)
        {
            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
                return 0;

            long freed = 0;
            try
            {
                var dirInfo = new DirectoryInfo(userDataDir);
                foreach (var profileDir in dirInfo.EnumerateDirectories())
                {
                    if (profileDir.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        profileDir.Name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                    {
                        string pPath = profileDir.FullName;

                        // Çerezler (Cookies) & Trust Tokens
                        freed += DeleteFileSafe(Path.Combine(pPath, @"Network\Cookies"));
                        freed += DeleteFileSafe(Path.Combine(pPath, @"Network\Cookies-journal"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Cookies"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Cookies-journal"));
                        freed += DeleteFileSafe(Path.Combine(pPath, @"Network\Trust Tokens"));
                        freed += DeleteFileSafe(Path.Combine(pPath, @"Network\Trust Tokens-journal"));

                        // Kayıtlı Şifreler (Login Data)
                        freed += DeleteFileSafe(Path.Combine(pPath, "Login Data"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Login Data-journal"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Login Data For Account"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Login Data For Account-journal"));

                        // Otomatik Doldurma (Web Data & Autofill)
                        freed += DeleteFileSafe(Path.Combine(pPath, "Web Data"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Web Data-journal"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "AutofillStrikeDatabase"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "AutofillStrikeDatabase"));

                        // Site Ayarları / İzinleri / Oturum Depolaması
                        freed += DeleteFileSafe(Path.Combine(pPath, "siteprefs.json"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "DIPS"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "DIPS-journal"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Affiliation Database"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "Affiliation Database-journal"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, @"Local Storage\leveldb"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "IndexedDB"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "Session Storage"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "databases"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, "blob_storage"));
                    }
                }
            }
            catch { }
            return freed;
        }

        private static long ScanFirefoxCookiesAndPasswords(string appData)
        {
            string roamMozilla = Path.Combine(appData, @"Mozilla\Firefox");
            if (!Directory.Exists(roamMozilla)) return 0;

            long total = 0;
            try
            {
                string roamProfiles = Path.Combine(roamMozilla, "Profiles");
                if (Directory.Exists(roamProfiles))
                {
                    var dirInfo = new DirectoryInfo(roamProfiles);
                    foreach (var profile in dirInfo.EnumerateDirectories())
                    {
                        string pPath = profile.FullName;
                        total += GetFileSizeSafe(Path.Combine(pPath, "cookies.sqlite"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "cookies.sqlite-wal"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "cookies.sqlite-shm"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "logins.json"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "logins-backup.json"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "key4.db"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "formhistory.sqlite"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "autofill-profiles.json"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "permissions.sqlite"));
                        total += GetFileSizeSafe(Path.Combine(pPath, "content-prefs.sqlite"));
                        total += CalculateDirectorySize(Path.Combine(pPath, @"storage\default"));
                    }
                }
            }
            catch { }
            return total;
        }

        private static long CleanFirefoxCookiesAndPasswords(string appData)
        {
            string roamMozilla = Path.Combine(appData, @"Mozilla\Firefox");
            if (!Directory.Exists(roamMozilla)) return 0;

            long freed = 0;
            try
            {
                string roamProfiles = Path.Combine(roamMozilla, "Profiles");
                if (Directory.Exists(roamProfiles))
                {
                    var dirInfo = new DirectoryInfo(roamProfiles);
                    foreach (var profile in dirInfo.EnumerateDirectories())
                    {
                        string pPath = profile.FullName;
                        freed += DeleteFileSafe(Path.Combine(pPath, "cookies.sqlite"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "cookies.sqlite-wal"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "cookies.sqlite-shm"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "logins.json"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "logins-backup.json"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "key4.db"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "formhistory.sqlite"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "autofill-profiles.json"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "permissions.sqlite"));
                        freed += DeleteFileSafe(Path.Combine(pPath, "content-prefs.sqlite"));
                        freed += CleanDirectorySafe(Path.Combine(pPath, @"storage\default"));
                    }
                }
            }
            catch { }
            return freed;
        }
        #endregion
        #endregion
        #endregion

        #region Windows Güncelleme Artıkları Yardımcıları
        private static void EnsureDeliveryOptimizationAccess()
        {
            try
            {
                string path = @"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization";
                if (Directory.Exists(path))
                {
                    TakeOwnershipSafe(path, 3000);
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "icacls.exe",
                        Arguments = $"\"{path}\" /grant \"*S-1-5-32-544:(OI)(CI)F\" /Q",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit(1500);
                }
            }
            catch { }
        }

        private static long GetDeliveryOptimizationSize()
        {
            EnsureDeliveryOptimizationAccess();
            long size = CalculateDirectorySize(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization");
            size += CalculateDirectorySize(@"C:\ProgramData\Microsoft\Windows\DeliveryOptimization");
            return size;
        }

        private static long CleanSoftwareDistribution()
        {
            long freed = 0;
            string downloadPath = @"C:\Windows\SoftwareDistribution\Download";
            if (!Directory.Exists(downloadPath)) return 0;

            bool stoppedService = false;
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "net.exe",
                    Arguments = "stop wuauserv /y",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p?.WaitForExit(5000);
                stoppedService = true;
            }
            catch { }

            try
            {
                freed = CleanDirectorySafe(downloadPath);
            }
            finally
            {
                if (stoppedService)
                {
                    try
                    {
                        using var p = Process.Start(new ProcessStartInfo
                        {
                            FileName = "net.exe",
                            Arguments = "start wuauserv",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        p?.WaitForExit(3000);
                    }
                    catch { }
                }
            }

            return freed;
        }

        private static long CleanDeliveryOptimization()
        {
            EnsureDeliveryOptimizationAccess();
            long sizeBefore = GetDeliveryOptimizationSize();

            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -Command \"Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p?.WaitForExit(3000);
            }
            catch { }

            CleanDirectorySafe(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache");
            CleanDirectorySafe(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization");
            CleanDirectorySafe(@"C:\ProgramData\Microsoft\Windows\DeliveryOptimization");

            long sizeAfter = GetDeliveryOptimizationSize();
            return Math.Max(0, sizeBefore - sizeAfter);
        }

        private static void TakeOwnershipSafe(string path, int timeoutMs = 15000)
        {
            try
            {
                if (Directory.Exists(path) || File.Exists(path))
                {
                    using var p1 = Process.Start(new ProcessStartInfo
                    {
                        FileName = "takeown.exe",
                        Arguments = $"/F \"{path}\" /A /R /D Y",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p1?.WaitForExit(timeoutMs);

                    using var p2 = Process.Start(new ProcessStartInfo
                    {
                        FileName = "icacls.exe",
                        Arguments = $"\"{path}\" /grant \"*S-1-5-32-544:(OI)(CI)F\" /T /Q",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p2?.WaitForExit(timeoutMs);
                }
            }
            catch { }
        }

        private static long CleanWindowsOld()
        {
            long freed = 0;
            string[] targets = { @"C:\Windows.old", @"C:\$WINDOWS.~BT", @"C:\$WINDOWS.~WS" };
            foreach (var target in targets)
            {
                if (Directory.Exists(target))
                {
                    TakeOwnershipSafe(target);
                    freed += CleanDirectorySafe(target);
                    try { Directory.Delete(target, true); } catch { }
                }
            }
            return freed;
        }

        private static long CleanWinSxS()
        {
            long freed = 0;

            // 1. WinSxS Temp altındaki PendingDeletes ve ManifestCache temizliği
            if (Directory.Exists(@"C:\Windows\WinSxS\Temp\PendingDeletes"))
            {
                TakeOwnershipSafe(@"C:\Windows\WinSxS\Temp\PendingDeletes", 5000);
                freed += CleanDirectorySafe(@"C:\Windows\WinSxS\Temp\PendingDeletes");
            }
            freed += CleanDirectorySafe(@"C:\Windows\WinSxS\ManifestCache");

            // 2. Windows'un resmi arka plan Servicing temizlik görevini tetikle (TrustedInstaller yetkisiyle temizler)
            try
            {
                using var taskProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/run /tn \"\\Microsoft\\Windows\\Servicing\\StartComponentCleanup\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                taskProc?.WaitForExit(3000);
            }
            catch { }

            return freed;
        }

        private static long CleanInstallerCache()
        {
            long freed = 0;
            freed += CleanDirectorySafe(@"C:\Windows\Installer\$PatchCache$");
            freed += CleanDirectoryFilesMatching(@"C:\Windows\Installer", "*.tmp");
            freed += CleanDirectoryFilesMatching(@"C:\Windows\Installer", "*.log");
            freed += CleanDirectorySafe(@"C:\Config.Msi");
            freed += CleanDirectorySafe(@"C:\Windows\Installer\Config.Msi");
            freed += CleanDirectorySafe(Path.Combine(Path.GetTempPath(), "Config.Msi"));
            return freed;
        }
        #endregion

        #region Sistem Geçmişi & Loglar Yardımcıları
        private static readonly string[] ExcludedSystemLogsSubDirs = new[]
        {
            "CBS", "DISM", "WindowsUpdate", "Setup", "MoSetup"
        };

        private static long CalculateSystemLogsSize()
        {
            long total = CalculateDirectorySize(@"C:\Windows\System32\LogFiles") +
                         CalculateDirectorySize(@"C:\Windows\Debug") +
                         GetDirectoryFilesSizeMatching(@"C:\Windows", "*.log");

            string logsDir = @"C:\Windows\Logs";
            if (Directory.Exists(logsDir))
            {
                try
                {
                    var di = new DirectoryInfo(logsDir);
                    foreach (var fi in di.EnumerateFiles())
                    {
                        try { total += fi.Length; } catch { }
                    }
                    foreach (var sub in di.EnumerateDirectories())
                    {
                        if (!ExcludedSystemLogsSubDirs.Any(ex => string.Equals(ex, sub.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            total += CalculateDirectorySize(sub.FullName);
                        }
                    }
                }
                catch { }
            }
            return total;
        }

        private static long CleanSystemLogs()
        {
            long freed = 0;
            freed += CleanDirectorySafe(@"C:\Windows\System32\LogFiles");
            freed += CleanDirectorySafe(@"C:\Windows\Debug");
            freed += CleanDirectoryFilesMatching(@"C:\Windows", "*.log");

            string logsDir = @"C:\Windows\Logs";
            if (Directory.Exists(logsDir))
            {
                try
                {
                    var di = new DirectoryInfo(logsDir);
                    foreach (var fi in di.EnumerateFiles())
                    {
                        freed += DeleteFileSafe(fi.FullName);
                    }
                    foreach (var sub in di.EnumerateDirectories())
                    {
                        if (!ExcludedSystemLogsSubDirs.Any(ex => string.Equals(ex, sub.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            freed += CleanDirectorySafe(sub.FullName);
                        }
                    }
                }
                catch { }
            }
            return freed;
        }

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        private static extern int DnsFlushResolverCache();

        public static int GetDnsRecordCount()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig.exe",
                    Arguments = "/displaydns",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return 0;
                int count = 0;
                string? line;
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    if (line.Contains("Record Name", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Kayıt Adı", StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                    }
                }
                p.WaitForExit(1000);
                return count;
            }
            catch
            {
                return 0;
            }
        }

        private static long CleanDnsCache()
        {
            try
            {
                DnsFlushResolverCache();
            }
            catch { }

            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "ipconfig.exe",
                    Arguments = "/flushdns",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p?.WaitForExit(2000);
            }
            catch { }

            return 0L;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        private static long CleanClipboardHistory(string localAppData)
        {
            long freed = 0;

            // 1. Windows Pano belleğini Win32 API ile boşalt
            try
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    EmptyClipboard();
                    CloseClipboard();
                }
            }
            catch { }

            // 2. Windows 10/11 Pano Geçmişi (Win+V) dosyalarını temizle
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Windows\Clipboard"));
            freed += CleanDirectorySafe(Path.Combine(localAppData, "ConnectedDevicesPlatform"));

            // 3. cbdhsvc (Clipboard User Service) önbelleğini sıfırla
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -Command \"Get-Service cbdhsvc* -ErrorAction SilentlyContinue | Restart-Service -Force -ErrorAction SilentlyContinue\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p?.WaitForExit(2000);
            }
            catch { }

            return freed;
        }

        private static long CleanRdpHistory(string localAppData)
        {
            long freed = 0;

            // 1. RDP Bitmap Cache temizliği
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Terminal Server Client\Cache"));

            // 2. Belgelerim altındaki Default.rdp gizli bağlantı dosyasını sil
            string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            freed += DeleteFileSafe(Path.Combine(docsPath, "Default.rdp"));

            // 3. Kayıt Defteri (Registry) RDP sunucu bağlantı geçmişini temizle
            try
            {
                using var defaultKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Terminal Server Client\Default", true);
                if (defaultKey != null)
                {
                    foreach (var val in defaultKey.GetValueNames())
                    {
                        try { defaultKey.DeleteValue(val); } catch { }
                    }
                }
            }
            catch { }

            try
            {
                using var serversKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Terminal Server Client\Servers", true);
                if (serversKey != null)
                {
                    foreach (var sub in serversKey.GetSubKeyNames())
                    {
                        try { serversKey.DeleteSubKeyTree(sub); } catch { }
                    }
                }
            }
            catch { }

            return freed;
        }

        private static void EnsureCscAccess()
        {
            try
            {
                string path = @"C:\Windows\CSC";
                if (Directory.Exists(path))
                {
                    TakeOwnershipSafe(path, 3000);
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "icacls.exe",
                        Arguments = $"\"{path}\" /grant \"*S-1-5-32-544:(OI)(CI)F\" /Q",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit(1500);
                }
            }
            catch { }
        }

        private static long GetNetCacheSize(string localAppData)
        {
            EnsureCscAccess();
            long size = CalculateDirectorySize(@"C:\Windows\CSC");
            size += CalculateDirectorySize(Path.Combine(localAppData, @"Microsoft\Windows\Network Shortcuts"));
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            size += CalculateDirectorySize(Path.Combine(appData, @"Microsoft\Windows\Network Shortcuts"));
            return size;
        }

        private static long CleanNetCache(string localAppData)
        {
            EnsureCscAccess();
            long freed = CleanDirectorySafe(@"C:\Windows\CSC");
            freed += CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Windows\Network Shortcuts"));
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            freed += CleanDirectorySafe(Path.Combine(appData, @"Microsoft\Windows\Network Shortcuts"));
            return freed;
        }

        private static long GetWindowsEventLogsSize()
        {
            long total = 0;
            string winevtDir = @"C:\Windows\System32\winevt\Logs";
            if (!Directory.Exists(winevtDir)) return 0;
            try
            {
                var di = new DirectoryInfo(winevtDir);
                foreach (var fi in di.EnumerateFiles("*.evtx"))
                {
                    total += GetEvtxCleanableBytes(fi.FullName);
                }
            }
            catch { }
            return total;
        }

        private static long CleanWindowsEventLogs()
        {
            long before = GetWindowsEventLogsSize();

            // 1. .NET EventLogSession ile tüm kayıtlı olay günlüklerini temizle
            try
            {
                var session = EventLogSession.GlobalSession;
                foreach (var logName in session.GetLogNames())
                {
                    try
                    {
                        session.ClearLog(logName);
                    }
                    catch
                    {
                        // Kilitli veya özel izin gerektiren loglar yoksayılır
                    }
                }
            }
            catch { }

            // 2. wevtutil ile ana Windows günlüklerini de doğrudan temizle
            string[] coreLogs = { "Application", "System", "Setup", "Security" };
            foreach (var log in coreLogs)
            {
                try
                {
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "wevtutil.exe",
                        Arguments = $"cl \"{log}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit(2000);
                }
                catch { }
            }

            long after = GetWindowsEventLogsSize();
            return Math.Max(0, before - after);
        }
        #endregion

        #region Donanım & Sürücü Artıkları Yardımcıları
        private static long GetDriverStoreLeftoversSize()
        {
            return CalculateDirectorySize(@"C:\Windows\System32\DriverStore\Temp");
        }

        private static long CleanUnusedDriverPackages()
        {
            TakeOwnershipSafe(@"C:\Windows\System32\DriverStore\Temp", 5000);
            long freed = CleanDirectorySafe(@"C:\Windows\System32\DriverStore\Temp");

            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = "/enum-drivers",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });

                if (p != null)
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);

                    var matches = System.Text.RegularExpressions.Regex.Matches(output, @"oem\d+\.inf", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        string oemInf = m.Value;
                        try
                        {
                            using var dp = Process.Start(new ProcessStartInfo
                            {
                                FileName = "pnputil.exe",
                                Arguments = $"/delete-driver {oemInf}",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            });
                            dp?.WaitForExit(2000);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return freed;
        }

        private static long CleanInfSetupLogs()
        {
            long freed = 0;
            freed += CleanDirectoryFilesMatching(@"C:\Windows\INF", "*.log");
            freed += CleanDirectoryFilesMatching(@"C:\Windows\INF", "*.bak");
            return freed;
        }

        private static void EnsureSpoolAccess()
        {
            try
            {
                string path = @"C:\Windows\System32\spool\PRINTERS";
                if (Directory.Exists(path))
                {
                    TakeOwnershipSafe(path, 3000);
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "icacls.exe",
                        Arguments = $"\"{path}\" /grant \"*S-1-5-32-544:(OI)(CI)F\" /Q",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit(1500);
                }
            }
            catch { }
        }

        private static long GetPrintSpoolerSize()
        {
            EnsureSpoolAccess();
            long size = CalculateDirectorySize(@"C:\Windows\System32\spool\PRINTERS");
            size += CalculateDirectorySize(@"C:\Windows\System32\spool\SERVERS");
            return size;
        }

        private static long CleanPrintSpooler()
        {
            EnsureSpoolAccess();
            long freed = 0;
            bool stoppedSpooler = false;
            try
            {
                using (var stopProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "net.exe",
                    Arguments = "stop spooler /y",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    stopProc?.WaitForExit(3000);
                    stoppedSpooler = true;
                }

                freed += CleanDirectorySafe(@"C:\Windows\System32\spool\PRINTERS");
                freed += CleanDirectorySafe(@"C:\Windows\System32\spool\SERVERS");
            }
            catch
            {
                freed += CleanDirectorySafe(@"C:\Windows\System32\spool\PRINTERS");
                freed += CleanDirectorySafe(@"C:\Windows\System32\spool\SERVERS");
            }
            finally
            {
                if (stoppedSpooler)
                {
                    try
                    {
                        using var startProc = Process.Start(new ProcessStartInfo
                        {
                            FileName = "net.exe",
                            Arguments = "start spooler",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        startProc?.WaitForExit(3000);
                    }
                    catch { }
                }
            }

            return freed;
        }

        private static string GetMpCmdRunPath()
        {
            try
            {
                string platformDir = @"C:\ProgramData\Microsoft\Windows Defender\Platform";
                if (Directory.Exists(platformDir))
                {
                    var latest = Directory.GetDirectories(platformDir)
                        .OrderByDescending(d => d)
                        .FirstOrDefault();
                    if (latest != null)
                    {
                        string p = Path.Combine(latest, "MpCmdRun.exe");
                        if (File.Exists(p)) return p;
                    }
                }
                string standard = @"C:\Program Files\Windows Defender\MpCmdRun.exe";
                if (File.Exists(standard)) return standard;
            }
            catch { }
            return "MpCmdRun.exe";
        }

        private static long GetDefenderCacheSize()
        {
            long size = 0;
            try
            {
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string defenderBase = Path.Combine(progData, @"Microsoft\Windows Defender");

                // Yalnızca geçici destek günlükleri, hata dökümleri ve analiz önbellekleri taranır.
                // Aktif virüs tanımları (Definition Updates) KESİNLİKLE taranmaz ve silinmez.
                string[] cachePaths = new[]
                {
                    Path.Combine(defenderBase, @"Support"),
                    Path.Combine(defenderBase, @"LocalCopy"),
                    Path.Combine(defenderBase, @"Scans\History\Store"),
                    Path.Combine(defenderBase, @"Scans\History\Results\Quick"),
                    Path.Combine(defenderBase, @"Scans\History\Results\Resource")
                };

                foreach (var p in cachePaths)
                {
                    size += CalculateDirectorySize(p);
                }
            }
            catch { }
            return size;
        }

        private static long CleanDefenderCache()
        {
            long freed = 0;
            try
            {
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string defenderBase = Path.Combine(progData, @"Microsoft\Windows Defender");

                // Sadece güvenli geçici destek günlükleri ve tarama dökümleri temizlenir.
                // Virüs tanımları (Definition Updates) ve dinamik imzalar KESİNLİKLE silinmez veya geri alınmaz.
                string[] cachePaths = new[]
                {
                    Path.Combine(defenderBase, @"Support"),
                    Path.Combine(defenderBase, @"LocalCopy"),
                    Path.Combine(defenderBase, @"Scans\History\Store"),
                    Path.Combine(defenderBase, @"Scans\History\Results\Quick"),
                    Path.Combine(defenderBase, @"Scans\History\Results\Resource")
                };

                foreach (var p in cachePaths)
                {
                    freed += CleanDirectorySafe(p);
                }
            }
            catch { }

            return freed;
        }

        private static long GetGpuSetupCacheSize()
        {
            long size = 0;
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string[] paths = new[]
            {
                @"C:\NVIDIA",
                @"C:\AMD",
                @"C:\Intel",
                @"C:\Drivers",
                Path.Combine(progData, @"NVIDIA Corporation\Downloader"),
                Path.Combine(progData, @"NVIDIA Corporation\Installer2"),
                Path.Combine(progData, @"NVIDIA Corporation\NetService"),
                Path.Combine(progData, @"AMD\Packages")
            };

            foreach (var p in paths)
            {
                size += CalculateDirectorySize(p);
            }
            return size;
        }

        private static long CleanGpuSetupCache()
        {
            long freed = 0;
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string[] paths = new[]
            {
                @"C:\NVIDIA",
                @"C:\AMD",
                @"C:\Intel",
                @"C:\Drivers",
                Path.Combine(progData, @"NVIDIA Corporation\Downloader"),
                Path.Combine(progData, @"NVIDIA Corporation\Installer2"),
                Path.Combine(progData, @"NVIDIA Corporation\NetService"),
                Path.Combine(progData, @"AMD\Packages")
            };

            foreach (var p in paths)
            {
                freed += CleanDirectorySafe(p);
            }
            return freed;
        }
        #endregion

        #region Chkdsk & Geri Yükleme Noktası Yardımcıları
        private static long GetChkdskArtifactsSize()
        {
            long total = 0;
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        string root = drive.RootDirectory.FullName;
                        for (int i = 0; i <= 99; i++)
                        {
                            string foundDir = Path.Combine(root, $"FOUND.{i:D3}");
                            if (Directory.Exists(foundDir))
                            {
                                TakeOwnershipSafe(foundDir, 2000);
                                total += CalculateDirectorySize(foundDir);
                            }
                        }
                    }
                }
            }
            catch { }
            return total;
        }

        private static long CleanChkdskArtifacts()
        {
            long freed = 0;
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        string root = drive.RootDirectory.FullName;
                        for (int i = 0; i <= 99; i++)
                        {
                            string foundDir = Path.Combine(root, $"FOUND.{i:D3}");
                            if (Directory.Exists(foundDir))
                            {
                                TakeOwnershipSafe(foundDir, 3000);
                                freed += CleanDirectorySafe(foundDir);
                                try { Directory.Delete(foundDir, true); } catch { }
                            }
                        }
                    }
                }
            }
            catch { }
            return freed;
        }

        public static (int Count, long OldBytes) GetOldRestorePointsInfo()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "vssadmin.exe",
                    Arguments = "list shadows /for=C:",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                if (p == null) return (0, 0);

                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);

                var matches = System.Text.RegularExpressions.Regex.Matches(output, @"Shadow Copy (ID|Volume):", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                int totalShadows = matches.Count;

                if (totalShadows <= 1)
                {
                    return (totalShadows, 0L);
                }

                long usedBytes = GetVssUsedStorageBytes();
                long oldBytes = (long)(usedBytes * ((double)(totalShadows - 1) / totalShadows));
                return (totalShadows, Math.Max(oldBytes, 1024 * 1024));
            }
            catch
            {
                return (0, 0);
            }
        }

        private static long GetVssUsedStorageBytes()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "vssadmin.exe",
                    Arguments = "list shadowstorage",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                if (p == null) return 0;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);

                var match = System.Text.RegularExpressions.Regex.Match(output, @":\s*([0-9.,]+)\s*(KB|MB|GB|TB)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string numStr = match.Groups[1].Value.Replace(',', '.');
                    string unit = match.Groups[2].Value.ToUpperInvariant();
                    if (double.TryParse(numStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
                    {
                        return unit switch
                        {
                            "TB" => (long)(num * 1024 * 1024 * 1024 * 1024),
                            "GB" => (long)(num * 1024 * 1024 * 1024),
                            "MB" => (long)(num * 1024 * 1024),
                            "KB" => (long)(num * 1024),
                            _ => (long)num
                        };
                    }
                }
            }
            catch { }
            return 0;
        }

        private static long CleanOldRestorePoints()
        {
            try
            {
                var (count, oldBytes) = GetOldRestorePointsInfo();
                if (count <= 1) return 0;

                long beforeVss = GetVssUsedStorageBytes();
                bool anySuccess = false;

                for (int i = 0; i < count - 1; i++)
                {
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "vssadmin.exe",
                        Arguments = "delete shadows /for=C: /oldest /quiet",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit(5000);
                    if (p != null && p.ExitCode == 0)
                    {
                        anySuccess = true;
                    }
                }

                if (!anySuccess) return 0;

                long afterVss = GetVssUsedStorageBytes();
                long actualFreed = Math.Max(0, beforeVss - afterVss);
                return actualFreed > 0 ? actualFreed : oldBytes;
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        #region Kayıt Defteri Yedekleri Temizliği (En Son Güncel Yedek Hariç)
        public static (int TotalFiles, int OldFiles, long OldBytes) GetRegistryBackupsInfo()
        {
            try
            {
                var allFiles = GetAllRegistryBackupFiles();
                if (allFiles.Count <= 1)
                {
                    return (allFiles.Count, 0, 0L);
                }

                // En yeniden en eskiye sırala
                var sorted = allFiles.OrderByDescending(f => f.LastWriteTimeUtc).ToList();
                var latestFile = sorted[0];

                // Aynı yedek oturumunda (10 saniye farkla, örn. hem HKCU hem HKLM alınan) dosyaları da en son yedeğin parçası say
                var latestThreshold = latestFile.LastWriteTimeUtc.AddSeconds(-10);

                var filesToDelete = sorted
                    .Where(f => f.FullName != latestFile.FullName && f.LastWriteTimeUtc < latestThreshold)
                    .ToList();

                if (filesToDelete.Count == 0 && sorted.Count > 1)
                {
                    filesToDelete = sorted.Skip(1).ToList();
                }

                long totalOldBytes = 0;
                foreach (var f in filesToDelete)
                {
                    try { totalOldBytes += f.Length; } catch { }
                }

                return (allFiles.Count, filesToDelete.Count, totalOldBytes);
            }
            catch
            {
                return (0, 0, 0L);
            }
        }

        private static long GetRegistryBackupsSize()
        {
            return GetRegistryBackupsInfo().OldBytes;
        }

        private static long CleanRegistryBackups()
        {
            try
            {
                var allFiles = GetAllRegistryBackupFiles();
                if (allFiles.Count <= 1) return 0;

                var sorted = allFiles.OrderByDescending(f => f.LastWriteTimeUtc).ToList();
                var latestFile = sorted[0];
                var latestThreshold = latestFile.LastWriteTimeUtc.AddSeconds(-10);

                var filesToDelete = sorted
                    .Where(f => f.FullName != latestFile.FullName && f.LastWriteTimeUtc < latestThreshold)
                    .ToList();

                if (filesToDelete.Count == 0 && sorted.Count > 1)
                {
                    filesToDelete = sorted.Skip(1).ToList();
                }

                long freedBytes = 0;
                foreach (var f in filesToDelete)
                {
                    try
                    {
                        if (File.Exists(f.FullName))
                        {
                            long len = f.Length;
                            File.Delete(f.FullName);
                            freedBytes += len;
                            Debug.WriteLine($"[RegistryBackupClean] Eski yedek silindi: {f.FullName} ({len} bayt)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RegistryBackupClean] Silinemedi: {f.FullName} - {ex.Message}");
                    }
                }

                return freedBytes;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RegistryBackupClean] Genel Hata: {ex.Message}");
                return 0;
            }
        }

        private static List<FileInfo> GetAllRegistryBackupFiles()
        {
            var filesByPath = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
            var backupDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Bilinen kayıt defteri yedek dizinleri (PureWin, Windows ve yaygın sistem araçları)
            string[] candidateDirs = new[]
            {
                Path.Combine(localAppData, "PureWin", "Backups", "Registry"),
                Path.Combine(appData, "PureWin", "Backups", "Registry"),
                Path.Combine(progData, "PureWin", "Backups", "Registry"),
                Path.Combine(localAppData, "RegistryBackups"),
                Path.Combine(appData, "RegistryBackups"),
                Path.Combine(progData, "RegistryBackups"),
                Path.Combine(docs, "Registry Backups"),
                Path.Combine(docs, "RegistryBackups"),
                Path.Combine(docs, "RegBack"),
                Path.Combine(userProfile, "RegistryBackups"),
                Path.Combine(userProfile, "Documents", "Registry Backups"),
                Path.Combine(userProfile, "Documents", "RegistryBackups"),
                Path.Combine(localAppData, "Wise Registry Cleaner", "Backup"),
                Path.Combine(appData, "Wise Registry Cleaner", "Backup"),
                Path.Combine(progData, "Wise Registry Cleaner", "Backup"),
                Path.Combine(localAppData, "IObit", "Advanced SystemCare", "Backup"),
                Path.Combine(appData, "IObit", "Advanced SystemCare", "Backup"),
                Path.Combine(localAppData, "Auslogics", "Registry Cleaner", "Backups"),
                Path.Combine(appData, "Glarysoft", "RegistryRepair", "Backups"),
                Path.Combine(localAppData, "Eusing Free Registry Cleaner", "Backup"),
                Path.Combine(appData, "Eusing Free Registry Cleaner", "Backup"),
                @"C:\Windows\System32\config\RegBack"
            };

            foreach (var dir in candidateDirs)
            {
                if (Directory.Exists(dir))
                {
                    backupDirs.Add(dir);
                }
            }

            // AppData ve ProgramData altındaki özel "Registry\Backups" veya "RegistryBackups" klasörlerini dinamik ve güvenli tara
            string[] searchRoots = new[] { localAppData, appData, progData };
            foreach (var root in searchRoots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var sub in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            string name = Path.GetFileName(sub);
                            if (name.StartsWith(".") ||
                                name.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
                                name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                                name.Equals("Packages", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            string[] subChecks = new[]
                            {
                                Path.Combine(sub, "RegistryBackups"),
                                Path.Combine(sub, "Backups", "Registry"),
                                Path.Combine(sub, "Backup", "Registry")
                            };

                            foreach (var sc in subChecks)
                            {
                                if (Directory.Exists(sc))
                                {
                                    backupDirs.Add(sc);
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // Tespit edilen kayıt defteri yedek dizinlerindeki yedek dosyalarını topla (tam yol ile tekilleştirilmiş, ReparsePoint korumalı)
            foreach (var dir in backupDirs)
            {
                try
                {
                    var rootDi = new DirectoryInfo(dir);
                    if ((rootDi.Attributes & FileAttributes.ReparsePoint) != 0) continue;

                    var dirQueue = new Queue<string>();
                    dirQueue.Enqueue(dir);

                    while (dirQueue.Count > 0)
                    {
                        string curDir = dirQueue.Dequeue();
                        try
                        {
                            var curDi = new DirectoryInfo(curDir);
                            if ((curDi.Attributes & FileAttributes.ReparsePoint) != 0) continue;

                            foreach (var fi in curDi.EnumerateFiles())
                            {
                                try
                                {
                                    string ext = fi.Extension.ToLowerInvariant();
                                    // Sadece kayıt defteri yedek dosyaları (.reg, .bak, .hiv, .hivb) veya RegBack hive dosyaları
                                    if (ext == ".reg" || ext == ".bak" || ext == ".hiv" || ext == ".hivb" ||
                                        dir.EndsWith("RegBack", StringComparison.OrdinalIgnoreCase))
                                    {
                                        filesByPath[fi.FullName] = fi;
                                    }
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
                }
                catch { }
            }

            return filesByPath.Values.ToList();
        }
        #endregion

        #region Güvenli Dosya & Dizin Yardımcıları

        /// <summary>
        /// Windows Defender imza/tanım dosyalarını, çekirdek sistem klasörlerini ve sürücü köklerini
        /// kazara silinmeye karşı %100 koruyan aşılmaz güvenlik kalkanı.
        /// </summary>
        public static bool IsCriticalProtectedSystemPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;

            try
            {
                string norm = Path.GetFullPath(path).TrimEnd('\\');

                // 1. Windows Defender İmzaları ve Çekirdek Platform koruması
                if (norm.Contains(@"Microsoft\Windows Defender\Definition Updates", StringComparison.OrdinalIgnoreCase) ||
                    norm.Contains(@"Microsoft\Windows Defender\Platform", StringComparison.OrdinalIgnoreCase) ||
                    norm.EndsWith(@"Microsoft\Windows Defender", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // 2. Windows Kritik Çekirdek Klasörleri
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
                string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System).TrimEnd('\\');
                if (norm.Equals(winDir, StringComparison.OrdinalIgnoreCase) ||
                    norm.Equals(sys32, StringComparison.OrdinalIgnoreCase) ||
                    norm.StartsWith(Path.Combine(sys32, "drivers"), StringComparison.OrdinalIgnoreCase) ||
                    norm.StartsWith(Path.Combine(sys32, "config"), StringComparison.OrdinalIgnoreCase) ||
                    norm.StartsWith(Path.Combine(winDir, "Boot"), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // 3. Sürücü Kökleri (C:\, D:\ vb.)
                if (Path.GetPathRoot(norm)?.TrimEnd('\\').Equals(norm, StringComparison.OrdinalIgnoreCase) == true)
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

        public static long CalculateDirectorySize(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath) || IsCriticalProtectedSystemPath(dirPath))
                return 0;

            long total = 0;
            try
            {
                var rootDi = new DirectoryInfo(dirPath);
                if ((rootDi.Attributes & FileAttributes.ReparsePoint) != 0)
                    return 0; // Kök dizinin kendisi bir kavşak noktası (junction) ise asla takip etme!

                var queue = new Queue<string>();
                queue.Enqueue(dirPath);

                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    try
                    {
                        var di = new DirectoryInfo(current);
                        if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                            continue;

                        foreach (var fi in di.EnumerateFiles())
                        {
                            try { total += fi.Length; } catch { }
                        }

                        foreach (var sub in di.EnumerateDirectories())
                        {
                            if ((sub.Attributes & FileAttributes.ReparsePoint) == 0)
                            {
                                queue.Enqueue(sub.FullName);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return total;
        }

        public static long GetDirectoryFilesSizeMatching(string dirPath, string searchPattern)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath) || IsCriticalProtectedSystemPath(dirPath))
                return 0;

            long total = 0;
            try
            {
                var di = new DirectoryInfo(dirPath);
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                    return 0;

                foreach (var fi in di.EnumerateFiles(searchPattern, SearchOption.TopDirectoryOnly))
                {
                    try { total += fi.Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        public static long GetFileSizeSafe(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || IsCriticalProtectedSystemPath(filePath))
                return 0;

            try
            {
                return new FileInfo(filePath).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static int _skippedLockedFilesCount = 0;
        public static int SkippedLockedFilesCount => _skippedLockedFilesCount;
        public static void ResetSkippedCount() => Interlocked.Exchange(ref _skippedLockedFilesCount, 0);

        public static long DeleteFileSafe(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || IsCriticalProtectedSystemPath(filePath))
                return 0;

            try
            {
                var fi = new FileInfo(filePath);
                long len = fi.Length;
                if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                {
                    fi.Attributes = FileAttributes.Normal;
                }
                fi.Delete();
                return len;
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Interlocked.Increment(ref _skippedLockedFilesCount);
                }
                return 0;
            }
        }

        public static long CleanDirectorySafe(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath) || IsCriticalProtectedSystemPath(dirPath))
                return 0;

            long freed = 0;
            try
            {
                var rootDi = new DirectoryInfo(dirPath);
                if ((rootDi.Attributes & FileAttributes.ReparsePoint) != 0)
                    return 0; // Kök dizin junction ise içeriğini asla silme!

                var queue = new Queue<string>();
                queue.Enqueue(dirPath);
                var subDirsToDelete = new List<string>();

                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    try
                    {
                        var di = new DirectoryInfo(current);
                        if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                            continue;

                        foreach (var fi in di.EnumerateFiles())
                        {
                            var origAttr = fi.Attributes;
                            bool isReadOnly = (origAttr & FileAttributes.ReadOnly) != 0;
                            try
                            {
                                long len = fi.Length;
                                if (isReadOnly)
                                {
                                    fi.Attributes = FileAttributes.Normal;
                                }
                                fi.Delete();
                                freed += len;
                            }
                            catch (Exception ex)
                            {
                                if (ex is IOException || ex is UnauthorizedAccessException)
                                {
                                    Interlocked.Increment(ref _skippedLockedFilesCount);
                                }
                                if (isReadOnly)
                                {
                                    try { fi.Attributes = origAttr; } catch { }
                                }
                            }
                        }

                        foreach (var sub in di.EnumerateDirectories())
                        {
                            if ((sub.Attributes & FileAttributes.ReparsePoint) == 0)
                            {
                                queue.Enqueue(sub.FullName);
                                subDirsToDelete.Add(sub.FullName);
                            }
                        }
                    }
                    catch { }
                }

                subDirsToDelete.Reverse();
                foreach (var dPath in subDirsToDelete)
                {
                    try
                    {
                        var dInfo = new DirectoryInfo(dPath);
                        if (!dInfo.EnumerateFileSystemInfos().GetEnumerator().MoveNext())
                        {
                            if ((dInfo.Attributes & FileAttributes.ReadOnly) != 0)
                            {
                                dInfo.Attributes = FileAttributes.Normal;
                            }
                            dInfo.Delete();
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return freed;
        }

        public static long CleanDirectoryFilesMatching(string dirPath, string searchPattern)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath) || IsCriticalProtectedSystemPath(dirPath))
                return 0;

            long freed = 0;
            try
            {
                var di = new DirectoryInfo(dirPath);
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                    return 0;

                foreach (var fi in di.EnumerateFiles(searchPattern, SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        long len = fi.Length;
                        if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                        {
                            fi.Attributes = FileAttributes.Normal;
                        }
                        fi.Delete();
                        freed += len;
                    }
                    catch { }
                }
            }
            catch { }
            return freed;
        }
        #endregion
    }
}
