using System;
using System.Globalization;
using System.Windows.Data;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ValueConverters;

public class PrimitiveWrapperConverter<T> : IValueConverter where T : IComparable
{
    public Object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? null : new PrimitiveWrapperViewModel<T>((T)value);
    }

    public Object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? null : ((PrimitiveWrapperViewModel<T>)value).Value;
    }
}

public class PrimitiveWrapperBackConverter<T> : IValueConverter where T : IComparable
{
    private readonly PrimitiveWrapperConverter<T> _converter = new();

    public Object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return _converter.ConvertBack(value, targetType, parameter, culture);
    }

    public Object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return _converter.Convert(value, targetType, parameter, culture);
    }
}

// Because Microsoft didn't implement XAML 2009 standard in compiled markup yet :(

public class PrimitiveWrapperConverterLabUri : PrimitiveWrapperConverter<LabURI>
{
}

public class PrimitiveWrapperConverterInt16 : PrimitiveWrapperConverter<Int16>
{
}

public class PrimitiveWrapperConverterInt32 : PrimitiveWrapperConverter<Int32>
{
}

public class PrimitiveWrapperConverterSingle : PrimitiveWrapperConverter<Single>
{
}

public class PrimitiveWrapperConverterUInt32 : PrimitiveWrapperConverter<UInt32>
{
}

public class PrimitiveWrapperConverterUInt64 : PrimitiveWrapperConverter<UInt64>
{
}

public class PrimitiveWrapperBackConverterByte : PrimitiveWrapperBackConverter<Byte>
{
}

public class PrimitiveWrapperBackConverterInt16 : PrimitiveWrapperBackConverter<Int16>
{
}

public class PrimitiveWrapperBackConverterUInt16 : PrimitiveWrapperBackConverter<UInt16>
{
}

public class PrimitiveWrapperBackConverterUInt64 : PrimitiveWrapperBackConverter<UInt64>
{
}

public class PrimitiveWrapperBackConverterLabUri : PrimitiveWrapperBackConverter<LabURI>
{
}