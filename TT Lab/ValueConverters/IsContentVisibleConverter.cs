using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace TT_Lab.ValueConverters;

public class IsContentVisibleConverter : IMultiValueConverter
{
    public static IsContentVisibleConverter Instance { get; } = new();
    
    public Object? Convert(IList<Object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values.Count == 2 && values[0] == values[1];
    }
}