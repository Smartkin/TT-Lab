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
using TT_Lab.Project;
using Splat;
using TT_Lab.ServiceProviders;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.AssetData.Graphics.TlModel;

/// <summary>
/// The materials of a TT Lab model file, which are the project's material assets that parts refer to by their index
/// </summary>
/// <param name="file">The file</param>
/// <param name="owner">Asset whose data the file is, materials the project doesn't have become placeholders of it</param>
/// <summary>
/// What a part uses its material for, a material made in Blender gets the shader the game draws that with
/// </summary>
public enum TlmMaterialUse
{
    Rigid,
    Skin
}

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
    /// The material asset of the index for a part that has to be drawn with one, a placeholder of the owner for a part made in
    /// Blender without any
    /// </summary>
    public LabURI GetRequired(Int32 index, TlmMaterialUse use = TlmMaterialUse.Rigid)
    {
        var uri = Get(index, use);
        if (uri != LabURI.Empty || owner == null)
        {
            return uri;
        }

        if (!_uris.TryGetValue(-1, out uri) || uri == LabURI.Empty)
        {
            Log.WriteLine($"A part of {owner.Name} has no material, it's drawn with a plain one", Log.LogType.Warning);
            uri = CreatePlaceholder(owner, "No material");
            _uris[-1] = uri;
        }

        return uri;
    }

    /// <summary>
    /// The material asset of the index, none when the file has no such material or the project no such asset
    /// </summary>
    public LabURI Get(Int32 index, TlmMaterialUse use = TlmMaterialUse.Rigid)
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
                uri = AddToProject(owner, material, use);
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
    // The render bucket the game's own materials of the kind are in: opaque ones of a level in 2, of the global packages in 3, blended
    // ones in 16 (unlit) or 14
    private static UInt32 RenderBucket(IAsset owner, LabShader shader)
    {
        if (shader.ABlending == TwinShader.AlphaBlending.ON)
        {
            return shader.ShaderType == TwinShader.Type.StandardUnlit ? 16u : 14u;
        }

        var project = Locator.Current.GetService<ProjectManager>()?.OpenedProject as Project.Project;
        var isGlobal = project != null && (owner.Package == project.GlobalPackagePS2.URI || owner.Package == project.GlobalPackageXbox.URI);
        return isGlobal ? 3u : 2u;
    }

    private LabURI AddToProject(IAsset owner, JsonObject entry, TlmMaterialUse use)
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
                ? Dispatcher.UIThread.Invoke(() => CreateMaterial(owner, name, png, entry, blenderId, use))
                : CreateMaterial(owner, name, png, entry, blenderId, use);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException or IOException)
        {
            Log.WriteLine($"Couldn't add {name} made in Blender to the project, {owner.Name} draws it with a plain material: {exception.Message}", Log.LogType.Warning);
            AddedToProject = false;
            return CreatePlaceholder(owner, name);
        }
    }

    private static LabURI CreateMaterial(IAsset owner, string name, Byte[] png, JsonObject entry, string? blenderId, TlmMaterialUse use)
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
                    var size = data.Bitmap!.PixelSize;
                    // Blender's images come in any size, the game's textures are powers of two of at most 256
                    data = data.ResizedForTheGame();
                    if (data.Bitmap!.PixelSize != size)
                    {
                        Log.WriteLine($"Resized the {size.Width}x{size.Height} image of {name} made in Blender to {data.Bitmap.PixelSize.Width}x{data.Bitmap.PixelSize.Height}, the biggest the game takes is {TextureData.MaxGameSize}x{TextureData.MaxGameSize}");
                    }

                    // Textures of the sizes the game's tools laid out get a palette and smaller versions for the distance, like the
                    // game's small textures, the rest are stored with every color like its big ones
                    var palette = PS2AnyTexture.HasPaletteLayout(data.Bitmap.PixelSize.Width, data.Bitmap.PixelSize.Height);
                    var textureAsset = (Texture)asset;
                    textureAsset.PixelFormat = palette ? ITwinTexture.TexturePixelFormat.PSMT8 : ITwinTexture.TexturePixelFormat.PSMCT32;
                    textureAsset.TextureFunction = ITwinTexture.TextureFunction.MODULATE;
                    textureAsset.GenerateMipmaps = palette;
                    asset.SetData(data);
                    return AssetCreationStatus.Success;
                });
        }

        var material = AssetFactory.CreateAsset(typeof(Material), TypeFolder(packageFolder, typeof(Material)), UniqueName<Material>(owner.Package, name), string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<Material>(), asset =>
            {
                // Skins are drawn by the skinned shader only, a rigid one fed a skin's packets hung the game
                var shader = new LabShader
                {
                    ShaderType = use == TlmMaterialUse.Skin ? TwinShader.Type.LitSkinnedModel : TwinShader.Type.StandardUnlit,
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

                asset.SetData(new MaterialData(asset) { Name = name, Shaders = [shader], ActivatedShaders = Enum.Parse<AppliedShaders>(shader.ShaderType.ToString()), DmaChainIndex = RenderBucket(owner, shader) });
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
