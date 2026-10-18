using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace PureWin.Models
{
    public enum SoftwareUpdateState
    {
        Ready,
        Updating,
        Completed,
        Failed
    }

    public class SoftwareUpdateItem : INotifyPropertyChanged
    {
        private SoftwareUpdateState _state = SoftwareUpdateState.Ready;
        private string _statusMessage = "Güncelle";
        private string _errorMessage = string.Empty;
        private int _progressPercent = 0;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string AvailableVersion { get; set; } = string.Empty;
        public string Source { get; set; } = "winget";
        public string Publisher { get; set; } = string.Empty;

        public ImageSource? IconSource { get; set; }
        public string FallbackIcon { get; set; } = "📦";
        public bool HasIconSource => IconSource != null;

        public string DisplaySubInfo
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Publisher))
                    return $"Yayıncı: {Publisher} • winget: {Id}";
                return $"winget: {Id}";
            }
        }

        public SoftwareUpdateState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsReady));
                    OnPropertyChanged(nameof(IsUpdating));
                    OnPropertyChanged(nameof(IsCompleted));
                    OnPropertyChanged(nameof(IsFailed));
                }
            }
        }

        public bool IsReady => State == SoftwareUpdateState.Ready;
        public bool IsUpdating => State == SoftwareUpdateState.Updating;
        public bool IsCompleted => State == SoftwareUpdateState.Completed;
        public bool IsFailed => State == SoftwareUpdateState.Failed;

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ProgressPercent
        {
            get => _progressPercent;
            set
            {
                if (_progressPercent != value)
                {
                    _progressPercent = value;
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
