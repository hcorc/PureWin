using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using PureWin.Models;

namespace PureWin
{
    internal static class BrushCache
    {
        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public static readonly SolidColorBrush GreenBg = CreateFrozenBrush("#1A10B981");
        public static readonly SolidColorBrush AmberBg = CreateFrozenBrush("#1AF59E0B");
        public static readonly SolidColorBrush GreenBorder = CreateFrozenBrush("#3310B981");
        public static readonly SolidColorBrush AmberBorder = CreateFrozenBrush("#33F59E0B");
        public static readonly SolidColorBrush GreenFg = CreateFrozenBrush("#10B981");
        public static readonly SolidColorBrush AmberFg = CreateFrozenBrush("#F59E0B");
        public static readonly SolidColorBrush BlueBg = CreateFrozenBrush("#1A3B82F6");
        public static readonly SolidColorBrush BlueBorder = CreateFrozenBrush("#333B82F6");
        public static readonly SolidColorBrush BlueFg = CreateFrozenBrush("#60A5FA");
        public static readonly SolidColorBrush RedBg = CreateFrozenBrush("#1AEF4444");
        public static readonly SolidColorBrush RedBorder = CreateFrozenBrush("#33EF4444");
        public static readonly SolidColorBrush RedFg = CreateFrozenBrush("#EF4444");
        public static readonly SolidColorBrush GrayBg = CreateFrozenBrush("#1A94A3B8");
        public static readonly SolidColorBrush GrayBorder = CreateFrozenBrush("#3394A3B8");
        public static readonly SolidColorBrush GrayFg = CreateFrozenBrush("#94A3B8");
    }

    public class SafeBadgeBackgroundConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isSafe = value is bool b && b;
            return isSafe ? BrushCache.GreenBg : BrushCache.AmberBg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class SafeBadgeBorderConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isSafe = value is bool b && b;
            return isSafe ? BrushCache.GreenBorder : BrushCache.AmberBorder;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class SafeBadgeForegroundConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isSafe = value is bool b && b;
            return isSafe ? BrushCache.GreenFg : BrushCache.AmberFg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class SafeBadgeTextConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isSafe = value is bool b && b;
            return isSafe ? "Güvenli" : "İsteğe Bağlı";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    // Kurulu / Temiz Rozet Dönüştürücüleri
    public class InstalledBadgeBackgroundConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInstalled = value is bool b && b;
            return isInstalled ? BrushCache.AmberBg : BrushCache.GreenBg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class InstalledBadgeBorderConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInstalled = value is bool b && b;
            return isInstalled ? BrushCache.AmberBorder : BrushCache.GreenBorder;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class InstalledBadgeForegroundConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInstalled = value is bool b && b;
            return isInstalled ? BrushCache.AmberFg : BrushCache.GreenFg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class InstalledBadgeTextConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInstalled = value is bool b && b;
            return isInstalled ? "● Sistemde Yüklü" : "✓ Yüklü Değil / Temiz";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class InstalledOpacityConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInstalled = value is bool b && b;
            return isInstalled ? 1.0 : 0.60;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    // Bloatware kaldırma durumu rozet renkleri
    public class RemovalStateBackgroundConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not BloatwareRemovalState state) return Brushes.Transparent;
            return state switch
            {
                BloatwareRemovalState.Queued => BrushCache.GrayBg,
                BloatwareRemovalState.Removing => BrushCache.BlueBg,
                BloatwareRemovalState.Verifying => BrushCache.BlueBg,
                BloatwareRemovalState.Removed => BrushCache.GreenBg,
                BloatwareRemovalState.Failed => BrushCache.RedBg,
                BloatwareRemovalState.Unverified => BrushCache.AmberBg,
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class RemovalStateBorderConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not BloatwareRemovalState state) return Brushes.Transparent;
            return state switch
            {
                BloatwareRemovalState.Queued => BrushCache.GrayBorder,
                BloatwareRemovalState.Removing => BrushCache.BlueBorder,
                BloatwareRemovalState.Verifying => BrushCache.BlueBorder,
                BloatwareRemovalState.Removed => BrushCache.GreenBorder,
                BloatwareRemovalState.Failed => BrushCache.RedBorder,
                BloatwareRemovalState.Unverified => BrushCache.AmberBorder,
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class RemovalStateForegroundConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not BloatwareRemovalState state) return Brushes.Transparent;
            return state switch
            {
                BloatwareRemovalState.Queued => BrushCache.GrayFg,
                BloatwareRemovalState.Removing => BrushCache.BlueFg,
                BloatwareRemovalState.Verifying => BrushCache.BlueFg,
                BloatwareRemovalState.Removed => BrushCache.GreenFg,
                BloatwareRemovalState.Failed => BrushCache.RedFg,
                BloatwareRemovalState.Unverified => BrushCache.AmberFg,
                _ => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class BooleanToVisibilityConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is bool b && b;
            return isVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class StringNotNullToVisibilityConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class BooleanToInverseVisibilityConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is bool b && b;
            return isVisible ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
