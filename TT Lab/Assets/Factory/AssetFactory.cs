using System;
using System.Diagnostics;
using TT_Lab.Services;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.Assets.Factory;

public static class AssetFactory
{
    public static IAsset? CreateAsset(Type type, Folder folder, string name, string variation, ITwinIdGeneratorService idGenerator, Func<IAsset, AssetCreationStatus>? dataCreator = null, Enums.Layouts? layout = null)
    {
        Debug.Assert(type.IsAssignableTo(typeof(IAsset)), $"The type {type.Name} must implement IAsset");
        var newAsset = (IAsset)Activator.CreateInstance(type)!;
        newAsset.Name = name;
        newAsset.Alias = name;
        newAsset.Package = folder.Package;
        newAsset.Variation = variation;
        newAsset.ID = idGenerator.GenerateTwinId();
        if (layout.HasValue)
        {
            newAsset.LayoutID = (int)layout.Value;
        }
        
        newAsset.RegenerateLinks(true);
        
        var dataCreationResult = dataCreator?.Invoke(newAsset);
        if (dataCreationResult is AssetCreationStatus.Failed)
        {
            return null;
        }
        
        folder.AddChild(newAsset);
        AssetManager.Get().AddAsset(newAsset);
        newAsset.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        folder.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData | SerializationFlags.FixReferences);
        
        var parent = folder.GetResourceTreeElement();
        parent.AddNewChild(newAsset.GetResourceTreeElement(parent));
        parent.ClearChildren();
        parent.LoadChildrenBack();
        parent.NotifyOfPropertyChange(nameof(parent.Children));
        
        return newAsset;
    }
}