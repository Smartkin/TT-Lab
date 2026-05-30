using System;

namespace TT_Lab.Util;

public interface IStringConverter
{
    object ConvertFromString(string s);
    
    bool IsConvertible(string s);
}

public interface IStringConverter<out T> : IStringConverter
{
    object IStringConverter.ConvertFromString(string s)
    {
        return ConvertFromString(s)!;
    }

    new T ConvertFromString(string s);
}

public class DecimalConverter : IStringConverter<Decimal>
{
    Decimal IStringConverter<Decimal>.ConvertFromString(string s)
    {
        return Decimal.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return Decimal.TryParse(s, out _);
    }
}

public class DoubleConverter : IStringConverter<Double>
{
    Double IStringConverter<Double>.ConvertFromString(string s)
    {
        return Double.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return Double.TryParse(s, out _);
    }
}

public class SingleConverter : IStringConverter<Single>
{
    Single IStringConverter<Single>.ConvertFromString(string s)
    {
        return Single.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return Single.TryParse(s, out _);
    }
}

public class UInt128Converter : IStringConverter<UInt128>
{
    UInt128 IStringConverter<UInt128>.ConvertFromString(string s)
    {
        return UInt128.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return UInt128.TryParse(s, out _);
    }
}

public class UInt64Converter : IStringConverter<UInt64>
{
    UInt64 IStringConverter<UInt64>.ConvertFromString(string s)
    {
        return UInt64.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return UInt64.TryParse(s, out _);
    }
}

public class UInt32Converter : IStringConverter<UInt32>
{
    UInt32 IStringConverter<UInt32>.ConvertFromString(string s)
    {
        return UInt32.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return UInt32.TryParse(s, out _);
    }
}

public class UInt16Converter : IStringConverter<UInt16>
{
    UInt16 IStringConverter<UInt16>.ConvertFromString(string s)
    {
        return UInt16.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return UInt16.TryParse(s, out _);
    }
}

public class Int128Converter : IStringConverter<Int128>
{
    Int128 IStringConverter<Int128>.ConvertFromString(string s)
    {
        return Int128.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return Int128.TryParse(s, out _);
    }
}

public class Int64Converter : IStringConverter<Int64>
{
    Int64 IStringConverter<Int64>.ConvertFromString(string s)
    {
        return Int64.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return Int64.TryParse(s, out _);
    }
}

public class Int32Converter : IStringConverter<Int32>
{
    Int32 IStringConverter<Int32>.ConvertFromString(string s)
    {
        return Int32.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return Int32.TryParse(s, out _);
    }
}

public class Int16Converter : IStringConverter<Int16>
{
    Int16 IStringConverter<Int16>.ConvertFromString(string s)
    {
        return Int16.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return Int16.TryParse(s, out _);
    }
}

public class SByteConverter : IStringConverter<SByte>
{
    SByte IStringConverter<SByte>.ConvertFromString(string s)
    {
        return SByte.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return SByte.TryParse(s, out _);
    }
}

public class ByteConverter : IStringConverter<Byte>
{
    Byte IStringConverter<Byte>.ConvertFromString(string s)
    {
        return Byte.Parse(s);
    }

    public Boolean IsConvertible(string s)
    {
        return Byte.TryParse(s, out _);
    }
}