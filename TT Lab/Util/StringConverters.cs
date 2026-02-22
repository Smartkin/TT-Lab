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
        return ConvertFromString(s);
    }

    new T ConvertFromString(string s);
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