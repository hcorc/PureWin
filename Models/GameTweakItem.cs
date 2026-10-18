using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PureWin.Models
{
    public class GameTweakItem : INotifyPropertyChanged
    {
        private bool _isApplied;
        private bool _isPendingChecked;
        private bool _isSupported = true;
        private string _statusText = "KAPALI";

        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "Önerilen Oyun Ayarları";
        public string Icon { get; set; } = "⚡";
        public string BadgeText { get; set; } = string.Empty;
        public string BadgeType { get; set; } = "recommended"; // "recommended", "advanced", "reboot"
        
        public bool RequiresReboot { get; set; } = false;
        public bool RequiresExplorerRestart { get; set; } = false;

        public bool HasBadge => !string.IsNullOrEmpty(BadgeText);

        public bool IsSupported
        {
            get => _isSupported;
            set
            {
                if (_isSupported != value)
                {
                    _isSupported = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNotSupported));
                }
            }
        }

        public bool IsNotSupported => !_isSupported;

        private string _unsupportedReason = string.Empty;
        public string UnsupportedReason
        {
            get => _unsupportedReason;
            set
            {
                if (_unsupportedReason != value)
                {
                    _unsupportedReason = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsApplied
        {
            get => _isApplied;
            set
            {
                if (_isApplied != value)
                {
                    _isApplied = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStaged));
                    UpdateStatusText();
                }
            }
        }

        public bool IsPendingChecked
        {
            get => _isPendingChecked;
            set
            {
                if (_isPendingChecked != value)
                {
                    _isPendingChecked = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStaged));
                    UpdateStatusText();
                }
            }
        }

        public bool IsStaged => _isPendingChecked != _isApplied;

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateStatusText()
        {
            StatusText = _isPendingChecked ? "AÇIK" : "KAPALI";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
