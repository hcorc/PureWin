using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace PureWin.Models
{
    public enum BloatwareRemovalState
    {
        None,
        Queued,
        Removing,
        Verifying,
        Removed,
        Failed,
        Unverified
    }

    public class BloatwareItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isInstalled = true;
        private BloatwareRemovalState _removalState = BloatwareRemovalState.None;

        public string AppId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "Gereksiz Paketler";
        public bool IsSafe { get; set; } = true;
        public string RemovalMethod { get; set; } = "Appx"; // Appx or WinGet

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled != value)
                {
                    _isInstalled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Yalnızca arayüzde gösterilen kaldırma durumu. Kataloğa veya Apps.json'a yazılmaz.
        /// </summary>
        [JsonIgnore]
        public BloatwareRemovalState RemovalState
        {
            get => _removalState;
            set
            {
                if (_removalState == value) return;
                _removalState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemovalStatusText));
                OnPropertyChanged(nameof(IsRemovalStatusVisible));
            }
        }

        [JsonIgnore]
        public string RemovalStatusText => RemovalState switch
        {
            BloatwareRemovalState.Queued => "• Sırada",
            BloatwareRemovalState.Removing => "⏳ Kaldırılıyor…",
            BloatwareRemovalState.Verifying => "🔎 Doğrulanıyor…",
            BloatwareRemovalState.Removed => "✓ Kaldırıldı",
            BloatwareRemovalState.Failed => "✗ Başarısız",
            BloatwareRemovalState.Unverified => "⚠ Doğrulanamadı",
            _ => string.Empty
        };

        [JsonIgnore]
        public bool IsRemovalStatusVisible => RemovalState != BloatwareRemovalState.None;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
