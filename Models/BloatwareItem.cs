using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PureWin.Models
{
    public class BloatwareItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isInstalled = true;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
