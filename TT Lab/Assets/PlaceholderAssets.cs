using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.ServiceProviders;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets;

/// <summary>
/// Minimal assets that stand in for deleted assets which other assets can't go without
/// </summary>
public static class PlaceholderAssets
{
    public const string PlaceholderName = "Placeholder";

    private static readonly Dictionary<Type, Func<IAsset, AssetCreationStatus>> DataCreators = new()
    {
        [typeof(GameObject)] = asset =>
        {
            asset.SetData(new GameObjectData(asset) { Name = PlaceholderName });
            return AssetCreationStatus.Success;
        },
        [typeof(Material)] = asset =>
        {
            asset.SetData(new MaterialData(asset)
            {
                Name = PlaceholderName,
                ActivatedShaders = Enums.AppliedShaders.StandardLit
            });
            return AssetCreationStatus.Success;
        },
        [typeof(Texture)] = asset =>
        {
            var texture = (Texture)asset;
            texture.PixelFormat = ITwinTexture.TexturePixelFormat.PSMT8;
            texture.TextureFunction = ITwinTexture.TextureFunction.MODULATE;
            texture.GenerateMipmaps = true;
            asset.SetData(TextureData.CreateSolidColor(asset, 16, 0xFFFFFFFF));
            return AssetCreationStatus.Success;
        },
    };

    /// <summary>
    /// Whether a placeholder can stand in for the asset without being one of the deleted assets itself
    /// </summary>
    public static bool CanReplace(IAsset asset, IReadOnlySet<LabURI> deletedAssets)
    {
        if (!DataCreators.ContainsKey(asset.GetType()))
        {
            return false;
        }

        var existingPlaceholder = FindPlaceholder(asset.GetType(), asset.Package);
        return existingPlaceholder == null || !deletedAssets.Contains(existingPlaceholder.URI);
    }

    /// <summary>
    /// Gets the placeholder for the asset's type within the asset's package, creating it if needed
    /// </summary>
    public static LabURI GetOrCreate(IAsset asset)
    {
        var type = asset.GetType();
        var existingPlaceholder = FindPlaceholder(type, asset.Package);
        if (existingPlaceholder != null)
        {
            return existingPlaceholder.URI;
        }

        var packageFolder = AssetManager.Get().GetAsset<Package>(asset.Package).GetPackageFolder();
        var typeFolderUri = packageFolder.Children.FirstOrDefault(child => AssetManager.Get().GetAsset(child) is Folder { Name: var name } && name == type.Name);
        var folder = typeFolderUri == null ? packageFolder : AssetManager.Get().GetAsset<Folder>(typeFolderUri);
        var placeholder = AssetFactory.CreateAsset(type, folder, PlaceholderName, string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator(type), DataCreators[type])!;
        Log.WriteLine($"Created placeholder {type.Name} {placeholder.URI}");
        return placeholder.URI;
    }

    private static IAsset? FindPlaceholder(Type type, LabURI package)
    {
        return AssetManager.Get().GetAllAssetsOf(type).FirstOrDefault(a => a.Package == package && a.InvariantName == PlaceholderName);
    }
}
