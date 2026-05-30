using System;
using System.Globalization;
using Avalonia.Data.Converters;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ValueConverters;

public class LabUriConverter : IValueConverter
{
    public Object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var assetManager = AssetManager.Get();
        var showData = parameter is bool && (bool)parameter;
        string? resultString = null;
        IAsset? asset = null;
        switch (value)
        {
            case LabURI labUri:
                asset = labUri == LabURI.Empty ? null : assetManager.GetAsset(labUri);
                resultString = labUri == LabURI.Empty ? "Empty" : asset!.Alias;
                break;
            case PrimitiveWrapperViewModel<LabURI> wrapper:
                asset = wrapper.Value == LabURI.Empty ? null : assetManager.GetAsset(wrapper.Value);
                resultString = wrapper.Value == LabURI.Empty ? "Empty" : asset!.Alias;
                break;
            case null:
                resultString = "Empty";
                break;
        }

        if (resultString != null)
        {
            return showData && asset != null ? $"{resultString} ({asset.FullDataPath})" : resultString;
        }

        return $"Provided type was {value?.GetType().Name}. Check your bindings!";
    }

    public Object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}