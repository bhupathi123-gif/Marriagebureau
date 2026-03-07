using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MarriageBureau.Converters
{
    /// <summary>Converts a file path string to a BitmapImage (for photo display)</summary>
    public class PathToImageConverter : IValueConverter
    {
        public static readonly PathToImageConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(path, UriKind.Absolute);
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 400;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                catch { }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Returns Visibility.Visible if value is not null/empty, else Collapsed</summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public static readonly NullToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
                return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Inverts a boolean</summary>
    public class BoolToInverseConverter : IValueConverter
    {
        public static readonly BoolToInverseConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }

    /// <summary>Bool to Visibility (true = Visible, false = Collapsed)</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public static readonly BoolToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>Returns "placeholder" image path when no photo</summary>
    public class PhotoFallbackConverter : IValueConverter
    {
        public static readonly PhotoFallbackConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Accept either a path string or a Biodata object
            string? path = null;

            if (value is string s && !string.IsNullOrWhiteSpace(s))
                path = s;
            else if (value is MarriageBureau.Models.Biodata bd)
                path = bd.PrimaryPhotoPath;

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(path, UriKind.Absolute);
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 120;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                catch { }
            }
            // Return built-in placeholder
            return new BitmapImage(new Uri("pack://application:,,,/Resources/placeholder.png"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Gender to color brush</summary>
    public class GenderToColorConverter : IValueConverter
    {
        public static readonly GenderToColorConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string g)
            {
                return g.ToUpper() switch
                {
                    "FEMALE" => System.Windows.Media.Brushes.DeepPink,
                    "MALE" => System.Windows.Media.Brushes.SteelBlue,
                    _ => System.Windows.Media.Brushes.Gray
                };
            }
            return System.Windows.Media.Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Maps import-row status string to a background brush colour</summary>
    public class ImportStatusColorConverter : IValueConverter
    {
        public static readonly ImportStatusColorConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                if (status.StartsWith("Imported", StringComparison.OrdinalIgnoreCase))
                    return new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x2E, 0x7D, 0x32));   // green
                if (status.StartsWith("Skipped", StringComparison.OrdinalIgnoreCase))
                    return new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xF5, 0x7F, 0x17));   // amber
                if (status.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                    return new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xC6, 0x28, 0x28));   // red
            }
            // Pending
            return new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x42, 0x42, 0x42));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// Converts an index + count to "dot" selected indicator.
    /// Parameter = "index" returns the item index from a ListBox/ItemsControl.
    /// Used by photo slideshow dots.
    /// </summary>
    public class IndexEqualsConverter : IMultiValueConverter
    {
        public static readonly IndexEqualsConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is int current && values[1] is int total)
                return current == total;
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => Array.Empty<object>();
    }
}
