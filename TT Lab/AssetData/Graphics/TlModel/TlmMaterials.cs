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

    /// <summary>
    /// Parameter of the materials TT Lab made out of Blender's, the kind of part it was made for: a Blender material a rigid part and a
    /// skin share becomes one material of each
    /// </summary>
    public const string BlenderMaterialUseParameter = "BlenderMaterialUse";

    /// <summary>
    /// Parameter of the textures TT Lab made of pictures of materials made in Blender, the add-on's ID of the picture: the add-on finds
    /// the texture by it, and the picture changed in Blender changes the texture
    /// </summary>
    public const string BlenderImageParameter = "BlenderImage";

    private readonly Dictionary<LabURI, Int32> _indexes = new();
    // The file's materials whose settings were taken, a rigid part and a skin can share one
    private readonly HashSet<Int32> _taken = [];
    // A material of the file can be a rigid part's and a skin's, which can't be the same material of the project
    private readonly Dictionary<(Int32 Index, TlmMaterialUse Use), LabURI> _uris = new();

    /// <summary>
    /// Whether materials made in Blender became the project's or the project's materials took the settings the file brings from
    /// Blender: the file has to be written again, referring to them without the settings
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

        if (!_uris.TryGetValue((-1, use), out uri) || uri == LabURI.Empty)
        {
            Log.WriteLine($"A part of {owner.Name} has no material, it's drawn with a plain one", Log.LogType.Warning);
            uri = CreatePlaceholder(owner, "No material", use);
            _uris[(-1, use)] = uri;
        }

        return uri;
    }

    /// <summary>
    /// The material asset of the index, none when the file has no such material or the project no such asset
    /// </summary>
    public LabURI Get(Int32 index, TlmMaterialUse use = TlmMaterialUse.Rigid)
    {
        if (_uris.TryGetValue((index, use), out var uri))
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
                // What was changed in Blender comes with the material's settings
                if (material["data"] is JsonObject settings && owner != null && !AssetManager.Get().IsCreating && _taken.Add(index))
                {
                    TakeSettings(owner, uri, settings, material);
                }

                // Creating a project reads the files of skins shared by several models back while their materials get written
                if (use == TlmMaterialUse.Skin && owner != null && !AssetManager.Get().IsCreating && !DrawsSkins(AssetManager.Get().GetAssetData<MaterialData>(uri)))
                {
                    Log.WriteLine($"{owner.Name}'s skin is drawn with {AssetManager.Get().GetAsset(uri).Alias}, whose first shader isn't {TwinShader.Type.LitSkinnedModel}: {SkinShaderRisk}",
                        Log.LogType.Warning);
                }
            }
            else if (string.IsNullOrEmpty(text) && owner != null)
            {
                uri = AddToProject(owner, material, use);
            }
            else
            {
                var name = material.GetString("name") ?? text ?? $"Material {index}";
                Log.WriteLine($"The project has no material {text ?? name}{(owner != null ? $", {owner.Name} draws it with a plain one" : string.Empty)}", Log.LogType.Warning);
                uri = owner != null ? CreatePlaceholder(owner, name, use) : LabURI.Empty;
            }
        }

        _uris.Add((index, use), uri);
        return uri;
    }

    // The game draws a skin's parts with their materials' VU1 programs (SetSkinDMA): every PAL and Xbox skin and blend skin has a
    // LitSkinnedModel material, NTSC's beach has one StandardLit part (Cortex's, which PAL made LitSkinnedModel) and a skin of TT Lab
    // with a StandardUnlit one hung the game
    private const string SkinShaderRisk = "every PAL and Xbox skin's material starts with it, and a skin drawn with a StandardUnlit one hung the game";

    /// <summary>
    /// Whether the material starts with the skinned shader, like the materials of every skin and blend skin of the PAL and Xbox versions
    /// </summary>
    public static Boolean DrawsSkins(MaterialData material) => material.Shaders.FirstOrDefault()?.ShaderType == TwinShader.Type.LitSkinnedModel;

    /// <summary>
    /// Warns about a skin or blend skin drawn with a material of another shader, which the retail data only has once
    /// </summary>
    public static void CheckDrawsSkins(IAsset skin, IEnumerable<LabURI> materials)
    {
        var assetManager = AssetManager.Get();
        foreach (var material in materials.Distinct())
        {
            var data = assetManager.GetAssetData<MaterialData>(material);
            if (!DrawsSkins(data))
            {
                Log.WriteLine($"{skin.Alias} has a part drawn with {assetManager.GetAsset(material).Alias}, whose first shader is " +
                              $"{data.Shaders.FirstOrDefault()?.ShaderType.ToString() ?? "none"} and not {TwinShader.Type.LitSkinnedModel}: {SkinShaderRisk}", Log.LogType.Warning);
            }
        }
    }

    // Made again whenever the owner loads, like the owner's other internal assets
    private static LabURI CreatePlaceholder(IAsset owner, string name, TlmMaterialUse use)
    {
        var skin = use == TlmMaterialUse.Skin;
        var material = new Material
        {
            Package = owner.Package,
            InvariantName = $"{owner.Name}_{RigidModelData.SanitizeName(name)}{(skin ? "_Skin" : string.Empty)}",
            Alias = name,
            IsInternal = true,
            InternalOwner = owner
        };
        var data = new MaterialData(material) { Name = name };
        if (skin)
        {
            data.Shaders[0].ShaderType = TwinShader.Type.LitSkinnedModel;
            data.ActivatedShaders = AppliedShaders.LitSkinnedModel;
        }

        material.SetData(data);
        AssetManager.Get().TryAddAsset(material);
        return material.URI;
    }

    // A material made in Blender becomes a material of the owner's package, drawn the way the game draws most of its textured
    // models, with a texture of its image. Blender keeps exporting it until the model gets imported again, the material made for
    // it the first time is used again: one for skins and one for rigid parts when it's on both, the skinned shader only takes skins
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
        var existing = blenderId == null ? null : assetManager.GetAllAssetsOf<Material>().FirstOrDefault(material => !material.IsInternal && IsMadeOf(material, blenderId, use));
        AddedToProject = true;
        if (existing != null)
        {
            if (entry["data"] is JsonObject settings)
            {
                TakeSettings(owner, existing.URI, settings, entry);
            }

            return existing.URI;
        }

        var name = entry.GetString("name") ?? "Blender material";
        try
        {
            var uri = OnUiThread(() => CreateMaterial(owner, name, entry, blenderId, use));
            if (use == TlmMaterialUse.Skin && !DrawsSkins(AssetManager.Get().GetAssetData<MaterialData>(uri)))
            {
                Log.WriteLine($"{owner.Name}'s skin is drawn with {name} made in Blender, whose first shader isn't {TwinShader.Type.LitSkinnedModel}: {SkinShaderRisk}", Log.LogType.Warning);
            }

            return uri;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException or IOException or Newtonsoft.Json.JsonException)
        {
            Log.WriteLine($"Couldn't add {name} made in Blender to the project, {owner.Name} draws it with a plain material: {exception.Message}", Log.LogType.Warning);
            AddedToProject = false;
            return CreatePlaceholder(owner, name, use);
        }
    }

    // Made for the kind of part, by the shader it was given before the kind was kept
    private static Boolean IsMadeOf(Material material, string blenderId, TlmMaterialUse use)
    {
        if (!material.Parameters.TryGetValue(BlenderMaterialParameter, out var id) || id?.ToString() != blenderId)
        {
            return false;
        }

        return material.Parameters.TryGetValue(BlenderMaterialUseParameter, out var kind) && kind != null
            ? kind.ToString() == use.ToString()
            : DrawsSkins(((IAsset)material).GetData<MaterialData>()) == (use == TlmMaterialUse.Skin);
    }

    private static T OnUiThread<T>(Func<T> action)
    {
        return Application.Current != null && !Dispatcher.UIThread.CheckAccess() ? Dispatcher.UIThread.Invoke(action) : action();
    }

    // Chunks are built in parallel, two model files can bring the same material's settings
    private static readonly Object SettingsLock = new();

    /// <summary>
    /// Gives a material of the project the settings the file brings from Blender, the shaders' pictures made into textures, and saves it.
    /// The data is the one of the current data scope, a chunk being built reads the file with what it brings
    /// </summary>
    private void TakeSettings(IAsset owner, LabURI uri, JsonObject settings, JsonObject entry)
    {
        var asset = AssetManager.Get().GetAsset(uri);
        try
        {
            var json = WithTextures(owner, settings, entry, asset.Package, asset.Alias);
            lock (SettingsLock)
            {
                var data = asset.GetData<MaterialData>();
                data.PopulateFrom(json.ToJsonString());
                asset.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData | SerializationFlags.FixReferences);
            }

            AddedToProject = true;
            Log.WriteLine($"{asset.Alias} took the settings {owner.Name}'s file brings from Blender");
        }
        catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException or IOException or Newtonsoft.Json.JsonException)
        {
            Log.WriteLine($"{asset.Alias} couldn't take the settings {owner.Name}'s file brings from Blender: {exception.Message}", Log.LogType.Warning);
        }
    }

    // The settings with a texture of the project in place of each picture the shaders draw (their "Image", an index into the entry's
    // "images"), and the package's placeholder texture in place of textures the project doesn't have
    private JsonObject WithTextures(IAsset owner, JsonObject settings, JsonObject entry, LabURI package, string material)
    {
        var copy = (JsonObject)settings.DeepClone();
        var images = entry["images"] as JsonArray;
        var textures = new Dictionary<Int32, LabURI>();
        foreach (var shader in (copy["Shaders"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (shader["Image"] is JsonValue picture && picture.TryGetValue<Int32>(out var index))
            {
                shader.Remove("Image");
                if (!textures.TryGetValue(index, out var texture))
                {
                    texture = images != null && index >= 0 && index < images.Count && images[index] is JsonObject image ? TextureOf(owner, image) : LabURI.Empty;
                    textures.Add(index, texture);
                }

                shader["TextureId"] = new JsonObject { ["_uri"] = texture.ToString() };
                continue;
            }

            var linked = (shader["TextureId"] as JsonObject)?.GetString("_uri");
            if (!string.IsNullOrEmpty(linked) && new LabURI(linked) != LabURI.Empty && !AssetManager.Get().DoesAssetExist(new LabURI(linked)))
            {
                Log.WriteLine($"{material} from Blender draws {linked}, which the project doesn't have: it draws the placeholder texture", Log.LogType.Warning);
                shader["TextureId"] = new JsonObject { ["_uri"] = OnUiThread(() => PlaceholderAssets.GetOrCreate(typeof(Texture), package)).ToString() };
            }
        }

        return copy;
    }

    // The texture made of the picture before, with the picture's pixels when they were changed in Blender, or a new one. Found or made
    // on the UI thread, which makes assets, so two chunks reading files of the same picture don't both make one
    private LabURI TextureOf(IAsset owner, JsonObject image)
    {
        var blenderImage = image.GetString("blender_id");
        var png = file.Read<Byte>(image["png"]);
        var name = image.GetString("name") ?? "Blender picture";
        if (png.Length == 0)
        {
            return FindTexture(blenderImage)?.URI ?? LabURI.Empty;
        }

        var (texture, made) = OnUiThread(() => FindTexture(blenderImage) is { } found ? (found, false) : (CreateTexture(owner, name, png, blenderImage), true));
        if (made)
        {
            return texture.URI;
        }

        var data = GameTexture(texture, name, png);
        lock (SettingsLock)
        {
            var current = ((IAsset)texture).GetData<TextureData>();
            if (data.Bitmap!.PixelSize != current.Bitmap?.PixelSize || !data.GetPixels().AsSpan().SequenceEqual(current.GetPixels()))
            {
                texture.UseGameLayout(data.Bitmap.PixelSize.Width, data.Bitmap.PixelSize.Height);
                texture.SetData(data);
                texture.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
                Log.WriteLine($"{texture.Alias} took the pixels of {name} changed in Blender");
            }
        }

        return texture.URI;
    }

    private static Texture? FindTexture(string? blenderImage)
    {
        return blenderImage == null ? null : AssetManager.Get().GetAllAssetsOf<Texture>()
            .FirstOrDefault(texture => !texture.IsInternal && texture.Parameters.TryGetValue(BlenderImageParameter, out var id) && id?.ToString() == blenderImage);
    }

    // Blender's images come in any size, the game's textures are powers of two of at most 256
    private static TextureData GameTexture(IAsset texture, string name, Byte[] png)
    {
        using var stream = new MemoryStream(png);
        var data = TextureData.FromPng(texture, stream);
        var size = data.Bitmap!.PixelSize;
        data = data.ResizedForTheGame();
        if (data.Bitmap!.PixelSize != size)
        {
            Log.WriteLine($"Resized the {size.Width}x{size.Height} image of {name} made in Blender to {data.Bitmap.PixelSize.Width}x{data.Bitmap.PixelSize.Height}, the biggest the game takes is {TextureData.MaxGameSize}x{TextureData.MaxGameSize}");
        }

        return data;
    }

    private static Texture CreateTexture(IAsset owner, string name, Byte[] png, string? blenderImage)
    {
        var packageFolder = AssetManager.Get().GetAsset<Package>(owner.Package).GetPackageFolder();
        return (Texture)AssetFactory.CreateAsset(typeof(Texture), TypeFolder(packageFolder, typeof(Texture)), UniqueName<Texture>(owner.Package, name), string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<Texture>(), asset =>
            {
                var data = GameTexture(asset, name, png);
                ((Texture)asset).UseGameLayout(data.Bitmap!.PixelSize.Width, data.Bitmap.PixelSize.Height);
                asset.SetData(data);
                if (blenderImage != null)
                {
                    asset.Parameters[BlenderImageParameter] = blenderImage;
                }

                return AssetCreationStatus.Success;
            })!;
    }

    private LabURI CreateMaterial(IAsset owner, string name, JsonObject entry, string? blenderId, TlmMaterialUse use)
    {
        var packageFolder = AssetManager.Get().GetAsset<Package>(owner.Package).GetPackageFolder();
        // The settings it has in Blender, what TT Lab gives a material of its kind for an add-on without them
        var settings = entry["data"] is JsonObject data ? WithTextures(owner, data, entry, owner.Package, name) : null;

        Texture? texture = null;
        var png = file.Read<Byte>(entry["image"] is JsonObject image ? image["png"] : null);
        if (settings == null && png.Length > 0)
        {
            texture = CreateTexture(owner, name, png, null);
        }

        var material = AssetFactory.CreateAsset(typeof(Material), TypeFolder(packageFolder, typeof(Material)), UniqueName<Material>(owner.Package, name), string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<Material>(), asset =>
            {
                if (settings != null)
                {
                    var materialData = new MaterialData(asset);
                    materialData.PopulateFrom(settings.ToJsonString());
                    asset.SetData(materialData);
                }
                else
                {
                    asset.SetData(DefaultMaterial(asset, owner, name, entry, texture, use));
                }

                if (blenderId != null)
                {
                    asset.Parameters[BlenderMaterialParameter] = blenderId;
                    asset.Parameters[BlenderMaterialUseParameter] = use.ToString();
                }

                return AssetCreationStatus.Success;
            })!;
        Log.WriteLine($"Added {name} made in Blender to the project as {material.URI}");
        return material.URI;
    }

    // Skins get the skinned shader like the game's, a rigid one fed a skin's packets hung the game. Shadows fall on rigid parts like on the
    // game's scenery, skins keep them off like the characters that cast them (their own would darken them)
    private static MaterialData DefaultMaterial(IAsset asset, IAsset owner, string name, JsonObject entry, Texture? texture, TlmMaterialUse use)
    {
        var shader = new LabShader
        {
            ShaderType = use == TlmMaterialUse.Skin ? TwinShader.Type.LitSkinnedModel : TwinShader.Type.StandardUnlit,
            TxtMapping = texture != null ? TwinShader.TextureMapping.ON : TwinShader.TextureMapping.OFF,
            TextureId = texture?.URI ?? LabURI.Empty,
            AlphaCorrectionValue = use != TlmMaterialUse.Skin
        };
        if (entry.GetString("alpha") == "BLEND")
        {
            shader.ABlending = TwinShader.AlphaBlending.ON;
        }
        else if (entry.GetString("alpha") == "CLIP")
        {
            // The GS compares the alpha's byte, 128 is opaque, the way the game's cut-outs do
            shader.ATest = TwinShader.AlphaTest.ON;
            shader.ATestMethod = TwinShader.AlphaTestMethod.GEQUAL;
            shader.AlphaValueToBeComparedTo = (Byte)Math.Clamp(Math.Round(entry.GetFloat("alpha_cutoff", 0.5f) * 128.0f), 0, 255);
        }

        return new MaterialData(asset) { Name = name, Shaders = [shader], ActivatedShaders = Enum.Parse<AppliedShaders>(shader.ShaderType.ToString()), DmaChainIndex = RenderBucket(owner, shader) };
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
