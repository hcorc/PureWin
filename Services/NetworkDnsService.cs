using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PureWin.Models;

namespace PureWin.Services
{
    public static class NetworkDnsService
    {
        private const int ScriptTimeoutSeconds = 20;

        /// <summary>
        /// Belirtilen InterfaceIndex için kayıtlı bir DNS yedeği bulunup bulunmadığını kontrol eder.
        /// </summary>
        public static bool HasSnapshot(int ifIndex)
        {
            try
            {
                string path = GetSnapshotFilePath(ifIndex);
                return File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Seçilen adaptörün IPv4 DNS ayarlarını güvenli bir şekilde günceller.
        /// Önce güncel durum yedeklenir. Doğrulama başarısız olursa otomatik geri alınır.
        /// </summary>
        public static async Task<(bool Ok, string Message)> ApplyDnsAsync(NetworkAdapterInfo adapter, bool useDhcp, List<string> ipv4Servers)
        {
            if (adapter == null)
            {
                return (false, "Bağdaştırıcı bilgisi bulunamadı.");
            }

            // Güvenlik: Bağdaştırıcı adında çift tırnak kontrolü
            if (adapter.Name.Contains('"'))
            {
                return (false, "Bağdaştırıcı adında geçersiz karakter (\") bulunduğu için işlem iptal edildi.");
            }

            var validatedServers = new List<string>();
            if (!useDhcp)
            {
                if (!ValidateIpv4Servers(ipv4Servers, out validatedServers, out string valErr))
                {
                    return (false, valErr);
                }
            }

            // 1) Güncel v4 durumunu oku: statik mi (registry NameServer) + sunucu listesi
            var (stateOk, isAutomaticNow, stateErr) = await GetIpv4DnsAutomaticStateAsync(adapter.IfIndex);
            if (!stateOk)
            {
                return (false, $"DNS değişikliği iptal edildi: mevcut ayar durumu okunamadı ({stateErr}).");
            }

            List<string> currentServers;
            if (isAutomaticNow)
            {
                currentServers = new List<string>();
            }
            else
            {
                var (readServers, readErr) = await ReadCurrentIpv4DnsServersAsync(adapter.IfIndex);
                if (readServers.Count == 0)
                {
                    string detail = !string.IsNullOrEmpty(readErr) ? readErr : "statik DNS listesi boş döndü.";
                    return (false, $"DNS değişikliği iptal edildi: mevcut ayarlar okunamadı ({detail}).");
                }

                currentServers = readServers;
            }

            // 2) Snapshot oluştur ve diske YAZ. Yazılamazsa HİÇBİR DEĞİŞİKLİK YAPMA.
            var snapshot = new DnsSnapshot
            {
                IfIndex = adapter.IfIndex,
                AdapterName = adapter.Name,
                AdapterDescription = adapter.Description,
                SavedAtUtc = DateTime.UtcNow,
                Ipv4Servers = currentServers,
                Ipv4WasAutomatic = isAutomaticNow
            };

            if (!SaveSnapshot(snapshot, out string snapshotPath))
            {
                return (false, "DNS değişikliği iptal edildi: yedek kaydedilemedi.");
            }

            // 3) Uygula
            bool applySuccess;
            if (useDhcp)
            {
                string script = $"netsh interface ipv4 set dnsservers name=\\\"{adapter.Name}\\\" source=dhcp";
                var (ok, _, _) = await RunPowerShellCommandAsync(script);
                applySuccess = ok;
            }
            else
            {
                string serverListStr = string.Join(",", validatedServers.Select(s => $"'{s}'"));
                string script = $"Set-DnsClientServerAddress -InterfaceIndex {adapter.IfIndex} -ServerAddresses @({serverListStr})";
                var (ok, _, _) = await RunPowerShellCommandAsync(script);
                applySuccess = ok;
            }

            // Ağ yığınının güncellenmesi için kısa bekleme
            await Task.Delay(300);

            // 4) DOĞRULA (read-back):
            //    - DHCP: registry NameServer boş olmalı (Get-DnsClientServerAddress, DHCP'den öğrenilen sunucuları döndürebilir)
            //    - Statik: v4 sunucu kümesi == beklenen küme (sıradan bağımsız)
            bool readBackOk = false;
            if (applySuccess)
            {
                if (useDhcp)
                {
                    var (verifyOk, isAutoAfter, _) = await GetIpv4DnsAutomaticStateAsync(adapter.IfIndex);
                    readBackOk = verifyOk && isAutoAfter;
                }
                else
                {
                    var (readBackServers, _) = await ReadCurrentIpv4DnsServersAsync(adapter.IfIndex);
                    var expectedSet = new HashSet<string>(validatedServers, StringComparer.OrdinalIgnoreCase);
                    var actualSet = new HashSet<string>(readBackServers, StringComparer.OrdinalIgnoreCase);
                    readBackOk = expectedSet.SetEquals(actualSet);
                }
            }

            // 5) useDhcp=false ise ayrıca ÇÖZÜMLEME TESTİ: en az 1 A kaydı dönmeli
            bool resolveOk = true;
            if (applySuccess && readBackOk && !useDhcp)
            {
                resolveOk = await VerifyDnsResolutionAsync(validatedServers[0]);
            }

            // 6) Doğrulama BAŞARISIZ → OTOMATİK GERİ ALMA
            if (!applySuccess || !readBackOk || !resolveOk)
            {
                bool rollbackOk = await InternalRestoreAsync(snapshot);
                if (rollbackOk)
                {
                    DeleteSnapshot(adapter.IfIndex);
                    return (false, "Yeni DNS doğrulanamadı; önceki ayar otomatik geri yüklendi.");
                }
                else
                {
                    return (false, $"DNS doğrulanamadı ve otomatik geri alma BAŞARISIZ! Lütfen ağ ayarlarını elle kontrol edin. Yedek: {snapshotPath}");
                }
            }

            // 7) Her şey başarılı → (true, "DNS güncellendi ve doğrulandı ✓") (yedek kalır).
            return (true, "DNS güncellendi ve doğrulandı ✓");
        }

        /// <summary>
        /// Daha önce kaydedilmiş DNS yedeğini geri yükler ve doğrular.
        /// </summary>
        public static async Task<(bool Ok, string Message)> RestoreSnapshotAsync(int ifIndex)
        {
            if (!HasSnapshot(ifIndex))
            {
                return (false, "Geri alınacak kayıt yok.");
            }

            var snapshot = LoadSnapshot(ifIndex, out string snapshotPath);
            if (snapshot == null)
            {
                return (false, $"Yedek dosyası okunamadı. Yedek: {snapshotPath}");
            }

            if (snapshot.AdapterName.Contains('"'))
            {
                return (false, "Bağdaştırıcı adında geçersiz karakter (\") bulunduğu için işlem iptal edildi.");
            }

            bool rollbackOk = await InternalRestoreAsync(snapshot);
            if (rollbackOk)
            {
                DeleteSnapshot(ifIndex);
                return (true, "Önceki DNS ayarı başarıyla geri yüklendi ✓");
            }
            else
            {
                return (false, $"DNS geri yükleme doğrulanamadı! Lütfen ağ ayarlarını elle kontrol edin. Yedek: {snapshotPath}");
            }
        }

        #region Dahili Yardımcılar

        private static async Task<bool> InternalRestoreAsync(DnsSnapshot snapshot)
        {
            try
            {
                bool applyOk;
                if (snapshot.Ipv4WasAutomatic)
                {
                    string script = $"netsh interface ipv4 set dnsservers name=\\\"{snapshot.AdapterName}\\\" source=dhcp";
                    var (ok, _, _) = await RunPowerShellCommandAsync(script);
                    applyOk = ok;
                }
                else
                {
                    string serverListStr = string.Join(",", snapshot.Ipv4Servers.Select(s => $"'{s}'"));
                    string script = $"Set-DnsClientServerAddress -InterfaceIndex {snapshot.IfIndex} -ServerAddresses @({serverListStr})";
                    var (ok, _, _) = await RunPowerShellCommandAsync(script);
                    applyOk = ok;
                }

                if (!applyOk) return false;

                await Task.Delay(300);

                // Geri yüklemeyi doğrula (otomatik: registry NameServer boş; statik: sunucu kümesi eşit)
                if (snapshot.Ipv4WasAutomatic)
                {
                    var (verifyOk, isAutoNow, _) = await GetIpv4DnsAutomaticStateAsync(snapshot.IfIndex);
                    return verifyOk && isAutoNow;
                }
                else
                {
                    if (snapshot.Ipv4Servers == null || snapshot.Ipv4Servers.Count == 0)
                    {
                        return false;
                    }

                    var (currentServers, _) = await ReadCurrentIpv4DnsServersAsync(snapshot.IfIndex);
                    var expectedSet = new HashSet<string>(snapshot.Ipv4Servers, StringComparer.OrdinalIgnoreCase);
                    var actualSet = new HashSet<string>(currentServers, StringComparer.OrdinalIgnoreCase);
                    return expectedSet.SetEquals(actualSet);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkDnsService] InternalRestoreAsync hatası: {ex.Message}");
                return false;
            }
        }

        private static async Task<(List<string> Servers, string? Error)> ReadCurrentIpv4DnsServersAsync(int ifIndex)
        {
            string script = $"[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; [pscustomobject]@{{ servers=@((Get-DnsClientServerAddress -InterfaceIndex {ifIndex} -AddressFamily IPv4).ServerAddresses) }} | ConvertTo-Json -Depth 3 -Compress";
            var (success, output, error) = await RunPowerShellCommandAsync(script);
            if (!success || string.IsNullOrWhiteSpace(output))
            {
                return (new List<string>(), !string.IsNullOrEmpty(error) ? error : "DNS bilgisi okunamadı.");
            }

            var servers = ParseServersJson(output);
            return (servers, null);
        }

        private static async Task<bool> VerifyDnsResolutionAsync(string serverAddress)
        {
            string script = $"[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; try {{ $res = Resolve-DnsName -Name 'www.microsoft.com' -Type A -Server '{serverAddress}' -QuickTimeout -ErrorAction Stop; if (@($res | Where-Object {{ $_.Type -eq 'A' }}).Count -gt 0) {{ 'PUREWIN_DNS_OK' }} else {{ 'PUREWIN_DNS_FAIL' }} }} catch {{ 'PUREWIN_DNS_FAIL' }}";
            var (success, output, _) = await RunPowerShellCommandAsync(script, timeoutSeconds: 10);
            return success && output.Contains("PUREWIN_DNS_OK");
        }

        /// <summary>
        /// Arabirimin IPv4 DNS ayarının otomatik (DHCP) olup olmadığını registry üzerinden kesin olarak tespit eder.
        /// Get-DnsClientServerAddress, otomatik modda DHCP'den öğrenilen sunucuları da döndürebildiği için
        /// "boş liste = otomatik" varsayımı güvenilir değildir; statik NameServer değerinin yokluğu esastır.
        /// </summary>
        public static async Task<(bool Success, bool IsAutomatic, string? Error)> GetIpv4DnsAutomaticStateAsync(int ifIndex)
        {
            string script = $"[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; try {{ $guid = (Get-NetAdapter -InterfaceIndex {ifIndex}).InterfaceGuid.ToString() -replace '[{{}}]',''; $k = 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\{{' + $guid + '}}'; $ns = $null; $p = Get-ItemProperty -Path $k -ErrorAction SilentlyContinue; if ($p) {{ $ns = $p.NameServer }}; [pscustomobject]@{{ automatic = [string]::IsNullOrWhiteSpace($ns) }} | ConvertTo-Json -Compress }} catch {{ 'PUREWIN_ERR' }}";
            var (success, output, error) = await RunPowerShellCommandAsync(script);
            if (!success || string.IsNullOrWhiteSpace(output) || output.Contains("PUREWIN_ERR"))
            {
                return (false, false, !string.IsNullOrEmpty(error) ? error : "DNS otomatik durum bilgisi okunamadı.");
            }

            try
            {
                using var doc = JsonDocument.Parse(output.Trim());
                if (doc.RootElement.TryGetProperty("automatic", out var autoElem) &&
                    (autoElem.ValueKind == JsonValueKind.True || autoElem.ValueKind == JsonValueKind.False))
                {
                    return (true, autoElem.GetBoolean(), null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkDnsService] Otomatik durum JSON parse hatası: {ex.Message}");
            }

            return (false, false, "DNS otomatik durum bilgisi ayrıştırılamadı.");
        }

        private static List<string> ParseServersJson(string jsonText)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(jsonText)) return list;

            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                if (doc.RootElement.TryGetProperty("servers", out var serversElem))
                {
                    if (serversElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in serversElem.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                string? s = item.GetString();
                                if (!string.IsNullOrWhiteSpace(s))
                                {
                                    list.Add(s.Trim());
                                }
                            }
                        }
                    }
                    else if (serversElem.ValueKind == JsonValueKind.String)
                    {
                        string? s = serversElem.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            list.Add(s.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkDnsService] JSON parse hatası: {ex.Message}");
            }

            return list;
        }

        private static bool ValidateIpv4Servers(List<string> servers, out List<string> validated, out string error)
        {
            validated = new List<string>();
            error = string.Empty;

            if (servers == null || servers.Count == 0)
            {
                error = "En az bir IPv4 DNS adresi belirtilmelidir.";
                return false;
            }

            if (servers.Count > 2)
            {
                error = "En fazla 2 DNS adresi girilebilir.";
                return false;
            }

            foreach (var s in servers)
            {
                string trimmed = s?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                var parts = trimmed.Split('.');
                if (parts.Length != 4 || !parts.All(p => byte.TryParse(p, out _)))
                {
                    error = $"Geçersiz IPv4 adresi: '{trimmed}'";
                    return false;
                }

                if (!IPAddress.TryParse(trimmed, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
                {
                    error = $"Geçersiz IPv4 adresi: '{trimmed}'";
                    return false;
                }

                string normalized = ip.ToString();
                if (validated.Contains(normalized))
                {
                    error = $"Aynı DNS adresi iki kez girilemez: {normalized}";
                    return false;
                }

                validated.Add(normalized);
            }

            if (validated.Count == 0)
            {
                error = "En az bir geçerli IPv4 DNS adresi girilmelidir.";
                return false;
            }

            return true;
        }

        private static string GetSnapshotDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "PureWin", "NetworkDns");
        }

        private static string GetSnapshotFilePath(int ifIndex)
        {
            return Path.Combine(GetSnapshotDirectory(), $"if{ifIndex}.json");
        }

        private static bool SaveSnapshot(DnsSnapshot snapshot, out string filePath)
        {
            filePath = GetSnapshotFilePath(snapshot.IfIndex);
            try
            {
                string dir = GetSnapshotDirectory();
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string json = JsonSerializer.Serialize(snapshot, options);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkDnsService] Snapshot kaydedilemedi: {ex.Message}");
                return false;
            }
        }

        private static DnsSnapshot? LoadSnapshot(int ifIndex, out string filePath)
        {
            filePath = GetSnapshotFilePath(ifIndex);
            try
            {
                if (!File.Exists(filePath)) return null;

                string json = File.ReadAllText(filePath, Encoding.UTF8);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<DnsSnapshot>(json, options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkDnsService] Snapshot okunamadı: {ex.Message}");
                return null;
            }
        }

        private static void DeleteSnapshot(int ifIndex)
        {
            try
            {
                string filePath = GetSnapshotFilePath(ifIndex);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkDnsService] Snapshot silinemedi: {ex.Message}");
            }
        }

        private static async Task<(bool Success, string Output, string Error)> RunPowerShellCommandAsync(string script, int timeoutSeconds = ScriptTimeoutSeconds)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using var process = new Process { StartInfo = psi };
                    process.Start();

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                    var exitTask = process.WaitForExitAsync(cts.Token);

                    if (await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) != exitTask)
                    {
                        try { process.Kill(); } catch { }
                        return (false, string.Empty, $"Komut zaman aşımına uğradı ({timeoutSeconds} sn).");
                    }

                    string stdout = (await outputTask).Trim();
                    string stderr = (await errorTask).Trim();

                    if (process.ExitCode == 0)
                    {
                        return (true, stdout, stderr);
                    }
                    else
                    {
                        string err = !string.IsNullOrEmpty(stderr) ? stderr : stdout;
                        return (false, stdout, string.IsNullOrEmpty(err) ? "Komut başarısız oldu." : err);
                    }
                }
                catch (Exception ex)
                {
                    return (false, string.Empty, ex.Message);
                }
            });
        }

        #endregion
    }
}
