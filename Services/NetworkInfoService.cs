using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PureWin.Models;

namespace PureWin.Services
{
    public static class NetworkInfoService
    {
        private const int ScriptTimeoutSeconds = 20;

        /// <summary>
        /// Salt okunur PowerShell komutu ile tüm ağ adaptörlerini, IP, ağ geçidi ve DNS bilgilerini çeker.
        /// </summary>
        public static async Task<(List<NetworkAdapterInfo> Adapters, string? Error)> GetNetworkSnapshotAsync()
        {
            return await Task.Run<(List<NetworkAdapterInfo> Adapters, string? Error)>(async () =>
            {
                try
                {
                    // Salt okunur PowerShell sorgusu
                    string psScript =
                        "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " +
                        "$a = Get-NetAdapter | Select-Object ifIndex, Name, InterfaceDescription, Status, LinkSpeed, MacAddress, MediaType, InterfaceType, Virtual; " +
                        "$ip = Get-NetIPAddress | Where-Object { $_.AddressFamily -eq 'IPv4' -or $_.AddressFamily -eq 'IPv6' } | Select-Object InterfaceIndex, AddressFamily, IPAddress, PrefixLength, PrefixOrigin; " +
                        "$gw = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Select-Object InterfaceIndex, NextHop, RouteMetric; " +
                        "$dns = Get-DnsClientServerAddress | Select-Object InterfaceIndex, AddressFamily, ServerAddresses; " +
                        "$dnsAuto = Get-NetAdapter | ForEach-Object { $g = $_.InterfaceGuid.ToString() -replace '[{}]',''; $v = (Get-ItemProperty -Path ('HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\{' + $g + '}') -Name NameServer -ErrorAction SilentlyContinue).NameServer; [pscustomobject]@{ ifIndex = $_.ifIndex; automatic = [string]::IsNullOrWhiteSpace($v) } }; " +
                        "[pscustomobject]@{ adapters=@($a); ips=@($ip); gateways=@($gw); dns=@($dns); dnsAuto=@($dnsAuto) } | ConvertTo-Json -Depth 6 -Compress";

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{psScript}\"",
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

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ScriptTimeoutSeconds));
                    var processExitTask = process.WaitForExitAsync(cts.Token);

                    if (await Task.WhenAny(processExitTask, Task.Delay(TimeSpan.FromSeconds(ScriptTimeoutSeconds))) != processExitTask)
                    {
                        try { process.Kill(); } catch { }
                        return (new List<NetworkAdapterInfo>(), "Ağ bilgileri sorgusu zaman aşımına uğradı (20 sn).");
                    }

                    string output = (await outputTask).Trim();
                    string stderr = (await errorTask).Trim();

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        return (new List<NetworkAdapterInfo>(), !string.IsNullOrEmpty(stderr) ? stderr : "Ağ bilgisi çıktısı alınamadı.");
                    }

                    var adapters = ParseNetworkSnapshotJson(output);
                    return (adapters, null);
                }
                catch (Exception ex)
                {
                    return (new List<NetworkAdapterInfo>(), $"Ağ bilgisi alınırken hata oluştu: {ex.Message}");
                }
            });
        }

        private static List<NetworkAdapterInfo> ParseNetworkSnapshotJson(string jsonText)
        {
            var result = new List<NetworkAdapterInfo>();

            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;

                // 1. Adaptörleri ayrıştır
                var rawAdapters = GetElementList(root, "adapters");
                var adapterMap = new Dictionary<int, NetworkAdapterInfo>();

                foreach (var a in rawAdapters)
                {
                    int ifIndex = GetIntProperty(a, "ifIndex");
                    if (ifIndex <= 0) continue;

                    var info = new NetworkAdapterInfo
                    {
                        IfIndex = ifIndex,
                        Name = GetStringProperty(a, "Name"),
                        Description = GetStringProperty(a, "InterfaceDescription"),
                        Status = GetStringProperty(a, "Status"),
                        MediaType = GetStringProperty(a, "MediaType"),
                        InterfaceType = GetStringProperty(a, "InterfaceType"),
                        LinkSpeed = GetStringProperty(a, "LinkSpeed"),
                        MacAddress = GetStringProperty(a, "MacAddress"),
                        IsVirtual = GetBoolProperty(a, "Virtual")
                    };

                    adapterMap[ifIndex] = info;
                    result.Add(info);
                }

                // 2. IP Adreslerini ayrıştır ve InterfaceIndex ile eşle
                var rawIps = GetElementList(root, "ips");
                foreach (var ipElem in rawIps)
                {
                    int ifIndex = GetIntProperty(ipElem, "InterfaceIndex");
                    if (!adapterMap.TryGetValue(ifIndex, out var adapter)) continue;

                    string ipAddress = GetStringProperty(ipElem, "IPAddress");
                    if (string.IsNullOrWhiteSpace(ipAddress)) continue;

                    int prefix = GetIntProperty(ipElem, "PrefixLength");
                    string ipWithPrefix = prefix > 0 ? $"{ipAddress}/{prefix}" : ipAddress;

                    string familyStr = GetStringProperty(ipElem, "AddressFamily");
                    bool isV4 = familyStr.Equals("IPv4", StringComparison.OrdinalIgnoreCase) || familyStr == "2" || ipAddress.Contains('.');

                    if (isV4)
                    {
                        if (!adapter.Ipv4Addresses.Contains(ipWithPrefix))
                            adapter.Ipv4Addresses.Add(ipWithPrefix);
                    }
                    else
                    {
                        if (!adapter.Ipv6Addresses.Contains(ipWithPrefix))
                            adapter.Ipv6Addresses.Add(ipWithPrefix);
                    }
                }

                // 3. Ağ Geçitlerini (Gateways) ayrıştır - en düşük RouteMetric'e sahip olanı seç
                var rawGateways = GetElementList(root, "gateways");
                var bestGatewayMap = new Dictionary<int, (string NextHop, int Metric)>();

                foreach (var gwElem in rawGateways)
                {
                    int ifIndex = GetIntProperty(gwElem, "InterfaceIndex");
                    string nextHop = GetStringProperty(gwElem, "NextHop");
                    int metric = GetIntProperty(gwElem, "RouteMetric", defaultValue: 99999);

                    if (string.IsNullOrWhiteSpace(nextHop) || nextHop == "0.0.0.0") continue;

                    if (!bestGatewayMap.TryGetValue(ifIndex, out var current) || metric < current.Metric)
                    {
                        bestGatewayMap[ifIndex] = (nextHop, metric);
                    }
                }

                foreach (var kvp in bestGatewayMap)
                {
                    if (adapterMap.TryGetValue(kvp.Key, out var adapter))
                    {
                        adapter.GatewayV4 = kvp.Value.NextHop;
                    }
                }

                // 3.5 IPv4 DNS'in otomatik (DHCP) olup olmadığını ayrıştır (registry NameServer)
                var dnsAutomaticMap = new Dictionary<int, bool>();
                foreach (var autoElem in GetElementList(root, "dnsAuto"))
                {
                    int autoIndex = GetIntProperty(autoElem, "ifIndex");
                    if (autoIndex > 0)
                        dnsAutomaticMap[autoIndex] = GetBoolProperty(autoElem, "automatic");
                }

                // 4. DNS Sunucularını ayrıştır
                var rawDns = GetElementList(root, "dns");
                foreach (var dnsElem in rawDns)
                {
                    int ifIndex = GetIntProperty(dnsElem, "InterfaceIndex");
                    if (!adapterMap.TryGetValue(ifIndex, out var adapter)) continue;

                    string familyStr = GetStringProperty(dnsElem, "AddressFamily");
                    bool isV4 = familyStr.Equals("IPv4", StringComparison.OrdinalIgnoreCase) || familyStr == "2";
                    bool isV6 = familyStr.Equals("IPv6", StringComparison.OrdinalIgnoreCase) || familyStr == "23";

                    var serverList = GetStringArrayProperty(dnsElem, "ServerAddresses");

                    if (isV4)
                    {
                        bool isAutomatic = dnsAutomaticMap.TryGetValue(ifIndex, out bool autoFlag)
                            ? autoFlag
                            : serverList.Count == 0;

                        adapter.DnsIsAutomaticV4 = isAutomatic;

                        // Otomatik (DHCP) modda Get-DnsClientServerAddress, DHCP'den öğrenilen sunucuları
                        // döndürebilir; kartta "Otomatik (DHCP)" gösterilmesi için liste boş bırakılır.
                        if (!isAutomatic)
                        {
                            foreach (var s in serverList)
                            {
                                if (!string.IsNullOrWhiteSpace(s) && !adapter.DnsServersV4.Contains(s))
                                    adapter.DnsServersV4.Add(s);
                            }
                        }
                    }
                    else if (isV6)
                    {
                        foreach (var s in serverList)
                        {
                            if (!string.IsNullOrWhiteSpace(s) && !adapter.DnsServersV6.Contains(s))
                                adapter.DnsServersV6.Add(s);
                        }
                        adapter.DnsIsAutomaticV6 = adapter.DnsServersV6.Count == 0;
                    }
                }

                // Sıralama: Önce Up (Bağlı) olanlar, sonra InterfaceIndex
                result = result.OrderByDescending(a => a.IsUp).ThenBy(a => a.IfIndex).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkInfoService] JSON parse hatası: {ex.Message}");
            }

            return result;
        }

        #region JSON Helper Metotları
        private static List<JsonElement> GetElementList(JsonElement parent, string propName)
        {
            var list = new List<JsonElement>();
            if (!parent.TryGetProperty(propName, out var element)) return list;

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(item);
                }
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                list.Add(element);
            }
            return list;
        }

        private static string GetStringProperty(JsonElement element, string propName)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String)
                    return prop.GetString() ?? string.Empty;
                if (prop.ValueKind == JsonValueKind.Number)
                    return prop.GetRawText();
                if (prop.ValueKind == JsonValueKind.True) return "True";
                if (prop.ValueKind == JsonValueKind.False) return "False";
            }
            return string.Empty;
        }

        private static int GetIntProperty(JsonElement element, string propName, int defaultValue = 0)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out int val))
                    return val;
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out int parsed))
                    return parsed;
            }
            return defaultValue;
        }

        private static bool GetBoolProperty(JsonElement element, string propName)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
                if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out bool b))
                    return b;
            }
            return false;
        }

        private static List<string> GetStringArrayProperty(JsonElement element, string propName)
        {
            var list = new List<string>();
            if (!element.TryGetProperty(propName, out var prop)) return list;

            if (prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    string s = item.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
                }
            }
            else if (prop.ValueKind == JsonValueKind.String)
            {
                string s = prop.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
            }

            return list;
        }
        #endregion

        /// <summary>
        /// DNS İstemci önbelleğini temizler (Clear-DnsClientCache).
        /// </summary>
        public static async Task<(bool Success, string Message)> ClearDnsCacheAsync()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Clear-DnsClientCache\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using var process = new Process { StartInfo = psi };
                    process.Start();

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var exitTask = process.WaitForExitAsync(cts.Token);

                    if (await Task.WhenAny(exitTask, Task.Delay(10000)) != exitTask)
                    {
                        try { process.Kill(); } catch { }
                        return (false, "DNS önbelleği temizleme işlemi zaman aşımına uğradı.");
                    }

                    if (process.ExitCode == 0)
                    {
                        return (true, "DNS önbelleği başarıyla temizlendi ✓");
                    }
                    else
                    {
                        string err = (await process.StandardError.ReadToEndAsync()).Trim();
                        return (false, !string.IsNullOrEmpty(err) ? $"Hata: {err}" : "DNS önbelleği temizlenemedi.");
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"İşlem sırasında hata oluştu: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Ağ geçitleri ve genel DNS sunucularına gecikme (ping) testi yapar.
        /// </summary>
        public static async Task<List<NetworkLatencyItem>> RunLatencyTestAsync(List<NetworkAdapterInfo> adapters)
        {
            return await Task.Run(async () =>
            {
                var targetList = new List<(string Target, string Label)>();

                // 1. Aktif adaptörlerin IPv4 ağ geçitleri
                foreach (var adapter in adapters)
                {
                    if (adapter.IsUp && !string.IsNullOrWhiteSpace(adapter.GatewayV4) && adapter.GatewayV4 != "0.0.0.0")
                    {
                        string gw = adapter.GatewayV4.Trim();
                        if (!targetList.Any(t => t.Target.Equals(gw, StringComparison.OrdinalIgnoreCase)))
                        {
                            targetList.Add((gw, $"Ağ Geçidi ({adapter.Name} - {gw})"));
                        }
                    }
                }

                // 2. 1.1.1.1 (Cloudflare) & 8.8.8.8 (Google)
                if (!targetList.Any(t => t.Target == "1.1.1.1"))
                    targetList.Add(("1.1.1.1", "Cloudflare DNS (1.1.1.1)"));

                if (!targetList.Any(t => t.Target == "8.8.8.8"))
                    targetList.Add(("8.8.8.8", "Google DNS (8.8.8.8)"));

                var results = new List<NetworkLatencyItem>();

                foreach (var item in targetList)
                {
                    using var ping = new Ping();
                    int attempts = 3;
                    int successCount = 0;
                    long totalRtt = 0;

                    for (int i = 0; i < attempts; i++)
                    {
                        try
                        {
                            var reply = await ping.SendPingAsync(item.Target, 1500);
                            if (reply.Status == IPStatus.Success)
                            {
                                successCount++;
                                totalRtt += reply.RoundtripTime;
                            }
                        }
                        catch
                        {
                            // Timeout veya hata
                        }
                    }

                    int lossPercent = (int)Math.Round((attempts - successCount) / (double)attempts * 100);
                    long avgMs = successCount > 0 ? (totalRtt / successCount) : -1;

                    results.Add(new NetworkLatencyItem
                    {
                        Target = item.Target,
                        TargetLabel = item.Label,
                        LossPercent = lossPercent,
                        AverageMs = avgMs
                    });
                }

                return results;
            });
        }
    }
}
