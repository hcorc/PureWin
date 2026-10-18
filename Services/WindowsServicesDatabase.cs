using System.Collections.Generic;
using PureWin.Models;

namespace PureWin.Services
{
    public static class WindowsServicesDatabase
    {
        public static List<ServiceItem> GetAllServices()
        {
            return new List<ServiceItem>
            {
                // 1. Telemetri & Tanılama (Güvenli)
                new() {
                    ServiceName = "DiagTrack",
                    DisplayName = "Bağlı Kullanıcı Deneyimleri ve Telemetrisi",
                    Description = "Windows kullanım, hata ve tanılama verilerini Microsoft sunucularına ileten ana telemetri hizmeti.",
                    IsSafe = true,
                    Category = "Telemetri & Tanılama",
                    OriginalStartupType = 2
                },
                new() {
                    ServiceName = "diagsvc",
                    DisplayName = "Diagnostic Execution Service",
                    Description = "Arka planda telemetri ve sistem tanılama eylemlerini yürüten hizmet.",
                    IsSafe = true,
                    Category = "Telemetri & Tanılama",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "diagnosticshub.standardcollector.service",
                    DisplayName = "Microsoft Diagnostics Hub Standart Toplayıcı",
                    Description = "Performans izleme ve tanılama verilerini toplayan hizmet.",
                    IsSafe = true,
                    Category = "Telemetri & Tanılama",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "WerSvc",
                    DisplayName = "Windows Hata Raporlama Hizmeti",
                    Description = "Uygulama çökmelerinde ve sistem hatalarında Microsoft'a günlük gönderilmesini sağlar.",
                    IsSafe = true,
                    Category = "Telemetri & Tanılama",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "wisvc",
                    DisplayName = "Windows Insider Hizmeti",
                    Description = "Windows Insider önizleme sürümlerinin sistem altyapısını ve geri bildirimlerini yönetir.",
                    IsSafe = true,
                    Category = "Telemetri & Tanılama",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "TroubleshootingSvc",
                    DisplayName = "Önerilen Sorun Giderme Hizmeti",
                    Description = "Windows'un arka planda otomatik sorun gidericileri çalıştırmasını sağlar.",
                    IsSafe = true,
                    Category = "Telemetri & Tanılama",
                    OriginalStartupType = 3
                },

                // 2. Güvenlik & Kartlar (Güvenli)
                new() {
                    ServiceName = "SCardSvr",
                    DisplayName = "Akıllı Kart",
                    Description = "Akıllı kart okuyucuları ve erişim izinlerini yönetir. Fiziksel akıllı kart kullanmıyorsanız gereksizdir.",
                    IsSafe = true,
                    Category = "Güvenlik & Kartlar",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "ScDeviceEnum",
                    DisplayName = "Akıllı Kart Cihaz Numaralandırması Hizmeti",
                    Description = "Sisteme bağlanan akıllı kart cihazları için yazılım arabirimleri oluşturur.",
                    IsSafe = true,
                    Category = "Güvenlik & Kartlar",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "SCPolicySvc",
                    DisplayName = "Akıllı Kart Kaldırma İlkesi",
                    Description = "Akıllı kart çıkarıldığında bilgisayarı kilitleme ilkesini yönetir.",
                    IsSafe = true,
                    Category = "Güvenlik & Kartlar",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "seclogon",
                    DisplayName = "İkincil Oturum Açma (Secondary Logon)",
                    Description = "Farklı kullanıcı kimlik bilgileriyle işlem başlatmayı (Farklı Kullanıcı Olarak Çalıştır) yönetir.",
                    WarningText = "Blizzard Battle.net, Riot Vanguard ve oyun başlatıcıları için gereklidir",
                    IsSafe = false,
                    Category = "Güvenlik & Kartlar",
                    OriginalStartupType = 3
                },

                // 3. Ağ & Uzak Bağlantı (Güvenli)
                new() {
                    ServiceName = "RemoteRegistry",
                    DisplayName = "Uzak Kayıt Defteri",
                    Description = "Uzak kullanıcıların bu bilgisayarın kayıt defterini uzaktan değiştirmesine olanak tanır. Kapatılması şiddetle önerilir.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "TermService",
                    DisplayName = "Uzak Masaüstü Hizmetleri",
                    Description = "Diğer kullanıcıların bu bilgisayara Uzak Masaüstü (RDP) ile bağlanmasını sağlar.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "UmRdpService",
                    DisplayName = "Uzak Masaüstü Kullanıcı Modu Bağlantı Noktası",
                    Description = "Uzak Masaüstü bağlantılarında yazıcı ve sürücü yönlendirmesini yönetir.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "SessionEnv",
                    DisplayName = "Uzak Masaüstü Yapılandırması",
                    Description = "Uzak Masaüstü oturum ortamını hazırlar ve yapılandırır.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "AJRouter",
                    DisplayName = "AllJoyn Yönlendirici Hizmeti",
                    Description = "Çevredeki AllJoyn IoT (akıllı ev) cihazlarıyla iletişim yönlendirmesini sağlar.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "PeerDistSvc",
                    DisplayName = "BranchCache",
                    Description = "Yerel alt ağdaki eşlerden gelen verileri önbelleğe alır. Ağdaki diğer bilgisayarlarla iletişim kurmayacaksanız kapatın.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "SharedAccess",
                    DisplayName = "İnternet Bağlantısı Paylaşımı (ICS)",
                    Description = "Ağ bağlantısını diğer cihazlarla paylaşır. Mobil Etkin Nokta (Hotspot) kullanmıyorsanız kapatılabilir.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "icssvc",
                    DisplayName = "Windows Mobil Etkin Nokta Hizmeti",
                    Description = "Bilgisayarınızı kablosuz Wi-Fi erişim noktası (Hotspot) olarak paylaşma desteği sağlar.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "iphlpsvc",
                    DisplayName = "IP Yardımcısı",
                    Description = "IPv6 tünelleme geçiş teknolojilerini sağlar. Özel IPv6 tüneli kullanmıyorsanız kapatılabilir.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 2
                },
                new() {
                    ServiceName = "lmhosts",
                    DisplayName = "TCP/IP NetBIOS Yardımcısı",
                    Description = "Ağdaki istemciler için NetBIOS ad çözümleme desteği sağlar.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "CscService",
                    DisplayName = "Çevrimdışı Dosyalar",
                    Description = "Ağ paylaşımlarındaki dosyaların çevrimdışı önbelleğe alınmasını yönetir.",
                    IsSafe = true,
                    Category = "Ağ & Uzak Erişim",
                    OriginalStartupType = 3
                },

                // 4. Karma Gerçeklik (Mixed Reality) (Güvenli)
                new() {
                    ServiceName = "SharedRealitySvc",
                    DisplayName = "Uzamsal Veri Hizmeti",
                    Description = "Windows Karma Gerçeklik başlıkları için uzamsal algılama verilerini işler.",
                    IsSafe = true,
                    Category = "Karma Gerçeklik",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "perceptionsimulation",
                    DisplayName = "Windows Algılama Benzetimi Hizmeti",
                    Description = "Sanal gerçeklik ve karma gerçeklik cihaz simülasyonunu yönetir.",
                    IsSafe = true,
                    Category = "Karma Gerçeklik",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "spectrum",
                    DisplayName = "Windows Algılama Hizmeti (Perception)",
                    Description = "Windows Karma Gerçeklik ve başlık algılama sensörlerini yönetir.",
                    IsSafe = true,
                    Category = "Karma Gerçeklik",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "MixedRealityOpenXRSvc",
                    DisplayName = "Windows Mixed Reality OpenXR Service",
                    Description = "Mixed Reality başlıkları için OpenXR çalışma zamanı desteği sağlar.",
                    IsSafe = true,
                    Category = "Karma Gerçeklik",
                    OriginalStartupType = 3
                },

                // 5. İletişim, Konum & Genel (Güvenli)
                new() {
                    ServiceName = "lfsvc",
                    DisplayName = "Coğrafi Konum Hizmeti",
                    Description = "Windows ve uygulamaların coğrafi konumunuzu izlemesini sağlar.",
                    IsSafe = true,
                    Category = "Genel & İletişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "WalletService",
                    DisplayName = "Cüzdan Hizmeti",
                    Description = "Windows Cüzdan ve dijital ödeme bilgilerini saklar.",
                    IsSafe = true,
                    Category = "Genel & İletişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "SEMgrSvc",
                    DisplayName = "Ödeme ve NFC/SE Yöneticisi",
                    Description = "Temassız NFC ve güvenli ödeme kartı çiplerini yönetir.",
                    IsSafe = true,
                    Category = "Genel & İletişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "tzautoupdate",
                    DisplayName = "Otomatik Saat Dilimi Güncelleştirici",
                    Description = "Sistem saat dilimini konuma göre otomatik ayarlar.",
                    IsSafe = true,
                    Category = "Genel & İletişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "RetailDemo",
                    DisplayName = "Perakende Gösteri Hizmeti",
                    Description = "Mağazalardaki tanıtım cihazları için demo modunu çalıştırır.",
                    IsSafe = true,
                    Category = "Genel & İletişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "PhoneSvc",
                    DisplayName = "Telefon",
                    Description = "Telefon arama ve sesli mesaj yönlendirmelerini yönetir.",
                    IsSafe = true,
                    Category = "Genel & İletişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "TapiSrv",
                    FallbackServiceName = "Telephony",
                    DisplayName = "Telefon Hizmeti (TapiSrv)",
                    Description = "Telefon ve faks iletişimi için Telephony API (TAPI) desteği sağlar.",
                    IsSafe = true,
                    Category = "Genel & İletişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "MapsBroker",
                    DisplayName = "İndirilen Haritalar Yöneticisi",
                    Description = "Windows Haritalar uygulamasının çevrimdışı haritaları arka planda indirmesini sağlar.",
                    IsSafe = true,
                    Category = "Genel & İletişim",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "SDRSVC",
                    DisplayName = "Windows Yedekleme",
                    Description = "Eski Windows 7 tarzı yedekleme ve sistem geri yükleme arşiv hizmeti.",
                    IsSafe = true,
                    Category = "Genel & İletişim",
                    OriginalStartupType = 3
                },

                // 6. İsteğe Bağlı / Donanım Servisleri (DİKKAT GEREKTİRENLER)
                new() {
                    ServiceName = "Spooler",
                    DisplayName = "Yazdırma Birikticisi",
                    Description = "Yazdırma işlerini sıraya koyar ve yazıcı ile iletişimi sağlar.",
                    WarningText = "Yazıcı Kullanıyorsanız Kapatmayın",
                    IsSafe = false,
                    Category = "Donanım & Çevre Birimleri",
                    OriginalStartupType = 2
                },
                new() {
                    ServiceName = "FrameServer",
                    DisplayName = "Windows Kamera Çerçeve Sunucusu",
                    Description = "Kamera video akışının birden fazla uygulama tarafından kullanılmasını sağlar.",
                    WarningText = "Kamera Kullanıyorsanız Kapatmayın",
                    IsSafe = false,
                    Category = "Donanım & Çevre Birimleri",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "FrameServerMonitor",
                    DisplayName = "Kamera Çerçeve Sunucusu İzleyicisi",
                    Description = "Kamera oturumlarını ve arka plan video akışlarını denetler.",
                    WarningText = "Kamera Kullanıyorsanız Kapatmayın",
                    IsSafe = false,
                    Category = "Donanım & Çevre Birimleri",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "WiaRpc",
                    DisplayName = "Resim Alma Olayları (WiaRpc)",
                    Description = "Tarayıcı (Scanner) ve görüntüleme cihazı olay bildirimlerini yönetir.",
                    WarningText = "Tarayıcı (Scanner) Kullanıyorsanız Kapatmayın",
                    IsSafe = false,
                    Category = "Donanım & Çevre Birimleri",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "stisvc",
                    DisplayName = "Windows Resim Alma (WIA)",
                    Description = "Tarayıcılardan ve kameralardan sisteme doğrudan görüntü aktarımı sağlar.",
                    WarningText = "Tarayıcı (Scanner) Kullanıyorsanız Kapatmayın",
                    IsSafe = false,
                    Category = "Donanım & Çevre Birimleri",
                    OriginalStartupType = 3
                },

                new() {
                    ServiceName = "WSearch",
                    DisplayName = "Windows Search (Arama ve Dizinleme)",
                    Description = "Dosya, e-posta ve Başlat menüsü arama sonuçları için dizin oluşturur.",
                    WarningText = "Kapatılırsa Başlat Arama Dizinlemesi Durur",
                    IsSafe = false,
                    Category = "Windows Bileşenleri",
                    OriginalStartupType = 2
                },
                new() {
                    ServiceName = "SysMain",
                    DisplayName = "SysMain (Hızlı Getirme / SuperFetch)",
                    Description = "Sık kullanılan uygulamaları belleğe önceden yükler. Mekanik sabit disklerde (HDD) takılmaları önlemek için kapatılabilir; SSD'lerde varsayılan açık kalması önerilir.",
                    WarningText = "Mekanik Sabit Diskler İçindir (SSD'lerde Gerekmez)",
                    IsSafe = false,
                    Category = "Windows Bileşenleri",
                    OriginalStartupType = 2
                },
                new() {
                    ServiceName = "WbioSrvc",
                    DisplayName = "Windows Biyometrik Hizmeti",
                    Description = "Parmak izi okuyucuları ve Windows Hello yüz tanıma biyometrik verilerini yönetir.",
                    WarningText = "Parmak İzi / Windows Hello Kullanıyorsanız Kapatmayın",
                    IsSafe = false,
                    Category = "Donanım & Çevre Birimleri",
                    OriginalStartupType = 3
                },
                new() {
                    ServiceName = "TrkWks",
                    DisplayName = "Dağıtılmış Bağlantı İzleme İstemcisi",
                    Description = "Yerel ağ içerisindeki bilgisayarlar arasında bağlı NTFS dosyalarının takibini sağlar.",
                    WarningText = "Ağdaki Bilgisayarlarla İletişim Kuracaksanız Kapatmayın",
                    IsSafe = false,
                    Category = "Ağ & Donanım",
                    OriginalStartupType = 2
                },
                new() {
                    ServiceName = "PcaSvc",
                    DisplayName = "Program Uyumluluk Yardımcısı Hizmeti",
                    Description = "Eski yazılımların ve oyunların uyumluluk modunda çalışmasını denetler.",
                    WarningText = "Eski Oyun / Program Çalıştıracaksanız Kapatmayın",
                    IsSafe = false,
                    Category = "Sistem & Uyumluluk",
                    OriginalStartupType = 2
                },
                new() {
                    ServiceName = "BDESVC",
                    DisplayName = "BitLocker Sürücü Şifreleme Hizmeti",
                    Description = "Sürücü şifreleme ve güvenlik anahtarı yönetimini sağlar.",
                    WarningText = "Diskiniz Şifreliyse (BitLocker) Kapatmayın",
                    IsSafe = false,
                    Category = "Güvenlik & Disk",
                    OriginalStartupType = 3
                }
            };
        }
    }
}
