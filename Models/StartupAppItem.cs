using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace PureWin.Models
{
    public enum StartupImpact
    {
        High,
        Medium,
        Low,
        Unknown
    }

    public enum StartupSourceType
    {
        HkcuRun,
        HklmRun,
        HklmRun32,
        UserStartupFolder,
        CommonStartupFolder,
        AppxPackage
    }

    public class StartupAppItem : INotifyPropertyChanged
    {
        private static readonly SolidColorBrush EnabledBrush = CreateFrozenBrush(52, 211, 153);
        private static readonly SolidColorBrush DisabledBrush = CreateFrozenBrush(100, 116, 139);

        private static readonly SolidColorBrush HighImpactBg = CreateFrozenBrush(239, 68, 68, 0.12);
        private static readonly SolidColorBrush HighImpactFg = CreateFrozenBrush(248, 113, 113);
        private static readonly SolidColorBrush HighImpactBorder = CreateFrozenBrush(239, 68, 68, 0.35);

        private static readonly SolidColorBrush MedImpactBg = CreateFrozenBrush(245, 158, 11, 0.12);
        private static readonly SolidColorBrush MedImpactFg = CreateFrozenBrush(251, 191, 36);
        private static readonly SolidColorBrush MedImpactBorder = CreateFrozenBrush(245, 158, 11, 0.35);

        private static readonly SolidColorBrush LowImpactBg = CreateFrozenBrush(59, 130, 246, 0.12);
        private static readonly SolidColorBrush LowImpactFg = CreateFrozenBrush(96, 165, 250);
        private static readonly SolidColorBrush LowImpactBorder = CreateFrozenBrush(59, 130, 246, 0.35);

        private static readonly SolidColorBrush UnknownImpactBg = CreateFrozenBrush(148, 163, 184, 0.10);
        private static readonly SolidColorBrush UnknownImpactFg = CreateFrozenBrush(148, 163, 184);
        private static readonly SolidColorBrush UnknownImpactBorder = CreateFrozenBrush(148, 163, 184, 0.25);

        private bool _isEnabled;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string CommandLine { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;

        public string InstallFolder { get; set; } = string.Empty;
        public string PackageFamilyName { get; set; } = string.Empty;
        public string StartupTaskKey { get; set; } = string.Empty;

        public StartupSourceType SourceType { get; set; }
        public string SourceDisplay { get; set; } = string.Empty;
        public string RegistryKeyPath { get; set; } = string.Empty;
        public string StartupApprovedKeyPath { get; set; } = string.Empty;
        public bool IsCurrentUser { get; set; } = true;
        public string ValueName { get; set; } = string.Empty;
        public bool IsFileBased { get; set; } = false;
        public bool IsUwpPackage => SourceType == StartupSourceType.AppxPackage;

        public string PublisherDisplay => string.IsNullOrWhiteSpace(Publisher) || Publisher.Equals("Bilinmeyen Yayıncı", StringComparison.OrdinalIgnoreCase)
            ? "Bilinmeyen Yayıncı"
            : (Publisher.StartsWith("Yayıncı:", StringComparison.OrdinalIgnoreCase) ? Publisher : $"Yayıncı: {Publisher}");

        public bool CanOpenLocation =>
            (!string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath)) ||
            (!string.IsNullOrWhiteSpace(InstallFolder) && Directory.Exists(InstallFolder)) ||
            (!string.IsNullOrWhiteSpace(FilePath) && Directory.Exists(FilePath));

        public StartupImpact Impact { get; set; } = StartupImpact.Low;
        public string ImpactDisplay { get; set; } = "🌱 Düşük Etki";

        public ImageSource? IconSource { get; set; }
        public string FallbackIcon { get; set; } = "🚀";
        public bool HasIconSource => IconSource != null;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }

        public string StatusText => IsEnabled ? "Etkin" : "Devre Dışı";
        public Brush StatusBrush => IsEnabled ? EnabledBrush : DisabledBrush;

        public Brush ImpactBadgeBackground => Impact switch
        {
            StartupImpact.High => HighImpactBg,
            StartupImpact.Medium => MedImpactBg,
            StartupImpact.Low => LowImpactBg,
            _ => UnknownImpactBg
        };

        public Brush ImpactBadgeForeground => Impact switch
        {
            StartupImpact.High => HighImpactFg,
            StartupImpact.Medium => MedImpactFg,
            StartupImpact.Low => LowImpactFg,
            _ => UnknownImpactFg
        };

        public Brush ImpactBadgeBorder => Impact switch
        {
            StartupImpact.High => HighImpactBorder,
            StartupImpact.Medium => MedImpactBorder,
            StartupImpact.Low => LowImpactBorder,
            _ => UnknownImpactBorder
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b, double opacity = 1.0)
        {
            byte a = (byte)(opacity * 255);
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
