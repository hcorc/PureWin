using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace PureWin.Models
{
    public class SystemHealthCategory : INotifyPropertyChanged
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public ObservableCollection<SystemHealthItem> Items { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public enum HealthRiskLevel
    {
        Low,        // Düşük risk: Rozet gösterilmez, doğrudan çalışır.
        LowMedium,  // Düşük-Orta risk: Turuncu rozet, çalıştırmadan önce bilgilendirme gösterir.
        Medium,     // Orta risk: Turuncu rozet.
        High        // Yüksek risk: Kırmızı rozet.
    }

    public enum HealthExecutionState
    {
        Ready,
        Running,
        Completed,
        Failed
    }

    public class SystemHealthItem : INotifyPropertyChanged
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

        private static readonly SolidColorBrush RedBrush = CreateFrozenBrush("#EF4444");
        private static readonly SolidColorBrush RedBgBrush = CreateFrozenBrush("#1AEF4444");
        private static readonly SolidColorBrush RedBorderBrush = CreateFrozenBrush("#33EF4444");

        private static readonly SolidColorBrush GrayBrush = CreateFrozenBrush("#94A3B8");
        private static readonly SolidColorBrush GrayBgBrush = CreateFrozenBrush("#1A94A3B8");
        private static readonly SolidColorBrush GrayBorderBrush = CreateFrozenBrush("#3394A3B8");

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CommandInfo { get; set; } = string.Empty;
        public HealthRiskLevel RiskLevel { get; set; } = HealthRiskLevel.Low;
        public bool IsRepair { get; set; } = false;
        public string DefaultActionText { get; set; } = "Kontrol Et";
        public bool RequiresConfirmation { get; set; } = false;
        public string? ExecutionArgument { get; set; }

        private string? _customCompletedBadgeText;
        public string? CustomCompletedBadgeText
        {
            get => _customCompletedBadgeText;
            set
            {
                if (_customCompletedBadgeText != value)
                {
                    _customCompletedBadgeText = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBadgeText));
                }
            }
        }

        // Risk DÜŞÜK olduğunda rozet gösterilmez; Düşük-Orta, Orta veya Yüksek ise gösterilir
        public bool HasRiskBadge => RiskLevel != HealthRiskLevel.Low;
        public string RiskBadgeText => RiskLevel switch
        {
            HealthRiskLevel.LowMedium => "⚠️ Düşük-Orta Risk",
            HealthRiskLevel.Medium => "⚠️ Orta Risk",
            HealthRiskLevel.High => "⚠️ Yüksek Risk",
            _ => string.Empty
        };

        public SolidColorBrush RiskForeground => RiskLevel switch
        {
            HealthRiskLevel.High => RedBrush,
            _ => OrangeBrush
        };

        public SolidColorBrush RiskBackground => RiskLevel switch
        {
            HealthRiskLevel.High => RedBgBrush,
            _ => OrangeBgBrush
        };

        public SolidColorBrush RiskBorder => RiskLevel switch
        {
            HealthRiskLevel.High => RedBorderBrush,
            _ => OrangeBorderBrush
        };

        private HealthExecutionState _state = HealthExecutionState.Ready;
        public HealthExecutionState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(IsActionEnabled));
                    OnPropertyChanged(nameof(ActionButtonText));
                    OnPropertyChanged(nameof(ActionButtonAppearance));
                    OnPropertyChanged(nameof(HasResultBadge));
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(StatusForeground));
                    OnPropertyChanged(nameof(StatusBackground));
                    OnPropertyChanged(nameof(StatusBorder));
                    OnPropertyChanged(nameof(ShowProgressBar));
                    OnPropertyChanged(nameof(ProgressForeground));
                }
            }
        }

        private int _progressPercentage = 0;
        public int ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                int clamped = Math.Clamp(value, 0, 100);
                if (_progressPercentage != clamped)
                {
                    _progressPercentage = clamped;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProgressText));
                    OnPropertyChanged(nameof(StatusBadgeText));
                }
            }
        }

        public string ProgressText => $"{ProgressPercentage}%";

        private string _stageText = string.Empty;
        public string StageText
        {
            get => _stageText;
            set
            {
                if (_stageText != value)
                {
                    _stageText = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasStageText));
                    OnPropertyChanged(nameof(ShowProgressBar));
                }
            }
        }

        public bool HasStageText => !string.IsNullOrWhiteSpace(StageText);

        public bool ShowProgressBar => State == HealthExecutionState.Running || (State == HealthExecutionState.Completed && HasStageText);

        public SolidColorBrush ProgressForeground => State switch
        {
            HealthExecutionState.Completed => IsHealthy ? GreenBrush : OrangeBrush,
            HealthExecutionState.Failed => RedBrush,
            _ => BlueBrush
        };

        private string _statusDetail = string.Empty;
        public string StatusDetail
        {
            get => _statusDetail;
            set
            {
                if (_statusDetail != value)
                {
                    _statusDetail = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasStatusDetail));
                }
            }
        }

        public bool HasStatusDetail => !string.IsNullOrWhiteSpace(StatusDetail);

        private bool _isHealthy = true;
        public bool IsHealthy
        {
            get => _isHealthy;
            set
            {
                if (_isHealthy != value)
                {
                    _isHealthy = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(StatusForeground));
                    OnPropertyChanged(nameof(StatusBackground));
                    OnPropertyChanged(nameof(StatusBorder));
                    OnPropertyChanged(nameof(ProgressForeground));
                }
            }
        }

        public bool IsRunning => State == HealthExecutionState.Running;
        public bool IsActionEnabled => State != HealthExecutionState.Running;

        public string ActionButtonText => State switch
        {
            HealthExecutionState.Running => "İşlem Sürüyor...",
            HealthExecutionState.Completed => "Yeniden " + (IsRepair ? "Onar" : "Kontrol Et"),
            HealthExecutionState.Failed => "Tekrar Dene",
            _ => DefaultActionText
        };

        public ControlAppearance ActionButtonAppearance => State switch
        {
            HealthExecutionState.Completed => ControlAppearance.Secondary,
            _ => ControlAppearance.Primary
        };

        public bool HasResultBadge => State == HealthExecutionState.Completed || State == HealthExecutionState.Failed || State == HealthExecutionState.Running;

        public string StatusBadgeText => State switch
        {
            HealthExecutionState.Running => ProgressPercentage > 0 ? $"⏳ %{ProgressPercentage}" : "⏳ Çalışıyor...",
            HealthExecutionState.Completed => !string.IsNullOrEmpty(CustomCompletedBadgeText)
                ? CustomCompletedBadgeText
                : (IsRepair
                    ? (IsHealthy ? "✓ Onarım Tamamlandı" : "⚠️ Onarım Tamamlanamadı")
                    : (IsHealthy ? "✓ Sağlıklı (Bozulma Yok)" : "⚠️ Bozulma Tespit Edildi")),
            HealthExecutionState.Failed => "✕ Hata Oluştu",
            _ => string.Empty
        };

        public SolidColorBrush StatusForeground => State switch
        {
            HealthExecutionState.Running => BlueBrush,
            HealthExecutionState.Completed => IsHealthy ? GreenBrush : OrangeBrush,
            HealthExecutionState.Failed => RedBrush,
            _ => GrayBrush
        };

        public SolidColorBrush StatusBackground => State switch
        {
            HealthExecutionState.Running => BlueBgBrush,
            HealthExecutionState.Completed => IsHealthy ? GreenBgBrush : OrangeBgBrush,
            HealthExecutionState.Failed => RedBgBrush,
            _ => GrayBgBrush
        };

        public SolidColorBrush StatusBorder => State switch
        {
            HealthExecutionState.Running => BlueBorderBrush,
            HealthExecutionState.Completed => IsHealthy ? GreenBorderBrush : OrangeBorderBrush,
            HealthExecutionState.Failed => RedBorderBrush,
            _ => GrayBorderBrush
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
