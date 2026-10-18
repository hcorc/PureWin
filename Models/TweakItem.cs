using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PureWin.Models
{
    public class DropdownOption
    {
        public string Label { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string RegFile { get; set; } = string.Empty;

        public override string ToString() => Label;
    }

    public class TweakItem : INotifyPropertyChanged
    {
        private bool _isApplied;
        private bool _isPendingChecked;
        private bool _isStaged;
        private DropdownOption? _selectedOption;
        private DropdownOption? _appliedOption;

        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "Diğer";
        public bool IsRecommended { get; set; } = false;

        // Toggle / Checkbox özellikleri
        public bool IsDropdown { get; set; } = false;
        public bool IsScheduledTask { get; set; } = false;
        public string ScheduledTaskType { get; set; } = string.Empty; // "Core" or "Optional"
        public string ApplyRegFile { get; set; } = string.Empty;
        public string UndoRegFile { get; set; } = string.Empty;

        // Yeniden başlatma gereksinimi takibi
        public bool RequiresReboot { get; set; } = false;
        public bool RequiresExplorerRestart { get; set; } = false;

        // Opsiyonel Hızlı Eylem Butonu (Örn: Masaüstü Simge Ayarlarını Aç)
        public string? ActionButtonText { get; set; }
        public string? ActionButtonToolTip { get; set; }
        public bool HasActionButton => !string.IsNullOrEmpty(ActionButtonText);

        // Dropdown özellikleri
        public List<DropdownOption> DropdownOptions { get; set; } = new();

        public DropdownOption? AppliedOption
        {
            get => _appliedOption;
            set
            {
                _appliedOption = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsVerified));
                if (!IsStaged)
                {
                    _selectedOption = value;
                    OnPropertyChanged(nameof(SelectedOption));
                }
            }
        }

        public DropdownOption? SelectedOption
        {
            get => _selectedOption;
            set
            {
                if (_selectedOption != value)
                {
                    _selectedOption = value;
                    OnPropertyChanged();
                    IsStaged = (_selectedOption != _appliedOption);
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
                    OnPropertyChanged(nameof(IsVerified));
                    if (!IsStaged)
                    {
                        _isPendingChecked = value;
                        OnPropertyChanged(nameof(IsPendingChecked));
                    }
                }
            }
        }

        public bool IsVerified
        {
            get
            {
                if (IsDropdown)
                {
                    return AppliedOption != null && !string.IsNullOrEmpty(AppliedOption.RegFile);
                }
                return IsApplied;
            }
        }

        public string VerificationTooltipText
        {
            get
            {
                if (IsScheduledTask)
                    return "✓ Bu ayar Windows Görev Zamanlayıcı'da canlı olarak kontrol edildi ve doğrulandı.";
                if (Id == "EnableWindowsSandbox" || Id == "EnableWindowsSubsystemForLinux")
                    return "✓ Bu özellik Windows sistem servislerinde canlı olarak kontrol edildi ve doğrulandı.";
                if (Id == "DisableMouseAcceleration")
                    return "✓ Fare ivmesi Windows User32 oturum belleğinde ve sürücüsünde canlı sorgulandı: 1:1 ham fare tepkisi devrede.";
                if (Id == "DisableStickyKeys")
                    return "✓ Yapışkan Tuşlar Windows erişilebilirlik alt sisteminde canlı sorgulandı: 5x Shift kısayolu devredışı.";
                if (Id == "DisableAnimations")
                    return "✓ Animasyon efektleri Windows User32 grafik alt sisteminde canlı sorgulandı ve kapatıldığı doğrulandı.";
                if (Id == "ReduceMenuDelay")
                    return "✓ Menü gecikmesi Windows User32 alt sisteminde canlı sorgulandı ve sıfırlandığı doğrulandı.";
                return "✓ Bu ayar Windows kayıt defterinde canlı olarak kontrol edildi ve doğrulandı.";
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
                    IsStaged = (_isPendingChecked != _isApplied);
                }
            }
        }

        public bool IsStaged
        {
            get => _isStaged;
            set
            {
                if (_isStaged != value)
                {
                    _isStaged = value;
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

    public class TweakCategoryGroup
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public List<TweakItem> Items { get; set; } = new();
    }
}
