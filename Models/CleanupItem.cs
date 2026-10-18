using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace PureWin.Models
{
    public class CleanupSubItem : INotifyPropertyChanged
    {
        private static readonly SolidColorBrush GreenBrush = CreateFrozenBrush("#10B981");
        private static readonly SolidColorBrush GreenBgBrush = CreateFrozenBrush("#1A10B981");
        private static readonly SolidColorBrush GreenBorderBrush = CreateFrozenBrush("#3310B981");

        private static readonly SolidColorBrush BlueBrush = CreateFrozenBrush("#3B82F6");
        private static readonly SolidColorBrush BlueBgBrush = CreateFrozenBrush("#1A3B82F6");
        private static readonly SolidColorBrush BlueBorderBrush = CreateFrozenBrush("#333B82F6");

        private static readonly SolidColorBrush OrangeBrush = CreateFrozenBrush("#F59E0B");
        private static readonly SolidColorBrush OrangeBgBrush = CreateFrozenBrush("#1AF59E0B");
        private static readonly SolidColorBrush OrangeBorderBrush = CreateFrozenBrush("#33F59E0B");

        private static readonly SolidColorBrush GrayBrush = CreateFrozenBrush("#94A3B8");
        private static readonly SolidColorBrush GrayBgBrush = CreateFrozenBrush("#1A94A3B8");
        private static readonly SolidColorBrush GrayBorderBrush = CreateFrozenBrush("#3394A3B8");

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private bool _isSelected = true;
        private long _sizeBytes = 0;
        private string _displaySize = "Taranmadı";
        private string _status = "Bekliyor";
        private SolidColorBrush _statusForeground = GrayBrush;
        private SolidColorBrush _statusBackground = GrayBgBrush;
        private SolidColorBrush _statusBorder = GrayBorderBrush;

        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CleanupGroupItem? ParentGroup { get; set; }

        private ImageSource? _iconImage;
        public ImageSource? IconImage
        {
            get => _iconImage;
            set
            {
                if (_iconImage != value)
                {
                    _iconImage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasIconImage));
                }
            }
        }

        public bool HasIconImage => _iconImage != null;
        private bool _isRisky = false;

        public bool IsRisky
        {
            get => _isRisky;
            set
            {
                if (_isRisky != value)
                {
                    _isRisky = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RiskyBadgeVisibility));
                }
            }
        }

        public Visibility RiskyBadgeVisibility => _isRisky ? Visibility.Visible : Visibility.Collapsed;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    ParentGroup?.OnSubItemSelectionChanged();
                }
            }
        }

        public long SizeBytes
        {
            get => _sizeBytes;
            set
            {
                _sizeBytes = value;
                OnPropertyChanged();
                UpdateDisplaySize();
                ParentGroup?.OnSubItemSizeChanged();
            }
        }

        public string DisplaySize
        {
            get => _displaySize;
            set
            {
                if (_displaySize != value)
                {
                    _displaySize = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public SolidColorBrush StatusForeground
        {
            get => _statusForeground;
            set
            {
                if (_statusForeground != value)
                {
                    _statusForeground = value;
                    OnPropertyChanged();
                }
            }
        }

        public SolidColorBrush StatusBackground
        {
            get => _statusBackground;
            set
            {
                if (_statusBackground != value)
                {
                    _statusBackground = value;
                    OnPropertyChanged();
                }
            }
        }

        public SolidColorBrush StatusBorder
        {
            get => _statusBorder;
            set
            {
                if (_statusBorder != value)
                {
                    _statusBorder = value;
                    OnPropertyChanged();
                }
            }
        }

        public void SetStatus(string text, string colorKey)
        {
            Status = text;
            switch (colorKey.ToLowerInvariant())
            {
                case "green":
                case "clean":
                case "success":
                    StatusForeground = GreenBrush;
                    StatusBackground = GreenBgBrush;
                    StatusBorder = GreenBorderBrush;
                    break;
                case "blue":
                case "info":
                case "scanning":
                    StatusForeground = BlueBrush;
                    StatusBackground = BlueBgBrush;
                    StatusBorder = BlueBorderBrush;
                    break;
                case "orange":
                case "amber":
                case "warning":
                    StatusForeground = OrangeBrush;
                    StatusBackground = OrangeBgBrush;
                    StatusBorder = OrangeBorderBrush;
                    break;
                default:
                    StatusForeground = GrayBrush;
                    StatusBackground = GrayBgBrush;
                    StatusBorder = GrayBorderBrush;
                    break;
            }
        }

        public void UpdateDisplaySize()
        {
            DisplaySize = FormatBytes(_sizeBytes);
            if (_sizeBytes == 0)
            {
                SetStatus("✓ Temiz", "green");
            }
            else
            {
                SetStatus("Hazır", "orange");
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CleanupGroupItem : INotifyPropertyChanged
    {
        private static readonly SolidColorBrush GreenBrush = CreateFrozenBrush("#10B981");
        private static readonly SolidColorBrush GreenBgBrush = CreateFrozenBrush("#1A10B981");
        private static readonly SolidColorBrush GreenBorderBrush = CreateFrozenBrush("#3310B981");

        private static readonly SolidColorBrush BlueBrush = CreateFrozenBrush("#3B82F6");
        private static readonly SolidColorBrush BlueBgBrush = CreateFrozenBrush("#1A3B82F6");
        private static readonly SolidColorBrush BlueBorderBrush = CreateFrozenBrush("#333B82F6");

        private static readonly SolidColorBrush OrangeBrush = CreateFrozenBrush("#F59E0B");
        private static readonly SolidColorBrush OrangeBgBrush = CreateFrozenBrush("#1AF59E0B");
        private static readonly SolidColorBrush OrangeBorderBrush = CreateFrozenBrush("#33F59E0B");

        private static readonly SolidColorBrush GrayBrush = CreateFrozenBrush("#94A3B8");
        private static readonly SolidColorBrush GrayBgBrush = CreateFrozenBrush("#1A94A3B8");
        private static readonly SolidColorBrush GrayBorderBrush = CreateFrozenBrush("#3394A3B8");

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private bool? _isSelected = true;
        private bool _isExpanded = false; // Kullanıcı kuralı 1: Varsayılan kapalı!
        private bool _isUpdatingInternally = false;
        private long _totalSizeBytes = 0;
        private string _displayTotalSize = "Taranmadı";
        private string _status = "Bekliyor";
        private SolidColorBrush _statusForeground = GrayBrush;
        private SolidColorBrush _statusBackground = GrayBgBrush;
        private SolidColorBrush _statusBorder = GrayBorderBrush;

        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = "📁";
        public ObservableCollection<CleanupSubItem> Items { get; set; } = new();

        public Action? OnGroupSelectionChangedCallback { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ChevronIcon));
                }
            }
        }

        public string ChevronIcon => _isExpanded ? "▼" : "▶";

        public bool? IsSelected
        {
            get => _isSelected;
            set
            {
                bool? target = value;
                if (!_isUpdatingInternally && !value.HasValue)
                {
                    target = false;
                }

                if (_isSelected != target)
                {
                    _isSelected = target;
                    OnPropertyChanged();

                    if (!_isUpdatingInternally && target.HasValue)
                    {
                        _isUpdatingInternally = true;
                        foreach (var item in Items)
                        {
                            item.IsSelected = target.Value;
                        }
                        _isUpdatingInternally = false;
                        OnGroupSelectionChangedCallback?.Invoke();
                    }
                }
            }
        }

        public long TotalSizeBytes
        {
            get => _totalSizeBytes;
            set
            {
                _totalSizeBytes = value;
                OnPropertyChanged();
                DisplayTotalSize = CleanupSubItem.FormatBytes(_totalSizeBytes);
                UpdateGroupStatus();
            }
        }

        public string DisplayTotalSize
        {
            get => _displayTotalSize;
            set
            {
                if (_displayTotalSize != value)
                {
                    _displayTotalSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public SolidColorBrush StatusForeground
        {
            get => _statusForeground;
            set
            {
                if (_statusForeground != value)
                {
                    _statusForeground = value;
                    OnPropertyChanged();
                }
            }
        }

        public SolidColorBrush StatusBackground
        {
            get => _statusBackground;
            set
            {
                if (_statusBackground != value)
                {
                    _statusBackground = value;
                    OnPropertyChanged();
                }
            }
        }

        public SolidColorBrush StatusBorder
        {
            get => _statusBorder;
            set
            {
                if (_statusBorder != value)
                {
                    _statusBorder = value;
                    OnPropertyChanged();
                }
            }
        }

        public void SetStatus(string text, string colorKey)
        {
            Status = text;
            switch (colorKey.ToLowerInvariant())
            {
                case "green":
                case "clean":
                case "success":
                    StatusForeground = GreenBrush;
                    StatusBackground = GreenBgBrush;
                    StatusBorder = GreenBorderBrush;
                    break;
                case "blue":
                case "info":
                case "scanning":
                    StatusForeground = BlueBrush;
                    StatusBackground = BlueBgBrush;
                    StatusBorder = BlueBorderBrush;
                    break;
                case "orange":
                case "amber":
                case "warning":
                    StatusForeground = OrangeBrush;
                    StatusBackground = OrangeBgBrush;
                    StatusBorder = OrangeBorderBrush;
                    break;
                default:
                    StatusForeground = GrayBrush;
                    StatusBackground = GrayBgBrush;
                    StatusBorder = GrayBorderBrush;
                    break;
            }
        }

        public void OnSubItemSelectionChanged()
        {
            if (_isUpdatingInternally) return;

            _isUpdatingInternally = true;
            int selCount = Items.Count(i => i.IsSelected);
            if (selCount == Items.Count)
                _isSelected = true;
            else if (selCount == 0)
                _isSelected = false;
            else
                _isSelected = null;

            OnPropertyChanged(nameof(IsSelected));
            _isUpdatingInternally = false;

            OnGroupSelectionChangedCallback?.Invoke();
        }

        public void OnSubItemSizeChanged()
        {
            TotalSizeBytes = Items.Sum(i => i.SizeBytes);
        }

        public void UpdateGroupStatus()
        {
            if (_totalSizeBytes == 0)
            {
                SetStatus("✓ Temiz", "green");
            }
            else
            {
                SetStatus("Hazır", "orange");
            }
        }

        public void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
