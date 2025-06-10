using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using Caliburn.Micro;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ValueConverters;

public class ObservableCollectionWrapperConverter<T> : IValueConverter where T : IComparable
{
    public Object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return null;
        }
        
        var collection = (ObservableCollection<T>)value;
        var wrapperCollection = new BindableCollection<PrimitiveWrapperViewModel<T>>();
        foreach (var item in collection)
        {
            wrapperCollection.Add(new PrimitiveWrapperViewModel<T>(item));
        }
        
        return wrapperCollection;
    }

    public Object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return null;
        }
        
        var collection = (ObservableCollection<PrimitiveWrapperViewModel<T>>)value;
        var wrapperCollection = new BindableCollection<T>();
        foreach (var item in collection)
        {
            wrapperCollection.Add(item.Value);
        }
        
        return wrapperCollection;
    }
}

public class ObservableCollectionWrapperBackConverter<T> : IValueConverter where T : IComparable
{
    private readonly ObservableCollectionWrapperConverter<T> _converter = new();

    public Object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return _converter.ConvertBack(value, targetType, parameter, culture);
    }

    public Object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return _converter.Convert(value, targetType, parameter, culture);
    }
}

public class ObservableCollectionWrapperBackLabUriConverter : ObservableCollectionWrapperBackConverter<LabURI>
{
}