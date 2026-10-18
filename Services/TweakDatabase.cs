using System.Collections.Generic;
using PureWin.Models;

namespace PureWin.Services
{
    public static class TweakDatabase
    {
        public static (List<TweakCategoryGroup> Col1, List<TweakCategoryGroup> Col2, List<TweakCategoryGroup> Col3) GetColumns()
        {
            var col1 = new List<TweakCategoryGroup>();
            var col2 = new List<TweakCategoryGroup>();
            var col3 = new List<TweakCategoryGroup>();

            // ==========================================
            // SÜTUN 1
            // ==========================================

            // 1. Gizlilik & Önerilen İçerikler
            col1.Add(new TweakCategoryGroup
            {
                Id = "PrivacySuggestedContent",
                Title = "Gizlilik & Önerilen İçerikler",
                Icon = "🔒",
                Items = new List<TweakItem>
                {
                    new() { 
                        Id = "DisableCoreTelemetryTasks", 
                        Title = "Arka plan telemetri zamanlanmış görevlerini kapat (17 Görev)", 
                        Description = "Windows Görev Zamanlayıcı'daki 17 adet telemetri, CompatTelRunner, hata raporlama ve anket görevini durdurur.", 
                        IsScheduledTask = true, 
                        ScheduledTaskType = "Core" 
                    },
                    new() { 
                        Id = "DisableOptionalTelemetryTasks", 
                        Title = "İsteğe bağlı arka plan görevlerini kapat (Edge & Aile Güvenliği)", 
                        Description = "Microsoft Edge arka plan güncelleyicileri ve Aile Güvenliği izleyicilerini devre dışı bırakır. (Edge'i ana tarayıcı olarak kullanmıyorsanız önerilir).", 
                        IsScheduledTask = true, 
                        ScheduledTaskType = "Optional" 
                    },
                    new() { Id = "DisableTelemetry", Title = "Telemetri, takip ve reklamları kapat", Description = "Windows kullanım verilerinin Microsoft'a iletilmesini engeller.", ApplyRegFile = "Disable_Telemetry.reg", UndoRegFile = "Enable_Telemetry.reg" },
                    new() { Id = "DisableDiagtrackListener", Title = "DiagTrack telemetri dinleyicisini kapat (AutoLogger)", Description = "Windows açılışında telemetri verisi toplayan çekirdek (ETW) dinleyicisini devre dışı bırakır.", ApplyRegFile = "Disable_Diagtrack_Listener.reg", UndoRegFile = "Enable_Diagtrack_Listener.reg", RequiresReboot = true },
                    new() { Id = "DisableSuggestions", Title = "İpuçları ve önerilen içerikleri kapat", Description = "Windows genelindeki öneri ve ipuçlarını temizler.", ApplyRegFile = "Disable_Windows_Suggestions.reg", UndoRegFile = "Enable_Windows_Suggestions.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableNotifications", Title = "Windows bildirimlerini kapat", Description = "Uygulama ve sistem bildirimlerini devre dışı bırakır.", ApplyRegFile = "Disable_Notifications.reg", UndoRegFile = "Enable_Notifications.reg" },
                    new() { Id = "DisableLocationServices", Title = "Konum servislerini ve erişimini kapat", Description = "Uygulamaların ve Windows'un coğrafi konum erişimini durdurur.", ApplyRegFile = "Disable_Location_Services.reg", UndoRegFile = "Enable_Location_Services.reg" },
                    new() { Id = "DisableFindMyDevice", Title = "'Cihazımı Bul' konum takibini kapat", Description = "Bilgisayarın konumunun Microsoft hesabına kaydedilmesini durdurur.", ApplyRegFile = "Disable_Find_My_Device.reg", UndoRegFile = "Enable_Find_My_Device.reg" },
                    new() { Id = "DisableLockscreenTips", Title = "Kilit ekranındaki ipuçlarını kapat", Description = "Kilit ekranı üzerindeki ipucu yazılarını gizler.", ApplyRegFile = "Disable_Lockscreen_Tips.reg", UndoRegFile = "Enable_Lockscreen_Tips.reg" },
                    new() { Id = "DisableEdgeAds", Title = "Edge reklam ve haber akışını kapat", Description = "Edge başlangıç sayfasındaki reklam ve haberleri kaldırır.", ApplyRegFile = "Disable_Edge_Ads_And_Suggestions.reg", UndoRegFile = "Enable_Edge_Ads_And_Suggestions.reg" },
                    new() { Id = "DisableSettings365Ads", Title = "Ayarlar'da Microsoft 365 reklamlarını gizle", Description = "Ayarlar giriş sayfasındaki Office/OneDrive reklamlarını kaldırır.", ApplyRegFile = "Disable_Settings_365_Ads.reg", UndoRegFile = "Enable_Settings_365_Ads.reg" }
                }
            });

            // 2. Yapay Zeka (AI)
            col1.Add(new TweakCategoryGroup
            {
                Id = "AI",
                Title = "Yapay Zeka (AI)",
                Icon = "✨",
                Items = new List<TweakItem>
                {
                    new() { Id = "DisableCopilot", Title = "Microsoft Copilot'u devre dışı bırak", Description = "Copilot butonunu ve arka plan servisini kaldırır.", ApplyRegFile = "Disable_Copilot.reg", UndoRegFile = "Enable_Copilot.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableRecall", Title = "Windows Recall'ı devre dışı bırak", Description = "Sürekli ekran kaydı alan Recall izleyicisini kapatır.", ApplyRegFile = "Disable_AI_Recall.reg", UndoRegFile = "Enable_AI_Recall.reg" },
                    new() { Id = "DisableClickToDo", Title = "Click To Do (AI analiz aracı) kapat", Description = "Ekrandaki tıklamaları inceleyen AI servisini durdurur.", ApplyRegFile = "Disable_Click_To_Do.reg", UndoRegFile = "Enable_Click_To_Do.reg" },
                    new() { Id = "DisableAISvcAutoStart", Title = "AI servisinin otomatik başlamasını engelle", Description = "Windows açılırken AI servislerinin RAM'e yüklenmesini önler.", ApplyRegFile = "Disable_AI_Service_Auto_Start.reg", UndoRegFile = "Enable_AI_Service_Auto_Start.reg" },
                    new() { Id = "DisableEdgeAI", Title = "Edge içindeki AI özelliklerini kapat", Description = "Edge içindeki Copilot kenar çubuğunu devre dışı bırakır.", ApplyRegFile = "Disable_Edge_AI_Features.reg", UndoRegFile = "Enable_Edge_AI_Features.reg" },
                    new() { Id = "DisableChromeAI", Title = "Google Chrome içindeki AI özelliklerini kapat", Description = "Chrome tarayıcısındaki deneysel AI sekmelerini kapatır.", ApplyRegFile = "Disable_Chrome_AI_Features.reg", UndoRegFile = "Enable_Chrome_AI_Features.reg" },
                    new() { Id = "DisablePaintAI", Title = "Paint içindeki AI özelliklerini kapat", Description = "Paint resim üretim AI aracını devre dışı bırakır.", ApplyRegFile = "Disable_Paint_AI_Features.reg", UndoRegFile = "Enable_Paint_AI_Features.reg" },
                    new() { Id = "DisableNotepadAI", Title = "Not Defteri içindeki AI özelliklerini kapat", Description = "Not Defteri metin yeniden yazma eklentisini kapatır.", ApplyRegFile = "Disable_Notepad_AI_Features.reg", UndoRegFile = "Enable_Notepad_AI_Features.reg" }
                }
            });

            // 3. Görünüm (Appearance)
            col1.Add(new TweakCategoryGroup
            {
                Id = "Appearance",
                Title = "Görünüm & Temalar",
                Icon = "🖌️",
                Items = new List<TweakItem>
                {
                    new() { Id = "EnableDarkMode", Title = "Sistem ve uygulamalar için koyu temayı aç", Description = "Göz yormayan koyu temayı etkinleştirir.", ApplyRegFile = "Enable_Dark_Mode.reg", UndoRegFile = "Enable_Light_Mode.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableTransparency", Title = "Saydamlık efektlerini kapat", Description = "Pencerelerdeki şeffaflığı kapatıp GPU yükünü azaltır.", ApplyRegFile = "Disable_Transparency.reg", UndoRegFile = "Enable_Transparency.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableAnimations", Title = "Animasyonları ve görsel efektleri kapat", Description = "Pencerelerin anında açılmasını sağlar.", ApplyRegFile = "Disable_Animations.reg", UndoRegFile = "Enable_Animations.reg", RequiresExplorerRestart = true },
                    new() {
                        Id = "DesktopSpotlight",
                        Title = "Masaüstünde Windows Spotlight",
                        Description = "Masaüstünde her gün değişen Spotlight duvar kağıtlarını ayarlar.",
                        IsDropdown = true,
                        RequiresExplorerRestart = true,
                        DropdownOptions = new List<DropdownOption>
                        {
                            new() { Label = "Değişiklik Yok", FeatureId = "", RegFile = "" },
                            new() { Label = "Kısayolu Göster (Varsayılan)", FeatureId = "EnableDesktopSpotlight", RegFile = "Enable_Desktop_Spotlight.reg" },
                            new() { Label = "Kısayolu Gizle", FeatureId = "HideDesktopSpotlightIcon", RegFile = "Hide_Desktop_Spotlight_Icon.reg" },
                            new() { Label = "Spotlight'ı Kapat", FeatureId = "DisableDesktopSpotlight", RegFile = "Disable_Desktop_Spotlight.reg" }
                        }
                    }
                }
            });

            // 4. Çoklu Görev (Multi-tasking)
            col1.Add(new TweakCategoryGroup
            {
                Id = "MultiTasking",
                Title = "Çoklu Görev (Multi-tasking)",
                Icon = "🗗",
                Items = new List<TweakItem>
                {
                    new() { Id = "DisableWindowSnapping", Title = "Pencere yaslamayı (Snapping) kapat", Description = "Ekran kenarlarına yaklaştığında otomatik yapışmayı kapatır.", ApplyRegFile = "Disable_Window_Snapping.reg", UndoRegFile = "Enable_Window_Snapping.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableSnapAssist", Title = "Yaslama önerilerini (Snap Assist) kapat", Description = "Pencere yaslayınca açık pencereleri seçtiren paneli gizler.", ApplyRegFile = "Disable_Snap_Assist.reg", UndoRegFile = "Enable_Snap_Assist.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableSnapLayouts", Title = "Büyütme butonundaki yerleşim menüsünü gizle", Description = "Ekranın üstündeki ızgara flyout menüsünü kapatır.", ApplyRegFile = "Disable_Snap_Layouts.reg", UndoRegFile = "Enable_Snap_Layouts.reg", RequiresExplorerRestart = true },
                    new() {
                        Id = "ShowTabsInAltTab",
                        Title = "Alt+Tab Basıldığında Tarayıcı Sekmeleri",
                        Description = "Alt+Tab pencerelerinde tarayıcı sekmelerinin görünmesini ayarlar.",
                        IsDropdown = true,
                        RequiresExplorerRestart = true,
                        DropdownOptions = new List<DropdownOption>
                        {
                            new() { Label = "Değişiklik Yok", FeatureId = "", RegFile = "" },
                            new() { Label = "Sekmeleri Gösterme (Klasik)", FeatureId = "HideTabsInAltTab", RegFile = "Hide_Tabs_In_Alt_Tab.reg" },
                            new() { Label = "En Son 3 Sekmeyi Göster", FeatureId = "Show3TabsInAltTab", RegFile = "Show_3_Tabs_In_Alt_Tab.reg" },
                            new() { Label = "En Son 5 Sekmeyi Göster", FeatureId = "Show5TabsInAltTab", RegFile = "Show_5_Tabs_In_Alt_Tab.reg" },
                            new() { Label = "En Son 20 Sekmeyi Göster", FeatureId = "Show20TabsInAltTab", RegFile = "Show_20_Tabs_In_Alt_Tab.reg" }
                        }
                    }
                }
            });

            // ==========================================
            // SÜTUN 2
            // ==========================================

            // 5. Sistem & Donanım
            col2.Add(new TweakCategoryGroup
            {
                Id = "System",
                Title = "Sistem & Donanım",
                Icon = "💻",
                Items = new List<TweakItem>
                {
                    new() { Id = "DisableBackgroundApps", Title = "Arka plan uygulamalarını kapat (RAM & Pil Tasarrufu)", Description = "Pencereleri kapalı olan Store uygulamalarının arkada gizlice bellek ve işlemci tüketmesini önler.", ApplyRegFile = "Disable_Background_Apps.reg", UndoRegFile = "Enable_Background_Apps.reg" },
                    new() { Id = "DisableDragTray", Title = "Dosya sürükleme tepsisini (Drag Tray) kapat", Description = "Dosya sürüklerken ekran üstünde çıkan paneli kapatır.", ApplyRegFile = "Disable_Share_Drag_Tray.reg", UndoRegFile = "Enable_Share_Drag_Tray.reg", RequiresExplorerRestart = true },
                    new() { Id = "RevertContextMenu", Title = "Windows 10 klasik sağ tık menüsünü kullan", Description = "'Daha fazla seçenek göster' adımını kaldırıp direkt klasik menüyü açar.", ApplyRegFile = "Disable_Show_More_Options_Context_Menu.reg", UndoRegFile = "Enable_W11_Style_Context_Menu.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableMouseAcceleration", Title = "Fare ivmesini (Pointer Precision) kapat", Description = "FPS oyunları için 1:1 ham fare takibi sağlar.", ApplyRegFile = "Disable_Enhance_Pointer_Precision.reg", UndoRegFile = "Enable_Enhance_Pointer_Precision.reg" },
                    new() { Id = "DisableStickyKeys", Title = "Yapışkan Tuşlar kısayolunu (5x Shift) kapat", Description = "Shift'e art arda basınca oyunu alta atan pencereyi engeller.", ApplyRegFile = "Disable_Sticky_Keys_Shortcut.reg", UndoRegFile = "Enable_Sticky_Keys_Shortcut.reg" },
                    new() { Id = "DisableStorageSense", Title = "Depolama Algısı otomatik temizliğini kapat", Description = "Windows'un arkaplanda habersiz dosya silmesini önler.", ApplyRegFile = "Disable_Storage_Sense.reg", UndoRegFile = "Enable_Storage_Sense.reg" },
                    new() { Id = "DisableFastStartup", Title = "Hızlı başlatmayı (Fast Startup) kapat", Description = "Bozuk bellek önbelleği ve çift çekirdek çökme hatalarını çözer.", ApplyRegFile = "Disable_Fast_Startup.reg", UndoRegFile = "Enable_Fast_Startup.reg", RequiresReboot = true },
                    new() { Id = "DisableBitlockerAutoEncryption", Title = "BitLocker otomatik cihaz şifrelemesini kapat", Description = "Sürücülerin kilitlenmesini ve SSD hız kaybını engeller.", ApplyRegFile = "Disable_Bitlocker_Auto_Encryption.reg", UndoRegFile = "Enable_Bitlocker_Auto_Encryption.reg", RequiresReboot = true }
                }
            });

            // 6. Windows Update
            col2.Add(new TweakCategoryGroup
            {
                Id = "WindowsUpdate",
                Title = "Windows Update",
                Icon = "🔄",
                Items = new List<TweakItem>
                {
                    new() { Id = "DisableUpdateASAP", Title = "Güncellemeleri hemen almayı kapat", Description = "Hatalı çıkabilecek güncellemeleri ilk gün yüklemek yerine bekler.", ApplyRegFile = "Disable_Update_ASAP.reg", UndoRegFile = "Enable_Update_ASAP.reg" },
                    new() { Id = "PreventUpdateAutoReboot", Title = "Oturum açıkken otomatik yeniden başlatmayı önle", Description = "Çalışırken veya oyun oynarken zorla yeniden başlatmayı durdurur.", ApplyRegFile = "Prevent_Auto_Reboot.reg", UndoRegFile = "Allow_Auto_Reboot.reg" },
                    new() { Id = "DisableDeliveryOptimization", Title = "Güncellemeleri diğer PC'lerle paylaşmayı kapat", Description = "İnternet bant genişliğinizi ve pinginizi korur.", ApplyRegFile = "Disable_Delivery_Optimization.reg", UndoRegFile = "Enable_Delivery_Optimization.reg" },
                    new() { Id = "DisableDeviceAutoAppDownload", Title = "Cihaz eşlikçi uygulamalarını indirmeyi engelle", Description = "Donanım takıldığında arkada gereksiz yazılım yüklenmesini önler.", ApplyRegFile = "Disable_Device_Auto_App_Download.reg", UndoRegFile = "Enable_Device_Auto_App_Download.reg" }
                }
            });

            // 7. Dosya Gezgini (File Explorer)
            col2.Add(new TweakCategoryGroup
            {
                Id = "FileExplorer",
                Title = "Dosya Gezgini",
                Icon = "📁",
                Items = new List<TweakItem>
                {
                    new() {
                        Id = "ExplorerLocation",
                        Title = "Dosya Gezgini Başlangıç Konumu",
                        Description = "Dosya Gezgini açıldığında varsayılan konumu seçin.",
                        IsDropdown = true,
                        RequiresExplorerRestart = true,
                        DropdownOptions = new List<DropdownOption>
                        {
                            new() { Label = "Değişiklik Yok", FeatureId = "", RegFile = "" },
                            new() { Label = "Giriş (Varsayılan)", FeatureId = "ExplorerToHome", RegFile = "Launch_File_Explorer_To_Home.reg" },
                            new() { Label = "Bu Bilgisayar", FeatureId = "ExplorerToThisPC", RegFile = "Launch_File_Explorer_To_This_PC.reg" },
                            new() { Label = "İndirilenler", FeatureId = "ExplorerToDownloads", RegFile = "Launch_File_Explorer_To_Downloads.reg" },
                            new() { Label = "OneDrive", FeatureId = "ExplorerToOneDrive", RegFile = "Launch_File_Explorer_To_OneDrive.reg" }
                        }
                    },
                    new() { Id = "ShowKnownFileExt", Title = "Dosya uzantılarını (.exe, .zip) göster", Description = "Tüm bilinen dosya uzantılarını görünür yapar.", ApplyRegFile = "Show_Extensions_For_Known_File_Types.reg", UndoRegFile = "Hide_Extensions_For_Known_File_Types.reg", RequiresExplorerRestart = true },
                    new() { Id = "EnableCompactView", Title = "Dosya Gezgininde kompakt görünümü aç", Description = "Dosya ve klasörler arasındaki boşlukları azaltarak ekranda daha fazla öğe listeler.", ApplyRegFile = "Enable_Compact_View.reg", UndoRegFile = "Disable_Compact_View.reg", RequiresExplorerRestart = true },
                    new() { Id = "ShowHiddenFolders", Title = "Gizli dosya ve klasörleri göster", Description = "Gizli sistem dosyalarını görünür yapar.", ApplyRegFile = "Show_Hidden_Folders.reg", UndoRegFile = "Hide_Hidden_Folders.reg", RequiresExplorerRestart = true },
                    new() { Id = "HideDupliDrive", Title = "Mükerrer sürücü simgelerini gizle", Description = "USB belleklerin sol panelde iki kez görünmesini engeller.", ApplyRegFile = "Hide_duplicate_removable_drives_from_navigation_pane_of_File_Explorer.reg", UndoRegFile = "Show_duplicate_removable_drives_in_navigation_pane_of_File_Explorer.reg", RequiresExplorerRestart = true },
                    new() { Id = "HideHome", Title = "Gezgin sol panelinden 'Giriş'i gizle", Description = "Sol gezinme bölmesindeki Giriş kısayolunu kaldırır.", ApplyRegFile = "Hide_Home_from_Explorer.reg", UndoRegFile = "Show_Home_in_Explorer.reg", RequiresExplorerRestart = true },
                    new() { Id = "HideGallery", Title = "Gezgin sol panelinden 'Galeri'yi gizle", Description = "Gereksiz resim albümü Galeri simgesini gizler.", ApplyRegFile = "Hide_Gallery_from_Explorer.reg", UndoRegFile = "Show_Gallery_in_Explorer.reg", RequiresExplorerRestart = true },
                    new() { Id = "HideOnedrive", Title = "Gezgin sol panelinden 'OneDrive'ı gizle", Description = "Sol paneldeki OneDrive kalabalığını temizler.", ApplyRegFile = "Hide_Onedrive_Folder.reg", UndoRegFile = "Show_Onedrive_Folder.reg", RequiresExplorerRestart = true },
                    new() { Id = "AddFoldersToThisPC", Title = "Klasörleri 'Bu Bilgisayar'a geri ekle", Description = "İndirilenler, Belgeler gibi klasik klasörleri Bu Bilgisayar ekranına ekler.", ApplyRegFile = "Add_All_Folders_Under_This_PC.reg", UndoRegFile = "Remove_All_Folders_Under_This_PC.reg", RequiresExplorerRestart = true },
                    new() {
                        Id = "DriveLetterPosition",
                        Title = "Sürücü Harfi Konumu",
                        Description = "C:, D: gibi sürücü harflerinin nerede gösterileceğini belirler.",
                        IsDropdown = true,
                        RequiresExplorerRestart = true,
                        DropdownOptions = new List<DropdownOption>
                        {
                            new() { Label = "Değişiklik Yok", FeatureId = "", RegFile = "" },
                            new() { Label = "Etiketinden Sonra (Varsayılan)", FeatureId = "ShowDriveLettersLast", RegFile = "Show_Drive_Letters_Last.reg" },
                            new() { Label = "Etiketinden Önce", FeatureId = "ShowDriveLettersFirst", RegFile = "Show_Drive_Letters_First.reg" },
                            new() { Label = "Ağ Sürücüleri Önce", FeatureId = "ShowNetworkDriveLettersFirst", RegFile = "Show_Network_Drive_Letters_First.reg" },
                            new() { Label = "Tüm Harfleri Gizle", FeatureId = "HideDriveLetters", RegFile = "Hide_Drive_Letters.reg" }
                        }
                    }
                }
            });

            // 8. İsteğe Bağlı Windows Özellikleri
            col2.Add(new TweakCategoryGroup
            {
                Id = "OptionalWindowsFeatures",
                Title = "İsteğe Bağlı Windows Özellikleri",
                Icon = "🧩",
                Items = new List<TweakItem>
                {
                    new() { Id = "EnableWindowsSandbox", Title = "Windows Sandbox'ı (Güvenli Alan) etkinleştir", Description = "Şüpheli dosyaları güvenle açmak için sanal Windows alanını açar.", ApplyRegFile = "Enable_Windows_Sandbox.reg", UndoRegFile = "Disable_Windows_Sandbox.reg", RequiresReboot = true },
                    new() { Id = "EnableWindowsSubsystemForLinux", Title = "Windows Subsystem for Linux (WSL) etkinleştir", Description = "Windows üzerinde yerel Linux çalıştırmayı sağlayan WSL çekirdeğini açar.", ApplyRegFile = "Enable_WSL.reg", UndoRegFile = "Disable_WSL.reg", RequiresReboot = true }
                }
            });

            // ==========================================
            // SÜTUN 3
            // ==========================================

            // 9. Başlat Menüsü & Arama
            col3.Add(new TweakCategoryGroup
            {
                Id = "StartMenuSearch",
                Title = "Başlat Menüsü & Arama",
                Icon = "🔍",
                Items = new List<TweakItem>
                {
                    new() {
                        Id = "ClearStart",
                        Title = "Başlat menüsü sabitlenmiş uygulamaları kaldır",
                        Description = "Başlat menüsündeki sabitlenmiş varsayılan kutucukları temizler.",
                        IsDropdown = true,
                        RequiresExplorerRestart = true,
                        DropdownOptions = new List<DropdownOption>
                        {
                            new() { Label = "Değişiklik Yok", FeatureId = "", RegFile = "" },
                            new() { Label = "Mevcut Kullanıcı İçin", FeatureId = "ClearStart", RegFile = "Clear_Start.reg" },
                            new() { Label = "Tüm Kullanıcılar İçin", FeatureId = "ClearStartAllUsers", RegFile = "Clear_Start_All_Users.reg" }
                        }
                    },
                    new() {
                        Id = "StartAllAppsView",
                        Title = "Başlat menüsü 'Tüm Uygulamalar' görünümü",
                        Description = "Başlat menüsü tüm uygulamalar ekranının düzenini belirler.",
                        IsDropdown = true,
                        RequiresExplorerRestart = true,
                        DropdownOptions = new List<DropdownOption>
                        {
                            new() { Label = "Değişiklik Yok", FeatureId = "", RegFile = "" },
                            new() { Label = "Kategori Görünümü (Varsayılan)", FeatureId = "StartAllAppsCategory", RegFile = "Start_AllApps_Category.reg" },
                            new() { Label = "Kılavuz (Grid) Görünümü", FeatureId = "StartAllAppsGrid", RegFile = "Start_AllApps_Grid.reg" },
                            new() { Label = "Liste Görünümü", FeatureId = "StartAllAppsList", RegFile = "Start_AllApps_List.reg" },
                            new() { Label = "Gizle", FeatureId = "DisableStartAllApps", RegFile = "Disable_Start_All_Apps.reg" }
                        }
                    },
                    new() { Id = "DisableStartRecommended", Title = "Başlat menüsünde önerilenler bölümünü gizle", Description = "Son açılan dosya ve önerilenler alanını temizler.", ApplyRegFile = "Disable_Start_Recommended.reg", UndoRegFile = "Enable_Start_Recommended.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableStartPhoneLink", Title = "Başlat menüsünde Telefon Bağlantısını kapat", Description = "Başlat menüsündeki telefon durumu panelini kaldırır.", ApplyRegFile = "Disable_Phone_Link_In_Start.reg", UndoRegFile = "Enable_Phone_Link_In_Start.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableBing", Title = "Aramada Bing web aramasını ve Copilot'u kapat", Description = "Arama yaparken internete bağlanmayı keser, aramayı hızlandırır.", ApplyRegFile = "Disable_Bing_Cortana_In_Search.reg", UndoRegFile = "Enable_Bing_Cortana_In_Search.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableStoreSearchSuggestions", Title = "Aramada Mağaza uygulama önerilerini kapat", Description = "Arama sonuçlarında Microsoft Store reklamlarını gizler.", ApplyRegFile = "Disable_Store_Search_Suggestions.reg", UndoRegFile = "Enable_Store_Search_Suggestions.reg", RequiresExplorerRestart = true }
                }
            });

            // 10. Görev Çubuğu (Taskbar)
            col3.Add(new TweakCategoryGroup
            {
                Id = "Taskbar",
                Title = "Görev Çubuğu",
                Icon = "🖥️",
                Items = new List<TweakItem>
                {
                    new() { Id = "TaskbarAlignLeft", Title = "Görev çubuğunu sola hizala", Description = "Ortalanmış simgeleri klasik Windows tarzında sola yaslar.", ApplyRegFile = "Align_Taskbar_Left.reg", UndoRegFile = "Align_Taskbar_Center.reg", RequiresExplorerRestart = true },
                    new() {
                        Id = "SearchIcon",
                        Title = "Görev çubuğu arama tarzı",
                        Description = "Görev çubuğundaki arama simgesi veya kutusunun görünümünü ayarlar.",
                        IsDropdown = true,
                        RequiresExplorerRestart = true,
                        DropdownOptions = new List<DropdownOption>
                        {
                            new() { Label = "Değişiklik Yok", FeatureId = "", RegFile = "" },
                            new() { Label = "Arama Kutusu (Varsayılan)", FeatureId = "ShowSearchBoxTb", RegFile = "Show_Search_Box.reg" },
                            new() { Label = "Arama Simgesi ve Etiket", FeatureId = "ShowSearchLabelTb", RegFile = "Show_Search_Icon_And_Label.reg" },
                            new() { Label = "Yalnızca Arama Simgesi", FeatureId = "ShowSearchIconTb", RegFile = "Show_Search_Icon.reg" },
                            new() { Label = "Gizle", FeatureId = "HideSearchTb", RegFile = "Hide_Search_Taskbar.reg" }
                        }
                    },
                    new() { Id = "HideTaskview", Title = "Görev çubuğundaki 'Görev Görünümü'nü gizle", Description = "Sanal masaüstü butonunu görev çubuğundan kaldırır.", ApplyRegFile = "Hide_Taskview_Taskbar.reg", UndoRegFile = "Show_Taskview_Taskbar.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableWidgets", Title = "Görev çubuğu ve kilit ekranı widget'larını kapat", Description = "Haber ve hava durumu widget servislerini durdurur.", ApplyRegFile = "Disable_Widgets.reg", UndoRegFile = "Enable_Widgets.reg", RequiresExplorerRestart = true },
                    new() { Id = "EnableEndTask", Title = "Sağ tıkta 'Görevi Sonlandır' seçeneğini göster", Description = "Donan uygulamaları görev çubuğundan anında kapatır.", ApplyRegFile = "Enable_End_Task.reg", UndoRegFile = "Disable_End_Task.reg", RequiresExplorerRestart = true },
                    new() { Id = "EnableLastActiveClick", Title = "Uygulamalar için 'Son Aktif Tıklama'yı aç", Description = "Görev çubuğunda simgeye tıklayınca önizleme yerine son pencereye geçer.", ApplyRegFile = "Enable_Last_Active_Click.reg", UndoRegFile = "Disable_Last_Active_Click.reg", RequiresExplorerRestart = true },
                    new() { Id = "ShowAllTrayIcons", Title = "Tüm sistem tepsisi simgelerini göster", Description = "Arka plan uygulamalarını yukarı ok (^) menüsüne gizlemek yerine doğrudan görev çubuğunda gösterir.", RequiresExplorerRestart = true },
                    new() {
                        Id = "MultiMon",
                        Title = "Görev çubuğu uygulamalarını göster",
                        Description = "Çoklu monitörde açık pencerelerin hangi barda gösterileceğini ayarlar.",
                        IsDropdown = true,
                        RequiresExplorerRestart = true,
                        DropdownOptions = new List<DropdownOption>
                        {
                            new() { Label = "Değişiklik Yok", FeatureId = "", RegFile = "" },
                            new() { Label = "Tüm Görev Çubuklarında (Varsayılan)", FeatureId = "MMTaskbarModeAll", RegFile = "MMTaskbarMode_All.reg" },
                            new() { Label = "Ana Ekran ve Açık Olduğu Ekranda", FeatureId = "MMTaskbarModeMainActive", RegFile = "MMTaskbarMode_Main_Active.reg" },
                            new() { Label = "Yalnızca Açık Olduğu Ekranda", FeatureId = "MMTaskbarModeActive", RegFile = "MMTaskbarMode_Active.reg" }
                        }
                    },
                    new() {
                        Id = "CombineButtons",
                        Title = "Ana ekranda butonları birleştir",
                        Description = "Görev çubuğu açık pencere butonlarının gruplanmasını ayarlar.",
                        IsDropdown = true,
                        RequiresExplorerRestart = true,
                        DropdownOptions = new List<DropdownOption>
                        {
                            new() { Label = "Değişiklik Yok", FeatureId = "", RegFile = "" },
                            new() { Label = "Her Zaman Birleştir (Varsayılan)", FeatureId = "CombineTaskbarAlways", RegFile = "Combine_Taskbar_Always.reg" },
                            new() { Label = "Görev Çubuğu Dolduğunda", FeatureId = "CombineTaskbarWhenFull", RegFile = "Combine_Taskbar_When_Full.reg" },
                            new() { Label = "Asla Birleştirme", FeatureId = "CombineTaskbarNever", RegFile = "Combine_Taskbar_Never.reg" }
                        }
                    },
                    new() {
                        Id = "CombineMMButtons",
                        Title = "İkincil ekranlarda butonları birleştir",
                        Description = "İkincil monitörlerde butonların gruplanmasını ayarlar.",
                        IsDropdown = true,
                        RequiresExplorerRestart = true,
                        DropdownOptions = new List<DropdownOption>
                        {
                            new() { Label = "Değişiklik Yok", FeatureId = "", RegFile = "" },
                            new() { Label = "Her Zaman Birleştir (Varsayılan)", FeatureId = "CombineMMTaskbarAlways", RegFile = "Combine_MMTaskbar_Always.reg" },
                            new() { Label = "Görev Çubuğu Dolduğunda", FeatureId = "CombineMMTaskbarWhenFull", RegFile = "Combine_MMTaskbar_When_Full.reg" },
                            new() { Label = "Asla Birleştirme", FeatureId = "CombineMMTaskbarNever", RegFile = "Combine_MMTaskbar_Never.reg" }
                        }
                    }
                }
            });

            // 11. Oyun (Gaming)
            col3.Add(new TweakCategoryGroup
            {
                Id = "Gaming",
                Title = "Oyun (Gaming)",
                Icon = "🎮",
                Items = new List<TweakItem>
                {
                    new() { Id = "DisableDVR", Title = "Xbox oyun/ekran kaydını kapat", Description = "Arka planda video kaydederek FPS düşüren servisi durdurur.", ApplyRegFile = "Disable_DVR.reg", UndoRegFile = "Enable_DVR.reg" },
                    new() { Id = "DisableGameBarIntegration", Title = "Xbox Game Bar entegrasyonunu kapat", Description = "Oyunlarda Win+G katmanını devre dışı bırakır.", ApplyRegFile = "Disable_Game_Bar_Integration.reg", UndoRegFile = "Enable_Game_Bar_Integration.reg" }
                }
            });

            // 12. Diğer (Other)
            col3.Add(new TweakCategoryGroup
            {
                Id = "Other",
                Title = "Diğer Ayarlar & Hızlandırmalar",
                Icon = "⚙️",
                Items = new List<TweakItem>
                {
                    new() { Id = "ReduceMenuDelay", Title = "Menü açılış gecikmesini sıfırla (MenuShowDelay)", Description = "Sağ tık ve açılır menülerin 400ms yapay gecikme olmadan anında açılmasını sağlar.", ApplyRegFile = "Reduce_Menu_Show_Delay.reg", UndoRegFile = "Restore_Menu_Show_Delay.reg", RequiresExplorerRestart = true },
                    new() { Id = "DisableStartupDelay", Title = "Windows başlangıç gecikmesini sıfırla (StartupDelay)", Description = "Masaüstü açıldıktan sonra başlangıç programlarının 10 sn beklemeden anında yüklenmesini sağlar.", ApplyRegFile = "Disable_Startup_Delay.reg", UndoRegFile = "Enable_Startup_Delay.reg", RequiresExplorerRestart = true },
                    new() { 
                        Id = "DisableRestartApps", 
                        Title = "Açılışta açık kalan uygulamaların otomatik yeniden açılmasını engelle", 
                        Description = "Bilgisayar açıldığında önceki oturumdan kalan klasör ve uygulamaların ekrana fırlamasını engeller, temiz bir başlangıç sağlar.", 
                        ApplyRegFile = "Disable_Restart_Apps.reg", 
                        UndoRegFile = "Enable_Restart_Apps.reg" 
                    },
                    new() { 
                        Id = "DisableBrowserBackgroundApps", 
                        Title = "Tarayıcıların arkada çalışmasını ve RAM tüketmesini engelle", 
                        Description = "Edge, Chrome, Brave ve Opera GX kapatıldığında arka planda açık kalarak RAM ve CPU tüketen gizli servisleri engeller.", 
                        ApplyRegFile = "Disable_Browser_Background_Apps.reg", 
                        UndoRegFile = "Enable_Browser_Background_Apps.reg" 
                    },
                    new() { Id = "ReduceKillWaitTimeout", Title = "Donan / yanıt vermeyen uygulamaları hızlı sonlandır (WaitToKill)", Description = "Kapanırken veya yeniden başlarken donan programların bekleme süresini 2 saniyeye indirir.", ApplyRegFile = "Reduce_Wait_To_Kill_Timeout.reg", UndoRegFile = "Restore_Wait_To_Kill_Timeout.reg" },
                    new() { 
                        Id = "AddThisPCToDesktop", 
                        Title = "Masaüstüne 'Bu Bilgisayar' simgesini ekle", 
                        Description = "Windows 11 masaüstüne klasik 'Bu Bilgisayar' (This PC) kısayolunu ekler veya kaldırır.", 
                        ApplyRegFile = "Add_This_PC_To_Desktop.reg", 
                        UndoRegFile = "Remove_This_PC_From_Desktop.reg", 
                        RequiresExplorerRestart = true,
                        ActionButtonText = "🖥️ Simge Ayarları",
                        ActionButtonToolTip = "Windows klasik Masaüstü Simge Ayarları penceresini aç"
                    },
                    new() { 
                        Id = "SetSolidBlackBackground", 
                        Title = "Masaüstü arka planını düz siyah yap", 
                        Description = "Yüksek çözünürlüklü duvar kağıdı yerine düz siyah arka plan uygulayarak GPU/RAM yükünü azaltır ve simgeleri netleştirir.", 
                        ApplyRegFile = "Set_Solid_Black_Background.reg", 
                        UndoRegFile = "Restore_Default_Wallpaper.reg"
                    },
                    new() { Id = "DisableSettingsHome", Title = "Ayarlar 'Giriş' sayfasını gizle", Description = "Ayarlar açıldığında karmaşık Giriş sayfası yerine Sistemi açar.", ApplyRegFile = "Disable_Settings_Home.reg", UndoRegFile = "Enable_Settings_Home.reg" },
                    new() { Id = "DisableBraveBloat", Title = "Brave tarayıcısındaki gereksiz eklentileri kapat", Description = "Brave içindeki Kripto Cüzdan, AI Leo ve VPN eklentilerini kapatır.", ApplyRegFile = "Disable_Brave_Bloat.reg", UndoRegFile = "Enable_Brave_Bloat.reg" },
                    new() { 
                        Id = "DisableHibernation", 
                        Title = "Hazırda bekletmeyi kapat (hiberfil.sys)", 
                        Description = "Sistem belleği kadar yer kaplayabilen hiberfil.sys dosyasını kaldırarak depolama alanı kazandırır.", 
                        RequiresReboot = false 
                    }
                }
            });

            return (col1, col2, col3);
        }

        public static List<TweakCategoryGroup> GetAllCategories()
        {
            var (c1, c2, c3) = GetColumns();
            var all = new List<TweakCategoryGroup>();
            all.AddRange(c1);
            all.AddRange(c2);
            all.AddRange(c3);
            return all;
        }

        public static List<TweakItem> GetAllTweaks()
        {
            var categories = GetAllCategories();
            var all = new List<TweakItem>();
            foreach (var g in categories) all.AddRange(g.Items);
            return all;
        }
    }
}
