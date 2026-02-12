using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace DeviceSim.App.Converters;

public class TypeVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string typeStr && parameter is string target)
        {
            bool isBool = typeStr.Equals("bool", StringComparison.OrdinalIgnoreCase);
            
            if (target == "bool") return isBool;
            if (target == "!bool") return !isBool;
        }
        return true; // Default show
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
