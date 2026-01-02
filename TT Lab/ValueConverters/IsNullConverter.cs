using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace TT_Lab.ValueConverters;

public class IsNullConverter : IValueConverter
{
    public Object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null;
    }

    public Object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}