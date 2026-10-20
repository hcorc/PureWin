using System.Windows.Media;

namespace PureWin.Models
{
    public class TweakAuditRow
    {
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public Brush StatusForeground { get; set; } = Brushes.Gray;
        public Brush StatusBackground { get; set; } = Brushes.Transparent;
        public Brush StatusBorder { get; set; } = Brushes.Gray;
        public int SortOrder { get; set; }
    }
}
