using System.Globalization;
using Avalonia.Data.Converters;
using DiffPlex.DiffBuilder.Model;
using XCommander.ViewModels;

namespace XCommander.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public static readonly FileSizeConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return FormatFileSize(bytes);
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
            int suffixIndex = 0;
            double size = bytes;
            
            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }
            
            return suffixIndex == 0 
                ? $"{size:N0} {suffixes[suffixIndex]}" 
                : $"{size:N2} {suffixes[suffixIndex]}";
        }
    }

    public class DateTimeConverter : IValueConverter
    {
        public static readonly DateTimeConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("yyyy-MM-dd HH:mm", culture);
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToOpacityConverter : IValueConverter
    {
        public static readonly BoolToOpacityConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isHidden && isHidden)
            {
                return 0.5;
            }
            return 1.0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ViewModeConverter : IValueConverter
    {
        public static readonly ViewModeConverter IsDetails = new(FilePanelViewMode.Details);
        public static readonly ViewModeConverter IsList = new(FilePanelViewMode.List);
        public static readonly ViewModeConverter IsThumbnails = new(FilePanelViewMode.Thumbnails);
        
        private readonly FilePanelViewMode _targetMode;
        
        public ViewModeConverter(FilePanelViewMode targetMode)
        {
            _targetMode = targetMode;
        }
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is FilePanelViewMode mode)
            {
                return mode == _targetMode;
            }
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToStatusBrushConverter : IValueConverter
    {
        public static readonly BoolToStatusBrushConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isEnabled && isEnabled)
            {
                return Avalonia.Media.Brushes.Green;
            }
            return Avalonia.Media.Brushes.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EnumToIntConverter : IValueConverter
    {
        public static readonly EnumToIntConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Enum enumValue)
            {
                return System.Convert.ToInt32(enumValue);
            }
            return 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int index && targetType.IsEnum)
            {
                return Enum.ToObject(targetType, index);
            }
            return Enum.ToObject(targetType, 0);
        }
    }

    public class BoolToEncodeDecodeConverter : IValueConverter
    {
        public static readonly BoolToEncodeDecodeConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isDecode && isDecode)
            {
                return "Decode";
            }
            return "Encode";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DiffModeConverter : IValueConverter
    {
        public static readonly DiffModeConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DiffDisplayMode mode)
            {
                return mode == DiffDisplayMode.Inline;
            }
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isInline && isInline)
            {
                return DiffDisplayMode.Inline;
            }
            return DiffDisplayMode.SideBySide;
        }
    }
    
    public class IsSideBySideConverter : IValueConverter
    {
        public static readonly IsSideBySideConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DiffDisplayMode mode)
            {
                return mode == DiffDisplayMode.SideBySide;
            }
            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class IsInlineModeConverter : IValueConverter
    {
        public static readonly IsInlineModeConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DiffDisplayMode mode)
            {
                return mode == DiffDisplayMode.Inline;
            }
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class ChangeTypeToIconConverter : IValueConverter
    {
        public static readonly ChangeTypeToIconConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ChangeType changeType)
            {
                return changeType switch
                {
                    ChangeType.Inserted => "+",
                    ChangeType.Deleted => "-",
                    ChangeType.Modified => "~",
                    _ => " "
                };
            }
            return " ";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
