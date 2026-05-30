using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace TT_Lab.ValueConverters;

public class EditableListBoxSizeLimitConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values[0] is not ItemCollection items)
        {
            return true;
        }
        
        var count = items.Count;
        
        if (values[1] is int limit)
        {
            if (limit < 0)
            {
                return true;
            }

            return count < limit;
        }

        return true;
    }
}