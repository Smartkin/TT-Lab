using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace TT_Lab.ValueConverters;

public class FloatConverter : IValueConverter
{
    private bool leadingDot = false;
    private int leadingZeros = 0;
    
    public Object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not float floatValue)
        {
            return null;
        }

        var result = string.Format(CultureInfo.InvariantCulture, "{0:0.################################################################}", floatValue);
        if (leadingDot)
        {
            result += '.';
        }

        for (var i = 0; i < leadingZeros; i++)
        {
            result += '0';
        }
        
        return result;
    }

    public Object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var stringValue = value as string;
        if (string.IsNullOrEmpty(stringValue))
        {
            return null;
        }

        if (stringValue.EndsWith("e") || stringValue.EndsWith("E") || stringValue.EndsWith("-") || stringValue.EndsWith("+"))
        {
            return null;
        }
        
        var dotIndex = stringValue.IndexOf(".");
        leadingZeros = 0;
        if (dotIndex != -1)
        {
            for (var i = dotIndex + 1; i < stringValue.Length; i++)
            {
                var num = stringValue[i];
                if (num != '0')
                {
                    leadingZeros = 0;
                    break;
                }
                
                leadingZeros++;
            }
        }

        leadingDot = (dotIndex == stringValue.Length - 1) || (leadingZeros > 0);

        var parsed = float.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out float floatValue);
        return !parsed ? 0.0f : floatValue;
    }
}