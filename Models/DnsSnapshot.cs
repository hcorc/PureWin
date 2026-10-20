using System;
using System.Collections.Generic;

namespace PureWin.Models
{
    public class DnsSnapshot
    {
        public int IfIndex { get; set; }
        public string AdapterName { get; set; } = string.Empty;
        public string AdapterDescription { get; set; } = string.Empty;
        public DateTime SavedAtUtc { get; set; }
        public List<string> Ipv4Servers { get; set; } = new(); // boş liste = otomatik (DHCP)
        public bool Ipv4WasAutomatic { get; set; }
    }
}
