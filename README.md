<p align="center">
  <img src="images/logo.png" width="110" alt="PureWin logosu">
</p>

<h1 align="center">PureWin</h1>

<p align="center">
  <b>Windows 10/11 için tek tıkla optimizasyon, gizlilik ve temizlik aracı.</b><br>
  Kurulum gerektirmez · Türkçe arayüz · Açık kaynak (MIT)
</p>

<p align="center">
  <img src="https://img.shields.io/badge/s%C3%BCr%C3%BCm-v1.1.0-brightgreen" alt="Sürüm">
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4" alt=".NET">
  <img src="https://img.shields.io/badge/lisans-MIT-green" alt="Lisans">
</p>

---

## 📌 PureWin Nedir?

PureWin, Windows 10/11 bilgisayarınızı hızlandırmak, gereksiz arka plan yükünü azaltmak ve gizliliğinizi güçlendirmek için geliştirilmiş kapsamlı bir optimizasyon aracıdır. Sistem ayarlarını **canlı olarak okur**, yaptığı her değişikliği izler ve dilediğiniz an geri almanızı sağlar.

## ✨ Öne Çıkan Özellikler

| | Özellik | Ne yapar? |
|---|---|---|
| 🔒 | Gizlilik & Telemetri | DiagTrack, hata raporlama servisi ve 17 zamanlanmış telemetri görevini kapatır. |
| 🤖 | Copilot & Recall | Windows'un AI izleyicilerini ve ekran geçmişi kaydını devre dışı bırakır. |
| ⚙️ | Windows Servisleri | 45 servisi "Güvenli / İsteğe Bağlı" olarak sınıflandırır; gereksizleri güvenle kapatır. |
| 🗑️ | Bloatware Kaldırıcı | Önyüklü gereksiz uygulamaları kaldırır (Edge vb. 4 temel araç korunur). |
| 🚀 | Görsel Hız & Efektler | Animasyon, şeffaflık ve efektleri kapatarak akıcılığı artırır. |
| 🎮 | Oyun ve Performans | Oyun modu, GPU planlama (HAGS) ve performans ayarları. |
| 🔎 | Windows Search | Arka plan disk indekslemeyi isteğe bağlı olarak kapatır. |
| 🧹 | Sistem Temizliği | Geçici dosyaları ve paket artıklarını derinlemesine temizler. |
| 📊 | Canlı Sağlık Raporu | Sistem durumunu puanlar ve eksikleri tek bakışta gösterir. |
| ⏪ | Geri Alma | Uygulanan tüm değişiklikler uygulama içinden geri alınabilir. |

## 📷 Ekran Görüntüleri

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
  <i>Bloatware Kaldırıcı — gereksiz önyüklü uygulamaları temizleyin</i>
</p>

## 🚀 Hızlı Başlangıç

En kolay yol — **[Releases](../../releases)** sayfasından hazır paketi indirin:

1. Size uygun paketi indirin:
   - **`PureWin_Portable_v1.zip`** — **Önerilen.** Kurulum gerektirmez; .NET dahil her şeyi kendi içinde taşır.
   - **`PureWin_Lite_v1.zip`** — Küçük boyutlu sürüm; bilgisayarınızda .NET 8 Desktop Runtime gerektirir.
2. ZIP dosyasını bir klasöre çıkarın.
3. `PureWin.exe` dosyasına çift tıklayın.

> ⚠️ Windows SmartScreen uyarısı görürseniz **"Daha fazla bilgi" → "Yine de çalıştır"** seçin. Uygulama sistem ayarlarını yönettiği için **yönetici izni** ister.

## 🖥️ Sistem Gereksinimleri

- Windows 10 / Windows 11 (x64)
- Yönetici yetkisi (sistem ayarlarını değiştirir)
- Lite sürüm için ayrıca: [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## 🔧 Kaynaktan Derleme

Gereksinimler: **Windows** + **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**

```bash
git clone https://github.com/hcorc/PureWin.git
cd PureWin
dotnet build -c Release
```

Tek dosyalık (self-contained) paket üretmek için:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -o publish
```

## 🗂️ Proje Yapısı

```
PureWin/
├─ Services/            # Optimizasyon servisleri (telemetri, servisler, oyun, temizlik...)
├─ Models/              # Veri modelleri
├─ Helpers/             # Yardımcı sınıflar
├─ Assets/              # İkon ve görseller
├─ Config/              # Uygulama veritabanı (bloatware listesi)
├─ Regfiles/            # Registry düzenleme dosyaları (280 adet)
├─ MainWindow.xaml(.cs) # Ana arayüz
└─ PureWin.csproj
```

## ⚠️ Önemli Notlar

- Uygulama sistem ayarlarını değiştirir; kullanım öncesi **geri yükleme noktası** oluşturmanız önerilir.
- Tüm değişiklikler uygulama içindeki **Geri Al (Undo)** mekanizmasıyla geri alınabilir.
- Program "olduğu gibi" sunulur; kullanım sorumluluğu kullanıcıya aittir (bkz. Lisans).

## 🤝 Katkı

Hata bildirimi ve öneriler için **Issues** bölümünü kullanabilirsiniz. Pull request'ler memnuniyetle karşılanır.

## 📄 Lisans

Bu proje **MIT Lisansı** ile lisanslanmıştır — ayrıntılar için [LICENSE](LICENSE) dosyasına bakın.

Copyright © 2026 **hcorc (PureWin Project)**
