using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace TT_Lab.ValueConverters;

public class ListBoxItemConverterIndexed : IValueConverter
{
    public Object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var listBoxItem = value as ListBoxItem;
        var view = ItemsControl.ItemsControlFromItemContainer(listBoxItem!);
        var index = view!.IndexFromContainer(listBoxItem!);
        
        return listBoxItem!.Content + " " + index;
    }

    public Object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}