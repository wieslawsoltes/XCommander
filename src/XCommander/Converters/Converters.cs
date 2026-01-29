using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
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
            return BindingOperations.DoNothing;
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
                var format = parameter as string;
                if (string.IsNullOrWhiteSpace(format))
                {
                    if (Application.Current?.Resources.TryGetValue("DateFormatString", out var resource) == true)
                    {
                        format = resource as string;
                    }
                }
                if (string.IsNullOrWhiteSpace(format))
                    format = "yyyy-MM-dd HH:mm";
                try
                {
                    return dateTime.ToString(format, culture);
                }
                catch
                {
                    return dateTime.ToString("yyyy-MM-dd HH:mm", culture);
                }
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }

    public class DateTimeFormatConverter : IMultiValueConverter
    {
        public static readonly DateTimeFormatConverter Instance = new();

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count == 0 || values[0] is not DateTime dateTime)
                return values.Count > 0 ? values[0]?.ToString() ?? string.Empty : string.Empty;

            var format = values.Count > 1 ? values[1]?.ToString() : null;
            if (string.IsNullOrWhiteSpace(format))
                format = "yyyy-MM-dd HH:mm";
            try
            {
                return dateTime.ToString(format, culture);
            }
            catch
            {
                return dateTime.ToString("yyyy-MM-dd HH:mm", culture);
            }
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            return new object[] { BindingOperations.DoNothing };
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
            return BindingOperations.DoNothing;
        }
    }

    public class BoolToGridLinesVisibilityConverter : IValueConverter
    {
        public static readonly BoolToGridLinesVisibilityConverter Instance = new();

        public DataGridGridLinesVisibility VisibleState { get; set; } = DataGridGridLinesVisibility.Horizontal;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var show = value is bool flag && flag;
            return show ? VisibleState : DataGridGridLinesVisibility.None;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }

    public class FileNameDisplayConverter : IMultiValueConverter
    {
        public static readonly FileNameDisplayConverter Instance = new();

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count == 0)
                return string.Empty;

            var name = values[0]?.ToString() ?? string.Empty;
            var showExtensions = values.Count > 1 && values[1] is bool flag ? flag : true;
            if (showExtensions)
                return name;

            if (name.StartsWith(".", StringComparison.Ordinal) && name.LastIndexOf('.') == 0)
                return name;

            var extension = Path.GetExtension(name);
            if (string.IsNullOrEmpty(extension))
                return name;

            return name.Substring(0, name.Length - extension.Length);
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            return new object[] { BindingOperations.DoNothing };
        }
    }

    public class EqualsConverter : IValueConverter
    {
        public static readonly EqualsConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null)
                return true;
            if (value == null || parameter == null)
                return false;
            if (value is string left && parameter is string right)
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            return Equals(value, parameter);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }


    public class AddValueConverter : IValueConverter
    {
        public static readonly AddValueConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return 0d;

            if (parameter is not string param ||
                !double.TryParse(param, NumberStyles.Float, CultureInfo.InvariantCulture, out var addend))
            {
                addend = 0d;
            }

            if (value is int intValue)
                return intValue + addend;
            if (value is double doubleValue)
                return doubleValue + addend;

            return value;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
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
            return BindingOperations.DoNothing;
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
            return BindingOperations.DoNothing;
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
            return BindingOperations.DoNothing;
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
            return BindingOperations.DoNothing;
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
            return BindingOperations.DoNothing;
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
            return BindingOperations.DoNothing;
        }
    }
}
