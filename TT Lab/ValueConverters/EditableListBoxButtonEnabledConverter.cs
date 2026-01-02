using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Xaml.Interactivity;

namespace TT_Lab.ValueConverters;

public class EditableListBoxButtonEnabledConverter : IMultiValueConverter
{
    public Object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var selectedItem = values[0];
        if (selectedItem == null)
        {
            return false;
        }

        if (values[1] is not int itemsCount)
        {
            return false;
        }

        if (values[2] is not int comparingNumber)
        {
            return false;
        }

        if (parameter is not ComparisonConditionType compareOperator)
        {
            return false;
        }

        switch (compareOperator)
        {
            case ComparisonConditionType.Equal:
                return itemsCount == comparingNumber;
            case ComparisonConditionType.NotEqual:
                return itemsCount != comparingNumber;
            case ComparisonConditionType.LessThan:
                return itemsCount < comparingNumber;
            case ComparisonConditionType.LessThanOrEqual:
                return itemsCount <= comparingNumber;
            case ComparisonConditionType.GreaterThan:
                return itemsCount > comparingNumber;
            case ComparisonConditionType.GreaterThanOrEqual:
                return itemsCount >= comparingNumber;
        }
        
        return true;
    }
}