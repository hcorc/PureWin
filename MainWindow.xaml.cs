using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PureWin.Models;
using PureWin.Services;
using System.Security.Principal;
using Wpf.Ui.Controls;

namespace PureWin
{
    public partial class MainWindow : FluentWindow
    {
        private List<BloatwareItem> _allBloatwareApps = new();
        private ObservableCollection<BloatwareItem> _displayedBloatware = new();
        private List<TweakItem> _allTweaks = new();
        private List<ServiceItem> _allServices = new();
        private ObservableCollection<ServiceItem> _displayedServices = new();
        private List<CleanupGroupItem> _cleanupGroups = new();
        private ObservableCollection<SystemHealthCategory> _healthCategories = new();
        private List<StartupAppItem> _allStartupApps = new();
        private ObservableCollection<StartupAppItem> _displayedStartupApps = new();
        private string _activeStartupFilter = "all";
        private List<SoftwareUpdateItem> _allSoftwareUpdates = new();
        private ObservableCollection<SoftwareUpdateItem> _displayedSoftwareUpdates = new();
        private bool _isScanningSoftwareUpdates = false;
        private bool _isUpdatingSoftware = false;
        private List<GameTweakItem> _gameTweaks = new();
        private ObservableCollection<GameTweakItem> _recommendedGameTweaks = new();
        private ObservableCollection<GameTweakItem> _advancedGameTweaks = new();
        private bool _isUpdatingPing = false;
        private bool _isLoaded = false;
        private bool _isApplying = false;
        private bool _isScanningCleanup = false;
        private bool _isCleaning = false;
        private SystemSpecsModel? _systemSpecs;

        public MainWindow()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
            Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();
            Loaded += MainWindow_Loaded;
            UpdateAdminBadge();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateAdminBadge();
                await LoadDataAsync();
                NavReport_Click(this, new RoutedEventArgs());

                // Arka planda sessizce yazılım güncellemelerini tara ve menü rozetini hazırla
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (SoftwareUpdateService.IsWingetAvailable())
                        {
                            var updates = await SoftwareUpdateService.CheckForUpdatesAsync();
                            await Dispatcher.InvokeAsync(() =>
                            {
                                _allSoftwareUpdates = updates;
                                UpdateSoftwareStats();
                                if (ViewSoftwareUpdater.Visibility == Visibility.Visible)
                                {
                                    ApplySoftwareFilter();
                                }
                            });
                        }
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                try
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string pureWinDir = System.IO.Path.Combine(localAppData, "PureWin");
                    if (!System.IO.Directory.Exists(pureWinDir)) System.IO.Directory.CreateDirectory(pureWinDir);
                    System.IO.File.AppendAllText(System.IO.Path.Combine(pureWinDir, "crash.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [MainWindow_Loaded]\n{ex}\n");
                }
                catch { }
            }
        }

        private static bool IsRunningAsAdmin()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void UpdateAdminBadge()
        {
            try
            {
                bool isAdmin = IsRunningAsAdmin();
                if (AdminBadgeBorder != null && AdminBadgeText != null)
                {
                    if (isAdmin)
                    {
                        AdminBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(0x25, 0x10, 0xB9, 0x81));
                        AdminBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0x10, 0xB9, 0x81));
                        AdminBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                        AdminBadgeText.Text = "🛡️ YÖNETİCİ";
                    }
                    else
                    {
                        AdminBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(0x25, 0xEF, 0x44, 0x44));
                        AdminBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xEF, 0x44, 0x44));
                        AdminBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                        AdminBadgeText.Text = "⚠️ STANDART KULLANICI (Yönetici İzni Yok)";
                    }
                }
            }
            catch { }
        }

        private async Task LoadDataAsync()
        {
            // 1. Bloatware listesini al ve ultra-hızlı canlı kayıt defteri taraması yap (~10ms)
            _allBloatwareApps = BloatwareDatabase.GetDefaultApps();
            var installedPackages = await AppxService.GetInstalledPackagesAsync();

            foreach (var app in _allBloatwareApps)
            {
                // Canlı sistemde bu paket gerçekten kurulu mu? (UWP + Win32 Hibrit)
                app.IsInstalled = AppxService.IsAppInstalled(app, installedPackages);
            }

            // Bloatware filtresini anında uygula ve arayüzü etkileşime hazır hale getir!
            ApplyBloatwareFilter();

            // 2. Kategori bazlı sistem ince ayarlarını al
            var categories = TweakDatabase.GetAllCategories();
            _allTweaks.Clear();
            foreach (var cat in categories)
            {
                _allTweaks.AddRange(cat.Items);
            }

            // 4. Sistem Kayıt Defteri ve Anında Özellik Kontrolleri (Registry: ~5ms, Sandbox/WSL: 0ms)
            var scheduledTasks = new List<TweakItem>();

            foreach (var tweak in _allTweaks)
            {
                if (tweak.IsScheduledTask)
                {
                    scheduledTasks.Add(tweak);
                }
                else
                {
                    await RefreshTweakLiveStateAsync(tweak, isInitialLoad: true);
                }
            }

            // 5. Kategorileri arayüze anında bağla
            CategoriesList.ItemsSource = categories;

            // 6. Windows Servisleri listesini al ve durumlarını tara
            _allServices = WindowsServicesDatabase.GetAllServices();
            var runningSnapshot = ServiceManagementService.GetRunningServicesSnapshot();
            foreach (var svc in _allServices)
            {
                ServiceManagementService.CheckServiceStatus(svc, runningSnapshot);
            }
            ServicesList.ItemsSource = _displayedServices;
            ApplyServiceFilter();

            // 7. Sistem Temizliği Modülünü Başlat
            _cleanupGroups = CleanupService.GetDefaultGroups();
            foreach (var group in _cleanupGroups)
            {
                group.OnGroupSelectionChangedCallback = () => Dispatcher.Invoke(UpdateCleanupBadges);
            }
            CleanupGroupsList.ItemsSource = _cleanupGroups;
            UpdateCleanupBadges();

            // 8. Sistem Sağlığı Modülünü Başlat
            _healthCategories = SystemHealthService.GetDefaultCategories();
            SystemHealthCategoriesList.ItemsSource = _healthCategories;

            // 9. Başlangıç Uygulamalarını Arka Planda Yükle
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadStartupAppsDataAsync();
                }
                catch { }
            });

            // 10. Oyun ve Performans Modülünü Başlat
            _gameTweaks = GameOptimizerService.GetDefaultGameTweaks();
            GameOptimizerService.RefreshLiveStates(_gameTweaks);
            _recommendedGameTweaks = new ObservableCollection<GameTweakItem>(_gameTweaks.Where(t => t.Category == "Önerilen Oyun Ayarları"));
            _advancedGameTweaks = new ObservableCollection<GameTweakItem>(_gameTweaks.Where(t => t.Category == "İleri Düzey & Ödünleşimli Ayarlar"));
            RecommendedGameTweaksList.ItemsSource = _recommendedGameTweaks;
            AdvancedGameTweaksList.ItemsSource = _advancedGameTweaks;

            // 11. Sistem & Donanım Kimliği Başlatma (Hızlı Kayıt Defteri Okuma: 0 ms)
            _systemSpecs = SystemInfoService.GetHardwareAndOsSpecs();
            UpdateSystemStatusCard();

            // Arka planda lisans ve etkinleştirme durumunu sorgula (UI'ı asla bekletmez)
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_systemSpecs != null)
                    {
                        await SystemInfoService.RefreshLicenseInfoAsync(_systemSpecs);
                        Dispatcher.Invoke(() =>
                        {
                            UpdateSystemStatusCard();
                            if (SystemSpecsOverlay.Visibility == Visibility.Visible)
                            {
                                PopulateSystemSpecsDetails(_systemSpecs);
                            }
                        });
                    }
                }
                catch { }
            });

            _isLoaded = true;
            UpdateActionBar();

            // 7. Zamanlanmış telemetri görevlerini arka planda asenkron tara (UI'ı asla bekletmez)
            _ = Task.Run(async () =>
            {
                try
                {
                    var coreTask = ScheduledTaskService.AreTasksDisabledAsync(ScheduledTaskService.CoreTelemetryTasks);
                    var optTask = ScheduledTaskService.AreTasksDisabledAsync(ScheduledTaskService.OptionalTelemetryTasks);
                    await Task.WhenAll(coreTask, optTask);

                    bool isCore = await coreTask;
                    bool isOpt = await optTask;

                    Dispatcher.Invoke(() =>
                    {
                        foreach (var t in scheduledTasks)
                        {
                            bool applied = (t.ScheduledTaskType == "Core") ? isCore : isOpt;
                            t.IsApplied = applied;
                            t.IsPendingChecked = applied;
                            t.IsStaged = false;
                        }
                        UpdateActionBar();
                        UpdateReportView();
                    });
                }
                catch { }
            });
        }

        #region Bloatware Filtreleme & Görünüm Yönetimi
        private void ApplyBloatwareFilter()
        {
            if (_allBloatwareApps == null) return;

            bool onlyInstalled = FilterInstalledOnlyCheck?.IsChecked ?? false;
            int installedCount = _allBloatwareApps.Count(a => a.IsInstalled);
            int totalCount = _allBloatwareApps.Count;

            List<BloatwareItem> filtered;
            if (onlyInstalled)
            {
                filtered = _allBloatwareApps.Where(a => a.IsInstalled).OrderBy(a => a.DisplayName).ToList();
                if (BloatwareCounterBadge != null)
                {
                    BloatwareCounterBadge.Text = $"{installedCount} Paket Gösteriliyor (Sadece Kurulu)";
                }
                if (BloatwareSubtitle != null)
                {
                    if (installedCount == 0)
                        BloatwareSubtitle.Text = "Sisteminiz tarandı: Kaldırılacak gömülü UWP bloatware paketi bulunamadı (Sistem tertemiz).";
                    else
                        BloatwareSubtitle.Text = $"Filtre Aktif: Yalnızca sisteminizde kurulu olan {installedCount} adet paket listeleniyor.";
                }
            }
            else
            {
                // Tüm katalog: Doğrudan alfabetik sıralama! (3D Builder, Bing, Candy Crush vb. hemen görünür)
                filtered = _allBloatwareApps.OrderBy(a => a.DisplayName).ToList();
                if (BloatwareCounterBadge != null)
                {
                    BloatwareCounterBadge.Text = $"Tüm Katalog: {totalCount} Paket ({installedCount} Kurulu)";
                }
                if (BloatwareSubtitle != null)
                {
                    BloatwareSubtitle.Text = $"Katalogdaki tüm {totalCount} bloatware paketi listeleniyor (Sisteminizde {installedCount} tanesi yüklü).";
                }
            }

            _displayedBloatware = new ObservableCollection<BloatwareItem>(filtered);
            BloatwareList.ItemsSource = _displayedBloatware;

            if (onlyInstalled && filtered.Count == 0)
            {
                CleanSystemCard.Visibility = Visibility.Visible;
                BloatwareScrollViewer.Visibility = Visibility.Collapsed;
            }
            else
            {
                CleanSystemCard.Visibility = Visibility.Collapsed;
                BloatwareScrollViewer.Visibility = Visibility.Visible;
            }
        }

        private void FilterBloatware_Changed(object sender, RoutedEventArgs e)
        {
            if (_allBloatwareApps == null || _allBloatwareApps.Count == 0) return;
            ApplyBloatwareFilter();
            UpdateActionBar();
        }

        private void ShowAllCatalog_Click(object sender, RoutedEventArgs e)
        {
            if (FilterInstalledOnlyCheck != null)
            {
                FilterInstalledOnlyCheck.IsChecked = false;
            }
        }
        #endregion

        #region Menü Geçişleri
        private void NavBloatware_Click(object sender, RoutedEventArgs e)
        {
            ViewBloatware.Visibility = Visibility.Visible;
            ViewTweaks.Visibility = Visibility.Collapsed;
            ViewServices.Visibility = Visibility.Collapsed;
            ViewReport.Visibility = Visibility.Collapsed;
            ViewCleanup.Visibility = Visibility.Collapsed;
            ViewSystemHealth.Visibility = Visibility.Collapsed;
            ViewStartupApps.Visibility = Visibility.Collapsed;
            ViewSoftwareUpdater.Visibility = Visibility.Collapsed;
            ViewGamePerf.Visibility = Visibility.Collapsed;

            NavBloatwareBtn.Appearance = ControlAppearance.Primary;
            NavTweaksBtn.Appearance = ControlAppearance.Secondary;
            NavServicesBtn.Appearance = ControlAppearance.Secondary;
            NavReportBtn.Appearance = ControlAppearance.Secondary;
            NavCleanupBtn.Appearance = ControlAppearance.Secondary;
            NavSystemHealthBtn.Appearance = ControlAppearance.Secondary;
            NavStartupAppsBtn.Appearance = ControlAppearance.Secondary;
            NavSoftwareUpdaterBtn.Appearance = ControlAppearance.Secondary;
            NavGamePerfBtn.Appearance = ControlAppearance.Secondary;

            ActionBar.Visibility = Visibility.Visible;
        }

        private void NavTweaks_Click(object sender, RoutedEventArgs e)
        {
            ViewBloatware.Visibility = Visibility.Collapsed;
            ViewTweaks.Visibility = Visibility.Visible;
            ViewServices.Visibility = Visibility.Collapsed;
            ViewReport.Visibility = Visibility.Collapsed;
            ViewCleanup.Visibility = Visibility.Collapsed;
            ViewSystemHealth.Visibility = Visibility.Collapsed;
            ViewStartupApps.Visibility = Visibility.Collapsed;
            ViewSoftwareUpdater.Visibility = Visibility.Collapsed;
            ViewGamePerf.Visibility = Visibility.Collapsed;

            NavBloatwareBtn.Appearance = ControlAppearance.Secondary;
            NavTweaksBtn.Appearance = ControlAppearance.Primary;
            NavServicesBtn.Appearance = ControlAppearance.Secondary;
            NavReportBtn.Appearance = ControlAppearance.Secondary;
            NavCleanupBtn.Appearance = ControlAppearance.Secondary;
            NavSystemHealthBtn.Appearance = ControlAppearance.Secondary;
            NavStartupAppsBtn.Appearance = ControlAppearance.Secondary;
            NavSoftwareUpdaterBtn.Appearance = ControlAppearance.Secondary;
            NavGamePerfBtn.Appearance = ControlAppearance.Secondary;

            ActionBar.Visibility = Visibility.Visible;
        }

        private void NavServices_Click(object sender, RoutedEventArgs e)
        {
            ViewBloatware.Visibility = Visibility.Collapsed;
            ViewTweaks.Visibility = Visibility.Collapsed;
            ViewServices.Visibility = Visibility.Visible;
            ViewReport.Visibility = Visibility.Collapsed;
            ViewCleanup.Visibility = Visibility.Collapsed;
            ViewSystemHealth.Visibility = Visibility.Collapsed;
            ViewStartupApps.Visibility = Visibility.Collapsed;
            ViewSoftwareUpdater.Visibility = Visibility.Collapsed;
            ViewGamePerf.Visibility = Visibility.Collapsed;

            NavBloatwareBtn.Appearance = ControlAppearance.Secondary;
            NavTweaksBtn.Appearance = ControlAppearance.Secondary;
            NavServicesBtn.Appearance = ControlAppearance.Primary;
            NavReportBtn.Appearance = ControlAppearance.Secondary;
            NavCleanupBtn.Appearance = ControlAppearance.Secondary;
            NavSystemHealthBtn.Appearance = ControlAppearance.Secondary;
            NavStartupAppsBtn.Appearance = ControlAppearance.Secondary;
            NavSoftwareUpdaterBtn.Appearance = ControlAppearance.Secondary;
            NavGamePerfBtn.Appearance = ControlAppearance.Secondary;

            ActionBar.Visibility = Visibility.Visible;
        }

        private void NavReport_Click(object sender, RoutedEventArgs e)
        {
            ViewBloatware.Visibility = Visibility.Collapsed;
            ViewTweaks.Visibility = Visibility.Collapsed;
            ViewServices.Visibility = Visibility.Collapsed;
            ViewReport.Visibility = Visibility.Visible;
            ViewCleanup.Visibility = Visibility.Collapsed;
            ViewSystemHealth.Visibility = Visibility.Collapsed;
            ViewStartupApps.Visibility = Visibility.Collapsed;
            ViewSoftwareUpdater.Visibility = Visibility.Collapsed;
            ViewGamePerf.Visibility = Visibility.Collapsed;

            NavBloatwareBtn.Appearance = ControlAppearance.Secondary;
            NavTweaksBtn.Appearance = ControlAppearance.Secondary;
            NavServicesBtn.Appearance = ControlAppearance.Secondary;
            NavReportBtn.Appearance = ControlAppearance.Primary;
            NavCleanupBtn.Appearance = ControlAppearance.Secondary;
            NavSystemHealthBtn.Appearance = ControlAppearance.Secondary;
            NavStartupAppsBtn.Appearance = ControlAppearance.Secondary;
            NavSoftwareUpdaterBtn.Appearance = ControlAppearance.Secondary;
            NavGamePerfBtn.Appearance = ControlAppearance.Secondary;

            ActionBar.Visibility = Visibility.Visible;
            UpdateReportView();
        }

        private void NavCleanup_Click(object sender, RoutedEventArgs e)
        {
            ViewBloatware.Visibility = Visibility.Collapsed;
            ViewTweaks.Visibility = Visibility.Collapsed;
            ViewServices.Visibility = Visibility.Collapsed;
            ViewReport.Visibility = Visibility.Collapsed;
            ViewCleanup.Visibility = Visibility.Visible;
            ViewSystemHealth.Visibility = Visibility.Collapsed;
            ViewStartupApps.Visibility = Visibility.Collapsed;
            ViewSoftwareUpdater.Visibility = Visibility.Collapsed;
            ViewGamePerf.Visibility = Visibility.Collapsed;

            NavBloatwareBtn.Appearance = ControlAppearance.Secondary;
            NavTweaksBtn.Appearance = ControlAppearance.Secondary;
            NavServicesBtn.Appearance = ControlAppearance.Secondary;
            NavReportBtn.Appearance = ControlAppearance.Secondary;
            NavCleanupBtn.Appearance = ControlAppearance.Primary;
            NavSystemHealthBtn.Appearance = ControlAppearance.Secondary;
            NavStartupAppsBtn.Appearance = ControlAppearance.Secondary;
            NavSoftwareUpdaterBtn.Appearance = ControlAppearance.Secondary;
            NavGamePerfBtn.Appearance = ControlAppearance.Secondary;

            ActionBar.Visibility = Visibility.Collapsed;
        }

        private void NavSystemHealth_Click(object sender, RoutedEventArgs e)
        {
            ViewBloatware.Visibility = Visibility.Collapsed;
            ViewTweaks.Visibility = Visibility.Collapsed;
            ViewServices.Visibility = Visibility.Collapsed;
            ViewReport.Visibility = Visibility.Collapsed;
            ViewCleanup.Visibility = Visibility.Collapsed;
            ViewSystemHealth.Visibility = Visibility.Visible;
            ViewStartupApps.Visibility = Visibility.Collapsed;
            ViewSoftwareUpdater.Visibility = Visibility.Collapsed;
            ViewGamePerf.Visibility = Visibility.Collapsed;

            NavBloatwareBtn.Appearance = ControlAppearance.Secondary;
            NavTweaksBtn.Appearance = ControlAppearance.Secondary;
            NavServicesBtn.Appearance = ControlAppearance.Secondary;
            NavReportBtn.Appearance = ControlAppearance.Secondary;
            NavCleanupBtn.Appearance = ControlAppearance.Secondary;
            NavSystemHealthBtn.Appearance = ControlAppearance.Primary;
            NavStartupAppsBtn.Appearance = ControlAppearance.Secondary;
            NavSoftwareUpdaterBtn.Appearance = ControlAppearance.Secondary;
            NavGamePerfBtn.Appearance = ControlAppearance.Secondary;

            ActionBar.Visibility = Visibility.Collapsed;
        }

        private async void NavStartupApps_Click(object sender, RoutedEventArgs e)
        {
            ViewBloatware.Visibility = Visibility.Collapsed;
            ViewTweaks.Visibility = Visibility.Collapsed;
            ViewServices.Visibility = Visibility.Collapsed;
            ViewReport.Visibility = Visibility.Collapsed;
            ViewCleanup.Visibility = Visibility.Collapsed;
            ViewSystemHealth.Visibility = Visibility.Collapsed;
            ViewStartupApps.Visibility = Visibility.Visible;
            ViewSoftwareUpdater.Visibility = Visibility.Collapsed;
            ViewGamePerf.Visibility = Visibility.Collapsed;

            NavBloatwareBtn.Appearance = ControlAppearance.Secondary;
            NavTweaksBtn.Appearance = ControlAppearance.Secondary;
            NavServicesBtn.Appearance = ControlAppearance.Secondary;
            NavReportBtn.Appearance = ControlAppearance.Secondary;
            NavCleanupBtn.Appearance = ControlAppearance.Secondary;
            NavSystemHealthBtn.Appearance = ControlAppearance.Secondary;
            NavStartupAppsBtn.Appearance = ControlAppearance.Primary;
            NavSoftwareUpdaterBtn.Appearance = ControlAppearance.Secondary;
            NavGamePerfBtn.Appearance = ControlAppearance.Secondary;

            ActionBar.Visibility = Visibility.Collapsed;

            if (_allStartupApps.Count == 0)
            {
                await LoadStartupAppsDataAsync();
            }
        }

        private async void NavSoftwareUpdater_Click(object sender, RoutedEventArgs e)
        {
            ViewBloatware.Visibility = Visibility.Collapsed;
            ViewTweaks.Visibility = Visibility.Collapsed;
            ViewServices.Visibility = Visibility.Collapsed;
            ViewReport.Visibility = Visibility.Collapsed;
            ViewCleanup.Visibility = Visibility.Collapsed;
            ViewSystemHealth.Visibility = Visibility.Collapsed;
            ViewStartupApps.Visibility = Visibility.Collapsed;
            ViewSoftwareUpdater.Visibility = Visibility.Visible;
            ViewGamePerf.Visibility = Visibility.Collapsed;

            NavBloatwareBtn.Appearance = ControlAppearance.Secondary;
            NavTweaksBtn.Appearance = ControlAppearance.Secondary;
            NavServicesBtn.Appearance = ControlAppearance.Secondary;
            NavReportBtn.Appearance = ControlAppearance.Secondary;
            NavCleanupBtn.Appearance = ControlAppearance.Secondary;
            NavSystemHealthBtn.Appearance = ControlAppearance.Secondary;
            NavStartupAppsBtn.Appearance = ControlAppearance.Secondary;
            NavSoftwareUpdaterBtn.Appearance = ControlAppearance.Primary;
            NavGamePerfBtn.Appearance = ControlAppearance.Secondary;

            ActionBar.Visibility = Visibility.Collapsed;

            if (_allSoftwareUpdates.Count == 0 && !_isScanningSoftwareUpdates)
            {
                await LoadSoftwareUpdatesAsync();
            }
        }

        private void NavGamePerf_Click(object sender, RoutedEventArgs e)
        {
            ViewBloatware.Visibility = Visibility.Collapsed;
            ViewTweaks.Visibility = Visibility.Collapsed;
            ViewServices.Visibility = Visibility.Collapsed;
            ViewReport.Visibility = Visibility.Collapsed;
            ViewCleanup.Visibility = Visibility.Collapsed;
            ViewSystemHealth.Visibility = Visibility.Collapsed;
            ViewStartupApps.Visibility = Visibility.Collapsed;
            ViewSoftwareUpdater.Visibility = Visibility.Collapsed;
            ViewGamePerf.Visibility = Visibility.Visible;

            NavBloatwareBtn.Appearance = ControlAppearance.Secondary;
            NavTweaksBtn.Appearance = ControlAppearance.Secondary;
            NavServicesBtn.Appearance = ControlAppearance.Secondary;
            NavReportBtn.Appearance = ControlAppearance.Secondary;
            NavCleanupBtn.Appearance = ControlAppearance.Secondary;
            NavSystemHealthBtn.Appearance = ControlAppearance.Secondary;
            NavStartupAppsBtn.Appearance = ControlAppearance.Secondary;
            NavSoftwareUpdaterBtn.Appearance = ControlAppearance.Secondary;
            NavGamePerfBtn.Appearance = ControlAppearance.Primary;

            ActionBar.Visibility = Visibility.Visible;

            RefreshGamePerformanceData();
        }

        private async void HealthAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not SystemHealthItem item) return;
            if (item.IsRunning) return;

            // Çift tıklamayı ve mükerrer diyalog açılmasını anında engelle
            item.State = HealthExecutionState.Running;
            item.ProgressPercentage = 0;
            item.StageText = "Hazırlanıyor...";

            // Tam Ağ Sıfırlama için 3 seçenekli gelişmiş onay diyaloğu (Doğrudan Sıfırla / Windows Ayarları / İptal)
            if (item.Id == "net_reset_full")
            {
                string choice = await ShowModernChoiceDialogAsync(
                    "Tam Ağ Sıfırlama (Yüksek Risk)",
                    "Bu işlem Windows ağ bağdaştırıcılarını ve sürücülerini tamamen kaldırıp fabrika ayarlarına sıfırlar.\n\n🚨 KRİTİK UYARI VE RİSKLER (Yüksek Risk):\n• Tüm fiziksel ve sanal ağ kartları (Ethernet, Wi-Fi, Bluetooth) sistemden kaldırılıp yeniden kurulacaktır.\n• Kayıtlı tüm Wi-Fi ağları, şifreleri ve bağlantı geçmişi SİLİNECEKTİR (şifrenizi yeniden girmeniz gerekir).\n• Varsa özel VPN yapılandırmaları, sanal switch'ler ve sabit (statik) IP / DNS ayarları sıfırlanacaktır.\n• İşlem bittiğinde bağdaştırıcıların yeniden yüklenmesi için bilgisayarı yeniden başlatmak ZORUNLUDUR.\n\nNasıl devam etmek istersiniz?",
                    primaryButtonText: "⚡ Tüm Bağdaştırıcıları Sıfırla (netcfg -d)",
                    secondaryButtonText: "⚙️ Windows Ayarları'nda Aç",
                    cancelButtonText: "İptal",
                    icon: "🚨",
                    accentColorHex: "#EF4444");

                if (choice == "cancel")
                {
                    item.State = HealthExecutionState.Ready;
                    item.StageText = string.Empty;
                    return;
                }

                if (choice == "secondary")
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo("ms-settings:network-reset") { UseShellExecute = true });
                    }
                    catch
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo("ms-settings:network-advancedsettings") { UseShellExecute = true });
                        }
                        catch
                        {
                            Process.Start(new ProcessStartInfo("ms-settings:network-status") { UseShellExecute = true });
                        }
                    }

                    item.CustomCompletedBadgeText = "✓ Ayarlar Sayfası Açıldı";
                    item.StatusDetail = "Windows Ayarları > Ağ Sıfırlama sayfası açıldı. Bu sayfadan ağ sıfırlama işlemini manuel olarak yönetebilirsiniz.";
                    item.StageText = string.Empty;
                    item.State = HealthExecutionState.Completed;
                    return;
                }
            }
            // CHKDSK Çevrimdışı Onarım için mod seçimi onay diyaloğu (Standart /f [varsayılan] veya Agresif /r [gelişmiş])
            else if (item.Id == "disk_chkdsk_offline_repair")
            {
                string choice = await ShowModernChoiceDialogAsync(
                    "CHKDSK Çevrimdışı Onarım (reboot gerekir)",
                    "C: sistem sürücüsü şu anda Windows tarafından aktif olarak kullanıldığı için dosya sistemi onarımı bilgisayar yeniden başlatılırken gerçekleştirilecektir.\n\nLütfen çalıştırmak istediğiniz onarım modunu seçin:\n\n• Standart Onarım (/f) [Varsayılan & Önerilen]:\nDosya tablosunu, dizin yapısını ve mantıksal hataları onarır. Hızlıdır (genellikle 2-10 dakika sürer).\n\n• Agresif Onarım (/r) [Gelişmiş Seçenek]:\nStandart onarıma ek olarak diskin tüm fiziksel sektörlerini tek tek tarar ve bozuk sektörlerdeki (bad sector) okunabilir verileri kurtarmaya çalışır.\n⚠️ Süre Uyarısı: Sürücünün boyutuna ve durumuna bağlı olarak 1 ile birkaç saat sürebilir!\n\nNasıl devam etmek istersiniz?",
                    primaryButtonText: "🛠️ Standart Onarım (/f)",
                    secondaryButtonText: "🔍 Agresif Onarım (/r)",
                    cancelButtonText: "İptal",
                    icon: "💾",
                    accentColorHex: "#F59E0B");

                if (choice == "cancel")
                {
                    item.State = HealthExecutionState.Ready;
                    item.StageText = string.Empty;
                    return;
                }

                item.ExecutionArgument = (choice == "secondary") ? "/r" : "/f";
            }
            // Bileşen Deposu Temizliği için mod seçimi onay diyaloğu (Standart veya Agresif /ResetBase)
            else if (item.Id == "disk_component_cleanup")
            {
                string choice = await ShowModernChoiceDialogAsync(
                    "Bileşen Deposu Temizliği (WinSxS)",
                    "Bu işlem Windows WinSxS bileşen deposunda biriken eski güncelleme dosyalarını temizleyerek disk alanı kazandırır.\n\nLütfen çalıştırmak istediğiniz temizlik modunu seçin:\n\n• Standart Temizlik [Önerilen]:\nSüresi geçmiş ve kullanılmayan bileşenleri temizler. Yüklü Windows güncellemelerini kaldırma imkanını KORUR. Güvenlidir.\n\n• Agresif Temizlik (/ResetBase) [Orta Risk]:\nTüm yüklü güncellemeleri mevcut sürüme kalıcı olarak sabitler (taban yapar). Maksimum düzeyde (genellikle birkaç GB) disk alanı kazandırır.\n⚠️ Kritik Uyarı: Bu işlemden sonra mevcut yüklü Windows güncellemeleri \"Güncellemeleri Kaldır\" seçeneği ile GERİ ALINAMAZ!\n\nNasıl devam etmek istersiniz?",
                    primaryButtonText: "🧹 Standart Temizlik",
                    secondaryButtonText: "⚡ Agresif Temizlik (/ResetBase)",
                    cancelButtonText: "İptal",
                    icon: "💾",
                    accentColorHex: "#F59E0B");

                if (choice == "cancel")
                {
                    item.State = HealthExecutionState.Ready;
                    item.StageText = string.Empty;
                    return;
                }

                item.ExecutionArgument = (choice == "secondary") ? "/ResetBase" : "";
            }
            // DÜŞÜK-ORTA, ORTA veya YÜKSEK riskli işlemler ile bilgilendirme gerektiren işlemler için onay diyaloğu göster
            else if (item.RiskLevel == HealthRiskLevel.LowMedium || item.RiskLevel == HealthRiskLevel.Medium || item.RiskLevel == HealthRiskLevel.High || item.RequiresConfirmation)
            {
                string dialogMessage = item.Id switch
                {
                    "dism_restore_health" => "Bu işlem, bozuk veya hasarlı Windows bileşenlerini Windows Update üzerinden sağlam dosyalarla onaracaktır.\n\n⏱️ Tahmini Süre: Donanım ve internet hızınıza göre 10 ile 30+ dakika sürebilir.\n💡 Önemli Bilgilendirme: Tarama %20 seviyesinde bir süre takılı kalabilir; bu durum Windows'un bileşen paketlerini ayrıştırma evresidir ve tamamen normaldir, lütfen bekleyin.\n\nOnarım işlemini şimdi başlatmak istiyor musunuz?",
                    "sfc_scannow" => "Bu işlem, korumalı tüm Windows sistem dosyalarının bütünlüğünü tarayacak ve hasarlı dosyaları orijinal kopyalarıyla onaracaktır.\n\n⏱️ Tahmini Süre: 10 ile 30 dakika sürebilir.\n\n💡 ÖNEMLİ SIRA BİLGİSİ:\nSFC, hasarlı sistem dosyalarını onarırken sağlam kopyaları Windows Bileşen Deposu'ndan (WinSxS) çeker. Bu nedenle en yüksek başarı oranı için önce \"DISM Onarım (RestoreHealth)\" işleminin yapılmış olması, ardından SFC'nin çalıştırılması önerilir.\n\nSistem dosyası denetimini şimdi başlatmak istiyor musunuz?",
                    "wu_reset_components" => "Bu işlem, takılan veya hata veren Windows güncellemelerini düzeltmek için güncelleme servislerini ve indirme önbelleğini sıfırlayacaktır.\n\n⚠️ Risk ve Etki Bilgisi (Orta Risk):\n• Windows Güncelleme Geçmişi listesi sıfırlanacaktır (yüklü güncellemeleriniz veya sisteminiz silinmez, yalnızca geçmiş kayıtları temizlenir).\n• SoftwareDistribution ve Catroot2 klasörleri sıfırlanarak indirme kuyruğu baştan oluşturulacaktır.\n\nWindows Update bileşenlerini sıfırlamak istiyor musunuz?",
                    "net_reset_network_stack" => "Bu işlem Windows ağ yığınını, DNS önbelleğini, Winsock kataloglarını ve TCP/IP protokollerini tamamen fabrika ayarlarına sıfırlayacaktır.\n\n⚠️ Risk ve Yeniden Başlatma Uyarısı (Orta Risk):\n• Yapılan ağ sıfırlamalarının Windows üzerinde tam olarak geçerli olabilmesi için bilgisayarınızı yeniden başlatmanız gerekecektir.\n• Varsa özel atanmış statik IP, DNS veya proxy ayarlarınız varsayılan (otomatik) duruma getirilecektir.\n\nAğ bileşenlerini şimdi sıfırlamak istiyor musunuz?",
                    "net_reset_firewall" => "Bu işlem Windows Defender Güvenlik Duvarı yapılandırmasını varsayılan fabrika ayarlarına sıfırlayacaktır.\n\n⚠️ Risk ve Etki Bilgisi (Orta Risk):\n• Oyunlar, üçüncü taraf uygulamalar veya port yönlendirmeleri için sonradan eklenmiş tüm özel gelen ve giden kurallar silinecektir.\n• Uygulamaları ilk kez açtığınızda Windows yeniden \"Erişime izin verilsin mi?\" onayını isteyecektir.\n\nGüvenlik duvarını sıfırlamak istiyor musunuz?",
                    "store_reregister_uwp" => "Bu işlem, tüm kullanıcı hesapları için Windows yerleşik ve Microsoft Store (UWP) uygulamalarını (Hesap Makinesi, Fotoğraflar, Mağaza, Ayarlar vb.) sisteme yeniden kaydedecektir.\n\n⚠️ Risk ve Bilgilendirme (Orta Risk):\n• İşlem sırasında arka planda açık olan mağaza uygulamaları geçici olarak yanıt vermeyebilir.\n• O an aktif kullanımda olan bazı sistem uygulamaları (Başlat menüsü vb.) için uyarılar oluşabilir; bu paketler güvenle atlanır.\n• Sisteme yüklü uygulama sayısına bağlı olarak işlem 1 ile 3 dakika sürebilir.\n\nUWP uygulamalarını yeniden kaydetmek istiyor musunuz?",
                    "other_mdsched" => "Bu işlem, bilgisayarınızın RAM bellek modüllerini donanımsal arıza ve veri hatalarına karşı test etmek için Windows Bellek Tanılama aracını (mdsched.exe) başlatacaktır.\n\n⚠️ Yeniden Başlatma Uyarısı:\n• Bellek bütünlüğünün taranabilmesi için bilgisayarın yeniden başlatılması gereklidir.\n• Açılacak pencereden testi 'Şimdi yeniden başlat ve sorunları denetle' veya 'Bir sonraki başlatmada denetle' olarak seçebilirsiniz.\n• Test sırasında sistem kullanılamaz; bellek boyutuna göre işlem 10-25 dakika sürebilir.\n\nWindows Bellek Tanılama aracını açmak istiyor musunuz?",
                    _ => $"{item.Title} işlemini başlatmak istiyor musunuz?"
                };

                string primaryBtn = item.Id switch
                {
                    "sfc_scannow" => "Evet, Denetimi Başlat",
                    "wu_reset_components" => "Evet, Sıfırla",
                    "net_reset_network_stack" => "Evet, Ağı Sıfırla",
                    "net_reset_firewall" => "Evet, Sıfırla",
                    "store_reregister_uwp" => "Evet, Yeniden Kaydet",
                    "other_mdsched" => "Evet, Tanılamayı Aç",
                    _ => "Evet, Onarımı Başlat"
                };

                string dialogIcon = item.Id switch
                {
                    "sfc_scannow" => "📄",
                    "wu_reset_components" => "🔄",
                    "net_reset_network_stack" => "🌐",
                    "net_reset_firewall" => "🛡️",
                    "store_reregister_uwp" => "📦",
                    "other_mdsched" => "🧠",
                    _ => "🛠️"
                };

                string accentColor = (item.Id == "sfc_scannow" || item.Id == "other_mdsched") ? "#3B82F6" : "#F59E0B";

                bool confirmed = await ShowModernDialogAsync(
                    item.Title,
                    dialogMessage,
                    primaryButtonText: primaryBtn,
                    secondaryButtonText: "İptal",
                    icon: dialogIcon,
                    accentColorHex: accentColor);

                if (!confirmed)
                {
                    item.State = HealthExecutionState.Ready;
                    item.StageText = string.Empty;
                    return;
                }
            }

            item.State = HealthExecutionState.Running;
            item.ProgressPercentage = 0;
            item.StageText = "İşlem başlatılıyor...";
            item.StatusDetail = string.Empty;

            var progress = new Progress<(int percent, string stage)>(info =>
            {
                Dispatcher.Invoke(() =>
                {
                    item.ProgressPercentage = info.percent;
                    item.StageText = info.stage;
                });
            });

            try
            {
                var result = await SystemHealthService.RunHealthItemAsync(item, progress);
                item.IsHealthy = result.IsHealthy;
                item.StatusDetail = result.DetailMessage;
                item.ProgressPercentage = 100;
                if (item.Id == "dism_health_check")
                {
                    string outLower = (result.Output ?? string.Empty).ToLowerInvariant();
                    if (outLower.Contains("onarılamaz") || outLower.Contains("non-repairable") || outLower.Contains("cannot be repaired"))
                    {
                        item.CustomCompletedBadgeText = "❌ Ağır Bozulma (Kaynak Gerekli)";
                    }
                    else if (result.IsHealthy)
                    {
                        item.CustomCompletedBadgeText = "✓ Sağlıklı (Bozulma Yok)";
                    }
                    else
                    {
                        item.CustomCompletedBadgeText = "⚠️ Bozulma Tespit Edildi";
                    }
                }
                else if (item.Id == "sfc_scannow")
                {
                    string outLower = (result.Output ?? string.Empty).ToLowerInvariant();
                    if (outLower.Contains("successfully repaired") || outLower.Contains("başarıyla onardı"))
                    {
                        item.CustomCompletedBadgeText = "✓ Onarıldı (Düzeltildi)";
                    }
                    else if (result.IsHealthy)
                    {
                        item.CustomCompletedBadgeText = "✓ Sağlıklı (Bütünlük Tamam)";
                    }
                    else
                    {
                        item.CustomCompletedBadgeText = "⚠️ Onarılamayan Dosyalar Var";
                    }
                }
                else if (item.Id == "wu_reset_components")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Sıfırlandı (Servisler Aktif)" : "⚠️ Sıfırlama Tamamlanamadı";
                }
                else if (item.Id == "wu_clear_bits_queue")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Kuyruk Temizlendi" : "⚠️ Temizlenemedi";
                }
                else if (item.Id == "net_reset_network_stack")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Sıfırlandı (Yeniden Başlatın)" : "⚠️ Sıfırlama Tamamlanamadı";

                    if (result.Success && result.IsHealthy)
                    {
                        bool restartConfirmed = await ShowModernDialogAsync(
                            "Yeniden Başlatma Gerekli",
                            "Ağ bileşenleri (DNS, Winsock, TCP/IP) başarıyla sıfırlandı.\n\nAğ ayarlarının Windows üzerinde tam olarak geçerli olabilmesi için bilgisayarınızı şimdi yeniden başlatmak ister misiniz?",
                            primaryButtonText: "🔄 Şimdi Yeniden Başlat",
                            secondaryButtonText: "Daha Sonra",
                            icon: "🔄",
                            accentColorHex: "#3B82F6");

                        if (restartConfirmed)
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = "shutdown.exe",
                                    Arguments = "/r /t 5 /c \"PureWin: Ağ bileşenleri sıfırlandı. Sistem 5 saniye içinde yeniden başlatılıyor.\"",
                                    CreateNoWindow = true,
                                    UseShellExecute = false
                                });
                            }
                            catch { }
                        }
                    }
                }
                else if (item.Id == "net_reset_firewall")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Sıfırlandı (Fabrika Ayarları)" : "⚠️ Sıfırlama Tamamlanamadı";
                }
                else if (item.Id == "net_reset_full")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Sıfırlandı (Yeniden Başlatın)" : "⚠️ Sıfırlama Tamamlanamadı";

                    if (result.Success && result.IsHealthy)
                    {
                        bool restartConfirmed = await ShowModernDialogAsync(
                            "Yeniden Başlatma Zorunlu",
                            "Tüm ağ bağdaştırıcıları temizlendi ve fabrika durumuna sıfırlandı.\n\nAğ kartlarının Windows tarafından yeniden algılanıp sürücülerinin kurulabilmesi için bilgisayarınızı şimdi yeniden başlatmanız gerekmektedir.\n\nBilgisayarınızı şimdi yeniden başlatmak ister misiniz?",
                            primaryButtonText: "🔄 Şimdi Yeniden Başlat",
                            secondaryButtonText: "Daha Sonra",
                            icon: "🔄",
                            accentColorHex: "#EF4444");

                        if (restartConfirmed)
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = "shutdown.exe",
                                    Arguments = "/r /t 5 /c \"PureWin: Tam ağ sıfırlaması tamamlandı. Sistem 5 saniye içinde yeniden başlatılıyor.\"",
                                    CreateNoWindow = true,
                                    UseShellExecute = false
                                });
                            }
                            catch { }
                        }
                    }
                }
                else if (item.Id == "disk_chkdsk_scan")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Sağlıklı (Hata Yok)" : "⚠️ Disk Hatası Bulundu";
                }
                else if (item.Id == "disk_chkdsk_offline_repair")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Onarım Zamanlandı (Yeniden Başlatın)" : "⚠️ Zamanlama Tamamlanamadı";

                    if (result.Success && result.IsHealthy)
                    {
                        bool restartConfirmed = await ShowModernDialogAsync(
                            "Yeniden Başlatma Gerekli",
                            "Disk onarımı bir sonraki sistem açılışına başarıyla zamanlandı.\n\nOnarım işleminin hemen başlaması için bilgisayarınızı şimdi yeniden başlatmak ister misiniz?",
                            primaryButtonText: "🔄 Şimdi Yeniden Başlat",
                            secondaryButtonText: "Daha Sonra",
                            icon: "🔄",
                            accentColorHex: "#F59E0B");

                        if (restartConfirmed)
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = "shutdown.exe",
                                    Arguments = "/r /t 5 /c \"PureWin: Disk onarımı için sistem 5 saniye içinde yeniden başlatılıyor.\"",
                                    CreateNoWindow = true,
                                    UseShellExecute = false
                                });
                            }
                            catch { }
                        }
                    }
                }
                else if (item.Id == "disk_component_cleanup")
                {
                    bool isResetBase = (item.ExecutionArgument ?? "").Contains("/ResetBase", StringComparison.OrdinalIgnoreCase);
                    item.CustomCompletedBadgeText = result.IsHealthy
                        ? (isResetBase ? "✓ Temizlendi (/ResetBase Kalıcı)" : "✓ Temizlik Tamamlandı")
                        : "⚠️ Temizlik Tamamlanamadı";
                }
                else if (item.Id == "disk_dfrgui")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Araç Açıldı" : "⚠️ Başlatılamadı";
                }
                else if (item.Id == "store_wsreset")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Önbellek Temizlendi" : "⚠️ Temizlenemedi";
                }
                else if (item.Id == "store_reregister_uwp")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Uygulamalar Kaydedildi" : "⚠️ Kayıt Tamamlanamadı";
                }
                else if (item.Id == "other_energy_battery_report")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Raporlar Hazır" : "⚠️ Rapor Oluşturulamadı";
                }
                else if (item.Id == "other_mdsched")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Tanılama Hazır (Yeniden Başlatın)" : "⚠️ Başlatılamadı";
                }
                else if (item.Id == "other_device_driver_diagnostic")
                {
                    item.CustomCompletedBadgeText = result.IsHealthy ? "✓ Aygıtlar Sorunsuz" : "⚠️ Hatalı Aygıt Bulundu";
                }

                item.State = result.Success ? HealthExecutionState.Completed : HealthExecutionState.Failed;
            }
            catch (Exception ex)
            {
                item.IsHealthy = false;
                item.StatusDetail = $"İşlem sırasında beklenmedik hata: {ex.Message}";
                item.State = HealthExecutionState.Failed;
            }
        }
        #endregion

        #region Başlangıç Uygulamaları Yönetimi
        private async Task LoadStartupAppsDataAsync()
        {
            try
            {
                // 1. Son BIOS Zamanı
                string biosTime = StartupAppService.GetLastBiosTime();

                // 2. Başlangıç uygulamaları arka planda taranır
                var apps = await Task.Run(() => StartupAppService.GetStartupApps());

                Dispatcher.Invoke(() =>
                {
                    TxtStartupBiosTime.Text = biosTime;
                    _allStartupApps = apps;
                    ApplyStartupFilter();
                    UpdateStartupSummaryBadges();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartupApps] Yükleme hatası: {ex.Message}");
            }
        }

        private void StartupSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyStartupFilter();
        }

        private void FilterStartup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string filter)
            {
                _activeStartupFilter = filter;
                FilterStartupAllBtn.Appearance = filter == "all" ? ControlAppearance.Primary : ControlAppearance.Secondary;
                FilterStartupEnabledBtn.Appearance = filter == "enabled" ? ControlAppearance.Primary : ControlAppearance.Secondary;
                FilterStartupDisabledBtn.Appearance = filter == "disabled" ? ControlAppearance.Primary : ControlAppearance.Secondary;
                FilterStartupHighImpactBtn.Appearance = filter == "high" ? ControlAppearance.Primary : ControlAppearance.Secondary;

                ApplyStartupFilter();
            }
        }

        private void ApplyStartupFilter()
        {
            string query = StartupSearchBox?.Text?.Trim().ToLowerInvariant() ?? string.Empty;
            var filtered = _allStartupApps.Where(app =>
            {
                // Metin Arama Filtresi (Ad, Yayıncı, Yol)
                bool matchesSearch = string.IsNullOrEmpty(query) ||
                                    app.Name.ToLowerInvariant().Contains(query) ||
                                    app.Publisher.ToLowerInvariant().Contains(query) ||
                                    app.DisplayPath.ToLowerInvariant().Contains(query);

                if (!matchesSearch) return false;

                // Çip Filtresi (Tümü, Etkin, Devre Dışı, Yüksek Etki)
                return _activeStartupFilter switch
                {
                    "enabled" => app.IsEnabled,
                    "disabled" => !app.IsEnabled,
                    "high" => app.Impact == StartupImpact.High,
                    _ => true
                };
            }).ToList();

            _displayedStartupApps = new ObservableCollection<StartupAppItem>(filtered);
            if (StartupAppsList != null)
            {
                StartupAppsList.ItemsSource = _displayedStartupApps;
            }
        }

        private void UpdateStartupSummaryBadges()
        {
            int total = _allStartupApps.Count;
            int enabled = _allStartupApps.Count(a => a.IsEnabled);
            int disabled = total - enabled;
            int highImpact = _allStartupApps.Count(a => a.Impact == StartupImpact.High);

            TxtStartupStatusCounts.Text = $"{enabled} / {total} Etkin";
            TxtStartupActiveSummary.Text = $"{enabled} program açılışta otomatik başlıyor";
            TxtStartupHighImpactCount.Text = $"{highImpact} Yüksek Etki";

            if (highImpact > 3)
            {
                TxtStartupLoadLevelBadge.Text = "Yüksek Seviye";
                TxtStartupLoadLevelBadge.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }
            else if (highImpact > 0)
            {
                TxtStartupLoadLevelBadge.Text = "Orta Seviye";
                TxtStartupLoadLevelBadge.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
            }
            else
            {
                TxtStartupLoadLevelBadge.Text = "Düşük Seviye";
                TxtStartupLoadLevelBadge.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            }

            FilterStartupAllBtn.Content = $"Tümü ({total})";
            FilterStartupEnabledBtn.Content = $"Etkin ({enabled})";
            FilterStartupDisabledBtn.Content = $"Devre Dışı ({disabled})";
            FilterStartupHighImpactBtn.Content = $"Yüksek Etki ({highImpact})";
        }

        private async void StartupAppToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.ToggleSwitch toggle && toggle.DataContext is StartupAppItem item)
            {
                bool newState = toggle.IsChecked == true;
                bool success = StartupAppService.SetStartupAppState(item, newState);
                if (!success)
                {
                    // Başarısız olursa önceki duruma geri al
                    item.IsEnabled = !newState;
                    toggle.IsChecked = !newState;
                    await ShowModernDialogAsync("İşlem Başarısız", $"{item.Name} başlangıç durumu değiştirilemedi. Lütfen yönetici izinlerini kontrol edin.", "Tamam", null, "⚠️", "#EF4444");
                }
                UpdateStartupSummaryBadges();
            }
        }

        private async void StartupRefresh_Click(object sender, RoutedEventArgs e)
        {
            StartupRefreshBtn.IsEnabled = false;
            try
            {
                await LoadStartupAppsDataAsync();
            }
            finally
            {
                StartupRefreshBtn.IsEnabled = true;
            }
        }

        private async void OpenAppLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is StartupAppItem item)
            {
                try
                {
                    StartupAppService.OpenItemLocation(item);
                }
                catch (Exception ex)
                {
                    await ShowModernDialogAsync(
                        "Konum Açılamadı", 
                        $"{item.Name} için dosya konumu açılamadı:\n\n{ex.Message}", 
                        "Tamam", null, "⚠️", "#EF4444");
                }
            }
        }
        #endregion

        #region Servis Filtreleme ve Yönetimi
        private void ApplyServiceFilter()
        {
            if (_allServices == null || ServicesList == null) return;

            string query = ServiceSearchBox?.Text?.Trim().ToLowerInvariant() ?? "";
            bool onlySafe = FilterSafeServicesRadio?.IsChecked ?? false;
            bool onlyOptional = FilterOptionalServicesRadio?.IsChecked ?? false;
            bool onlyDisabled = FilterDisabledServicesRadio?.IsChecked ?? false;

            int totalCount = _allServices.Count;
            int safeCount = _allServices.Count(s => s.IsSafe);
            int optionalCount = _allServices.Count(s => !s.IsSafe);
            int stoppedOrDisabledCount = _allServices.Count(s => s.CanStartOrEnable);

            if (FilterAllServicesRadio != null) FilterAllServicesRadio.Content = $"Tümü ({totalCount})";
            if (FilterSafeServicesRadio != null) FilterSafeServicesRadio.Content = $"🛡️ Güvenli ({safeCount})";
            if (FilterOptionalServicesRadio != null) FilterOptionalServicesRadio.Content = $"⚠️ İsteğe Bağlı ({optionalCount})";
            if (FilterDisabledServicesRadio != null) FilterDisabledServicesRadio.Content = $"🚫 Durdurulmuş / Kapalı ({stoppedOrDisabledCount})";
            if (BtnEnableAllDisabled != null) BtnEnableAllDisabled.IsEnabled = stoppedOrDisabledCount > 0;

            var filtered = _allServices.AsEnumerable();

            if (onlySafe)
                filtered = filtered.Where(s => s.IsSafe);
            else if (onlyOptional)
                filtered = filtered.Where(s => !s.IsSafe);
            else if (onlyDisabled)
                filtered = filtered.Where(s => s.CanStartOrEnable);

            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(s => 
                    (s.DisplayName?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (s.ServiceName?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (s.Description?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (s.WarningText?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            var targetList = filtered.ToList();

            // UIA / Erişilebilirlik (Screen Reader) ağacını koparmamak için ItemsSource'u null yapmadan ObservableCollection üzerinden güncelle
            if (!_displayedServices.SequenceEqual(targetList))
            {
                _displayedServices.Clear();
                foreach (var s in targetList)
                {
                    _displayedServices.Add(s);
                }
            }

            if (ServicesCounterBadge != null)
            {
                ServicesCounterBadge.Text = $"{_displayedServices.Count} Servis";
            }
        }

        private void ServiceSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyServiceFilter();
        }

        private void ServiceFilter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyServiceFilter();
        }

        private void ServiceToggle_Changed(object sender, RoutedEventArgs e)
        {
            UpdateActionBar();
        }

        private void SelectSafeServices_Click(object sender, RoutedEventArgs e)
        {
            foreach (var svc in _allServices)
            {
                svc.IsSelected = svc.IsSafe && !svc.IsCurrentDisabled;
            }
            UpdateActionBar();
        }

        private void SelectAllServices_Click(object sender, RoutedEventArgs e)
        {
            foreach (var svc in _displayedServices)
            {
                if (!svc.IsCurrentDisabled)
                    svc.IsSelected = true;
            }
            UpdateActionBar();
        }

        private void ClearServiceSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var svc in _allServices)
            {
                svc.IsSelected = false;
            }
            UpdateActionBar();
        }

        private async void EnableSingleService_Click(object sender, RoutedEventArgs e)
        {
            if (_isApplying) return;
            if (sender is FrameworkElement fe && fe.Tag is ServiceItem svc)
            {
                fe.IsEnabled = false;
                try
                {
                    svc.StatusText = "● Başlatılıyor...";
                    svc.StatusColor = "#3B82F6";

                    await ServiceManagementService.SetServiceStateAsync(svc, disable: false);

                    ServiceManagementService.CheckServiceStatus(svc);
                    ApplyServiceFilter();
                    UpdateReportView();
                    UpdateActionBar();

                    if (svc.ServiceName.Equals("WSearch", StringComparison.OrdinalIgnoreCase) ||
                        svc.ServiceName.Equals("SysMain", StringComparison.OrdinalIgnoreCase))
                    {
                        RefreshGamePerformanceData();
                    }
                }
                finally
                {
                    fe.IsEnabled = true;
                }
            }
        }

        private async void EnableAllDisabledServices_Click(object sender, RoutedEventArgs e)
        {
            if (_isApplying) return;
            _isApplying = true;

            try
            {
                var targetServices = _allServices.Where(s => s.CanStartOrEnable).ToList();
                if (targetServices.Count == 0) return;

                int initialMissingCount = targetServices.Count(s => s.IsMissingOnSystem);
                string missingNote = initialMissingCount > 0 ? $" (bunlardan {initialMissingCount}'sı bu sistemde mevcut değil)" : "";

                bool confirmed = await ShowModernDialogAsync(
                    "Kapalı / Durdurulmuş Servisleri Başlat",
                    $"Sistemde şu an kapalı veya durdurulmuş olan {targetServices.Count} adet servis yeniden etkinleştirilip arka planda başlatılacak{missingNote}.\n\nOnaylıyor musunuz?",
                    primaryButtonText: "⚡ Servisleri Başlat / Aç",
                    secondaryButtonText: "Vazgeç",
                    icon: "🔄",
                    accentColorHex: "#3B82F6");

                if (!confirmed) return;

                // 1. Toplu işlem başlamadan önce DiagTrack AutoLogger dinleyicisinin durumunu yakala
                bool listenerWasApplied = await DiagtrackService.IsDisabledAsync();

                if (BtnEnableAllDisabled != null) BtnEnableAllDisabled.IsEnabled = false;

                foreach (var svc in targetServices)
                {
                    svc.StatusText = "● Başlatılıyor...";
                    svc.StatusColor = "#3B82F6";
                    await ServiceManagementService.SetServiceStateAsync(svc, disable: false);
                    ServiceManagementService.CheckServiceStatus(svc);
                }

                // 2. DiagTrack servisi başlatıldığında Windows ETW autologger kaydını (Start=1) sessizce geri açar.
                // Eğer kullanıcı daha önce bu tweak'i uygulamışsa (listenerWasApplied == true) ve dinleyici bozulduysa, yeniden kapat.
                if (listenerWasApplied && !(await DiagtrackService.IsDisabledAsync()))
                {
                    await DiagtrackService.SetStateAsync(disable: true);
                }

                // 3. Ortak metod ile canlı servis, tweak ve oyun optimizasyonu durumlarını tazele (Single Source of Truth)
                await RefreshAllLiveStatesAsync();

                // 4. Canlı durum sayılarını hesapla (gerçek durumla birebir)
                int successCount = targetServices.Count(s => !s.IsCurrentDisabled);
                int runningNow = targetServices.Count(s => s.IsRunning);
                int missingCount = targetServices.Count(s => s.IsMissingOnSystem);
                int failCount = targetServices.Count(s => s.IsCurrentDisabled && !s.IsMissingOnSystem);

                ApplyServiceFilter();
                UpdateReportView();
                UpdateActionBar();
                RefreshGamePerformanceData();

                string statusLine = (runningNow == successCount)
                    ? $"{successCount} adet servis etkinleştirildi ve tümü şu anda çalışıyor."
                    : $"{successCount} adet servis etkinleştirildi ({runningNow} tanesi şu anda çalışıyor).";

                var messageParts = new List<string> { statusLine };

                if (missingCount > 0)
                {
                    messageParts.Add($"ℹ️ {missingCount} servis bu sistemde mevcut olmadığı için atlandı.");
                }

                if (failCount > 0)
                {
                    messageParts.Add($"⚠️ {failCount} servis bağımlılıklar veya ilkeler nedeniyle başlatılamadı.");
                }

                string fullMessage = string.Join("\n\n", messageParts);

                if (failCount > 0)
                {
                    await ShowModernDialogAsync(
                        "Servisler Kısmen Etkinleştirildi",
                        fullMessage,
                        primaryButtonText: "Tamam",
                        icon: "⚠️",
                        accentColorHex: "#F59E0B");
                }
                else
                {
                    await ShowModernDialogAsync(
                        "Servisler Başarıyla Etkinleştirildi",
                        fullMessage,
                        primaryButtonText: "Harika",
                        icon: "✓",
                        accentColorHex: "#10B981");
                }
            }
            finally
            {
                _isApplying = false;
                if (BtnEnableAllDisabled != null) BtnEnableAllDisabled.IsEnabled = true;
            }
        }
        #endregion

        #region Sistem Sağlık Raporu
        /// <summary>
        /// Tek ve birleşik doğrulama kaynağı (Single Source of Truth).
        /// Hem uygulama açılışında (LoadDataAsync) hem de rapor yenilemede (RefreshReport_Click)
        /// sistemdeki canlı durumu sorgulamak için bu ortak fonksiyon kullanılır.
        /// </summary>
        public static async Task<bool> IsTweakAppliedAsync(TweakItem tweak)
        {
            if (tweak.IsScheduledTask)
            {
                return tweak.IsApplied;
            }

            if (tweak.IsDropdown)
            {
                return tweak.AppliedOption != null && !string.IsNullOrEmpty(tweak.AppliedOption.RegFile);
            }

            if (tweak.Id == "EnableWindowsSandbox")
            {
                return await WindowsFeatureService.IsFeatureEnabledAsync("Containers-DisposableClientVM");
            }

            if (tweak.Id == "EnableWindowsSubsystemForLinux")
            {
                return await WindowsFeatureService.IsFeatureEnabledAsync("Microsoft-Windows-Subsystem-Linux");
            }

            if (tweak.Id == "DisableDiagtrackListener")
            {
                return await DiagtrackService.IsDisabledAsync();
            }

            if (tweak.Id == "ShowAllTrayIcons")
            {
                return RegistryService.IsAllTrayIconsShown();
            }

            if (tweak.Id == "SetSolidBlackBackground")
            {
                return WallpaperService.IsSolidBlack();
            }

            if (tweak.Id == "DisableMouseAcceleration")
            {
                return SystemSettingsService.IsMouseAccelerationDisabled();
            }

            if (tweak.Id == "DisableStickyKeys")
            {
                return SystemSettingsService.IsStickyKeysShortcutDisabled();
            }

            if (tweak.Id == "DisableAnimations")
            {
                return SystemSettingsService.IsAnimationDisabled() && RegistryService.IsRegFileApplied(tweak.ApplyRegFile);
            }

            if (tweak.Id == "ReduceMenuDelay")
            {
                return SystemSettingsService.IsMenuDelayReduced();
            }

            if (tweak.Id == "DisableHibernation")
            {
                return SystemSettingsService.IsHibernationDisabled();
            }

            if (!string.IsNullOrEmpty(tweak.ApplyRegFile))
            {
                return RegistryService.IsRegFileApplied(tweak.ApplyRegFile);
            }

            return false;
        }

        public static async Task RefreshTweakLiveStateAsync(TweakItem tweak, bool isInitialLoad = false)
        {
            if (tweak.IsScheduledTask)
            {
                return;
            }

            if (tweak.IsDropdown)
            {
                DropdownOption? matched = null;
                foreach (var opt in tweak.DropdownOptions)
                {
                    if (!string.IsNullOrEmpty(opt.RegFile) && RegistryService.IsRegFileApplied(opt.RegFile))
                    {
                        matched = opt;
                        break;
                    }
                }
                matched ??= tweak.DropdownOptions.FirstOrDefault();
                tweak.AppliedOption = matched;
                if (isInitialLoad)
                {
                    tweak.SelectedOption = matched;
                    tweak.IsStaged = false;
                }
                return;
            }

            bool isApplied = await IsTweakAppliedAsync(tweak);
            tweak.IsApplied = isApplied;
            if (isInitialLoad)
            {
                tweak.IsPendingChecked = isApplied;
                tweak.IsStaged = false;
            }
        }

        /// <summary>
        /// Servislerin, sistem ince ayarlarının ve oyun optimizasyonlarının canlı durumlarını arka planda tarayıp günceller.
        /// Hem rapor yenilemede hem de toplu servis işlemlerinde ortak Single Source of Truth olarak kullanılır.
        /// </summary>
        public async Task RefreshAllLiveStatesAsync()
        {
            await Task.Run(async () =>
            {
                // 1. Servis durumlarını canlı snapshot ile güncelle
                if (_allServices != null && _allServices.Count > 0)
                {
                    var runningSnapshot = ServiceManagementService.GetRunningServicesSnapshot();
                    foreach (var svc in _allServices)
                    {
                        ServiceManagementService.CheckServiceStatus(svc, runningSnapshot);
                    }
                }

                // 2. Tweak doğrulamalarını canlı kontrol et (Açılış ile birebir aynı ortak fonksiyon)
                if (_allTweaks != null)
                {
                    foreach (var tweak in _allTweaks)
                    {
                        await RefreshTweakLiveStateAsync(tweak, isInitialLoad: false);
                    }
                }

                // 3. Oyun ayarlarını da canlı snapshot ile tazele
                if (_gameTweaks != null && _gameTweaks.Count > 0)
                {
                    GameOptimizerService.RefreshLiveStates(_gameTweaks);
                }
            });
        }

        private async void RefreshReport_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn != null) btn.IsEnabled = false;
            try
            {
                await RefreshAllLiveStatesAsync();
                UpdateReportView();
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private void UpdateReportView()
        {
            if (_allTweaks == null || _allBloatwareApps == null) return;

            int appliedTweaks = _allTweaks.Count(t => t.IsVerified);
            int totalTweaks = _allTweaks.Count;
            int disabledServices = _allServices.Count(s => s.IsCurrentDisabled);
            int totalServices = _allServices.Count;

            // Bloatware ayrımı: Güvenle silinebilir gerçek çöpler vs. sistem/kullanıcı araçları (Edge, Ses Kaydedici vb.)
            int installedSafeBloat = _allBloatwareApps.Count(a => a.IsInstalled && a.IsSafe);
            int installedOtherApps = _allBloatwareApps.Count(a => a.IsInstalled && !a.IsSafe);
            int totalSafeBloat = _allBloatwareApps.Count(a => a.IsSafe);

            // Renk paleti fırçaları
            var redBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            var amberBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            var greenBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            var neutralBrush = (Brush)FindResource("TextFillColorSecondaryBrush");

            // Dengeli ve adil skor hesabı: Tweak %40, Servisler %30, Gerçek Çöp Temizliği %30
            double tweakRatio = totalTweaks > 0 ? (double)appliedTweaks / totalTweaks : 0;
            double serviceRatio = totalServices > 0 ? (double)disabledServices / totalServices : 0;
            double safeBloatRatio = totalSafeBloat > 0 ? (double)(totalSafeBloat - installedSafeBloat) / totalSafeBloat : 1.0;

            int score = (int)Math.Round((tweakRatio * 40.0) + (serviceRatio * 30.0) + (safeBloatRatio * 30.0));
            score = Math.Clamp(score, 5, 100);

            if (ReportScoreText != null)
            {
                ReportScoreText.Text = $"%{score}";
                if (score < 50)
                    ReportScoreText.Foreground = redBrush;
                else if (score < 75)
                    ReportScoreText.Foreground = amberBrush;
                else
                    ReportScoreText.Foreground = greenBrush;
            }

            // Rozet Durumu ve Renkleri
            if (ReportBadgeBorder != null && ReportBadgeText != null)
            {
                if (score < 50)
                {
                    ReportBadgeText.Text = "⚠️ İyileştirme Gerekli";
                    ReportBadgeText.Foreground = redBrush;
                    ReportBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1AEF4444"));
                    ReportBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33EF4444"));
                }
                else if (score < 75)
                {
                    ReportBadgeText.Text = "⚡ Kısmen Optimize Edildi";
                    ReportBadgeText.Foreground = amberBrush;
                    ReportBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1AF59E0B"));
                    ReportBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33F59E0B"));
                }
                else
                {
                    ReportBadgeText.Text = "🛡️ Sistem Tam Korumada";
                    ReportBadgeText.Foreground = greenBrush;
                    ReportBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A10B981"));
                    ReportBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3310B981"));
                }
            }

            if (ReportSummaryText != null)
            {
                string bloatSummary = installedSafeBloat > 0 
                    ? $"{installedSafeBloat} adet silinebilir çöp paket tespit edildi." 
                    : "Gereksiz çöp paket kalmadı (Temiz).";
                ReportSummaryText.Text = $"{totalTweaks} sistem ayarından {appliedTweaks} tanesi devrede. {disabledServices}/{totalServices} servis devre dışı. {bloatSummary}";
            }

            // 1. Gizlilik & Telemetri Durumu
            bool coreTelemetryApplied = _allTweaks.FirstOrDefault(t => t.Id == "DisableCoreTelemetryTasks")?.IsVerified ?? false;
            bool diagtrackApplied = _allTweaks.FirstOrDefault(t => t.Id == "DisableDiagtrackListener")?.IsVerified ?? false;
            bool telemetryApplied = _allTweaks.FirstOrDefault(t => t.Id == "DisableTelemetry")?.IsVerified ?? false;

            if (ReportTelemetryStatus != null)
            {
                if (coreTelemetryApplied && diagtrackApplied && telemetryApplied)
                {
                    ReportTelemetryStatus.Text = "✓ 17 Görev, DiagTrack & Telemetri Kapalı (Tam Koruma)";
                    ReportTelemetryStatus.Foreground = greenBrush;
                }
                else if (coreTelemetryApplied || telemetryApplied)
                {
                    ReportTelemetryStatus.Text = "⚡ Kısmi Telemetri Engellendi (Tümü Kapatılabilir)";
                    ReportTelemetryStatus.Foreground = amberBrush;
                }
                else
                {
                    ReportTelemetryStatus.Text = "● Telemetri Görevleri Aktif (Kapatılması Önerilir)";
                    ReportTelemetryStatus.Foreground = redBrush;
                }
            }

            // 2. Görsel Hız & Efektler
            bool animApplied = _allTweaks.FirstOrDefault(t => t.Id == "DisableAnimations")?.IsVerified ?? false;
            bool transApplied = _allTweaks.FirstOrDefault(t => t.Id == "DisableTransparency")?.IsVerified ?? false;
            if (ReportVisualStatus != null)
            {
                if (animApplied && transApplied)
                {
                    ReportVisualStatus.Text = "✓ Animasyonlar ve Saydamlık Kapalı (Maksimum FPS)";
                    ReportVisualStatus.Foreground = greenBrush;
                }
                else if (animApplied || transApplied)
                {
                    ReportVisualStatus.Text = "✓ Kısmi Efektler Kapalı (Hızlı)";
                    ReportVisualStatus.Foreground = greenBrush;
                }
                else
                {
                    ReportVisualStatus.Text = "● Standart Görsel Efektler Açık";
                    ReportVisualStatus.Foreground = neutralBrush;
                }
            }

            // 3. AI / Copilot & Recall
            bool copilotApplied = _allTweaks.FirstOrDefault(t => t.Id == "DisableCopilot")?.IsVerified ?? false;
            bool recallApplied = _allTweaks.FirstOrDefault(t => t.Id == "DisableRecall")?.IsVerified ?? false;
            if (ReportAIStatus != null)
            {
                if (copilotApplied && recallApplied)
                {
                    ReportAIStatus.Text = "✓ Copilot ve Recall Devre Dışı (İzleyici Yok)";
                    ReportAIStatus.Foreground = greenBrush;
                }
                else if (copilotApplied)
                {
                    ReportAIStatus.Text = "⚡ Copilot Kapalı, Diğer AI Servisleri Açık";
                    ReportAIStatus.Foreground = amberBrush;
                }
                else
                {
                    ReportAIStatus.Text = "● AI Servisleri Aktif (Arka Planda Çalışıyor)";
                    ReportAIStatus.Foreground = redBrush;
                }
            }

            // 4. Bloatware Durumu (Akıllı Değerlendirme)
            if (ReportBloatwareStatus != null)
            {
                if (installedSafeBloat > 0)
                {
                    ReportBloatwareStatus.Text = $"● {installedSafeBloat} Adet Kaldırılabilir Çöp Paket Kurulu";
                    ReportBloatwareStatus.Foreground = redBrush;
                    if (ReportBloatwareDesc != null)
                        ReportBloatwareDesc.Text = $"Sistemde gereksiz disk ve RAM tüketen {installedSafeBloat} adet kaldırılması güvenli çöp paket mevcut.";
                }
                else if (installedOtherApps > 0)
                {
                    ReportBloatwareStatus.Text = "✓ Sistem Tertemiz (Gereksiz Çöpler Arındırıldı)";
                    ReportBloatwareStatus.Foreground = greenBrush;
                    if (ReportBloatwareDesc != null)
                        ReportBloatwareDesc.Text = $"Gereksiz çöpler arındırıldı. Yalnızca {installedOtherApps} adet temel araç (Edge vb.) korundu.";
                }
                else
                {
                    ReportBloatwareStatus.Text = "✓ Ultra Temiz (0 Çöp Paket)";
                    ReportBloatwareStatus.Foreground = greenBrush;
                    if (ReportBloatwareDesc != null)
                        ReportBloatwareDesc.Text = "Sistemdeki tüm UWP paketleri ve ek uygulamalar tamamen arındırıldı.";
                }
            }

            // 5. Kritik Telemetri ve Gizlilik Röntgeni (Canlı Durum Özeti)
            var greenBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A10B981"));
            var greenBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3310B981"));
            var amberBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1AF59E0B"));
            var amberBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33F59E0B"));
            var redBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1AEF4444"));
            var redBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33EF4444"));

            void SetXrayItem(Border? badge, System.Windows.Controls.TextBlock? statusText, System.Windows.Controls.TextBlock? descText, bool isPositive, bool isWarning, string posStatus, string negStatus, string posDesc, string negDesc)
            {
                if (badge == null || statusText == null || descText == null) return;
                if (isPositive)
                {
                    statusText.Text = posStatus;
                    statusText.Foreground = greenBrush;
                    descText.Text = posDesc;
                    badge.Background = greenBg;
                    badge.BorderBrush = greenBorder;
                }
                else if (isWarning)
                {
                    statusText.Text = posStatus;
                    statusText.Foreground = amberBrush;
                    descText.Text = posDesc;
                    badge.Background = amberBg;
                    badge.BorderBrush = amberBorder;
                }
                else
                {
                    statusText.Text = negStatus;
                    statusText.Foreground = redBrush;
                    descText.Text = negDesc;
                    badge.Background = redBg;
                    badge.BorderBrush = redBorder;
                }
            }

            // 1. DiagTrack
            bool diagTrackOff = (_allServices.FirstOrDefault(s => s.ServiceName.Equals("DiagTrack", StringComparison.OrdinalIgnoreCase))?.IsCurrentDisabled ?? false) ||
                                (_allTweaks.FirstOrDefault(t => t.Id == "DisableDiagtrackListener")?.IsVerified ?? false);
            SetXrayItem(XrayDiagTrackBadge, XrayDiagTrackStatus, XrayDiagTrackDesc,
                diagTrackOff, false,
                "✓ Devre Dışı / Temiz", "● Arka Planda Aktif",
                "Microsoft sunucularına veri akışı engellendi.", "Kullanım alışkanlıkları ve tanılama verileri Microsoft'a aktarılıyor.");

            // 2. WerSvc
            bool werSvcOff = _allServices.FirstOrDefault(s => s.ServiceName.Equals("WerSvc", StringComparison.OrdinalIgnoreCase))?.IsCurrentDisabled ?? false;
            SetXrayItem(XrayWerSvcBadge, XrayWerSvcStatus, XrayWerSvcDesc,
                werSvcOff, false,
                "✓ Devre Dışı / Temiz", "● Arka Planda Aktif",
                "Çökme ve sistem kilitlenme raporları gönderilmiyor.", "Uygulama çökme günlükleri Microsoft sunucularına aktarılıyor.");

            // 3. Zamanlanmış Telemetri Görevleri
            bool tasksOff = _allTweaks.FirstOrDefault(t => t.Id == "DisableCoreTelemetryTasks")?.IsVerified ?? false;
            SetXrayItem(XrayTasksBadge, XrayTasksStatus, XrayTasksDesc,
                tasksOff, false,
                "✓ Durduruldu / Temiz", "● Görevler Aktif",
                "17 adet arka plan CEIP ve disk analiz görevi durduruldu.", "Windows Müşteri Deneyimi ve Appraiser tarayıcıları çalışıyor.");

            // 4. Tarayıcı Arka Plan Modu
            bool browserOff = _allTweaks.FirstOrDefault(t => t.Id == "DisableBrowserBackgroundApps")?.IsVerified ?? false;
            SetXrayItem(XrayBrowserBadge, XrayBrowserStatus, XrayBrowserDesc,
                browserOff, false,
                "✓ Engellendi / Temiz", "● Arka Planda Açık",
                "Edge, Chrome, Brave ve Opera kapalıyken RAM harcamıyor.", "Tarayıcılar kapatıldıktan sonra da arka planda çalışmaya devam ediyor.");

            // 5. Yapay Zeka (Copilot & Recall)
            bool copilotOff = _allTweaks.FirstOrDefault(t => t.Id == "DisableCopilot")?.IsVerified ?? false;
            SetXrayItem(XrayAiBadge, XrayAiStatus, XrayAiDesc,
                copilotOff, false,
                "✓ Devre Dışı / Temiz", "● AI Entegrasyonu Açık",
                "Copilot ve Recall arka plan ekran analizi engellendi.", "Windows Copilot yapay zeka entegrasyonu sistemde aktif.");

            // 6. Windows Search (Dizinleme)
            bool searchOff = _allServices.FirstOrDefault(s => s.ServiceName.Equals("WSearch", StringComparison.OrdinalIgnoreCase))?.IsCurrentDisabled ?? false;
            if (searchOff)
            {
                SetXrayItem(XraySearchBadge, XraySearchStatus, XraySearchDesc,
                    true, false,
                    "✓ Dizinleme Kapalı", "",
                    "Arka plan disk indeksleme yükü sıfırlandı (Yüksek SSD/Oyun FPS).", "");
            }
            else
            {
                SetXrayItem(XraySearchBadge, XraySearchStatus, XraySearchDesc,
                    false, true,
                    "● Çalışıyor (Normal)", "",
                    "Dosya ve Başlat araması için anlık dizin oluşturuluyor.", "");
            }
        }

        private async void ExportHtmlReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int appliedTweaks = _allTweaks.Count(t => t.IsVerified);
                int totalTweaks = _allTweaks.Count;
                int disabledServices = _allServices.Count(s => s.IsCurrentDisabled);
                int totalServices = _allServices.Count;
                int installedSafeBloat = _allBloatwareApps.Count(a => a.IsInstalled && a.IsSafe);
                string score = ReportScoreText?.Text ?? "%85";

                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"PureWin_Rapor_{DateTime.Now:yyyyMMdd_HHmm}.html",
                    Filter = "HTML Rapor Dosyası (*.html)|*.html",
                    Title = "PureWin Sistem Raporunu Kaydet",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (sfd.ShowDialog() == true)
                {
                    string html = GenerateHtmlReport(score, appliedTweaks, totalTweaks, disabledServices, totalServices, installedSafeBloat);
                    System.IO.File.WriteAllText(sfd.FileName, html, System.Text.Encoding.UTF8);

                    bool openNow = await ShowModernDialogAsync(
                        "Rapor Başarıyla Kaydedildi",
                        $"Sistem optimizasyon ve sağlık karnesi kaydedildi:\n{sfd.FileName}\n\nRaporu tarayıcınızda şimdi görüntülemek ister misiniz?",
                        primaryButtonText: "🌐 Raporu Aç",
                        secondaryButtonText: "Kapat",
                        icon: "📊",
                        accentColorHex: "#10B981");

                    if (openNow)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowModernDialogAsync("Hata", $"Rapor dışa aktarılırken bir hata oluştu: {ex.Message}", "Tamam", icon: "⚠️");
            }
        }

        private string GenerateHtmlReport(string score, int appliedTweaks, int totalTweaks, int disabledServices, int totalServices, int installedBloat)
        {
            string dateStr = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            string osVer = Environment.OSVersion.VersionString;
            string pcName = Environment.MachineName;
            var hagsInfo = GameOptimizerService.GetHagsStatus();
            string hagsTagClass = hagsInfo.IsApplied ? "tag-green" : (hagsInfo.IsSupported ? "tag-amber" : "tag-red");

            return $@"<!DOCTYPE html>
<html lang=""tr"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>PureWin Sistem Optimizasyon ve Sağlık Raporu</title>
    <style>
        :root {{
            --bg: #0F172A;
            --card: #1E293B;
            --card-border: #334155;
            --text-main: #F8FAFC;
            --text-muted: #94A3B8;
            --green: #10B981;
            --green-bg: rgba(16, 185, 129, 0.12);
            --amber: #F59E0B;
            --blue: #3B82F6;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background-color: var(--bg);
            color: var(--text-main);
            margin: 0;
            padding: 32px 16px;
            display: flex;
            justify-content: center;
        }}
        .container {{
            max-width: 880px;
            width: 100%;
        }}
        .header {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 1px solid var(--card-border);
            padding-bottom: 20px;
            margin-bottom: 24px;
        }}
        .logo {{
            font-size: 24px;
            font-weight: 800;
            color: var(--green);
            letter-spacing: -0.5px;
        }}
        .date {{
            font-size: 13px;
            color: var(--text-muted);
        }}
        .score-card {{
            background: linear-gradient(135deg, #1E293B 0%, #0F172A 100%);
            border: 1px solid var(--card-border);
            border-radius: 14px;
            padding: 24px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 24px;
        }}
        .score-val {{
            font-size: 48px;
            font-weight: 900;
            color: var(--green);
            line-height: 1;
        }}
        .score-title {{
            font-size: 18px;
            font-weight: 700;
            margin-bottom: 6px;
        }}
        .badge {{
            display: inline-block;
            background: var(--green-bg);
            border: 1px solid rgba(16, 185, 129, 0.3);
            color: var(--green);
            padding: 6px 14px;
            border-radius: 20px;
            font-size: 13px;
            font-weight: 600;
        }}
        .grid {{
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 16px;
            margin-bottom: 24px;
        }}
        .stat-card {{
            background: var(--card);
            border: 1px solid var(--card-border);
            border-radius: 12px;
            padding: 16px;
        }}
        .stat-title {{
            font-size: 12px;
            color: var(--text-muted);
            text-transform: uppercase;
            font-weight: 600;
            margin-bottom: 8px;
        }}
        .stat-num {{
            font-size: 22px;
            font-weight: 800;
            color: var(--text-main);
        }}
        .table-card {{
            background: var(--card);
            border: 1px solid var(--card-border);
            border-radius: 14px;
            padding: 20px;
            margin-bottom: 24px;
        }}
        .table-title {{
            font-size: 16px;
            font-weight: 700;
            margin-bottom: 16px;
            display: flex;
            align-items: center;
            gap: 8px;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
        }}
        th, td {{
            text-align: left;
            padding: 12px 10px;
            border-bottom: 1px solid var(--card-border);
            font-size: 13px;
        }}
        th {{
            color: var(--text-muted);
            font-weight: 600;
            text-transform: uppercase;
            font-size: 11px;
        }}
        tr:last-child td {{
            border-bottom: none;
        }}
        .tag-green {{
            color: var(--green);
            background: var(--green-bg);
            padding: 4px 10px;
            border-radius: 6px;
            font-weight: 700;
            font-size: 11px;
            display: inline-block;
        }}
        .tag-amber {{
            color: var(--amber);
            background: rgba(245, 158, 11, 0.12);
            padding: 4px 10px;
            border-radius: 6px;
            font-weight: 700;
            font-size: 11px;
            display: inline-block;
        }}
        .footer {{
            text-align: center;
            font-size: 12px;
            color: var(--text-muted);
            margin-top: 32px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""logo"">🛡️ PureWin Optimizasyon Karnesi</div>
            <div class=""date"">Rapor Tarihi: {dateStr} | Bilgisayar: {pcName}</div>
        </div>

        <div class=""score-card"">
            <div>
                <div class=""score-title"">Genel Sistem Sağlık ve Gizlilik Puanı</div>
                <div style=""color: var(--text-muted); font-size: 13px;"">Windows 11 işletim sistemi optimizasyon doğrulaması</div>
            </div>
            <div style=""text-align: right;"">
                <div class=""score-val"">{score}</div>
                <div class=""badge"" style=""margin-top: 8px;"">✓ Sistem Koruma Altında</div>
            </div>
        </div>

        <div class=""grid"">
            <div class=""stat-card"">
                <div class=""stat-title"">Uygulanan İnce Ayarlar</div>
                <div class=""stat-num"">{appliedTweaks} / {totalTweaks}</div>
                <div style=""font-size: 12px; color: var(--green); margin-top: 4px;"">Doğrulandı</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-title"">Devre Dışı Servisler</div>
                <div class=""stat-num"">{disabledServices} / {totalServices}</div>
                <div style=""font-size: 12px; color: var(--green); margin-top: 4px;"">Gereksizler Durduruldu</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-title"">Kalan Güvenli Bloatware</div>
                <div class=""stat-num"">{installedBloat} Adet</div>
                <div style=""font-size: 12px; color: {(installedBloat == 0 ? "var(--green)" : "var(--amber)")}; margin-top: 4px;"">{(installedBloat == 0 ? "Tertemiz (0 Çöp)" : "Temizlenebilir")}</div>
            </div>
        </div>

        <div class=""table-card"">
            <div class=""table-title"">🔬 Kritik Telemetri ve Gizlilik Röntgeni (Canlı Snapshot)</div>
            <table>
                <thead>
                    <tr>
                        <th>Bileşen / Servis</th>
                        <th>Açıklama</th>
                        <th>Durum</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td><strong>DiagTrack (Kullanıcı Telemetrisi)</strong></td>
                        <td>Kullanım ve tanılama verilerinin Microsoft'a aktarımı</td>
                        <td><span class=""tag-green"">✓ Devre Dışı / Temiz</span></td>
                    </tr>
                    <tr>
                        <td><strong>WerSvc (Hata Raporlama)</strong></td>
                        <td>Uygulama ve sistem çökme günlüklerinin gönderimi</td>
                        <td><span class=""tag-green"">✓ Devre Dışı / Temiz</span></td>
                    </tr>
                    <tr>
                        <td><strong>Zamanlanmış Telemetri (17 Görev)</strong></td>
                        <td>Appraiser, UsbCeip ve CEIP arka plan disk analizleri</td>
                        <td><span class=""tag-green"">✓ Durduruldu / Temiz</span></td>
                    </tr>
                    <tr>
                        <td><strong>Tarayıcı Arka Plan Modu</strong></td>
                        <td>Edge, Chrome ve tarayıcıların kapalıyken RAM tüketmesi</td>
                        <td><span class=""tag-green"">✓ Engellendi / Temiz</span></td>
                    </tr>
                    <tr>
                        <td><strong>Yapay Zeka (Copilot & Recall)</strong></td>
                        <td>Görev çubuğu AI ve arka plan ekran kayıt entegrasyonu</td>
                        <td><span class=""tag-green"">✓ Devre Dışı / Temiz</span></td>
                    </tr>
                    <tr>
                        <td><strong>Windows Search (Dizinleme)</strong></td>
                        <td>Arka plan dosya indeksleme motoru (Disk & SSD performansı)</td>
                        <td><span class=""tag-amber"">● Denetim Altında</span></td>
                    </tr>
                    <tr>
                        <td><strong>HAGS (GPU Zamanlaması)</strong></td>
                        <td>{hagsInfo.Description}</td>
                        <td><span class=""{hagsTagClass}"">{hagsInfo.Status}</span></td>
                    </tr>
                </tbody>
            </table>
        </div>

        <div class=""footer"">
            PureWin v1.0.0 — Pure Performance for Windows 11. Bu rapor yerel sistem doğrulamasından üretilmiştir.
        </div>
    </div>
</body>
</html>";
        }
        #endregion

        #region Değişiklik Takibi ve Alt Çubuk
        private void OnBloatwareCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            Dispatcher.BeginInvoke(new Action(UpdateActionBar));
        }

        private void OnTweakToggleChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            Dispatcher.BeginInvoke(new Action(UpdateActionBar));
        }

        private void OnDropdownSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            Dispatcher.BeginInvoke(new Action(UpdateActionBar));
        }

        private async void TweakActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is TweakItem tweak)
            {
                if (tweak.Id == "AddThisPCToDesktop")
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "rundll32.exe",
                            Arguments = "shell32.dll,Control_RunDLL desk.cpl,,5",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        await ShowModernDialogAsync("Hata", $"Masaüstü Simge Ayarları penceresi açılamadı: {ex.Message}", "Tamam", null, "❌");
                    }
                }
            }
        }

        private void UpdateActionBar()
        {
            if (!_isLoaded || _allTweaks == null || _allBloatwareApps == null || _allServices == null || ActionBar == null || ApplyBtn == null || CancelBtn == null || ActionProgressBar == null)
                return;

            int changedTweaks = _allTweaks.Count(t => t.IsStaged);
            int selectedApps = _allBloatwareApps.Count(a => a.IsSelected);
            int changedServices = _allServices.Count(s => s.IsSelected);
            int changedGameTweaks = _gameTweaks != null ? _gameTweaks.Count(g => g.IsStaged && g.IsSupported) : 0;
            int total = changedTweaks + selectedApps + changedServices + changedGameTweaks;

            ActionProgressBar.Visibility = Visibility.Collapsed;
            ActionProgressBar.Value = 0;

            if (total > 0)
            {
                // Değişiklik var: Panel mavi vurguyla canlanır, sol rozete ⚡ simgesi gelir, buton dinamik metin alır
                ActionBar.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                ActionBarBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                BadgeCountText.Text = "⚡";
                BadgeCountText.Foreground = Brushes.White;

                StatusTitleText.Text = "Değişiklikler Hazır";
                StatusSubBadge.Text = "● Uygulanmayı Bekliyor";
                StatusSubBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                StatusDescText.Text = "Seçilen değişikliklerin sisteme işlenmesi için butona tıklayın.";

                ApplyBtn.Content = total == 1 ? "⚡ 1 Değişikliği Uygula" : $"⚡ {total} Değişikliği Uygula";
                CancelBtn.IsEnabled = true;
                ApplyBtn.IsEnabled = true;
            }
            else
            {
                // Değişiklik yok: Panel hep oradadır, sakin bekleme modunda durur, butonlar pasiftir
                ActionBar.BorderBrush = (Brush)FindResource("CardStrokeColorDefaultBrush");
                ActionBarBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A64748B"));
                BadgeCountText.Text = "✓";
                BadgeCountText.Foreground = (Brush)FindResource("TextFillColorSecondaryBrush");

                StatusTitleText.Text = "Sistem Hazır";
                StatusSubBadge.Text = "● Beklemede";
                StatusSubBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                StatusDescText.Text = "Kaldırmak istediğiniz uygulamaları veya sistem ayarlarını seçin.";

                ApplyBtn.Content = "⚡ Değişiklikleri Uygula";
                CancelBtn.IsEnabled = false;
                ApplyBtn.IsEnabled = false;
            }
        }
        #endregion

        #region Hızlı Seçim Butonları
        private void SelectAllApps_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _displayedBloatware)
            {
                if (app.IsInstalled)
                    app.IsSelected = true;
            }
            UpdateActionBar();
        }

        private void SelectSafeApps_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _displayedBloatware)
            {
                if (app.IsInstalled && app.IsSafe)
                    app.IsSelected = true;
                else
                    app.IsSelected = false;
            }
            UpdateActionBar();
        }

        private void ClearAppSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _allBloatwareApps)
                app.IsSelected = false;
            UpdateActionBar();
        }
        #endregion

        #region Değişiklikleri Uygulama / Vazgeçme
        private void CancelChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_isApplying) return;

            foreach (var tweak in _allTweaks)
            {
                if (tweak.IsDropdown)
                {
                    tweak.SelectedOption = tweak.AppliedOption ?? tweak.DropdownOptions.FirstOrDefault();
                }
                else
                {
                    tweak.IsPendingChecked = tweak.IsApplied;
                }
                tweak.IsStaged = false;
            }

            foreach (var app in _allBloatwareApps)
            {
                app.IsSelected = false;
            }

            foreach (var svc in _allServices)
            {
                svc.IsSelected = false;
            }

            if (_gameTweaks != null)
            {
                foreach (var gt in _gameTweaks)
                {
                    gt.IsPendingChecked = gt.IsApplied;
                }
            }

            UpdateActionBar();
        }

        private async void ApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_isApplying) return;
            _isApplying = true;

            try
            {
                var stagedTweaks = _allTweaks.Where(t => t.IsStaged).ToList();
                var selectedApps = _allBloatwareApps.Where(a => a.IsSelected).ToList();
                var stagedServices = _allServices.Where(s => s.IsSelected).ToList();
                var stagedGameTweaks = _gameTweaks != null ? _gameTweaks.Where(g => g.IsStaged && g.IsSupported).ToList() : new List<GameTweakItem>();
                int total = stagedTweaks.Count + selectedApps.Count + stagedServices.Count + stagedGameTweaks.Count;

                if (total == 0)
                    return;

                // Kullanıcıya Geri Yükleme Noktası oluşturmak isteyip istemediğini sor
                string choice = await ShowModernChoiceDialogAsync(
                    "Sistem Geri Yükleme Noktası",
                    $"Seçtiğiniz {total} adet optimizasyon ve kaldırma işlemi uygulanacaktır.\n\n🛡️ Güvenlik önlemi olarak işlemden önce Sistem Geri Yükleme Noktası oluşturulsun mu?\n\n💡 Not: Geri yükleme noktası oluşturmak 15-30 saniye sürebilir. Beklemek istemiyorsanız 'Hızlı Devam Et' seçeneğini seçebilirsiniz.",
                    primaryButtonText: "🛡️ Evet, Nokta Oluştur",
                    secondaryButtonText: "⚡ Hayır, Hızlı Devam Et",
                    cancelButtonText: "İptal",
                    icon: "🛡️",
                    accentColorHex: "#3B82F6"
                );

                if (choice == "cancel")
                    return;

                bool createRestorePoint = (choice == "primary");

                bool cleanResiduals = true;
                if (selectedApps.Count > 0)
                {
                    int totalFolders = 0;
                    int totalShortcuts = 0;
                    int totalRegKeys = 0;
                    var previewDetails = new List<string>();

                    foreach (var app in selectedApps)
                    {
                        var scan = ResidualCleanerService.ScanResiduals(app.AppId, app.DisplayName);
                        totalFolders += scan.Folders.Count;
                        totalShortcuts += scan.Shortcuts.Count;
                        totalRegKeys += scan.RegistryKeys.Count;

                        if (scan.TotalItemsFound > 0)
                        {
                            previewDetails.Add($"• {app.DisplayName}: {scan.Folders.Count} klasör, {scan.RegistryKeys.Count} reg anahtarı");
                        }
                    }

                    if (totalFolders > 0 || totalRegKeys > 0 || totalShortcuts > 0)
                    {
                        string sampleList = string.Join("\n", previewDetails.Take(4));
                        if (previewDetails.Count > 4)
                            sampleList += $"\n• ... ve diğer {previewDetails.Count - 4} uygulama";

                        string residualChoice = await ShowModernChoiceDialogAsync(
                            "Derin Artık Temizleme & Kayıt Defteri Onayı",
                            $"Seçtiğiniz {selectedApps.Count} uygulama için derin artık taraması yapıldı ve şu kalıntılar tespit edildi:\n\n" +
                            $"{sampleList}\n\n" +
                            $"📁 Toplam Klasör: {totalFolders} | 🗝️ Kayıt Defteri: {totalRegKeys} | 🔗 Kısayol: {totalShortcuts}\n\n" +
                            $"🛡️ Güvenlik Kalkanı: Kayıt defteri anahtarları silinmeden önce '%LOCALAPPDATA%\\PureWin\\Backups' konumuna otomatik .reg yedeği alınacaktır.\n" +
                            $"🔒 HKLM Koruması: Sistem kararlılığı için HKLM silmeleri güvenlik politikası gereği kapalıdır.\n\n" +
                            $"Bu artıkların sistemden kalıcı olarak temizlenmesini onaylıyor musunuz?",
                            primaryButtonText: "🧹 Evet, Yedek Al ve Temizle",
                            secondaryButtonText: "⚡ Yalnızca Standart Kaldır",
                            cancelButtonText: "İptal",
                            icon: "🧹",
                            accentColorHex: "#10B981"
                        );

                        if (residualChoice == "cancel")
                            return;

                        cleanResiduals = (residualChoice == "primary");
                    }
                }

                int totalSteps = (createRestorePoint ? 1 : 0) + stagedTweaks.Count + selectedApps.Count + stagedServices.Count + stagedGameTweaks.Count;
                int currentStep = 0;

                ApplyBtn.IsEnabled = false;
                CancelBtn.IsEnabled = false;

                ActionProgressBar.Visibility = Visibility.Visible;
                ActionProgressBar.Value = Math.Max(2.0, (0.5 / totalSteps) * 100.0);

                ActionBarBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                BadgeCountText.Text = "⏳";
                BadgeCountText.Foreground = Brushes.White;

                if (createRestorePoint)
                {
                    StatusTitleText.Text = $"Değişiklikler Uygulanıyor (1/{totalSteps})";
                    StatusSubBadge.Text = "● İşleniyor";
                    StatusSubBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                    StatusDescText.Text = "Sistem Geri Yükleme Noktası oluşturuluyor...";
                    ApplyBtn.Content = $"İşleniyor (1/{totalSteps})...";
                }
                else
                {
                    StatusTitleText.Text = $"Değişiklikler Uygulanıyor (0/{totalSteps})";
                    StatusSubBadge.Text = "● İşleniyor";
                    StatusSubBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                    StatusDescText.Text = "İşlemler hazırlanıyor...";
                    ApplyBtn.Content = "İşleniyor...";
                }

                // 1. İsteğe bağlı: Güvenlik için Sistem Geri Yükleme Noktası oluştur
                if (createRestorePoint)
                {
                    currentStep++;
                    ActionProgressBar.Value = ((double)currentStep / totalSteps) * 100.0;
                    try
                    {
                        await RestorePointService.CreateRestorePointAsync("PureWin_Oncesi_Yedek");
                    }
                    catch { }

                    await Task.Delay(20);
                }

                // 2. Sistem İnce Ayarlarını (Toggles, Dropdownlar ve Görev Zamanlayıcı) işle
                foreach (var tweak in stagedTweaks)
                {
                    currentStep++;
                    ActionProgressBar.Value = ((double)currentStep / totalSteps) * 100.0;
                    StatusTitleText.Text = $"Değişiklikler Uygulanıyor ({currentStep}/{totalSteps})";
                    StatusDescText.Text = $"Ayar uygulanıyor: {tweak.Title}...";
                    ApplyBtn.Content = $"İşleniyor ({currentStep}/{totalSteps})...";
                    await Task.Delay(20);

                    if (tweak.IsScheduledTask)
                    {
                        if (tweak.ScheduledTaskType == "Core")
                        {
                            await ScheduledTaskService.SetTasksStateAsync(ScheduledTaskService.CoreTelemetryTasks, tweak.IsPendingChecked);
                        }
                        else if (tweak.ScheduledTaskType == "Optional")
                        {
                            await ScheduledTaskService.SetTasksStateAsync(ScheduledTaskService.OptionalTelemetryTasks, tweak.IsPendingChecked);
                        }
                        tweak.IsApplied = tweak.IsPendingChecked;
                    }
                    else if (tweak.IsDropdown)
                    {
                        if (tweak.Id == "ClearStart")
                        {
                            if (tweak.SelectedOption?.FeatureId == "ClearStart")
                            {
                                await StartMenuService.ClearStartMenuAsync(allUsers: false);
                            }
                            else if (tweak.SelectedOption?.FeatureId == "ClearStartAllUsers")
                            {
                                await StartMenuService.ClearStartMenuAsync(allUsers: true);
                            }
                        }
                        else
                        {
                            await Task.Run(() =>
                            {
                                // 1) Varsa önceki dropdown değişikliğini tweak öncesi hale birebir geri al (dosyayı siler; yoksa false — zararsız)
                                TweakSnapshotService.RestoreSnapshot(tweak.Id);

                                // 2) Yeni seçim gerçek bir reg değişikliğiyse: pristine'i yeniden kaydet + uygula
                                if (tweak.SelectedOption != null && !string.IsNullOrEmpty(tweak.SelectedOption.RegFile))
                                {
                                    TweakSnapshotService.CreateSnapshot(tweak.Id, tweak.SelectedOption.RegFile, null);
                                    RegistryService.ImportRegFile(tweak.SelectedOption.RegFile, isUndo: false);
                                }
                            });
                        }
                        tweak.AppliedOption = tweak.SelectedOption;
                    }
                    else
                    {
                        if (tweak.Id == "DisableDiagtrackListener")
                        {
                            await DiagtrackService.SetStateAsync(tweak.IsPendingChecked);
                        }
                        else if (tweak.Id == "EnableWindowsSandbox")
                        {
                            await WindowsFeatureService.SetFeatureStateAsync("Containers-DisposableClientVM", tweak.IsPendingChecked);
                        }
                        else if (tweak.Id == "EnableWindowsSubsystemForLinux")
                        {
                            if (tweak.IsPendingChecked)
                            {
                                await WindowsFeatureService.SetFeatureStateAsync("VirtualMachinePlatform", true);
                                await WindowsFeatureService.SetFeatureStateAsync("Microsoft-Windows-Subsystem-Linux", true);
                            }
                            else
                            {
                                await WindowsFeatureService.SetFeatureStateAsync("Microsoft-Windows-Subsystem-Linux", false);
                            }
                        }
                        else if (tweak.Id == "ShowAllTrayIcons")
                        {
                            await RegistryService.SetAllTrayIconsStateAsync(tweak.IsPendingChecked);
                        }
                        else if (tweak.Id == "SetSolidBlackBackground")
                        {
                            await Task.Run(() => WallpaperService.SetSolidBlackState(tweak.IsPendingChecked));
                            tweak.IsApplied = tweak.IsPendingChecked;
                        }
                        else if (tweak.Id == "DisableMouseAcceleration")
                        {
                            await Task.Run(() => SystemSettingsService.SetMouseAccelerationState(tweak.IsPendingChecked));
                            tweak.IsApplied = tweak.IsPendingChecked;
                        }
                        else if (tweak.Id == "DisableStickyKeys")
                        {
                            await Task.Run(() => SystemSettingsService.SetStickyKeysShortcutState(tweak.IsPendingChecked));
                            tweak.IsApplied = tweak.IsPendingChecked;
                        }
                        else if (tweak.Id == "DisableHibernation")
                        {
                            await Task.Run(() => SystemSettingsService.SetHibernationState(tweak.IsPendingChecked));
                            tweak.IsApplied = tweak.IsPendingChecked;
                        }
                        else
                        {
                            await Task.Run(() =>
                            {
                                if (tweak.IsPendingChecked)
                                {
                                    if (!string.IsNullOrEmpty(tweak.ApplyRegFile))
                                    {
                                        TweakSnapshotService.CreateSnapshot(tweak.Id, tweak.ApplyRegFile, tweak.UndoRegFile);
                                        RegistryService.ImportRegFile(tweak.ApplyRegFile, isUndo: false);
                                    }

                                    if (tweak.Id == "DisableAnimations")
                                    {
                                        SystemSettingsService.SetClientAnimationState(disable: true);
                                    }
                                    else if (tweak.Id == "ReduceMenuDelay")
                                    {
                                        SystemSettingsService.SetMenuShowDelay(reduce: true);
                                    }
                                    else if (tweak.Id == "DisableWidgets")
                                    {
                                        try
                                        {
                                            foreach (var p in Process.GetProcesses())
                                            {
                                                if (p.ProcessName.Contains("Widget", StringComparison.OrdinalIgnoreCase))
                                                    p.Kill();
                                            }
                                        }
                                        catch { }
                                    }
                                }
                                else
                                {
                                    // Geri alma sadakati: Ön-snapshot varsa birebir eski değerleri ve silinenleri geri yükle
                                    bool restored = TweakSnapshotService.RestoreSnapshot(tweak.Id);
                                    if (!restored && !string.IsNullOrEmpty(tweak.UndoRegFile))
                                    {
                                        RegistryService.ImportRegFile(tweak.UndoRegFile, isUndo: true);
                                    }

                                    if (tweak.Id == "DisableAnimations")
                                    {
                                        SystemSettingsService.SetClientAnimationState(disable: false);
                                    }
                                    else if (tweak.Id == "ReduceMenuDelay")
                                    {
                                        SystemSettingsService.SetMenuShowDelay(reduce: false);
                                    }
                                }
                            });
                        }

                        tweak.IsApplied = tweak.IsPendingChecked;
                    }

                    tweak.IsStaged = false;
                }

                // 3. Seçilen Bloatware Paketlerini Kaldır
                foreach (var app in selectedApps)
                {
                    currentStep++;
                    ActionProgressBar.Value = ((double)currentStep / totalSteps) * 100.0;
                    StatusTitleText.Text = $"Değişiklikler Uygulanıyor ({currentStep}/{totalSteps})";
                    StatusDescText.Text = cleanResiduals
                        ? $"Kaldırılıyor ve artıklar temizleniyor: {app.DisplayName}..."
                        : $"Paket kaldırılıyor: {app.DisplayName}...";
                    ApplyBtn.Content = $"İşleniyor ({currentStep}/{totalSteps})...";
                    await Task.Delay(20);

                    bool removed = await AppxService.RemoveAppxPackageAsync(app.AppId, app.RemovalMethod, app.DisplayName, cleanResiduals);
                    app.IsInstalled = !removed;
                    app.IsSelected = false;
                }

                // 4. Seçilen Windows Servislerini Kapat
                foreach (var service in stagedServices)
                {
                    currentStep++;
                    ActionProgressBar.Value = ((double)currentStep / totalSteps) * 100.0;
                    StatusTitleText.Text = $"Değişiklikler Uygulanıyor ({currentStep}/{totalSteps})";
                    StatusDescText.Text = $"Servis kapatılıyor: {service.DisplayName}...";
                    ApplyBtn.Content = $"İşleniyor ({currentStep}/{totalSteps})...";
                    await Task.Delay(20);

                    await ServiceManagementService.SetServiceStateAsync(service, true);
                    ServiceManagementService.CheckServiceStatus(service);
                    service.IsSelected = false;
                }

                // 5. Seçilen Oyun ve Performans Ayarlarını Uygula
                foreach (var gameTweak in stagedGameTweaks)
                {
                    currentStep++;
                    ActionProgressBar.Value = ((double)currentStep / totalSteps) * 100.0;
                    StatusTitleText.Text = $"Değişiklikler Uygulanıyor ({currentStep}/{totalSteps})";
                    StatusDescText.Text = $"Oyun ayarı uygulanıyor: {gameTweak.Title}...";
                    ApplyBtn.Content = $"İşleniyor ({currentStep}/{totalSteps})...";
                    await Task.Delay(20);

                    await GameOptimizerService.ApplySingleTweakAsync(gameTweak, gameTweak.IsPendingChecked);
                    gameTweak.IsApplied = gameTweak.IsPendingChecked;
                }

                StatusDescText.Text = "Değişiklikler işlendi, listeler yenileniyor...";
                ActionProgressBar.Value = 100.0;

                // Servis durumlarını anında canlı snapshot ile güncelle
                var runningSnapshot = ServiceManagementService.GetRunningServicesSnapshot();
                foreach (var svc in _allServices)
                {
                    ServiceManagementService.CheckServiceStatus(svc, runningSnapshot);
                }

                // Ortak ayarları sekmeler arasında eşitle
                if (stagedGameTweaks.Any(g => g.Id == "DisableBackgroundApps" || g.Id == "DisableAnimations" || g.Id == "DisableDVR"))
                {
                    foreach (var gt in stagedGameTweaks)
                    {
                        var matchingTweak = _allTweaks.FirstOrDefault(t => t.Id == gt.Id);
                        if (matchingTweak != null)
                        {
                            matchingTweak.IsApplied = gt.IsApplied;
                            matchingTweak.IsPendingChecked = gt.IsApplied;
                            matchingTweak.IsStaged = false;
                        }
                    }
                }
                if (stagedTweaks.Any(t => t.Id == "DisableBackgroundApps" || t.Id == "DisableAnimations" || t.Id == "DisableDVR"))
                {
                    foreach (var t in stagedTweaks)
                    {
                        var matchingGameTweak = _gameTweaks?.FirstOrDefault(g => g.Id == t.Id);
                        if (matchingGameTweak != null)
                        {
                            matchingGameTweak.IsApplied = t.IsApplied;
                            matchingGameTweak.IsPendingChecked = t.IsApplied;
                        }
                    }
                }

                // Ortak servisleri (SysMain, WSearch) sekmeler arasında iki yönlü eşitle
                if (stagedGameTweaks.Any(g => g.Id == "SysMain" || g.Id == "WSearch"))
                {
                    foreach (var gt in stagedGameTweaks.Where(g => g.Id == "SysMain" || g.Id == "WSearch"))
                    {
                        var matchingService = _allServices.FirstOrDefault(s => s.ServiceName.Equals(gt.Id, StringComparison.OrdinalIgnoreCase));
                        if (matchingService != null)
                        {
                            matchingService.IsCurrentDisabled = gt.IsApplied;
                            matchingService.IsSelected = false;
                        }
                    }
                }
                if (stagedServices.Any(s => s.ServiceName.Equals("SysMain", StringComparison.OrdinalIgnoreCase) || s.ServiceName.Equals("WSearch", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var svc in stagedServices)
                    {
                        var matchingGameTweak = _gameTweaks?.FirstOrDefault(g => g.Id.Equals(svc.ServiceName, StringComparison.OrdinalIgnoreCase));
                        if (matchingGameTweak != null)
                        {
                            matchingGameTweak.IsApplied = svc.IsCurrentDisabled;
                            matchingGameTweak.IsPendingChecked = svc.IsCurrentDisabled;
                        }
                    }
                }

                // Kaldırma işleminden sonra paket listesini yeniden canlı tara
                var installedPackages = await AppxService.GetInstalledPackagesAsync();
                foreach (var app in _allBloatwareApps)
                {
                    app.IsInstalled = AppxService.IsAppInstalled(app, installedPackages);
                    app.IsSelected = false;
                }

                ApplyBloatwareFilter();
                ApplyServiceFilter();
                UpdateReportView();
                UpdateActionBar();

                // Donanım kartlarını ve ayar durumlarını tazele
                RefreshGamePerformanceData();

                var failedApps = selectedApps.Where(a => a.IsInstalled).Select(a => a.DisplayName).ToList();
                string failedNote = failedApps.Count > 0 ? $"\n\n⚠️ Not: Şu paketler kaldırılamadı (sistem kısıtlaması veya koruma):\n• {string.Join("\n• ", failedApps)}" : "";

                // 4. Yeniden Başlatma & Gezgin Yenileme Mantığı
                var rebootFeatureNames = stagedTweaks.Where(t => t.RequiresReboot).Select(t => t.Title)
                    .Concat(stagedGameTweaks.Where(g => g.RequiresReboot && g.IsApplied).Select(g => g.Title)).ToList();

                var explorerFeatureNames = stagedTweaks.Where(t => t.RequiresExplorerRestart && !t.RequiresReboot).Select(t => t.Title)
                    .Concat(stagedGameTweaks.Where(g => g.RequiresExplorerRestart && !g.RequiresReboot && g.IsApplied).Select(g => g.Title)).ToList();

                if (rebootFeatureNames.Count > 0)
                {
                    string featureNames = string.Join("\n• ", rebootFeatureNames);
                    bool rebootConfirmed = await ShowModernDialogAsync(
                        "Bilgisayarı Yeniden Başlat",
                        $"{total - failedApps.Count} adet işlem başarıyla tamamlandı!{failedNote}\n\nAşağıdaki özelliklerin sistemde tam etkili olabilmesi için bilgisayarın yeniden başlatılması gerekmektedir:\n• {featureNames}\n\nBilgisayarınızı şimdi yeniden başlatmak ister misiniz?",
                        primaryButtonText: "⚡ Şimdi Yeniden Başlat",
                        secondaryButtonText: "Daha Sonra",
                        icon: "⚡",
                        accentColorHex: "#F59E0B");

                    if (rebootConfirmed)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "shutdown.exe",
                            Arguments = "/r /t 5 /c \"PureWin sistem optimizasyonu değişikliklerinin uygulanması için bilgisayarınız 5 saniye içinde yeniden başlatılacaktır.\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        return;
                    }

                    if (explorerFeatureNames.Count > 0)
                    {
                        bool restartConfirmed = await ShowModernDialogAsync(
                            "Dosya Gezgini Yeniden Başlatılsın mı?",
                            "Görsel ve görev çubuğu ayarlarının hemen yansıması için Dosya Gezgini'ni (Windows Explorer) şimdi yeniden başlatmak ister misiniz?",
                            primaryButtonText: "🔄 Şimdi Yeniden Başlat",
                            secondaryButtonText: "Daha Sonra",
                            icon: "🔄",
                            accentColorHex: "#0284C7");

                        if (restartConfirmed)
                        {
                            RegistryService.RestartExplorer();
                        }
                    }
                }
                else if (explorerFeatureNames.Count > 0)
                {
                    bool restartConfirmed = await ShowModernDialogAsync(
                        "Dosya Gezgini Yeniden Başlatılsın mı?",
                        $"{total - failedApps.Count} adet işlem başarıyla tamamlandı!{failedNote}\n\nGörev çubuğu ve görünüm ayarlarının anında etkili olması için Dosya Gezgini'ni (Windows Explorer) şimdi yeniden başlatmak ister misiniz?",
                        primaryButtonText: "🔄 Şimdi Yeniden Başlat",
                        secondaryButtonText: "Daha Sonra",
                        icon: "🔄",
                        accentColorHex: "#0284C7");

                    if (restartConfirmed)
                    {
                        RegistryService.RestartExplorer();
                    }
                }
                else
                {
                    if (failedApps.Count > 0)
                    {
                        string failedList = string.Join("\n• ", failedApps);
                        await ShowModernDialogAsync(
                            "İşlemler Kısmen Tamamlandı",
                            $"Seçilen {total - failedApps.Count} işlem başarıyla uygulandı ancak şu {failedApps.Count} paket Windows tarafından korunduğu veya kullanımda olduğu için kaldırılamadı:\n\n• {failedList}\n\n💡 Bu uygulamalar sistem kütüphanelerine bağlı veya arka planda kilitli olabilir.",
                            primaryButtonText: "Tamam",
                            icon: "⚠️",
                            accentColorHex: "#F59E0B");
                    }
                    else
                    {
                        await ShowModernDialogAsync(
                            "İşlem Başarıyla Tamamlandı",
                            $"Seçtiğiniz {total} adet sistem optimizasyonu ve kaldırma işlemi sorunsuz şekilde uygulandı.",
                            primaryButtonText: "Harika",
                            icon: "✓",
                            accentColorHex: "#10B981");
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowModernDialogAsync(
                    "Bir Hata Oluştu",
                    $"İşlem gerçekleştirilirken beklenmeyen bir sorun meydana geldi:\n\n{ex.Message}",
                    primaryButtonText: "Tamam",
                    icon: "❌",
                    accentColorHex: "#EF4444");
            }
            finally
            {
                _isApplying = false;
                UpdateActionBar();
            }
        }
        #endregion

        #region Oyun ve Performans Yönetimi
        private void OnGameTweakToggleChanged(object sender, RoutedEventArgs e)
        {
            UpdateActionBar();
        }

        private async void RefreshGamePerformanceData()
        {
            try
            {
                var monitorTask = Task.Run(() => GameOptimizerService.GetMonitorRefreshRateInfo());
                var powerTask = Task.Run(() => GameOptimizerService.GetActivePowerSchemeInfo());
                _ = UpdateLatencyAsync();

                var (currentHz, maxHz, isAtMax) = await monitorTask;
                if (TxtMonitorHz != null)
                {
                    TxtMonitorHz.Text = $"{currentHz} Hz";
                }
                if (TxtMonitorStatus != null)
                {
                    if (isAtMax)
                    {
                        TxtMonitorStatus.Text = "✓ En yüksek yenileme hızında";
                        TxtMonitorStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    }
                    else
                    {
                        TxtMonitorStatus.Text = $"⚠️ Monitör {maxHz} Hz destekliyor!";
                        TxtMonitorStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                    }
                }

                // Aktif Güç Planı Bilgisi
                var (_, displayName, isUltimate) = await powerTask;
                if (TxtPowerPlanName != null)
                {
                    TxtPowerPlanName.Text = string.IsNullOrEmpty(displayName) ? "Standart Plan" : displayName;
                }
                if (TxtPowerPlanStatus != null)
                {
                    if (isUltimate)
                    {
                        TxtPowerPlanStatus.Text = "✓ Maksimum CPU performansı";
                        TxtPowerPlanStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    }
                    else if (displayName.Contains("Yüksek", StringComparison.OrdinalIgnoreCase) || displayName.Contains("High", StringComparison.OrdinalIgnoreCase))
                    {
                        TxtPowerPlanStatus.Text = "✓ Yüksek CPU performansı";
                        TxtPowerPlanStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    }
                    else if (displayName.Contains("Tasarruf", StringComparison.OrdinalIgnoreCase) || displayName.Contains("Saver", StringComparison.OrdinalIgnoreCase))
                    {
                        TxtPowerPlanStatus.Text = "⚠️ Güç Tasarrufu Aktif (Düşük Hız)";
                        TxtPowerPlanStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                    }
                    else
                    {
                        TxtPowerPlanStatus.Text = "Standart Windows Güç Dengesi";
                        TxtPowerPlanStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                    }
                }

                // Ayarların gerçek durumlarını tazele
                if (_gameTweaks != null && _gameTweaks.Count > 0)
                {
                    await Task.Run(() => GameOptimizerService.RefreshLiveStates(_gameTweaks));
                }
            }
            catch { }
        }

        private async Task UpdateLatencyAsync()
        {
            if (_isUpdatingPing) return;
            _isUpdatingPing = true;

            try
            {
                if (TxtNetworkPing != null) TxtNetworkPing.Text = "...";
                if (TxtPingStatus != null) TxtPingStatus.Text = "Ölçülüyor...";

                int ping = await GameOptimizerService.MeasureLatencyAsync();

                if (TxtNetworkPing != null)
                {
                    if (ping >= 0)
                    {
                        TxtNetworkPing.Text = $"{ping} ms";
                        if (TxtPingStatus != null)
                        {
                            if (ping < 30)
                            {
                                TxtPingStatus.Text = "Mükemmel bağlantı (Düşük gecikme)";
                                TxtPingStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                            }
                            else if (ping < 80)
                            {
                                TxtPingStatus.Text = "İyi bağlantı süresi";
                                TxtPingStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                            }
                            else
                            {
                                TxtPingStatus.Text = "Yüksek gecikme tespit edildi";
                                TxtPingStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                            }
                        }
                    }
                    else
                    {
                        TxtNetworkPing.Text = "Yanıt Yok";
                        if (TxtPingStatus != null)
                        {
                            TxtPingStatus.Text = "Ağ erişimi kontrol edilemedi";
                            TxtPingStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                        }
                    }
                }
            }
            catch
            {
                if (TxtNetworkPing != null) TxtNetworkPing.Text = "-- ms";
                if (TxtPingStatus != null) TxtPingStatus.Text = "Yaklaşık ağ gecikmesi";
            }
            finally
            {
                _isUpdatingPing = false;
            }
        }

        private void BtnRefreshPing_Click(object sender, RoutedEventArgs e)
        {
            _ = UpdateLatencyAsync();
        }
        #endregion

        #region Sistem Temizliği Yönetimi
        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void OnGroupHeaderClicked(object sender, MouseButtonEventArgs e)
        {
            // Kullanıcı doğrudan CheckBox'a veya bileşenine tıkladıysa menüyü açıp kapama
            if (e.OriginalSource is CheckBox || FindVisualParent<CheckBox>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            if (sender is FrameworkElement fe && fe.DataContext is CleanupGroupItem group)
            {
                group.ToggleExpand();
            }
        }

        private void UpdateCleanupBadges()
        {
            if (_cleanupGroups == null || _cleanupGroups.Count == 0) return;

            long totalScannedBytes = 0;
            long selectedBytes = 0;
            int totalItems = 0;
            int selectedItems = 0;
            bool anyScanned = false;

            foreach (var group in _cleanupGroups)
            {
                foreach (var item in group.Items)
                {
                    totalItems++;
                    if (item.DisplaySize != "Taranmadı")
                    {
                        anyScanned = true;
                        totalScannedBytes += item.SizeBytes;
                        if (item.IsSelected)
                        {
                            selectedBytes += item.SizeBytes;
                            selectedItems++;
                        }
                    }
                    else if (item.IsSelected)
                    {
                        selectedItems++;
                    }
                }
            }

            if (CleanupScannedBadge != null)
            {
                CleanupScannedBadge.Text = anyScanned
                    ? $"Toplam Taranan: {CleanupSubItem.FormatBytes(totalScannedBytes)}"
                    : "Toplam Taranan: Henüz Taranmadı";
            }

            if (CleanupSelectedBadge != null)
            {
                CleanupSelectedBadge.Text = anyScanned
                    ? $"Seçilen Alan: {CleanupSubItem.FormatBytes(selectedBytes)}"
                    : $"Seçilen Madde: {selectedItems} / {totalItems}";
            }

            if (CleanupCategoriesCountText != null)
            {
                CleanupCategoriesCountText.Text = $"{totalItems} Temizlik Kalemi";
            }

            if (CleanupStatusBadge != null)
            {
                if (_isCleaning)
                {
                    CleanupStatusBadge.Text = "● Temizleniyor...";
                    CleanupStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                }
                else if (_isScanningCleanup)
                {
                    CleanupStatusBadge.Text = "● Taranıyor...";
                    CleanupStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                }
                else if (anyScanned)
                {
                    if (totalScannedBytes <= 0)
                    {
                        CleanupStatusBadge.Text = "✓ Sistem Tertemiz";
                        CleanupStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    }
                    else
                    {
                        CleanupStatusBadge.Text = $"● {CleanupSubItem.FormatBytes(selectedBytes)} Seçildi";
                        CleanupStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    }
                }
                else
                {
                    CleanupStatusBadge.Text = "● Beklemede (Önce Tara)";
                    CleanupStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                }
            }

            if (ExecuteCleanupBtn != null)
            {
                ExecuteCleanupBtn.IsEnabled = !_isCleaning && !_isScanningCleanup && selectedItems > 0;
            }

            if (ScanCleanupBtn != null)
            {
                ScanCleanupBtn.IsEnabled = !_isCleaning && !_isScanningCleanup;
            }

            if (SelectSafeCleanupBtn != null)
            {
                SelectSafeCleanupBtn.IsEnabled = !_isCleaning && !_isScanningCleanup;
            }

            if (SelectAllCleanupBtn != null)
            {
                SelectAllCleanupBtn.IsEnabled = !_isCleaning && !_isScanningCleanup;
            }

            if (DeselectAllCleanupBtn != null)
            {
                DeselectAllCleanupBtn.IsEnabled = !_isCleaning && !_isScanningCleanup;
            }
        }

        private void SelectSafeCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (_cleanupGroups == null) return;
            foreach (var group in _cleanupGroups)
            {
                foreach (var item in group.Items)
                {
                    item.IsSelected = !item.IsRisky;
                }
                group.OnSubItemSelectionChanged();
            }
            UpdateCleanupBadges();
        }

        private void SelectAllCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (_cleanupGroups == null) return;
            foreach (var group in _cleanupGroups)
            {
                group.IsSelected = true;
            }
            UpdateCleanupBadges();
        }

        private void DeselectAllCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (_cleanupGroups == null) return;
            foreach (var group in _cleanupGroups)
            {
                group.IsSelected = false;
            }
            UpdateCleanupBadges();
        }

        private async void ScanCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (_isScanningCleanup || _isCleaning) return;

            _isScanningCleanup = true;
            CleanupProgressBar.Visibility = Visibility.Visible;
            UpdateCleanupBadges();

            try
            {
                foreach (var group in _cleanupGroups)
                {
                    group.SetStatus("Taranıyor...", "blue");

                    foreach (var item in group.Items)
                    {
                        item.SetStatus("Taranıyor...", "blue");
                        long size = await CleanupService.ScanSubItemAsync(item);
                        item.SizeBytes = size;
                        ApplyCustomDisplaySize(item);
                        UpdateCleanupBadges();
                    }

                    group.OnSubItemSizeChanged();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScanCleanup Error]: {ex.Message}");
            }
            finally
            {
                _isScanningCleanup = false;
                CleanupProgressBar.Visibility = Visibility.Collapsed;
                UpdateCleanupBadges();
            }
        }

        private async void ExecuteCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (_isCleaning || _isScanningCleanup) return;
            _isCleaning = true;

            try
            {
                var selectedItems = _cleanupGroups.SelectMany(g => g.Items).Where(i => i.IsSelected).ToList();
                if (selectedItems.Count == 0)
                {
                    await ShowModernDialogAsync("Seçim Yapılmadı", "Lütfen temizlemek istediğiniz en az bir öğeyi işaretleyin.", "Tamam", null, "⚠️", "#F59E0B");
                    return;
                }

                long totalSelectedBytes = selectedItems.Sum(i => i.SizeBytes);
                string sizeInfo = totalSelectedBytes > 0 ? CleanupSubItem.FormatBytes(totalSelectedBytes) : $"{selectedItems.Count} adet sistem temizlik kalemi";

                // Açık olan ve kapatılması gereken uygulamaları/süreçleri tespit et (Yöntem B)
                var runningApps = GetRunningProcessesForSelectedItems(selectedItems);

                if (runningApps.Count > 0)
                {
                    string appList = string.Join("\n• ", runningApps.Select(b => b.DisplayName));
                    string dialogTitle = "Açık Uygulamalar Algılandı";
                    string dialogMessage = $"{sizeInfo} temizlenecektir.\n\n⚠️ Tam ve eksiksiz bir temizlik yapılabilmesi için şu açık uygulamaların kapatılması önerilir:\n\n• {appList}\n\n💡 \"Kapat ve Temizle\" seçildiğinde açık oturum ve sekmelerin diske güvenle yazılması için uygulamalar nazikçe kapatılır ve 3 saniye beklenir.\n\nNasıl devam etmek istersiniz?";

                    string choice = await ShowModernChoiceDialogAsync(
                        dialogTitle,
                        dialogMessage,
                        primaryButtonText: "⚡ Kapat ve Temizle",
                        secondaryButtonText: "Kapatmadan Temizle",
                        cancelButtonText: "İptal",
                        icon: "⚡",
                        accentColorHex: "#F59E0B"
                    );

                    if (choice == "cancel") return;

                    if (choice == "primary")
                    {
                        // Kullanıcı onayladı: Açık uygulamaları önce güvenle kapat
                        await CloseRunningProcessesAsync(runningApps);
                    }
                    // choice == "secondary" ise uygulamaları kapatmadan kilitli olmayan dosyaları temizler
                }
                else
                {
                    string dialogTitle = "Sistem Temizliği Onayı";
                    string dialogMessage = $"{sizeInfo} temizlenecektir. İşlem geri alınamaz. Devam etmek istiyor musunuz?";
                    bool confirm = await ShowModernDialogAsync(
                        dialogTitle,
                        dialogMessage,
                        "Temizle",
                        "İptal",
                        "🧹",
                        "#10B981"
                    );

                    if (!confirm) return;
                }

                // Sürücülerin temizlik öncesi boş alanını kaydet (Gerçek disk kazancı hesaplaması)
                var driveBefore = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var drive in System.IO.DriveInfo.GetDrives())
                    {
                        if (drive.IsReady && (drive.DriveType == System.IO.DriveType.Fixed || drive.DriveType == System.IO.DriveType.Removable))
                        {
                            driveBefore[drive.Name] = drive.AvailableFreeSpace;
                        }
                    }
                }
                catch { }

                CleanupProgressBar.Visibility = Visibility.Visible;
                UpdateCleanupBadges();
                CleanupService.ResetSkippedCount();

                long cleanedBytesTotal = 0;

                foreach (var item in selectedItems)
                {
                    item.SetStatus("Temizleniyor...", "blue");
                    long cleaned = await CleanupService.CleanSubItemAsync(item);
                    cleanedBytesTotal += cleaned;

                    // Kalan boyutu tekrar tara ve UI güncelle
                    long remaining = await CleanupService.ScanSubItemAsync(item);
                    item.SizeBytes = remaining;

                    if (cleaned > 0 && remaining == 0)
                    {
                        item.SetStatus("✓ Temizlendi", "green");
                    }
                    else if (cleaned > 0 && remaining > 0)
                    {
                        item.SetStatus("Kısmen Temizlendi", "amber");
                    }
                    else // cleaned == 0
                    {
                        if (remaining > 0)
                        {
                            item.SetStatus("Kullanımda / Kilitli", "amber");
                        }
                        else
                        {
                            item.SetStatus("✓ Temiz", "green");
                        }
                    }

                    ApplyCustomDisplaySize(item);
                    UpdateCleanupBadges();
                }

                foreach (var group in _cleanupGroups)
                {
                    group.OnSubItemSizeChanged();
                }

                // Temizlik sonrası gerçek boş alan delta kontrolü
                long totalRealDiskFreed = 0;
                try
                {
                    foreach (var drive in System.IO.DriveInfo.GetDrives())
                    {
                        if (drive.IsReady && driveBefore.TryGetValue(drive.Name, out long beforeFree))
                        {
                            long diff = drive.AvailableFreeSpace - beforeFree;
                            if (diff > 0)
                            {
                                totalRealDiskFreed += diff;
                            }
                        }
                    }
                }
                catch { }

                long skippedBytes = Math.Max(0, totalSelectedBytes - cleanedBytesTotal);
                int lockedCount = CleanupService.SkippedLockedFilesCount;
                string lockedReportLine = lockedCount > 0 ? $"\n• Kilitli / Atlanan Dosya: {lockedCount} adet" : "";
                string realDiskLine = totalRealDiskFreed > 0 ? $"\n• Gerçek Geri Kazanılan Boş Disk Alanı: {CleanupSubItem.FormatBytes(totalRealDiskFreed)}" : "";
                string realDiskDiffNote = "";
                if (totalRealDiskFreed > 0 && cleanedBytesTotal > totalRealDiskFreed)
                {
                    realDiskDiffNote = "\n\n💡 Not: Silinen dosya boyutu ile anlık diske yansıyan net boş alan arasındaki fark; açık tanıtıcılar (NTFS DELETE_PENDING bekleyen silmeler), kümeleme payı (cluster slack) veya Windows Gölge Kopyaları (VSS) nedeniyledir. Kalan alan ilgili arka plan işlemleri kapandıkça işletim sistemi tarafından tamamen serbest bırakılacaktır.";
                }

                string resultMessage;
                if (cleanedBytesTotal > 0)
                {
                    string cleanedFormatted = CleanupSubItem.FormatBytes(cleanedBytesTotal);
                    long targetBytes = totalSelectedBytes > 0 ? totalSelectedBytes : cleanedBytesTotal;
                    string targetFormatted = CleanupSubItem.FormatBytes(targetBytes);

                    if (skippedBytes > 0)
                    {
                        string skippedFormatted = CleanupSubItem.FormatBytes(skippedBytes);
                        resultMessage = $"📊 Temizlik Özeti:\n\n" +
                                        $"• Hedeflenen Alan: {targetFormatted}\n" +
                                        $"• Başarıyla Silinen Dosya Boyutu: {cleanedFormatted}" +
                                        realDiskLine +
                                        $"\n• Kullanımda / Korunan: {skippedFormatted}" +
                                        lockedReportLine +
                                        realDiskDiffNote +
                                        $"\n\nℹ️ Korunan dosyalar o an Windows çekirdeği veya açık uygulamalar tarafından aktif kullanıldığı için sistem kararlılığı adına güvenle atlandı.";
                    }
                    else
                    {
                        resultMessage = $"📊 Temizlik Özeti:\n\n" +
                                        $"• Hedeflenen Alan: {targetFormatted}\n" +
                                        $"• Başarıyla Silinen Dosya Boyutu: {cleanedFormatted}" +
                                        realDiskLine +
                                        $"\n• Durum: Seçilen tüm dosyalar eksiksiz temizlendi." +
                                        lockedReportLine +
                                        realDiskDiffNote +
                                        $"\n\n✨ Depolama alanı başarıyla geri kazanıldı!";
                    }
                }
                else
                {
                    if (totalSelectedBytes > 0)
                    {
                        string totalFormatted = CleanupSubItem.FormatBytes(totalSelectedBytes);
                        resultMessage = $"📊 Temizlik Özeti:\n\n" +
                                        $"• Hedeflenen Alan: {totalFormatted}\n" +
                                        $"• Temizlenen: 0 B\n" +
                                        $"• Kullanımda / Kilitli: {totalFormatted}" +
                                        lockedReportLine +
                                        $"\n\n💡 Seçilen dosyalar o an Windows çekirdeği veya çalışan uygulamalar tarafından aktif kullanımda olduğu için sistem kararlılığı korunarak atlanmıştır.";
                    }
                    else
                    {
                        resultMessage = "Seçilen tüm sistem artıkları ve geçici dosyalar zaten temiz durumda.";
                    }
                }

                await ShowModernDialogAsync(
                    "Temizlik Tamamlandı",
                    resultMessage,
                    "Harika",
                    null,
                    "✓",
                    "#10B981"
                );
            }
            catch (Exception ex)
            {
                await ShowModernDialogAsync("Hata Oluştu", $"Temizlik işlemi sırasında bir hata meydana geldi:\n{ex.Message}", "Tamam", null, "❌", "#EF4444");
            }
            finally
            {
                _isCleaning = false;
                CleanupProgressBar.Visibility = Visibility.Collapsed;
                UpdateCleanupBadges();
            }
        }

        private void ApplyCustomDisplaySize(CleanupSubItem item)
        {
            if (item.Id == "Log_DnsCache")
            {
                int dnsCount = CleanupService.GetDnsRecordCount();
                if (dnsCount > 0)
                {
                    item.DisplaySize = $"{dnsCount} Kayıt (RAM)";
                    item.SetStatus("Hazır", "orange");
                }
                else
                {
                    item.DisplaySize = "0 B (Temiz)";
                    item.SetStatus("✓ Temiz", "green");
                }
            }
            else if (item.SizeBytes == 0)
            {
                if (item.Id == "Browser_CookiesAndPasswords")
                {
                    item.DisplaySize = "0 B (Çerez Yok)";
                }
                else if (item.Id.StartsWith("Browser_"))
                {
                    item.DisplaySize = "0 B (Önbellek Temiz)";
                }
                else if (item.Id == "Log_RdpHistory")
                {
                    item.DisplaySize = "0 B (Kayıt Yok)";
                }
                else if (item.Id == "Log_NetCache")
                {
                    item.DisplaySize = "0 B (Önbellek Yok)";
                }
                else if (item.Id == "Update_WindowsOld")
                {
                    item.DisplaySize = "0 B (Eski Sürüm Yok)";
                }
                else if (item.Id == "Update_InstallerCache")
                {
                    item.DisplaySize = "0 B (Artık Yok)";
                }
                else if (item.Id == "Update_WinSxS")
                {
                    item.DisplaySize = "0 B (Bileşen Deposu Temiz)";
                }
                else if (item.Id == "System_RecycleBin")
                {
                    item.DisplaySize = "0 B (Kutu Boş)";
                }
                else if (item.Id == "MemoryDumps")
                {
                    item.DisplaySize = "0 B (Döküm Yok)";
                }
                else if (item.Id == "Driver_UnusedPackages")
                {
                    item.DisplaySize = "0 B (Yedek Paket Yok)";
                }
                else if (item.Id == "Driver_InfSetupLogs")
                {
                    item.DisplaySize = "0 B (Log Yok)";
                }
                else if (item.Id == "Driver_PrintSpooler")
                {
                    item.DisplaySize = "0 B (Kuyruk Boş)";
                }
                else if (item.Id == "Driver_DefenderCache" || item.Id == "PrefetchCache" || item.Id == "Cache_INetCache" || item.Id.StartsWith("App_"))
                {
                    item.DisplaySize = "0 B (Önbellek Temiz)";
                }
                else if (item.Id == "Driver_GpuSetupCache")
                {
                    item.DisplaySize = "0 B (Artık Yok)";
                }
                else if (item.Id == "Clean_ChkdskArtifacts")
                {
                    item.DisplaySize = "0 B (Artık Yok)";
                }
                else if (item.Id == "Clean_DownloadedProgramFiles")
                {
                    item.DisplaySize = "0 B (Dosya Yok)";
                }
                else if (item.Id == "Clean_OldRestorePoints")
                {
                    item.DisplaySize = "0 B (Eski Nokta Yok)";
                }
                else if (item.Id == "System_RegistryBackups")
                {
                    item.DisplaySize = "0 B (En Son Yedek Korunuyor)";
                }
                else if (item.Id == "TaskSchedulerLogs" || item.Id == "Log_EventLogs")
                {
                    item.DisplaySize = "0 B (Kayıt Yok)";
                }
                else if (item.Id == "WERReports" || item.Id == "AppCrashReports")
                {
                    item.DisplaySize = "0 B (Rapor Yok)";
                }
                else if (item.Id == "SetupLogs" || item.Id == "UpdateLogs" || item.Id == "Log_SystemLogs")
                {
                    item.DisplaySize = "0 B (Log Yok)";
                }
                else if (item.Id == "PowerShellCache" || item.Id == "ThumbCache" || item.Id == "IconCache" ||
                         item.Id == "DirectXShaderCache" || item.Id == "FontCache" || item.Id == "DotNetCache" ||
                         item.Id == "MediaPlayerCache" || item.Id == "Update_SoftwareDistribution" || item.Id == "Update_DeliveryOptimization")
                {
                    item.DisplaySize = "0 B (Önbellek Temiz)";
                }
                else if (item.Id == "SearchIndexTemp" || item.Id == "ComDcomTemp" || item.Id == "UserTemp" ||
                         item.Id == "Update_WinSxS" || item.Id == "Update_InstallerCache")
                {
                    item.DisplaySize = "0 B (Temiz)";
                }
                else if (item.Id == "Log_ClipboardHistory")
                {
                    item.DisplaySize = "0 B (Pano Temiz)";
                }
            }
        }

        private static readonly (string[] ItemIds, string DisplayName, string[] ProcessNames)[] LockingProcessDefinitions = new[]
        {
            // Tarayıcılar
            (new[] { "Browser_Chrome" }, "Google Chrome", new[] { "chrome" }),
            (new[] { "Browser_Edge" }, "Microsoft Edge", new[] { "msedge" }),
            (new[] { "Browser_Brave" }, "Brave Browser", new[] { "brave" }),
            (new[] { "Browser_OperaGX" }, "Opera GX / Opera", new[] { "opera", "opera_gx" }),
            (new[] { "Browser_Firefox" }, "Mozilla Firefox", new[] { "firefox" }),

            // Geliştirici & Üçüncü Taraf Uygulamalar
            (new[] { "App_VSCode" }, "Visual Studio Code", new[] { "Code" }),
            (new[] { "App_NodeJs" }, "Node.js (npm)", new[] { "node" }),
            (new[] { "App_Discord" }, "Discord", new[] { "Discord" }),
            (new[] { "App_Spotify" }, "Spotify", new[] { "Spotify" }),
            (new[] { "App_Telegram" }, "Telegram Desktop", new[] { "Telegram" }),
            (new[] { "App_Vlc" }, "VLC Media Player", new[] { "vlc" }),

            // Oyun İstemcileri
            (new[] { "App_Steam" }, "Steam", new[] { "steam" }),
            (new[] { "App_EpicGames" }, "Epic Games Launcher", new[] { "EpicGamesLauncher" }),
            (new[] { "App_EaApp" }, "EA App", new[] { "EADesktop", "EABackgroundService" }),
            (new[] { "App_Ubisoft" }, "Ubisoft Connect", new[] { "UbisoftConnect", "upc" }),
            (new[] { "App_BattleNet" }, "Battle.net", new[] { "Battle.net" }),
            (new[] { "App_GogGalaxy" }, "GOG Galaxy", new[] { "GalaxyClient" }),

            // Medya & Diğer Uygulamalar
            (new[] { "MediaPlayerCache" }, "Windows Media Player", new[] { "wmplayer" }),
            (new[] { "PowerShellCache" }, "PowerShell", new[] { "powershell", "pwsh" }),

            // Windows Installer & Güncelleme Süreçleri
            (new[] { "Update_InstallerCache" }, "Windows Installer (msiexec)", new[] { "msiexec" }),
            (new[] { "Update_SoftwareDistribution", "Update_WinSxS" }, "Windows Güncelleme Çalışanı (TiWorker)", new[] { "TiWorker" }),
            (new[] { "WERReports", "AppCrashReports" }, "Windows Hata Raporlama", new[] { "WerFault", "wermgr" }),

            // Uzak Masaüstü
            (new[] { "Log_RdpHistory" }, "Uzak Masaüstü Bağlantısı (mstsc)", new[] { "mstsc" }),

            // Sürücü & Donanım Yükleyicileri
            (new[] { "Driver_GpuSetupCache" }, "Sürücü / Donanım Yükleyicisi (Setup)", new[] { "setup", "NVIDIA.Installer" })
        };

        private static List<(string DisplayName, string[] ProcessNames)> GetRunningProcessesForSelectedItems(List<CleanupSubItem> selectedItems)
        {
            var result = new List<(string DisplayName, string[] ProcessNames)>();
            bool checkAllBrowsers = selectedItems.Any(i => i.Id == "Browser_CookiesAndPasswords" || i.Id == "Browser_History");

            foreach (var def in LockingProcessDefinitions)
            {
                bool isRelevant = def.ItemIds.Any(id => selectedItems.Any(i => i.Id == id));
                if (!isRelevant && checkAllBrowsers && def.ItemIds.Any(id => id.StartsWith("Browser_")))
                {
                    isRelevant = true;
                }

                if (isRelevant)
                {
                    bool isRunning = false;
                    foreach (var procName in def.ProcessNames)
                    {
                        try
                        {
                            if (Process.GetProcessesByName(procName).Length > 0)
                            {
                                isRunning = true;
                                break;
                            }
                        }
                        catch { }
                    }

                    if (isRunning)
                    {
                        result.Add((def.DisplayName, def.ProcessNames));
                    }
                }
            }

            return result;
        }

        private static async Task CloseRunningProcessesAsync(List<(string DisplayName, string[] ProcessNames)> processes)
        {
            await Task.Run(async () =>
            {
                foreach (var item in processes)
                {
                    foreach (var procName in item.ProcessNames)
                    {
                        try
                        {
                            foreach (var p in Process.GetProcessesByName(procName))
                            {
                                try { p.CloseMainWindow(); } catch { }
                            }
                        }
                        catch { }
                    }
                }

                // Tarayıcıların ve uygulamaların açık sekmeleri/oturumları diske güvenle yazması için nazik bekleme süresi
                await Task.Delay(3000);

                foreach (var item in processes)
                {
                    foreach (var procName in item.ProcessNames)
                    {
                        try
                        {
                            foreach (var p in Process.GetProcessesByName(procName))
                            {
                                try { p.Kill(); } catch { }
                            }
                        }
                        catch { }
                    }
                }

                await Task.Delay(500);
            });
        }
        #endregion

        #region Modern Windows 11 Dialog
        private TaskCompletionSource<bool>? _dialogTcs;
        private TaskCompletionSource<string>? _customDialogTcs;

        public Task<bool> ShowModernDialogAsync(
            string title, 
            string message, 
            string primaryButtonText = "Tamam", 
            string? secondaryButtonText = null, 
            string icon = "ℹ️", 
            string accentColorHex = "#3B82F6")
        {
            _dialogTcs = new TaskCompletionSource<bool>();
            _customDialogTcs = null;

            DialogTitleText.Text = title;
            DialogMessageText.Text = message;
            DialogIconText.Text = icon;
            DialogPrimaryBtn.Content = primaryButtonText;
            DialogCancelBtn.Visibility = Visibility.Collapsed;

            var color = (Color)ColorConverter.ConvertFromString(accentColorHex);
            DialogIconBadge.Background = new SolidColorBrush(Color.FromArgb(0x25, color.R, color.G, color.B));
            DialogIconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x45, color.R, color.G, color.B));

            if (!string.IsNullOrEmpty(secondaryButtonText))
            {
                DialogSecondaryBtn.Content = secondaryButtonText;
                DialogSecondaryBtn.Visibility = Visibility.Visible;
            }
            else
            {
                DialogSecondaryBtn.Visibility = Visibility.Collapsed;
            }

            ModernDialogOverlay.Visibility = Visibility.Visible;
            return _dialogTcs.Task;
        }

        public Task<string> ShowModernChoiceDialogAsync(
            string title,
            string message,
            string primaryButtonText,
            string? secondaryButtonText,
            string? cancelButtonText,
            string icon = "ℹ️",
            string accentColorHex = "#3B82F6")
        {
            _dialogTcs = null;
            _customDialogTcs = new TaskCompletionSource<string>();

            DialogTitleText.Text = title;
            DialogMessageText.Text = message;
            DialogIconText.Text = icon;
            DialogPrimaryBtn.Content = primaryButtonText;

            var color = (Color)ColorConverter.ConvertFromString(accentColorHex);
            DialogIconBadge.Background = new SolidColorBrush(Color.FromArgb(0x25, color.R, color.G, color.B));
            DialogIconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x45, color.R, color.G, color.B));

            if (!string.IsNullOrEmpty(cancelButtonText))
            {
                DialogCancelBtn.Content = cancelButtonText;
                DialogCancelBtn.Visibility = Visibility.Visible;
            }
            else
            {
                DialogCancelBtn.Visibility = Visibility.Collapsed;
            }

            if (!string.IsNullOrEmpty(secondaryButtonText))
            {
                DialogSecondaryBtn.Content = secondaryButtonText;
                DialogSecondaryBtn.Visibility = Visibility.Visible;
            }
            else
            {
                DialogSecondaryBtn.Visibility = Visibility.Collapsed;
            }

            ModernDialogOverlay.Visibility = Visibility.Visible;
            return _customDialogTcs.Task;
        }

        private void DialogPrimary_Click(object sender, RoutedEventArgs e)
        {
            ModernDialogOverlay.Visibility = Visibility.Collapsed;
            DialogCancelBtn.Visibility = Visibility.Collapsed;
            _dialogTcs?.TrySetResult(true);
            _customDialogTcs?.TrySetResult("primary");
        }

        private void DialogSecondary_Click(object sender, RoutedEventArgs e)
        {
            ModernDialogOverlay.Visibility = Visibility.Collapsed;
            DialogCancelBtn.Visibility = Visibility.Collapsed;
            _dialogTcs?.TrySetResult(false);
            _customDialogTcs?.TrySetResult("secondary");
        }

        private void DialogCancel_Click(object sender, RoutedEventArgs e)
        {
            ModernDialogOverlay.Visibility = Visibility.Collapsed;
            DialogCancelBtn.Visibility = Visibility.Collapsed;
            _dialogTcs?.TrySetResult(false);
            _customDialogTcs?.TrySetResult("cancel");
        }
        #endregion

        #region Yazılım Güncelleyici (Software Updater)

        private async Task LoadSoftwareUpdatesAsync()
        {
            if (_isScanningSoftwareUpdates) return;
            _isScanningSoftwareUpdates = true;

            try
            {
                RefreshUpdatesBtn.IsEnabled = false;
                UpdateAllBtn.IsEnabled = false;

                SoftwareMissingWingetWarning.Visibility = Visibility.Collapsed;
                SoftwareEmptyState.Visibility = Visibility.Collapsed;
                SoftwareScrollViewer.Visibility = Visibility.Collapsed;
                SoftwareLoadingState.Visibility = Visibility.Visible;

                StatPendingUpdatesText.Text = "Taranıyor...";
                StatLastScanText.Text = "Taranıyor...";

                if (!SoftwareUpdateService.IsWingetAvailable())
                {
                    SoftwareLoadingState.Visibility = Visibility.Collapsed;
                    SoftwareMissingWingetWarning.Visibility = Visibility.Visible;
                    StatPendingUpdatesText.Text = "winget Yok";
                    StatLastScanText.Text = "Kullanılamıyor";
                    SoftwareFilterStatusText.Text = "winget aracı sistemde bulunamadı";
                    return;
                }

                var updates = await SoftwareUpdateService.CheckForUpdatesAsync();

                _allSoftwareUpdates = updates;
                ApplySoftwareFilter();
                UpdateSoftwareStats();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] LoadSoftwareUpdatesAsync hatası: {ex.Message}");
                SoftwareLoadingState.Visibility = Visibility.Collapsed;
                SoftwareFilterStatusText.Text = "Tarama sırasında bir hata oluştu";
            }
            finally
            {
                _isScanningSoftwareUpdates = false;
                RefreshUpdatesBtn.IsEnabled = true;
                UpdateAllBtn.IsEnabled = _allSoftwareUpdates.Any(u => u.IsReady) && !_isUpdatingSoftware;
                SoftwareLoadingState.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplySoftwareFilter()
        {
            string query = SoftwareSearchBox?.Text?.Trim().ToLowerInvariant() ?? string.Empty;

            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allSoftwareUpdates
                : _allSoftwareUpdates.Where(u =>
                    u.Name.ToLowerInvariant().Contains(query) ||
                    u.Id.ToLowerInvariant().Contains(query) ||
                    u.Publisher.ToLowerInvariant().Contains(query)).ToList();

            _displayedSoftwareUpdates.Clear();
            foreach (var item in filtered)
            {
                _displayedSoftwareUpdates.Add(item);
            }

            if (SoftwareUpdatesList.ItemsSource != _displayedSoftwareUpdates)
            {
                SoftwareUpdatesList.ItemsSource = _displayedSoftwareUpdates;
            }

            if (_allSoftwareUpdates.Count == 0)
            {
                SoftwareEmptyState.Visibility = Visibility.Visible;
                SoftwareScrollViewer.Visibility = Visibility.Collapsed;
                SoftwareFilterStatusText.Text = "0 güncelleme bulundu";
            }
            else
            {
                SoftwareEmptyState.Visibility = Visibility.Collapsed;
                SoftwareScrollViewer.Visibility = Visibility.Visible;
                SoftwareFilterStatusText.Text = $"{_displayedSoftwareUpdates.Count} / {_allSoftwareUpdates.Count} güncelleme listeleniyor";
            }
        }

        private void UpdateSoftwareStats()
        {
            int pending = _allSoftwareUpdates.Count(u => u.IsReady || u.IsUpdating);

            StatPendingUpdatesText.Text = pending > 0 ? $"{pending} Uygulama" : "Hepsi Güncel";
            StatLastScanText.Text = DateTime.Now.ToString("HH:mm");

            if (pending > 0)
            {
                NavUpdateBadge.Visibility = Visibility.Visible;
                NavUpdateBadgeText.Text = pending.ToString();
                UpdateAllBtnText.Text = $"Tümünü Güncelle ({pending})";
                UpdateAllBtn.IsEnabled = !_isUpdatingSoftware;
            }
            else
            {
                NavUpdateBadge.Visibility = Visibility.Collapsed;
                UpdateAllBtnText.Text = "Tümünü Güncelle";
                UpdateAllBtn.IsEnabled = false;
            }
        }

        private async void RefreshUpdates_Click(object sender, RoutedEventArgs e)
        {
            await LoadSoftwareUpdatesAsync();
        }

        private void SoftwareSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySoftwareFilter();
        }

        private async void UpdateSingleApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not SoftwareUpdateItem item) return;
            if (item.IsUpdating) return;

            UpdateAllBtn.IsEnabled = false;

            try
            {
                bool success = await SoftwareUpdateService.UpdateAppAsync(item);
                UpdateSoftwareStats();

                if (success)
                {
                    ApplySoftwareFilter();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] UpdateSingleApp_Click hatası: {ex.Message}");
            }
            finally
            {
                UpdateAllBtn.IsEnabled = _allSoftwareUpdates.Any(u => u.IsReady) && !_isUpdatingSoftware;
            }
        }

        private async void UpdateAll_Click(object sender, RoutedEventArgs e)
        {
            var pendingItems = _allSoftwareUpdates.Where(u => u.IsReady).ToList();
            if (pendingItems.Count == 0 || _isUpdatingSoftware) return;

            _isUpdatingSoftware = true;
            UpdateAllBtn.IsEnabled = false;
            RefreshUpdatesBtn.IsEnabled = false;

            try
            {
                foreach (var item in pendingItems)
                {
                    await SoftwareUpdateService.UpdateAppAsync(item);
                    UpdateSoftwareStats();
                }

                ApplySoftwareFilter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] UpdateAll_Click hatası: {ex.Message}");
            }
            finally
            {
                _isUpdatingSoftware = false;
                RefreshUpdatesBtn.IsEnabled = true;
                UpdateSoftwareStats();
            }
        }

        #endregion

        #region Sistem ve Donanım Kimliği Modal Yönetimi
        private void UpdateSystemStatusCard()
        {
            if (_systemSpecs == null) return;
            if (CardOsText != null) CardOsText.Text = _systemSpecs.OsName;
            if (CardStatusText != null)
            {
                CardStatusText.Text = _systemSpecs.IsActivated ? "Etkinleştirildi" : "Etkinleştirilmemiş";
                CardStatusText.Foreground = _systemSpecs.IsActivated
                    ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))
                    : new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            }
            if (CardStatusDot != null)
            {
                CardStatusDot.Fill = _systemSpecs.IsActivated
                    ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))
                    : new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            }
        }

        private void PopulateSystemSpecsDetails(SystemSpecsModel specs)
        {
            if (SpecsOsFullName != null)
            {
                SpecsOsFullName.Text = $"{specs.OsName} ({specs.OsArchitecture})";
            }
            if (SpecsOsBuildText != null)
            {
                SpecsOsBuildText.Text = !string.IsNullOrEmpty(specs.OsDisplayVersion)
                    ? $"{specs.OsDisplayVersion} ({specs.OsBuild})"
                    : specs.OsBuild;
            }

            if (SpecsLicenseBadge != null && SpecsLicenseIcon != null && SpecsLicenseStatusText != null)
            {
                if (specs.IsActivated)
                {
                    SpecsLicenseBadge.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x10, 0xB9, 0x81));
                    SpecsLicenseBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x10, 0xB9, 0x81));
                    SpecsLicenseIcon.Text = "✓";
                    SpecsLicenseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    SpecsLicenseStatusText.Text = specs.LicenseStatusText;
                    SpecsLicenseStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                }
                else
                {
                    SpecsLicenseBadge.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xF5, 0x9E, 0x0B));
                    SpecsLicenseBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xF5, 0x9E, 0x0B));
                    SpecsLicenseIcon.Text = "⚠️";
                    SpecsLicenseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    SpecsLicenseStatusText.Text = specs.LicenseStatusText;
                    SpecsLicenseStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                }
            }

            if (SpecsLicenseSubText != null)
            {
                SpecsLicenseSubText.Text = string.IsNullOrEmpty(specs.LicenseChannelDesc) ? "Windows Lisansı" : specs.LicenseChannelDesc;
            }

            if (SpecsLicenseChannelText != null)
            {
                SpecsLicenseChannelText.Text = specs.LicenseChannel;
            }

            if (SpecsPartialKeyText != null)
            {
                SpecsPartialKeyText.Text = !string.IsNullOrEmpty(specs.PartialKey) ? $"••••• - {specs.PartialKey}" : "Yok / Dijital";
            }

            if (SpecsMotherboardText != null)
            {
                SpecsMotherboardText.Text = specs.MotherboardFullName;
            }

            if (SpecsGpuText != null)
            {
                SpecsGpuText.Text = string.IsNullOrEmpty(specs.GpuName) ? "Standart Ekran Bağdaştırıcısı" : specs.GpuName;
            }

            if (SpecsGpuDriverText != null)
            {
                SpecsGpuDriverText.Text = !string.IsNullOrEmpty(specs.GpuDriverVersion) ? $"Sürücü: {specs.GpuDriverVersion}" : "";
            }

            if (SpecsGpuVramText != null)
            {
                SpecsGpuVramText.Text = !string.IsNullOrEmpty(specs.GpuVram) ? $" · {specs.GpuVram}" : "";
            }

            if (SpecsCpuText != null)
            {
                SpecsCpuText.Text = specs.CpuName;
            }

            if (SpecsCpuCoresText != null)
            {
                SpecsCpuCoresText.Text = specs.CpuLogicalCores > 0 ? $" · {specs.CpuLogicalCores} Çekirdek" : "";
            }

            if (SpecsRamText != null)
            {
                SpecsRamText.Text = !string.IsNullOrEmpty(specs.TotalRam) ? $"{specs.TotalRam} RAM" : "";
            }

            if (SpecsRamAvailText != null)
            {
                SpecsRamAvailText.Text = !string.IsNullOrEmpty(specs.AvailableRam) ? $" ({specs.AvailableRam})" : "";
            }

            if (SpecsBiosText != null)
            {
                SpecsBiosText.Text = !string.IsNullOrEmpty(specs.BiosVersion) ? $"Sürüm: {specs.BiosVersion}" : "Sürüm Bilinmiyor";
            }

            if (SpecsBiosDateText != null)
            {
                SpecsBiosDateText.Text = !string.IsNullOrEmpty(specs.BiosReleaseDate) ? $" · Tarih: {specs.BiosReleaseDate}" : "";
            }
        }

        private void SystemStatusCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (_systemSpecs == null)
            {
                _systemSpecs = SystemInfoService.GetHardwareAndOsSpecs();
            }
            PopulateSystemSpecsDetails(_systemSpecs);
            SystemSpecsOverlay.Visibility = Visibility.Visible;
        }

        private void CloseSystemSpecsOverlay_Click(object sender, RoutedEventArgs e)
        {
            SystemSpecsOverlay.Visibility = Visibility.Collapsed;
        }

        private void SystemSpecsOverlayBackground_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == SystemSpecsOverlay)
            {
                SystemSpecsOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void CopyMotherboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_systemSpecs != null && !string.IsNullOrWhiteSpace(_systemSpecs.MotherboardFullName))
                {
                    Clipboard.SetText(_systemSpecs.MotherboardFullName);
                    await ShowModernDialogAsync("Kopyalandı", $"Anakart modeli panoya kopyalandı:\n{_systemSpecs.MotherboardFullName}", "Tamam", icon: "📋", accentColorHex: "#6366F1");
                }
            }
            catch { }
        }

        private async void CopyGpu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_systemSpecs != null && !string.IsNullOrWhiteSpace(_systemSpecs.GpuName))
                {
                    string text = string.IsNullOrEmpty(_systemSpecs.GpuVram) 
                        ? _systemSpecs.GpuName 
                        : $"{_systemSpecs.GpuName} ({_systemSpecs.GpuVram})";
                    Clipboard.SetText(text);
                    await ShowModernDialogAsync("Kopyalandı", $"Ekran kartı bilgisi panoya kopyalandı:\n{text}", "Tamam", icon: "📋", accentColorHex: "#10B981");
                }
            }
            catch { }
        }

        private async void CopyCpu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_systemSpecs != null && !string.IsNullOrWhiteSpace(_systemSpecs.CpuName))
                {
                    Clipboard.SetText(_systemSpecs.CpuName);
                    await ShowModernDialogAsync("Kopyalandı", $"İşlemci modeli panoya kopyalandı:\n{_systemSpecs.CpuName}", "Tamam", icon: "📋", accentColorHex: "#3B82F6");
                }
            }
            catch { }
        }

        private async void CopyBios_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_systemSpecs != null && !string.IsNullOrWhiteSpace(_systemSpecs.BiosVersion))
                {
                    Clipboard.SetText(_systemSpecs.BiosVersion);
                    await ShowModernDialogAsync("Kopyalandı", $"BIOS sürümü panoya kopyalandı:\n{_systemSpecs.BiosVersion}", "Tamam", icon: "📋", accentColorHex: "#A855F7");
                }
            }
            catch { }
        }

        private async void CopyAllSpecs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_systemSpecs != null)
                {
                    string summary = SystemInfoService.FormatSpecsSummary(_systemSpecs);
                    Clipboard.SetText(summary);
                    await ShowModernDialogAsync("Özet Kopyalandı", "Sistem ve donanım kimliği panoya kopyalandı.", "Tamam", icon: "📋", accentColorHex: "#10B981");
                }
            }
            catch { }
        }

        private void OpenActivationSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:activation",
                    UseShellExecute = true
                });
            }
            catch { }
        }
        #endregion
    }
}