using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Threading;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Project;
using Splat;
using TT_Lab.Tools.Blender;
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
    /// Parameter of the materials TT Lab made out of Blender's, the add-on's ID of the Blender material: the model it was made for
    /// (<see cref="BlenderAssets.OwnerParameter"/>) finds it again by it
    /// </summary>
    public const string BlenderMaterialParameter = "BlenderMaterial";

    /// <summary>
    /// Parameter of the materials TT Lab made out of Blender's, the kind of part it was made for: a Blender material a rigid part and a
    /// skin shared (the add-on refuses that now) became one material of each
    /// </summary>
    public const string BlenderMaterialUseParameter = "BlenderMaterialUse";

    /// <summary>
    /// Parameter of the textures TT Lab made of pictures of Blender's, the add-on's ID of the picture: the model it was made for finds it
    /// again by it
    /// </summary>
    public const string BlenderImageParameter = "BlenderImage";

    /// <summary>
    /// The key at the top of the files the Blender add-on writes, which TT Lab's own never have: an export TT Lab didn't read yet
    /// </summary>
    public const string ExportedByKey = "exported_by";

    private readonly Dictionary<LabURI, Int32> _indexes = new();
    // The assets the file's settings and pictures were given to, a rigid part and a skin can share a material of the file
    private readonly HashSet<LabURI> _taken = [];
    // A material of the file can be a rigid part's and a skin's, which can't be the same material of the project
    private readonly Dictionary<(Int32 Index, TlmMaterialUse Use), LabURI> _uris = new();
    private readonly List<IAsset> _changed = [];

    /// <summary>
    /// Whether the file brought what TT Lab made or changed assets of: it has to be written again, referring to them by their URIs
    /// </summary>
    public Boolean AddedToProject { get; private set; }

    /// <summary>
    /// Index of the material in the file, -1 for none. A material made of Blender's comes with its Blender ID and its pictures' textures,
    /// the add-on learns their URIs from them
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
        var entry = new JsonObject
        {
            ["uri"] = material.ToString(),
            ["name"] = assetManager.DoesAssetExist(material) ? assetManager.GetAsset(material).Alias : material.ToString()
        };
        if (assetManager.DoesAssetExist(material) && assetManager.GetAsset(material) is { IsInternal: false } asset && !assetManager.IsCreating
            && asset.Parameters.TryGetValue(BlenderMaterialParameter, out var blenderId) && blenderId?.ToString() is { } id && !BlenderAssets.IsShared(id))
        {
            entry["blender_id"] = id;
            var images = new JsonArray();
            foreach (var texture in assetManager.GetAssetData<MaterialData>(material).Shaders.Select(shader => shader.TextureId).Distinct())
            {
                if (texture != LabURI.Empty && assetManager.DoesAssetExist(texture) && assetManager.GetAsset(texture).Parameters.TryGetValue(BlenderImageParameter, out var image)
                    && image != null)
                {
                    images.Add(new JsonObject { ["blender_id"] = image.ToString(), ["uri"] = texture.ToString() });
                }
            }

            if (images.Count > 0)
            {
                entry["images"] = images;
            }
        }

        file.Materials.Add(entry);
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
    /// The material asset of the index, none when the file has no such material or the project no such asset. What the file brings from
    /// Blender (the settings and pictures of its materials, materials made in Blender) the project's materials and textures take
    /// </summary>
    public LabURI Get(Int32 index, TlmMaterialUse use = TlmMaterialUse.Rigid)
    {
        if (_uris.TryGetValue((index, use), out var uri))
        {
            return uri;
        }

        uri = index >= 0 && index < file.Materials.Count && file.Materials[index] is JsonObject entry ? Resolve(entry, use, index) : LabURI.Empty;
        _uris.Add((index, use), uri);
        return uri;
    }

    private LabURI Resolve(JsonObject entry, TlmMaterialUse use, Int32 index)
    {
        var assetManager = AssetManager.Get();
        var text = entry.GetString("uri");
        var blenderId = entry.GetString("blender_id");
        var name = entry.GetString("name") ?? text ?? $"Material {index}";
        // A check that a file reads (a file another program changed) changes nothing, the read after it does
        var dry = AssetDataScope.IsReadingDry;
        if (!string.IsNullOrEmpty(text) && assetManager.DoesAssetExist(new LabURI(text)))
        {
            var uri = new LabURI(text);
            // Creating a project reads the files of skins shared by several models back while their materials get written
            if (owner != null && !assetManager.IsCreating && !dry && Brings(entry) && _taken.Add(uri))
            {
                Take(assetManager.GetAsset<Material>(uri), entry);
            }

            if (use == TlmMaterialUse.Skin && owner != null && !assetManager.IsCreating && !DrawsSkins(assetManager.GetAssetData<MaterialData>(uri)))
            {
                Log.WriteLine($"{owner.Name}'s skin is drawn with {assetManager.GetAsset(uri).Alias}, whose first shader isn't {TwinShader.Type.LitSkinnedModel}: {SkinShaderRisk}",
                    Log.LogType.Warning);
            }

            return uri;
        }

        if (owner == null)
        {
            Log.WriteLine($"The project has no material {text ?? name}", Log.LogType.Warning);
            return LabURI.Empty;
        }

        // Made in Blender, or made of Blender's and deleted in TT Lab since
        if (blenderId != null || string.IsNullOrEmpty(text))
        {
            var found = blenderId == null ? null : BlenderAssets.FindMaterial(owner, blenderId, use, !dry);
            if (found != null)
            {
                if (!dry && Brings(entry) && _taken.Add(found.URI))
                {
                    Take(found, entry);
                }

                return found.URI;
            }

            return dry ? CreatePlaceholder(owner, name, use) : Make(entry, name, blenderId, use);
        }

        Log.WriteLine($"The project has no material {text}, {owner.Name} draws it with a plain one", Log.LogType.Warning);
        return CreatePlaceholder(owner, name, use);
    }

    // The settings or pictures a material of the file brings from Blender
    private static Boolean Brings(JsonObject entry)
    {
        return entry["data"] is JsonObject || entry["image"] is JsonObject || entry["images"] is JsonArray images && images.OfType<JsonObject>().Any(image => image["png"] != null);
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

    /// <summary>
    /// Once the file is read: a Blender export's model loses the materials and pictures made in Blender for it that the export doesn't use
    /// any more (once nothing else uses them either), and the editors showing what the file changed show it. A build reads files with
    /// nothing to spare, the next read after it does that
    /// </summary>
    /// <returns>Whether the file has to be written again, referring to what it brought by URI: the add-on finds what TT Lab took of the file
    /// that way</returns>
    public Boolean Finish()
    {
        if (AssetDataScope.IsReadingDry)
        {
            return false;
        }

        var assetManager = AssetManager.Get();
        if (owner != null && file.Json[ExportedByKey] != null && !assetManager.IsCreating && !assetManager.IsBuilding)
        {
            var used = _uris.Values.Where(uri => uri != LabURI.Empty).ToHashSet();
            foreach (var material in used.ToList())
            {
                if (assetManager.DoesAssetExist(material) && assetManager.GetAsset(material) is Material { IsInternal: false })
                {
                    used.UnionWith(assetManager.GetAssetData<MaterialData>(material).Shaders.Select(shader => shader.TextureId));
                }
            }

            BlenderAssets.DeleteUnused(owner, used);
        }

        BlenderAssets.TellEditors(_changed.Distinct().ToList());
        return AddedToProject;
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

    // The project's material takes what Blender changed of its settings and the pictures Blender painted, in place: every model drawing it
    // shows them. An earlier add-on's material of a picture without settings only brings the picture
    private void Take(Material material, JsonObject entry)
    {
        try
        {
            if (entry["data"] is JsonObject settings)
            {
                var resolved = WithTextures(settings, entry, material.Package, material.Alias);
                if (BlenderAssets.ApplySettings(material, resolved, entry["changed"] as JsonArray, entry["shader_sources"] as JsonArray))
                {
                    _changed.Add(material);
                }
            }
            else if (entry["image"] is JsonObject image && file.Read<Byte>(image["png"]) is { Length: > 0 } png && owner != null)
            {
                var texture = AssetManager.Get().GetAssetData<MaterialData>(material.URI).Shaders.FirstOrDefault()?.TextureId ?? LabURI.Empty;
                if (texture != LabURI.Empty && AssetManager.Get().DoesAssetExist(texture) && AssetManager.Get().GetAsset(texture) is Texture picture
                    && BlenderAssets.IsOwnedBy(picture, owner) && BlenderAssets.ReplacePicture(picture, image.GetString("name") ?? material.Alias, png))
                {
                    _changed.Add(picture);
                }
            }

            AddedToProject = true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException or IOException or Newtonsoft.Json.JsonException)
        {
            Log.WriteLine($"{material.Alias} couldn't take what {owner?.Name}'s file brings from Blender: {exception.Message}", Log.LogType.Warning);
        }
    }

    // A material made in Blender becomes a material of the owner's package: with the add-on's settings, or drawn the way the game draws most
    // of its textured models with a texture of its image (an earlier add-on's)
    private LabURI Make(JsonObject entry, string name, string? blenderId, TlmMaterialUse use)
    {
        try
        {
            Material material;
            if (entry["data"] is JsonObject settings)
            {
                var resolved = WithTextures(settings, entry, owner!.Package, name);
                material = BlenderAssets.CreateMaterial(owner, name, asset =>
                {
                    var data = new MaterialData(asset);
                    data.PopulateFrom(resolved.ToJsonString());
                    return data;
                }, blenderId, use);
            }
            else
            {
                var image = entry["image"] as JsonObject;
                var png = file.Read<Byte>(image?["png"]);
                var texture = png.Length > 0 ? BlenderAssets.CreateTexture(owner!, image?.GetString("name") ?? name, png, null, blenderId == null || !BlenderAssets.IsShared(blenderId)) : null;
                material = BlenderAssets.CreateMaterial(owner!, name, asset => DefaultMaterial(asset, owner!, name, entry, texture?.URI ?? LabURI.Empty, use), blenderId, use);
            }

            AddedToProject = true;
            _taken.Add(material.URI);
            if (use == TlmMaterialUse.Skin && !DrawsSkins(AssetManager.Get().GetAssetData<MaterialData>(material.URI)))
            {
                Log.WriteLine($"{owner!.Name}'s skin is drawn with {name} made in Blender, whose first shader isn't {TwinShader.Type.LitSkinnedModel}: {SkinShaderRisk}", Log.LogType.Warning);
            }

            return material.URI;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException or IOException or Newtonsoft.Json.JsonException)
        {
            Log.WriteLine($"Couldn't add {name} made in Blender to the project, {owner!.Name} draws it with a plain material: {exception.Message}", Log.LogType.Warning);
            return CreatePlaceholder(owner, name, use);
        }
    }

    // The settings with a texture of the project in place of each picture the shaders draw (their "Image", an index into the entry's
    // "images"), and the package's placeholder texture in place of textures the project doesn't have
    private JsonObject WithTextures(JsonObject settings, JsonObject entry, LabURI package, string material)
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
                    texture = images != null && index >= 0 && index < images.Count && images[index] is JsonObject image ? TextureOf(image, package, material) : LabURI.Empty;
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

    // A picture of the project's texture (painted in Blender) goes into it, one made in Blender into the texture made of it for the model,
    // a new one makes it
    private LabURI TextureOf(JsonObject image, LabURI package, string material)
    {
        var assetManager = AssetManager.Get();
        var uri = image.GetString("uri");
        var blenderId = image.GetString("blender_id");
        var name = image.GetString("name") ?? "Blender picture";
        var png = file.Read<Byte>(image["png"]);
        var texture = !string.IsNullOrEmpty(uri) && assetManager.DoesAssetExist(new LabURI(uri)) && assetManager.GetAsset(new LabURI(uri)) is Texture projectTexture ? projectTexture
            : blenderId != null ? BlenderAssets.FindTexture(owner!, blenderId)
            : null;
        if (texture != null)
        {
            if (png.Length > 0 && _taken.Add(texture.URI) && BlenderAssets.ReplacePicture(texture, name, png))
            {
                _changed.Add(texture);
            }

            return texture.URI;
        }

        if (png.Length == 0)
        {
            Log.WriteLine($"{material} from Blender draws the picture {name}, which the project has no texture of: it draws the placeholder texture", Log.LogType.Warning);
            return OnUiThread(() => PlaceholderAssets.GetOrCreate(typeof(Texture), package));
        }

        AddedToProject = true;
        var made = BlenderAssets.CreateTexture(owner!, name, png, blenderId, blenderId == null || !BlenderAssets.IsShared(blenderId));
        _taken.Add(made.URI);
        return made.URI;
    }

    private static T OnUiThread<T>(Func<T> action)
    {
        return Application.Current != null && !Dispatcher.UIThread.CheckAccess() ? Dispatcher.UIThread.Invoke(action) : action();
    }

    // Skins get the skinned shader like the game's, a rigid one fed a skin's packets hung the game. Shadows fall on rigid parts like on the
    // game's scenery, skins keep them off like the characters that cast them (their own would darken them)
    private static MaterialData DefaultMaterial(IAsset asset, IAsset owner, string name, JsonObject entry, LabURI texture, TlmMaterialUse use)
    {
        var shader = new LabShader
        {
            ShaderType = use == TlmMaterialUse.Skin ? TwinShader.Type.LitSkinnedModel : TwinShader.Type.StandardUnlit,
            TxtMapping = texture != LabURI.Empty ? TwinShader.TextureMapping.ON : TwinShader.TextureMapping.OFF,
            TextureId = texture,
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
}
