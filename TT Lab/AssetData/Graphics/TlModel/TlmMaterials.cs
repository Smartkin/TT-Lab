using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Threading;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.ServiceProviders;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.AssetData.Graphics.TlModel;

/// <summary>
/// The materials of a TT Lab model file, which are the project's material assets that parts refer to by their index
/// </summary>
/// <param name="file">The file</param>
/// <param name="owner">Asset whose data the file is, materials the project doesn't have become placeholders of it</param>
public sealed class TlmMaterials(TlmFile file, IAsset? owner = null)
{
    /// <summary>
    /// Parameter of the materials TT Lab made out of Blender's, the add-on's ID of the Blender material
    /// </summary>
    public const string BlenderMaterialParameter = "BlenderMaterial";

    private readonly Dictionary<LabURI, Int32> _indexes = new();
    private readonly Dictionary<Int32, LabURI> _uris = new();

    /// <summary>
    /// Whether materials made in Blender became the project's, the file has to be written again to refer to them
    /// </summary>
    public Boolean AddedToProject { get; private set; }

    /// <summary>
    /// Index of the material in the file, -1 for none
    /// </summary>
    public Int32 Use(LabURI material)
    {
        if (material == LabURI.Empty)
        {
            return -1;
        }

        if (_indexes.TryGetValue(material, out var index))
        {
            return index;
        }

        var assetManager = AssetManager.Get();
        index = file.Materials.Count;
        file.Materials.Add(new JsonObject
        {
            ["uri"] = material.ToString(),
            ["name"] = assetManager.DoesAssetExist(material) ? assetManager.GetAsset(material).Alias : material.ToString()
        });
        _indexes.Add(material, index);
        return index;
    }

    /// <summary>
    /// The material asset of the index, none when the file has no such material or the project no such asset
    /// </summary>
    public LabURI Get(Int32 index)
    {
        if (_uris.TryGetValue(index, out var uri))
        {
            return uri;
        }

        uri = LabURI.Empty;
        if (index >= 0 && index < file.Materials.Count && file.Materials[index] is JsonObject material)
        {
            var text = material.GetString("uri");
            if (!string.IsNullOrEmpty(text) && AssetManager.Get().DoesAssetExist(new LabURI(text)))
            {
                uri = new LabURI(text);
            }
            else if (string.IsNullOrEmpty(text) && owner != null)
            {
                uri = AddToProject(owner, material);
            }
            else
            {
                var name = material.GetString("name") ?? text ?? $"Material {index}";
                Log.WriteLine($"The project has no material {text ?? name}{(owner != null ? $", {owner.Name} draws it with a plain one" : string.Empty)}", Log.LogType.Warning);
                uri = owner != null ? CreatePlaceholder(owner, name) : LabURI.Empty;
            }
        }

        _uris.Add(index, uri);
        return uri;
    }

    // Made again whenever the owner loads, like the owner's other internal assets
    private static LabURI CreatePlaceholder(IAsset owner, string name)
    {
        var material = new Material
        {
            Package = owner.Package,
            InvariantName = $"{owner.Name}_{RigidModelData.SanitizeName(name)}",
            Alias = name,
            IsInternal = true,
            InternalOwner = owner
        };
        material.SetData(new MaterialData(material) { Name = name });
        AssetManager.Get().TryAddAsset(material);
        return material.URI;
    }

    // A material made in Blender becomes a material of the owner's package, drawn the way the game draws most of its textured
    // models, with a texture of its image. Blender keeps exporting it until the model gets imported again, the material made for
    // it the first time is used again
    private LabURI AddToProject(IAsset owner, JsonObject entry)
    {
        var assetManager = AssetManager.Get();
        var blenderId = entry.GetString("blender_id");
        var existing = blenderId == null ? null : assetManager.GetAllAssetsOf<Material>()
            .FirstOrDefault(material => !material.IsInternal && material.Parameters.TryGetValue(BlenderMaterialParameter, out var id) && id?.ToString() == blenderId);
        AddedToProject = true;
        if (existing != null)
        {
            return existing.URI;
        }

        var name = entry.GetString("name") ?? "Blender material";
        var png = file.Read<Byte>(entry["image"] is JsonObject image ? image["png"] : null);
        try
        {
            return Application.Current != null && !Dispatcher.UIThread.CheckAccess()
                ? Dispatcher.UIThread.Invoke(() => CreateMaterial(owner, name, png, entry, blenderId))
                : CreateMaterial(owner, name, png, entry, blenderId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException or IOException)
        {
            Log.WriteLine($"Couldn't add {name} made in Blender to the project, {owner.Name} draws it with a plain material: {exception.Message}", Log.LogType.Warning);
            AddedToProject = false;
            return CreatePlaceholder(owner, name);
        }
    }

    private static LabURI CreateMaterial(IAsset owner, string name, Byte[] png, JsonObject entry, string? blenderId)
    {
        var packageFolder = AssetManager.Get().GetAsset<Package>(owner.Package).GetPackageFolder();
        Texture? texture = null;
        if (png.Length > 0)
        {
            texture = (Texture?)AssetFactory.CreateAsset(typeof(Texture), TypeFolder(packageFolder, typeof(Texture)), UniqueName<Texture>(owner.Package, name), string.Empty,
                TwinIdGeneratorServiceProvider.GetGenerator<Texture>(), asset =>
                {
                    using var stream = new MemoryStream(png);
                    var data = TextureData.FromPng(asset, stream);
                    // Big textures are stored with every color, small ones with a palette and smaller versions for the distance
                    var isBig = data.Bitmap!.PixelSize.Width >= 256 || data.Bitmap.PixelSize.Height >= 256;
                    var textureAsset = (Texture)asset;
                    textureAsset.PixelFormat = isBig ? ITwinTexture.TexturePixelFormat.PSMCT32 : ITwinTexture.TexturePixelFormat.PSMT8;
                    textureAsset.TextureFunction = ITwinTexture.TextureFunction.MODULATE;
                    textureAsset.GenerateMipmaps = !isBig;
                    asset.SetData(data);
                    return AssetCreationStatus.Success;
                });
        }

        var material = AssetFactory.CreateAsset(typeof(Material), TypeFolder(packageFolder, typeof(Material)), UniqueName<Material>(owner.Package, name), string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<Material>(), asset =>
            {
                var shader = new LabShader
                {
                    ShaderType = TwinShader.Type.StandardUnlit,
                    TxtMapping = texture != null ? TwinShader.TextureMapping.ON : TwinShader.TextureMapping.OFF,
                    TextureId = texture?.URI ?? LabURI.Empty
                };
                if (entry.GetString("alpha") == "BLEND")
                {
                    shader.ABlending = TwinShader.AlphaBlending.ON;
                }
                else if (entry.GetString("alpha") == "CLIP")
                {
                    shader.ATest = TwinShader.AlphaTest.ON;
                    shader.AlphaValueToBeComparedTo = (Byte)Math.Clamp(Math.Round(entry.GetFloat("alpha_cutoff", 0.5f) * 255.0f), 0, 255);
                }

                asset.SetData(new MaterialData(asset) { Name = name, Shaders = [shader], ActivatedShaders = Enum.Parse<AppliedShaders>(shader.ShaderType.ToString()) });
                if (blenderId != null)
                {
                    asset.Parameters[BlenderMaterialParameter] = blenderId;
                }

                return AssetCreationStatus.Success;
            })!;
        Log.WriteLine($"Added {name} made in Blender to the project as {material.URI}");
        return material.URI;
    }

    private static Folder TypeFolder(Folder packageFolder, Type type)
    {
        var assetManager = AssetManager.Get();
        var typeFolder = packageFolder.Children.FirstOrDefault(child => assetManager.GetAsset(child) is Folder { Name: var name } && name == type.Name);
        return typeFolder == null ? packageFolder : assetManager.GetAsset<Folder>(typeFolder);
    }

    private static string UniqueName<T>(LabURI package, string name) where T : IAsset
    {
        var taken = AssetManager.Get().GetAllAssetsOf<T>().Where(asset => asset.Package == package).Select(asset => asset.InvariantName).ToHashSet();
        var candidate = RigidModelData.SanitizeName(name);
        for (var number = 2; taken.Contains(candidate); number++)
        {
            candidate = $"{RigidModelData.SanitizeName(name)} {number}";
        }

        return candidate;
    }
}
