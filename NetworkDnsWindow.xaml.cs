using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PureWin.Models;
using PureWin.Services;
using Wpf.Ui.Controls;

namespace PureWin
{
    public partial class NetworkDnsWindow : FluentWindow
    {
        private List<NetworkAdapterInfo> _cachedAdapters = new();
        private bool _isConfirmingFlush = false;
        private CancellationTokenSource? _flushCts;
        private NetworkAdapterInfo? _editingAdapter;

        public NetworkDnsWindow()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
            Loaded += NetworkDnsWindow_Loaded;
            Closed += NetworkDnsWindow_Closed;
        }

        private async void NetworkDnsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadNetworkAdaptersAsync();
        }

        private void NetworkDnsWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                _flushCts?.Cancel();
                _flushCts?.Dispose();
                _flushCts = null;
            }
            catch { }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadNetworkAdaptersAsync();
        }

        private async Task LoadNetworkAdaptersAsync()
        {
            SetControlsBusy(true);
            LoadingBar.Visibility = Visibility.Visible;
            EmptyStateBorder.Visibility = Visibility.Collapsed;
            StatusText.Text = "Ağ bilgileri yükleniyor...";

            var (adapters, error) = await NetworkInfoService.GetNetworkSnapshotAsync();

            _cachedAdapters = adapters ?? new List<NetworkAdapterInfo>();

            // Adaptörler listelenmeden önce yedek durumlarını kontrol et
            foreach (var a in _cachedAdapters)
            {
                a.HasSnapshot = NetworkDnsService.HasSnapshot(a.IfIndex);
            }

            AdaptersList.ItemsSource = _cachedAdapters;
            LoadingBar.Visibility = Visibility.Collapsed;
            SetControlsBusy(false);

            if (!string.IsNullOrEmpty(error))
            {
                if (_cachedAdapters.Count == 0)
                {
                    EmptyStateBorder.Visibility = Visibility.Visible;
                    EmptyStateText.Text = $"Hata: {error}";
                }
                StatusText.Text = $"Ağ bilgileri alınırken sorun oluştu: {error}";
            }
            else if (_cachedAdapters.Count == 0)
            {
                EmptyStateBorder.Visibility = Visibility.Visible;
                EmptyStateText.Text = "Ağ bağdaştırıcısı bulunamadı.";
                StatusText.Text = "Hiçbir ağ bağdaştırıcısı bulunamadı.";
            }
            else
            {
                EmptyStateBorder.Visibility = Visibility.Collapsed;
                StatusText.Text = $"{_cachedAdapters.Count} ağ bağdaştırıcısı başarıyla listelendi.";
            }

            BtnPingTest.IsEnabled = _cachedAdapters.Count > 0;
        }

        private void SetControlsBusy(bool isBusy)
        {
            BtnRefresh.IsEnabled = !isBusy;
            BtnFlushDns.IsEnabled = !isBusy;
            BtnPingTest.IsEnabled = !isBusy && (_cachedAdapters.Count > 0);
            AdaptersList.IsEnabled = !isBusy;
        }

        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConfirmingFlush)
            {
                // 1. Adım: Onay bekleme moduna geç
                _isConfirmingFlush = true;
                FlushDnsIcon.Text = "⚠️";
                FlushDnsText.Text = "Emin misin? Tekrar tıkla";

                _flushCts?.Cancel();
                _flushCts = new CancellationTokenSource();
                var token = _flushCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(3000, token);
                        if (!token.IsCancellationRequested)
                        {
                            await Dispatcher.InvokeAsync(ResetFlushButton);
                        }
                    }
                    catch (TaskCanceledException) { }
                });
                return;
            }

            // 2. Adım: Kullanıcı 3 saniye içinde tekrar tıkladı, işlemi çalıştır
            _flushCts?.Cancel();
            ResetFlushButton();

            BtnFlushDns.IsEnabled = false;
            FlushDnsIcon.Text = "⏳";
            FlushDnsText.Text = "Temizleniyor...";
            StatusText.Text = "DNS önbelleği temizleniyor...";

            var (success, message) = await NetworkInfoService.ClearDnsCacheAsync();

            ResetFlushButton();
            BtnFlushDns.IsEnabled = true;
            StatusText.Text = message;
        }

        private void ResetFlushButton()
        {
            _isConfirmingFlush = false;
            FlushDnsIcon.Text = "🧹";
            FlushDnsText.Text = "DNS Önbelleğini Temizle";
        }

        private async void BtnPingTest_Click(object sender, RoutedEventArgs e)
        {
            BtnPingTest.IsEnabled = false;
            PingTestIcon.Text = "⏳";
            PingTestText.Text = "Test ediliyor...";
            LatencySection.Visibility = Visibility.Visible;
            StatusText.Text = "Ağ geçitleri ve DNS sunucularına gecikme testi yapılıyor...";

            var results = await NetworkInfoService.RunLatencyTestAsync(_cachedAdapters);

            LatencyResultsList.ItemsSource = results;
            BtnPingTest.IsEnabled = true;
            PingTestIcon.Text = "📡";
            PingTestText.Text = "Gecikme Testi";
            StatusText.Text = $"Gecikme testi tamamlandı ({results.Count} hedef test edildi) ✓";
        }

        #region FAZ B: DNS Değiştirme ve Geri Alma İşleyicileri

        private async void DnsEditBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not NetworkAdapterInfo adapter)
                return;

            _editingAdapter = adapter;
            DnsEditAdapterName.Text = $"{adapter.Name} — {adapter.Description}";
            DnsEditErrorText.Text = string.Empty;
            DnsEditErrorText.Visibility = Visibility.Collapsed;
            DnsApplyBtn.Content = "Uygula ve Doğrula";
            DnsApplyBtn.IsEnabled = true;
            DnsCancelBtn.IsEnabled = true;

            // Otomatik (DHCP) durumu registry üzerinden kesin tespit edilir;
            // okunamazsa adaptör listesindeki bilgiye düşülür.
            var (stateOk, isAutomatic, _) = await NetworkDnsService.GetIpv4DnsAutomaticStateAsync(adapter.IfIndex);
            if (!stateOk)
            {
                isAutomatic = adapter.DnsIsAutomaticV4 || adapter.DnsServersV4.Count == 0;
            }

            if (isAutomatic)
            {
                DnsDhcpRadio.IsChecked = true;
                DnsPrimaryBox.Text = string.Empty;
                DnsSecondaryBox.Text = string.Empty;
            }
            else
            {
                DnsCustomRadio.IsChecked = true;
                DnsPrimaryBox.Text = adapter.DnsServersV4.Count > 0 ? adapter.DnsServersV4[0] : string.Empty;
                DnsSecondaryBox.Text = adapter.DnsServersV4.Count > 1 ? adapter.DnsServersV4[1] : string.Empty;
            }

            UpdateOverlayModeControls();
            DnsEditOverlay.Visibility = Visibility.Visible;
        }

        private void DnsCancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DnsEditOverlay.Visibility = Visibility.Collapsed;
            _editingAdapter = null;
        }

        private void DnsModeRadio_Changed(object sender, RoutedEventArgs e)
        {
            UpdateOverlayModeControls();
        }

        private void UpdateOverlayModeControls()
        {
            if (DnsPresetCloudflare == null || DnsPrimaryBox == null) return;

            bool isCustom = DnsCustomRadio.IsChecked == true;
            DnsPresetCloudflare.IsEnabled = isCustom;
            DnsPresetGoogle.IsEnabled = isCustom;
            DnsPresetQuad9.IsEnabled = isCustom;
            DnsPrimaryBox.IsEnabled = isCustom;
            DnsSecondaryBox.IsEnabled = isCustom;

            if (!isCustom)
            {
                DnsEditErrorText.Text = string.Empty;
                DnsEditErrorText.Visibility = Visibility.Collapsed;
            }
        }

        private void DnsPresetCloudflare_Click(object sender, RoutedEventArgs e)
        {
            DnsCustomRadio.IsChecked = true;
            DnsPrimaryBox.Text = "1.1.1.1";
            DnsSecondaryBox.Text = "1.0.0.1";
            DnsEditErrorText.Visibility = Visibility.Collapsed;
        }

        private void DnsPresetGoogle_Click(object sender, RoutedEventArgs e)
        {
            DnsCustomRadio.IsChecked = true;
            DnsPrimaryBox.Text = "8.8.8.8";
            DnsSecondaryBox.Text = "8.8.4.4";
            DnsEditErrorText.Visibility = Visibility.Collapsed;
        }

        private void DnsPresetQuad9_Click(object sender, RoutedEventArgs e)
        {
            DnsCustomRadio.IsChecked = true;
            DnsPrimaryBox.Text = "9.9.9.9";
            DnsSecondaryBox.Text = "149.112.112.112";
            DnsEditErrorText.Visibility = Visibility.Collapsed;
        }

        private async void DnsApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_editingAdapter == null) return;

            bool useDhcp = DnsDhcpRadio.IsChecked == true;
            var servers = new List<string>();

            if (!useDhcp)
            {
                string pText = DnsPrimaryBox.Text?.Trim() ?? string.Empty;
                string sText = DnsSecondaryBox.Text?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(pText))
                {
                    ShowEditError("Birincil DNS adresi boş olamaz.");
                    return;
                }

                if (!IsValidIpv4Address(pText, out string normalizedPrimary))
                {
                    ShowEditError($"Birincil DNS adresi geçerli bir IPv4 adresi değil: '{pText}'");
                    return;
                }

                servers.Add(normalizedPrimary);

                if (!string.IsNullOrWhiteSpace(sText))
                {
                    if (!IsValidIpv4Address(sText, out string normalizedSecondary))
                    {
                        ShowEditError($"İkincil DNS adresi geçerli bir IPv4 adresi değil: '{sText}'");
                        return;
                    }

                    if (string.Equals(normalizedPrimary, normalizedSecondary, StringComparison.OrdinalIgnoreCase))
                    {
                        ShowEditError("Birincil ve ikincil DNS adresleri aynı olamaz.");
                        return;
                    }

                    servers.Add(normalizedSecondary);
                }
            }

            DnsEditErrorText.Visibility = Visibility.Collapsed;
            DnsEditErrorText.Text = string.Empty;

            // Kontrolleri kilitle ve ilerlemeyi bildir
            DnsApplyBtn.IsEnabled = false;
            DnsApplyBtn.Content = "Uygulanıyor...";
            DnsCancelBtn.IsEnabled = false;
            SetControlsBusy(true);
            StatusText.Text = "DNS uygulanıyor ve doğrulanıyor...";

            var currentEditing = _editingAdapter;
            var (ok, message) = await NetworkDnsService.ApplyDnsAsync(currentEditing, useDhcp, servers);

            if (ok)
            {
                DnsEditOverlay.Visibility = Visibility.Collapsed;
                _editingAdapter = null;
                await LoadNetworkAdaptersAsync();
                SetControlsBusy(false);
                StatusText.Text = message;
            }
            else
            {
                ShowEditError(message);
                await LoadNetworkAdaptersAsync();
                SetControlsBusy(false);
                StatusText.Text = message;

                DnsApplyBtn.IsEnabled = true;
                DnsApplyBtn.Content = "Uygula ve Doğrula";
                DnsCancelBtn.IsEnabled = true;
            }
        }

        private void ShowEditError(string message)
        {
            DnsEditErrorText.Text = message;
            DnsEditErrorText.Visibility = Visibility.Visible;
        }

        private static bool IsValidIpv4Address(string ipStr, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(ipStr)) return false;

            string trimmed = ipStr.Trim();
            var parts = trimmed.Split('.');
            if (parts.Length != 4) return false;

            foreach (var part in parts)
            {
                if (!byte.TryParse(part, out _)) return false;
            }

            if (IPAddress.TryParse(trimmed, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
            {
                normalized = ip.ToString();
                return true;
            }

            return false;
        }

        private async void DnsRestoreBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not NetworkAdapterInfo adapter)
                return;

            SetControlsBusy(true);
            StatusText.Text = $"{adapter.Name} için DNS ayarı geri alınıyor...";

            var (ok, message) = await NetworkDnsService.RestoreSnapshotAsync(adapter.IfIndex);

            await LoadNetworkAdaptersAsync();
            SetControlsBusy(false);
            StatusText.Text = message;
        }

        #endregion
    }
}
