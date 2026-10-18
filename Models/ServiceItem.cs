using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PureWin.Models
{
    public class ServiceItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isCurrentDisabled;
        private string _statusText = "Bilinmiyor";
        private string _statusColor = "#64748B";

        public string ServiceName { get; set; } = string.Empty;
        public string? FallbackServiceName { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? WarningText { get; set; }
        public bool IsSafe { get; set; } = true;
        public string Category { get; set; } = "Genel";
        public int OriginalStartupType { get; set; } = 3; // 2: Auto, 3: Manual, 4: Disabled

        private bool _isRunning;
        private bool _isMissingOnSystem;

        /// <summary>
        /// Sistemde şu an gerçekten devre dışı mı?
        /// </summary>
        public bool IsCurrentDisabled
        {
            get => _isCurrentDisabled;
            set
            {
                if (_isCurrentDisabled != value)
                {
                    _isCurrentDisabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanStartOrEnable));
                    OnPropertyChanged(nameof(ActionButtonText));
                }
            }
        }

        /// <summary>
        /// Servis şu an sistemde çalışıyor mu?
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanStartOrEnable));
                    OnPropertyChanged(nameof(ActionButtonText));
                }
            }
        }

        /// <summary>
        /// Servis sistemde mevcut/kurulu değil mi?
        /// </summary>
        public bool IsMissingOnSystem
        {
            get => _isMissingOnSystem;
            set
            {
                if (_isMissingOnSystem != value)
                {
                    _isMissingOnSystem = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Servis çalışmıyorsa (Devre Dışı veya Durduruldu ise) kullanıcı tek tıkla açabilsin/başlatabilsin.
        /// </summary>
        public bool CanStartOrEnable => !IsRunning;

        /// <summary>
        /// Buton üzerindeki akıllı yazı: Devre dışı ise "Geri Aç", Durdurulmuş ise "Başlat"
        /// </summary>
        public string ActionButtonText => IsCurrentDisabled ? "🔄 Geri Aç" : "⚡ Başlat";

        /// <summary>
        /// Kullanıcının devre dışı bırakmak için seçip seçmediği (Checkbox)
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsPendingDisabled));
                    OnPropertyChanged(nameof(IsStaged));
                }
            }
        }

        /// <summary>
        /// Geriye dönük uyumluluk için alias
        /// </summary>
        public bool IsPendingDisabled
        {
            get => _isSelected;
            set => IsSelected = value;
        }

        public bool IsStaged
        {
            get => _isSelected;
            set => IsSelected = value;
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusColor
        {
            get => _statusColor;
            set
            {
                if (_statusColor != value)
                {
                    _statusColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
