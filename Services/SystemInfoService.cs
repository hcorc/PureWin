using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PureWin.Services
{
    public class SystemSpecsModel
    {
        // OS & Lisans
        public string OsName { get; set; } = "Windows 11";
        public string OsDisplayVersion { get; set; } = "";
        public string OsBuild { get; set; } = "";
        public string OsArchitecture { get; set; } = "64-bit";
        public bool IsActivated { get; set; } = false;
        public string LicenseStatusText { get; set; } = "Kontrol Ediliyor...";
        public string LicenseChannel { get; set; } = "Bilinmiyor";
        public string LicenseChannelDesc { get; set; } = "";
        public string PartialKey { get; set; } = "";

        // Donanım
        public string MotherboardManufacturer { get; set; } = "";
        public string MotherboardModel { get; set; } = "";
        public string MotherboardFullName => SystemInfoService.GetSanitizedMotherboard(MotherboardManufacturer, MotherboardModel);
        public string BiosVersion { get; set; } = "";
        public string BiosReleaseDate { get; set; } = "";
        public string BiosVendor { get; set; } = "";

        public string GpuName { get; set; } = "";
        public string GpuVram { get; set; } = "";
        public string GpuDriverVersion { get; set; } = "";

        public string CpuName { get; set; } = "";
        public int CpuLogicalCores { get; set; } = 0;
        public string TotalRam { get; set; } = "";
        public string AvailableRam { get; set; } = "";
    }

    public static class SystemInfoService
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        private static SystemSpecsModel? _cachedSpecs;

        /// <summary>
        /// Donanım ve İşletim Sistemi bilgilerini doğrudan Windows Kayıt Defteri'nden ultra hızlı (0 ms) çeker.
        /// </summary>
        public static SystemSpecsModel GetHardwareAndOsSpecs()
        {
            if (_cachedSpecs != null) return _cachedSpecs;

            var model = new SystemSpecsModel();

            try
            {
                // 1. İşletim Sistemi ve Sürüm
                using (var cvKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    if (cvKey != null)
                    {
                        string editionId = cvKey.GetValue("EditionID")?.ToString() ?? "Pro";
                        string productName = cvKey.GetValue("ProductName")?.ToString() ?? "Windows 11";
                        string currentBuildStr = cvKey.GetValue("CurrentBuild")?.ToString() ?? "22000";
                        string ubrStr = cvKey.GetValue("UBR")?.ToString() ?? "0";
                        string displayVer = cvKey.GetValue("DisplayVersion")?.ToString() ?? "";

                        int.TryParse(currentBuildStr, out int buildNum);

                        // Windows 11 kontrolü: Build >= 22000 ise Windows 11'dir (ProductName Windows 10 yazsa dahi)
                        if (buildNum >= 22000)
                        {
                            string editionFriendly = editionId.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) ? "Enterprise" :
                                                     (editionId.Contains("Home", StringComparison.OrdinalIgnoreCase) ? "Home" : "Pro");
                            model.OsName = $"Windows 11 {editionFriendly}";
                        }
                        else
                        {
                            model.OsName = productName;
                        }

                        model.OsDisplayVersion = displayVer;
                        model.OsBuild = $"{currentBuildStr}.{ubrStr}";
                        model.OsArchitecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
                    }
                }

                // 2. Anakart ve BIOS Bilgileri (BIOS Key)
                using (var biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS", false))
                {
                    if (biosKey != null)
                    {
                        model.MotherboardManufacturer = biosKey.GetValue("BaseBoardManufacturer")?.ToString() ?? "Bilinmiyor";
                        model.MotherboardModel = biosKey.GetValue("BaseBoardProduct")?.ToString() ?? "Bilinmiyor";
                        model.BiosVersion = biosKey.GetValue("BIOSVersion")?.ToString() ?? "";
                        model.BiosVendor = biosKey.GetValue("BIOSVendor")?.ToString() ?? "";
                        string rawBiosDate = biosKey.GetValue("BIOSReleaseDate")?.ToString()?.Trim() ?? "";
                        if (DateTime.TryParse(rawBiosDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt) ||
                            DateTime.TryParse(rawBiosDate, out dt))
                        {
                            model.BiosReleaseDate = dt.ToString("dd.MM.yyyy");
                        }
                        else
                        {
                            model.BiosReleaseDate = rawBiosDate;
                        }
                    }
                }

                // 3. İşlemci (CPU)
                using (var cpuKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", false))
                {
                    if (cpuKey != null)
                    {
                        string rawCpu = cpuKey.GetValue("ProcessorNameString")?.ToString() ?? "";
                        model.CpuName = System.Text.RegularExpressions.Regex.Replace(rawCpu, @"\s+", " ").Trim();
                    }
                }
                model.CpuLogicalCores = Environment.ProcessorCount;

                // 4. Ekran Kartı (GPU)
                using (var videoKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", false))
                {
                    if (videoKey != null)
                    {
                        model.GpuName = videoKey.GetValue("DriverDesc")?.ToString() ?? "Bilinmiyor";
                        model.GpuDriverVersion = videoKey.GetValue("DriverVersion")?.ToString() ?? "";

                        object? vramObj = videoKey.GetValue("HardwareInformation.qwMemorySize") ?? videoKey.GetValue("HardwareInformation.MemorySize");
                        if (vramObj != null && long.TryParse(vramObj.ToString(), out long bytes) && bytes > 0)
                        {
                            double gb = (double)bytes / (1024 * 1024 * 1024);
                            model.GpuVram = $"{Math.Round(gb)} GB";
                        }
                    }
                }

                // 5. Bellek (RAM)
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus) && memStatus.ullTotalPhys > 0)
                {
                    double totalGb = (double)memStatus.ullTotalPhys / (1024 * 1024 * 1024);
                    model.TotalRam = $"{Math.Round(totalGb)} GB";

                    if (memStatus.ullAvailPhys > 0)
                    {
                        double availGb = (double)memStatus.ullAvailPhys / (1024 * 1024 * 1024);
                        model.AvailableRam = $"{availGb:F1} GB Kullanılabilir";
                    }
                }
            }
            catch { }

            _cachedSpecs = model;
            return model;
        }

        /// <summary>
        /// Windows Lisans ve Etkinleştirme bilgilerini arka planda asenkron olarak sorgular (Açılış hızını asla etkilemez).
        /// </summary>
        public static async Task RefreshLicenseInfoAsync(SystemSpecsModel model)
        {
            await Task.Run(() =>
            {
                try
                {
                    // 1. Doğrudan PowerShell CIM WQL sorgusu ile temiz JSON oku
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-CimInstance -Query 'SELECT Name, Description, LicenseStatus, PartialProductKey FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL' | Select-Object -First 1 Name, Description, LicenseStatus, PartialProductKey | ConvertTo-Json -Compress\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        string json = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit(4000);

                        if (!string.IsNullOrEmpty(json) && json.StartsWith("{"))
                        {
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;

                            int licenseStatus = root.TryGetProperty("LicenseStatus", out var lsProp) && lsProp.TryGetInt32(out int ls) ? ls : 0;
                            string description = root.TryGetProperty("Description", out var dProp) ? dProp.GetString() ?? "" : "";
                            string partialKey = root.TryGetProperty("PartialProductKey", out var pkProp) ? pkProp.GetString() ?? "" : "";

                            ParseLicenseDetails(model, licenseStatus, description, partialKey);
                            return;
                        }
                    }
                }
                catch { }

                // 2. Fallback: slmgr.vbs /dli çıktısını ayrıştır
                try
                {
                    string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string slmgrPath = Path.Combine(system32, "slmgr.vbs");

                    var psiSlmgr = new ProcessStartInfo
                    {
                        FileName = "cscript.exe",
                        Arguments = $"//nologo \"{slmgrPath}\" /dli",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using var procSlmgr = Process.Start(psiSlmgr);
                    if (procSlmgr != null)
                    {
                        string output = procSlmgr.StandardOutput.ReadToEnd();
                        procSlmgr.WaitForExit(3000);

                        bool isLicensed = output.Contains("Lisanslı", StringComparison.OrdinalIgnoreCase) ||
                                          output.Contains("Licensed", StringComparison.OrdinalIgnoreCase);

                        string partialKey = "";
                        var matchKey = System.Text.RegularExpressions.Regex.Match(output, @"(?:Kısmi Ürün Anahtarı|Partial Product Key):\s*([A-Za-z0-9]+)");
                        if (matchKey.Success)
                        {
                            partialKey = matchKey.Groups[1].Value.Trim();
                        }

                        ParseLicenseDetails(model, isLicensed ? 1 : 0, output, partialKey);
                    }
                }
                catch
                {
                    model.IsActivated = false;
                    model.LicenseStatusText = "Lisans Durumu Alınamadı";
                }
            });
        }

        private static void ParseLicenseDetails(SystemSpecsModel model, int licenseStatus, string description, string partialKey)
        {
            model.PartialKey = !string.IsNullOrEmpty(partialKey) ? partialKey : "";

            if (licenseStatus == 1)
            {
                model.IsActivated = true;
                model.LicenseStatusText = "Kalıcı Olarak Etkinleştirildi";
            }
            else if (licenseStatus == 2)
            {
                model.IsActivated = false;
                model.LicenseStatusText = "Süre Tanındı (Grace Period)";
            }
            else
            {
                model.IsActivated = false;
                model.LicenseStatusText = "Etkinleştirilmemiş";
            }

            // Kanal türü tespiti
            if (description.Contains("RETAIL", StringComparison.OrdinalIgnoreCase))
            {
                model.LicenseChannel = "Perakende (Retail Channel)";
                model.LicenseChannelDesc = "Dijital Lisans / Taşınabilir";
            }
            else if (description.Contains("OEM", StringComparison.OrdinalIgnoreCase))
            {
                model.LicenseChannel = "Cihaz Üreticisi (OEM Channel)";
                model.LicenseChannelDesc = "Anakart Lisansı (OEM:DM)";
            }
            else if (description.Contains("VOLUME_KMS", StringComparison.OrdinalIgnoreCase) || description.Contains("KMS", StringComparison.OrdinalIgnoreCase))
            {
                model.LicenseChannel = "Toplu Lisanslama (Volume:KMS)";
                model.LicenseChannelDesc = "Kurumsal Ağ Lisansı";
            }
            else if (description.Contains("VOLUME_MAK", StringComparison.OrdinalIgnoreCase) || description.Contains("MAK", StringComparison.OrdinalIgnoreCase))
            {
                model.LicenseChannel = "Toplu Lisanslama (Volume:MAK)";
                model.LicenseChannelDesc = "Çoklu Etkinleştirme";
            }
            else
            {
                model.LicenseChannel = model.IsActivated ? "Dijital Lisans" : "Bilinmiyor";
                model.LicenseChannelDesc = "Windows Lisansı";
            }
        }

        public static string GetSanitizedMotherboard(string? rawManufacturer, string? rawModel)
        {
            string mfg = (rawManufacturer ?? "").Trim();
            string model = (rawModel ?? "").Trim();

            if (string.IsNullOrWhiteSpace(mfg) && string.IsNullOrWhiteSpace(model))
                return "Bilinmiyor";

            // 1. Bilinen üreticileri standart temiz marka adına dönüştür
            string cleanMfg = mfg;
            if (mfg.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase) || mfg.Contains("MSI", StringComparison.OrdinalIgnoreCase))
            {
                cleanMfg = "MSI";
            }
            else if (mfg.Contains("ASUSTeK", StringComparison.OrdinalIgnoreCase) || mfg.Contains("ASUS", StringComparison.OrdinalIgnoreCase))
            {
                cleanMfg = "ASUS";
            }
            else if (mfg.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase))
            {
                cleanMfg = "GIGABYTE";
            }
            else if (mfg.Contains("ASRock", StringComparison.OrdinalIgnoreCase))
            {
                cleanMfg = "ASRock";
            }
            else if (mfg.Contains("Hewlett-Packard", StringComparison.OrdinalIgnoreCase) || mfg.Equals("HP", StringComparison.OrdinalIgnoreCase))
            {
                cleanMfg = "HP";
            }
            else if (mfg.Contains("Dell", StringComparison.OrdinalIgnoreCase))
            {
                cleanMfg = "Dell";
            }
            else if (mfg.Contains("Lenovo", StringComparison.OrdinalIgnoreCase))
            {
                cleanMfg = "Lenovo";
            }
            else
            {
                // Genel unvan eklerini temizle (Co., Ltd., INC., Corporation, LLC vb.)
                cleanMfg = System.Text.RegularExpressions.Regex.Replace(cleanMfg, @"(?i)\b(co\.|ltd\.|inc\.|corporation|corp\.|llc|technolog(?:y|ies))\b", "").Trim(' ', ',', '.');
            }

            if (string.IsNullOrWhiteSpace(cleanMfg))
                return string.IsNullOrWhiteSpace(model) ? "Bilinmiyor" : model;

            if (string.IsNullOrWhiteSpace(model))
                return cleanMfg;

            // Model zaten marka adıyla başlıyorsa (örn: "ASUS ROG STRIX...") çift marka yazma
            if (model.StartsWith(cleanMfg, StringComparison.OrdinalIgnoreCase))
            {
                return model;
            }

            return $"{cleanMfg} {model}".Trim();
        }

        public static string FormatSpecsSummary(SystemSpecsModel model)
        {
            return $"=== PUREWIN SİSTEM VE DONANIM KİMLİĞİ ===\n" +
                   $"İşletim Sistemi: {model.OsName} ({model.OsDisplayVersion}) [{model.OsArchitecture}]\n" +
                   $"İs Derlemesi: {model.OsBuild}\n" +
                   $"Etkinleştirme Durumu: {model.LicenseStatusText}\n" +
                   $"Lisans Kanalı: {model.LicenseChannel} ({model.LicenseChannelDesc})\n" +
                   $"Kısmi Ürün Anahtarı: {(string.IsNullOrEmpty(model.PartialKey) ? "Yok" : $"••••• - {model.PartialKey}")}\n\n" +
                   $"Anakart: {model.MotherboardFullName}\n" +
                   $"Ekran Kartı: {model.GpuName} {(string.IsNullOrEmpty(model.GpuVram) ? "" : $"({model.GpuVram})")} [Sürücü: {model.GpuDriverVersion}]\n" +
                   $"İşlemci: {model.CpuName} ({model.CpuLogicalCores} Mantıksal Çekirdek)\n" +
                   $"Bellek: {model.TotalRam} {(string.IsNullOrEmpty(model.AvailableRam) ? "" : $"[{model.AvailableRam}]")}\n" +
                   $"BIOS: {model.BiosVersion} {(string.IsNullOrEmpty(model.BiosReleaseDate) ? "" : $"({model.BiosReleaseDate})")}\n" +
                   $"========================================";
        }
    }
}
