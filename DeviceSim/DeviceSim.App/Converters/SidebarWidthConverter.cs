using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DeviceSim.App.Converters;

public class SidebarWidthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCollapsed)
        {
            return isCollapsed ? 64.0 : 240.0;
        }
        return 240.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
