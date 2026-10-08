using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.ServiceProviders;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Enumerations;

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
        // The boat guy the viewport draws for parts without a material, at a size and in a layout the game takes
        [typeof(Texture)] = asset =>
        {
            using var png = ManifestResourceLoader.Open(MiscUtils.BoatGuyPath);
            var data = TextureData.FromPng(asset, png).ResizedForTheGame();
            ((Texture)asset).UseGameLayout(data.Bitmap!.PixelSize.Width, data.Bitmap.PixelSize.Height);
            asset.SetData(data);
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
    public static LabURI GetOrCreate(IAsset asset) => GetOrCreate(asset.GetType(), asset.Package);

    /// <summary>
    /// Gets the placeholder of the type within the package, creating it if needed
    /// </summary>
    public static LabURI GetOrCreate(Type type, LabURI package)
    {
        var existingPlaceholder = FindPlaceholder(type, package);
        if (existingPlaceholder != null)
        {
            return existingPlaceholder.URI;
        }

        var packageFolder = AssetManager.Get().GetAsset<Package>(package).GetPackageFolder();
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
