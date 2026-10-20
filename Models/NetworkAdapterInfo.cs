using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PureWin.Models
{
    public class NetworkAdapterInfo
    {
        public int IfIndex { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsUp => string.Equals(Status, "Up", StringComparison.OrdinalIgnoreCase);
        public string MediaType { get; set; } = string.Empty;
        public string InterfaceType { get; set; } = string.Empty;
        public bool IsVirtual { get; set; }
        public string LinkSpeed { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public List<string> Ipv4Addresses { get; set; } = new();
        public List<string> Ipv6Addresses { get; set; } = new();
        public string? GatewayV4 { get; set; }
        public List<string> DnsServersV4 { get; set; } = new();
        public List<string> DnsServersV6 { get; set; } = new();
        public bool DnsIsAutomaticV4 { get; set; }
        public bool DnsIsAutomaticV6 { get; set; }

        public bool HasSnapshot { get; set; }
        public Visibility RestoreButtonVisibility => HasSnapshot ? Visibility.Visible : Visibility.Collapsed;

        // UI Bağlayıcı Yardımcıları (Read-only helpers)
        public string TypeIcon
        {
            get
            {
                if (IsVirtual) return "🔌";
                if (!string.IsNullOrEmpty(MediaType) && MediaType.Contains("802.11", StringComparison.OrdinalIgnoreCase)) return "📶";
                if (!string.IsNullOrEmpty(InterfaceType) && InterfaceType.Contains("Wireless", StringComparison.OrdinalIgnoreCase)) return "📶";
                if (!string.IsNullOrEmpty(Name) && (Name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) || Name.Contains("WLAN", StringComparison.OrdinalIgnoreCase))) return "📶";
                return "🖧";
            }
        }

        public string StatusDisplayText
        {
            get
            {
                if (string.Equals(Status, "Up", StringComparison.OrdinalIgnoreCase)) return "Bağlı";
                if (string.Equals(Status, "Disconnected", StringComparison.OrdinalIgnoreCase)) return "Bağlantı Kesildi";
                if (string.Equals(Status, "Disabled", StringComparison.OrdinalIgnoreCase)) return "Devre Dışı";
                return string.IsNullOrWhiteSpace(Status) ? "Bilinmiyor" : Status;
            }
        }

        public Brush StatusBadgeBackground
        {
            get
            {
                if (string.Equals(Status, "Up", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(Color.FromArgb(0x1A, 0x10, 0xB9, 0x81)); // 10% #10B981
                if (string.Equals(Status, "Disconnected", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(Color.FromArgb(0x1A, 0xF5, 0x9E, 0x0B)); // 10% #F59E0B
                if (string.Equals(Status, "Disabled", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(Color.FromArgb(0x1A, 0xEF, 0x44, 0x44)); // 10% #EF4444
                return new SolidColorBrush(Color.FromArgb(0x1A, 0x64, 0x74, 0x8B));
            }
        }

        public Brush StatusBadgeBorder
        {
            get
            {
                if (string.Equals(Status, "Up", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(Color.FromArgb(0x33, 0x10, 0xB9, 0x81));
                if (string.Equals(Status, "Disconnected", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(Color.FromArgb(0x33, 0xF5, 0x9E, 0x0B));
                if (string.Equals(Status, "Disabled", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(Color.FromArgb(0x33, 0xEF, 0x44, 0x44));
                return new SolidColorBrush(Color.FromArgb(0x33, 0x64, 0x74, 0x8B));
            }
        }

        public Brush StatusBadgeForeground
        {
            get
            {
                if (string.Equals(Status, "Up", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                if (string.Equals(Status, "Disconnected", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                if (string.Equals(Status, "Disabled", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                return new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            }
        }

        public string Ipv4DisplayText => Ipv4Addresses.Count > 0 ? string.Join(", ", Ipv4Addresses) : "Yok";
        public string Ipv6DisplayText => Ipv6Addresses.Count > 0 ? string.Join(", ", Ipv6Addresses) : "Yok";
        public string GatewayDisplayText => !string.IsNullOrWhiteSpace(GatewayV4) ? GatewayV4 : "Yok";

        public string DnsV4DisplayText => DnsIsAutomaticV4 || DnsServersV4.Count == 0
            ? "Otomatik (DHCP)"
            : string.Join(", ", DnsServersV4);

        public string DnsV6DisplayText => DnsServersV6.Count > 0
            ? string.Join(", ", DnsServersV6)
            : "Otomatik (DHCP)";
    }

    public class NetworkLatencyItem
    {
        public string Target { get; set; } = string.Empty;
        public string TargetLabel { get; set; } = string.Empty;
        public int LossPercent { get; set; }
        public long AverageMs { get; set; }
        public string DisplayMs => AverageMs >= 0 ? $"{AverageMs} ms" : "Zaman Aşımı";
        public string DisplayLoss => $"%{LossPercent}";

        public Brush QualityBadgeBackground
        {
            get
            {
                if (AverageMs < 0 || LossPercent == 100)
                    return new SolidColorBrush(Color.FromArgb(0x1A, 0xEF, 0x44, 0x44));
                if (AverageMs < 50)
                    return new SolidColorBrush(Color.FromArgb(0x1A, 0x10, 0xB9, 0x81));
                if (AverageMs < 120)
                    return new SolidColorBrush(Color.FromArgb(0x1A, 0xF5, 0x9E, 0x0B));
                return new SolidColorBrush(Color.FromArgb(0x1A, 0xEF, 0x44, 0x44));
            }
        }

        public Brush QualityBadgeBorder
        {
            get
            {
                if (AverageMs < 0 || LossPercent == 100)
                    return new SolidColorBrush(Color.FromArgb(0x33, 0xEF, 0x44, 0x44));
                if (AverageMs < 50)
                    return new SolidColorBrush(Color.FromArgb(0x33, 0x10, 0xB9, 0x81));
                if (AverageMs < 120)
                    return new SolidColorBrush(Color.FromArgb(0x33, 0xF5, 0x9E, 0x0B));
                return new SolidColorBrush(Color.FromArgb(0x33, 0xEF, 0x44, 0x44));
            }
        }

        public Brush QualityBadgeForeground
        {
            get
            {
                if (AverageMs < 0 || LossPercent == 100)
                    return new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                if (AverageMs < 50)
                    return new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                if (AverageMs < 120)
                    return new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                return new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            }
        }
    }
}
