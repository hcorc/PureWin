<p align="center">
  <img src="images/logo.png" width="110" alt="PureWin logosu">
</p>

<h1 align="center">PureWin</h1>

<p align="center">
  <b>Windows 10/11 için tek tıkla optimizasyon, gizlilik ve temizlik aracı.</b><br>
  Kurulum gerektirmez • Türkçe arayüz • Açık kaynak (MIT)
</p>

<p align="center">
  <img src="https://img.shields.io/badge/s%C3%BCr%C3%BCm-v1.1.2-brightgreen" alt="Sürüm">
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4" alt=".NET">
  <img src="https://img.shields.io/badge/lisans-MIT-green" alt="Lisans">
</p>

---

## PureWin Nedir?

PureWin, Windows 10/11 bilgisayarınızı hızlandırmak, gereksiz arka plan yükünü azaltmak ve gizliliğinizi güçlendirmek için geliştirilmiş kapsamlı bir optimizasyon aracıdır. Sistem ayarlarını **canlı olarak okur**, yaptığınız değişiklikleri izler ve uygulama üzerinden geri almanızı sağlar.

## v1.1.2 Yenilikleri

- **Bloatware canlı ilerleme kartı:** Kaldırma sırasında hangi paketin işlendiğini, kalan/success/fail sayılarını ve geçen süreyi gösterir.
- **Satır durum rozetleri:** `Sırada`, `Kaldırılıyor`, `Doğrulanıyor`, `Kaldırıldı`, `Başarısız` ve `Doğrulanamadı` durumları.
- **Nihai doğrulama:** Kaldırma servisi başarılı dönse bile yeşil rozet, son sistem taramasından sonra gösterilir.
- **DNS geri alma düzeltmesi:** “Geri Al” artık DHCP’ye gerçekten döner; doğrulama birden fazla kez denenir.
- **Game Mode geri yükleme sadakati:** Orijinalde registry değeri yoksa geri alma sırasında 0/1 yazılmaz; değer tamamen silinir.
- **Bloatware registry iz temizliği:** HKCU’daki TileProperties, BackgroundAccessApplications, URL ilişkilendirmeleri, PolicyCache ve HKLM’deki uygulamaya özel CapabilityAccessManager izinleri temizlenir.
- **Yazılım Güncelleyici metin düzeltmesi:** 0 güncelleme varken “Hepsi Güncel / Güncelleme Yok” gösterilir.
- **Başlangıç uygulamaları:** Toggle olay sırası sağlamlaştırıldı.

> Windows tarafından yönetilen COM, StateRepository ve AppxAllUserStore/Deprovisioned kayıtları güvenlik nedeniyle korunur.

## Öne Çıkan Özellikler

| | Özellik | Ne yapar? |
|---|---|---|
| 🛡️ | Gizlilik & Telemetri | DiagTrack, hata raporlama servisi ve 17 zamanlanmış telemetri görevini kapatır. |
| 🤖 | Copilot & Recall | Windows’un AI izleyicilerini ve ekran geçmişi kaydını devre dışı bırakır. |
| ⚙️ | Windows Servisleri | 45 servisi “Güvenli / İsteğe Bağlı” olarak sınıflandırır; gereksizleri güvenle kapatır. |
| 🗑️ | Bloatware Kaldırıcı | Önyüklü gereksiz uygulamaları kaldırır; canlı ilerleme ve doğrulama gösterir. |
| 🎨 | Görsel Hız & Efektler | Animasyon, şeffaflık ve efektleri kapatarak akıcılığı artırır. |
| 🎮 | Oyun ve Performans | Oyun modu, GPU planlama (HAGS) ve performans ayarları. |
| 🔍 | Windows Search | Arka plan disk indekslemeyi isteğe bağlı olarak kapatır. |
| 🧹 | Sistem Temizliği | Geçici dosyaları ve paket artıklarını derinlemesine temizler. |
| 📊 | Canlı Sağlık Raporu | Sistem durumunu puanlar ve eksikleri tek bakışta gösterir. |
| 🔎 | Tweak Kontrol | Güncelleme sonrası tweak durumlarını salt okunur denetler. |
| 🌐 | Ağ & DNS Merkezi | IPv4 DNS’i otomatik (DHCP) veya statik yönetir; önbelleği temizler ve değişiklikleri geri alır. |
| 📦 | Yazılım Güncelleyici | winget ile yüklü uygulamaları tarar, güncellemeleri listeler ve uygular. |
| 🚀 | Başlangıç Uygulamaları | Başlangıçta açılan uygulamaları etki dereceleriyle gösterir; açar/kapatır. |
| ℹ️ | Hakkında | Sürüm, sistem ve konum bilgileri; GitHub bağlantısı ve klavye erişimi. |
| ↩️ | Geri Alma | Uygulama üzerinden yapılan tweak’ler snapshot ile geri alınabilir. |

## Ekran Görüntüleri

<p align="center">
  <img src="images/screenshot-1.png" alt="Sistem Raporu" width="800"><br>
  <i>Sistem Optimizasyon ve Sağlık Raporu — canlı durum ve puanlama</i>
</p>

<p align="center">
  <img src="images/screenshot-2.png" alt="Sistem İnce Ayarları" width="800"><br>
  <i>Sistem İnce Ayarları — istediğiniz optimizasyonları seçin</i>
</p>

<p align="center">
  <img src="images/screenshot-3.png" alt="Bloatware Kaldırıcı" width="800"><br>
  <i>Bloatware Kaldırıcı — katalog, filtreler ve hızlı seçim</i>
</p>

<p align="center">
  <img src="images/screenshot-4.png" alt="Tweak Kontrol" width="800"><br>
  <i>Tweak Kontrol — güncelleme sonrası tweak durumlarını denetleyin</i>
</p>

<p align="center">
  <img src="images/screenshot-5.png" alt="Yazılım Güncelleyici" width="800"><br>
  <i>Yazılım Güncelleyici — winget ile güncellemeleri tek tıkla uygulayın</i>
</p>

<p align="center">
  <img src="images/screenshot-6.png" alt="Bloatware kaldırma sonucu" width="800"><br>
  <i>Kaldırma sonrası doğrulanmış satır durumu ve özet kartı</i>
</p>

## Hızlı Başlangıç

En kolay yol — **[Releases](../../releases)** sayfasından hazır paketi indirin:

1. Size uygun paketi indirin:
   - **`PureWin_Portable_v1.1.2.zip`** — **Önerilen.** Kurulum gerektirmez; .NET dahil her şeyi kendi içinde taşır.
   - **`PureWin_Lite_v1.1.2.zip`** — Küçük boyutlu sürüm; bilgisayarınızda .NET 8 Desktop Runtime gerektirir.
2. ZIP dosyasını bir klasöre çıkarın.
3. `PureWin.exe` dosyasına çift tıklayın.

> Windows SmartScreen uyarısı görürseniz **“Daha fazla bilgi” → “Yine de çalıştır”** seçin. Uygulama sistem ayarlarını yönettiği için **yönetici izni** ister.

## Sistem Gereksinimleri

- Windows 10 / Windows 11 (x64)
- Yönetici yetkisi (sistem ayarlarını değiştirir)
- Lite sürüm için ayrıca: [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Kaynaktan Derleme

Gereksinimler: **Windows** + **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**

```bash
git clone https://github.com/hcorc/PureWin.git
cd PureWin

# Geliştirme derlemesi
dotnet build -c Release

# Portable (self-contained, tek dosya)
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None -p:DebugSymbols=false -o publish/portable

# Lite (framework-dependent, tek dosya)
dotnet publish -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None -p:DebugSymbols=false -o publish/lite
```

## Proje Yapısı

```text
PureWin/
├── Services/            # Optimizasyon servisleri (telemetri, servisler, oyun, temizlik, DNS...)
├── Models/              # Veri modelleri
├── Helpers/             # Yardımcı sınıflar
├── Assets/              # İkon ve görseller
├── Config/              # Uygulama veritabanı (bloatware listesi)
├── Regfiles/            # Registry düzenleme dosyaları
├── NetworkDnsWindow.xaml(.cs) # Ağ & DNS Merkezi
├── MainWindow.xaml(.cs) # Ana arayüz
└── PureWin.csproj
```

## Önemli Notlar

- Uygulama sistem ayarlarını değiştirir; kullanım öncesi **geri yükleme noktası** oluşturmanız önerilir.
- Uygulama üzerinden uygulanan tweak’ler snapshot ile geri alınabilir.
- Windows tarafından yönetilen COM, StateRepository ve AppxAllUserStore/Deprovisioned kayıtları güvenlik nedeniyle korunur.
- Program “olduğu gibi” sunulur; kullanım sorumluluğu kullanıcıya aittir (bkz. Lisans).

## Katkı

Hata bildirimi ve öneriler için **Issues** bölümünü kullanabilirsiniz. Pull request’ler memnuniyetle karşılanır.

## Lisans

Bu proje **MIT Lisansı** ile lisanslanmıştır — ayrıntılar için [LICENSE](LICENSE) dosyasına bakın.

Copyright © 2026 **hcorc (PureWin Project)**
