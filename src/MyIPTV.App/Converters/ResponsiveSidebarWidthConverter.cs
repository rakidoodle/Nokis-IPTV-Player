using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyIPTV.App.Converters;

public sealed class ResponsiveSidebarWidthConverter : IValueConverter
{
    private const double ExpandedThreshold = 960;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is double width && width >= ExpandedThreshold
            ? new GridLength(232)
            : new GridLength(80);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
