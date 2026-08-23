using System.Globalization;
using System.Windows.Data;
using MyIPTV.Core.Models;

namespace MyIPTV.App.Converters;

public sealed class ProfileConnectionTypeToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            ProfileConnectionType.M3uPlaylist => "M3U",
            ProfileConnectionType.XtreamApi => "Xtream",
            ProfileConnectionType.StalkerPortal => "Stalker",
            _ => "Unknown",
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
