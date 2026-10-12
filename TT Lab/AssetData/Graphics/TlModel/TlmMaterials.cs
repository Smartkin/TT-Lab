using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    /// Parameter of the textures TT Lab made of pictures of materials made in Blender, the add-on's ID of the picture: the picture finds the
    /// texture again by it while it makes the same (<see cref="BlenderImageHashParameter"/>, <see cref="BlenderImagePngParameter"/>)
    /// </summary>
    public const string BlenderImageParameter = "BlenderImage";

    /// <summary>
    /// Parameter of the textures TT Lab made of pictures made in Blender, the ID the build gives what the picture makes (the hash of its
    /// data): a picture of the same ID only finds the texture again while it makes the same or what the texture is now. A blend file copied
    /// keeps its pictures' IDs, and the copy's picture of another file is another texture
    /// </summary>
    public const string BlenderImageHashParameter = "BlenderImageHash";

    /// <summary>
    /// Parameter of the textures TT Lab made of pictures made in Blender, the SHA-256 of the PNG of the picture they were made of: the
    /// add-on names the texture instead of sending the picture only while the picture's PNG is that one, it can't work out the build's hash
    /// </summary>
    public const string BlenderImagePngParameter = "BlenderImagePng";

    /// <summary>
    /// Parameter of the materials TT Lab made out of Blender's, the ID the build gives what the Blender material makes (the hash of its data,
    /// its pictures by theirs): a Blender material of the same ID only finds the material again while it makes the same or what the material
    /// is now. A blend file copied keeps its materials' IDs, and the copy's material of another picture is another material
    /// </summary>
    public const string BlenderMaterialHashParameter = "BlenderMaterialHash";

    // The picture of a material made in Blender without the add-on's settings, which only becomes a texture with the material
    private static readonly LabURI MadePicture = new("res://TTLab/BlenderPicture");

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
                    var asset = AssetManager.Get().GetAsset(uri);
                    TakeSettings(owner, asset, () => WithTextures(owner, settings, material, asset.Package, asset.Alias));
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

    // What a material of the file made in Blender makes: the add-on's settings with the project's textures in their pictures' place, or
    // the picture of one without them, and the ID the build gives that
    private sealed class MadeInBlender(JsonObject? settings, TextureData? picture, UInt32 hash) : IDisposable
    {
        public JsonObject? Settings { get; } = settings;
        public TextureData? Picture { get; } = picture;
        public UInt32 Hash { get; } = hash;

        public void Dispose() => Picture?.Dispose();
    }

    // A material made in Blender becomes a material of the owner's package, drawn the way the game draws most of its textured
    // models, with a texture of its image. Blender keeps exporting it until the model gets imported again, the material made for
    // it the first time is used again while the Blender material makes the same: one for skins and one for rigid parts when it's
    // on both, the skinned shader only takes skins. It's told by the ID the build gives what it makes: a blend file copied keeps its
    // materials' IDs, and the copy's material drawing another picture got the material the first file's export made
    private LabURI AddToProject(IAsset owner, JsonObject entry, TlmMaterialUse use)
    {
        var blenderId = entry.GetString("blender_id");
        var name = entry.GetString("name") ?? "Blender material";
        AddedToProject = true;
        MadeInBlender? made = null;
        try
        {
            made = Make(owner, name, entry, use);
            var existing = blenderId == null ? null : MadeOf(MaterialsOf(blenderId, use), BlenderMaterialHashParameter, made.Hash, MaterialHashNow);
            if (existing != null)
            {
                Record(existing, (BlenderMaterialHashParameter, Hex(made.Hash)));
                if (made.Settings is { } settings)
                {
                    TakeSettings(owner, existing, () => settings);
                }

                return existing.URI;
            }

            // Two chunks built at once can read files of the same material
            var making = made;
            var uri = OnUiThread(() => (blenderId == null ? null : MaterialsOf(blenderId, use).FirstOrDefault(material => IsRecorded(material, BlenderMaterialHashParameter, making.Hash)))?.URI
                ?? CreateMaterial(owner, name, entry, making, blenderId, use));
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
        finally
        {
            made?.Dispose();
        }
    }

    private MadeInBlender Make(IAsset owner, string name, JsonObject entry, TlmMaterialUse use)
    {
        if (entry["data"] is JsonObject data)
        {
            var settings = WithTextures(owner, data, entry, owner.Package, name);
            var material = new MaterialData(owner);
            material.PopulateFrom(settings.ToJsonString());
            return new MadeInBlender(settings, null, BuildHash(material, string.Empty, TextureHash));
        }

        var png = file.Read<Byte>(entry["image"] is JsonObject image ? image["png"] : null);
        var picture = png.Length > 0 ? GameTexture(owner, name, png) : null;
        var pictureHash = picture != null ? SerializableAsset.HashOf(picture.GetStringified()) : 0;
        var made = DefaultMaterial(owner, owner, name, entry, picture != null ? MadePicture : LabURI.Empty, use);
        return new MadeInBlender(null, picture, BuildHash(made, string.Empty, texture => texture == MadePicture ? pictureHash : TextureHash(texture)));
    }

    // The ID the build gives the material (SerializableAsset.GetDataHash, its textures by theirs), worked out of the data: the build keeps
    // the hash it works out first, of the data file's bytes while the data isn't loaded
    private static UInt32 BuildHash(MaterialData material, string salt, Func<LabURI, UInt32> textureHash)
    {
        return SerializableAsset.HashOf(material.GetStringified(textureHash) + salt);
    }

    private static UInt32 TextureHash(LabURI uri)
    {
        var assetManager = AssetManager.Get();
        if (!assetManager.DoesAssetExist(uri))
        {
            return 0;
        }

        var texture = assetManager.GetAsset(uri);
        return SerializableAsset.HashOf(texture.GetData<AbstractAssetData>().GetStringified() + texture.HashSalt);
    }

    // The asset a Blender material or picture made before that's made of the same: of what it was made of (kept with it, however it was
    // changed in TT Lab since) or of what it is now (made before what it was made of was kept)
    private static T? MadeOf<T>(List<T> candidates, string parameter, UInt32 hash, Func<T, UInt32?> now) where T : class, IAsset
    {
        return candidates.FirstOrDefault(asset => IsRecorded(asset, parameter, hash)) ?? candidates.FirstOrDefault(asset => now(asset) == hash);
    }

    private static Boolean IsRecorded(IAsset asset, string parameter, UInt32 hash) => IsRecorded(asset, parameter, Hex(hash));

    private static Boolean IsRecorded(IAsset asset, string parameter, string value)
    {
        return asset.Parameters.TryGetValue(parameter, out var recorded) && recorded?.ToString() == value;
    }

    private static string Hex(UInt32 hash) => hash.ToString("X8");

    // What the Blender material or picture makes now, the next export of it finds the asset by it
    private static void Record(IAsset asset, params (string Parameter, string Value)[] values)
    {
        if (values.All(value => IsRecorded(asset, value.Parameter, value.Value)))
        {
            return;
        }

        OnUiThread(() =>
        {
            foreach (var (parameter, value) in values)
            {
                asset.Parameters[parameter] = value;
            }

            asset.Serialize();
            return true;
        });
    }

    private static List<Material> MaterialsOf(string blenderId, TlmMaterialUse use)
    {
        return AssetManager.Get().GetAllAssetsOf<Material>().Where(material => !material.IsInternal && IsMadeOf(material, blenderId, use)).ToList();
    }

    private static UInt32? MaterialHashNow(Material material)
    {
        try
        {
            return BuildHash(((IAsset)material).GetData<MaterialData>(), material.HashSalt, TextureHash);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException or IOException or Newtonsoft.Json.JsonException)
        {
            Log.WriteLine($"Couldn't tell what {material.Alias} made in Blender is made of: {exception.Message}", Log.LogType.Warning);
            return null;
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
    private void TakeSettings(IAsset owner, IAsset asset, Func<JsonObject> settings)
    {
        try
        {
            var json = settings();
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

    // The texture made of the picture before while it makes the same (MadeOf), else a new one: a blend file copied keeps its pictures' IDs,
    // and a picture changed in Blender is another texture, the one before stays for what draws it. Made on the UI thread, which makes
    // assets, so two chunks reading files of the same picture don't both make one
    private LabURI TextureOf(IAsset owner, JsonObject image)
    {
        var blenderImage = image.GetString("blender_id");
        var png = file.Read<Byte>(image["png"]);
        var name = image.GetString("name") ?? "Blender picture";
        if (png.Length == 0)
        {
            // Nothing to tell it by
            return blenderImage == null ? LabURI.Empty : TexturesOf(blenderImage).FirstOrDefault()?.URI ?? LabURI.Empty;
        }

        using var picture = GameTexture(owner, name, png);
        var hash = SerializableAsset.HashOf(picture.GetStringified());
        var bytes = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        var found = blenderImage == null ? null : MadeOf(TexturesOf(blenderImage), BlenderImageHashParameter, hash, TextureHashNow);
        if (found != null)
        {
            Record(found, (BlenderImageHashParameter, Hex(hash)), (BlenderImagePngParameter, bytes));
            return found.URI;
        }

        return OnUiThread(() =>
        {
            if (blenderImage != null && TexturesOf(blenderImage).FirstOrDefault(texture => IsRecorded(texture, BlenderImageHashParameter, hash)) is { } made)
            {
                return made.URI;
            }

            var texture = CreateTexture(owner, name, picture.GetPngBytes(),
                blenderImage == null ? [] : [(BlenderImageParameter, blenderImage), (BlenderImageHashParameter, Hex(hash)), (BlenderImagePngParameter, bytes)]);
            Log.WriteLine($"Added the picture {name} made in Blender to the project as {texture.URI}");
            return texture.URI;
        });
    }

    private static List<Texture> TexturesOf(string blenderImage)
    {
        return AssetManager.Get().GetAllAssetsOf<Texture>().Where(texture => !texture.IsInternal && IsRecorded(texture, BlenderImageParameter, blenderImage)).ToList();
    }

    private static UInt32? TextureHashNow(Texture texture)
    {
        try
        {
            return TextureHash(texture.URI);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException or IOException)
        {
            Log.WriteLine($"Couldn't tell what {texture.Alias} made in Blender is made of: {exception.Message}", Log.LogType.Warning);
            return null;
        }
    }

    // Blender's images come in any size, the game's textures are powers of two of at most 256
    private static TextureData GameTexture(IAsset texture, string name, Byte[] png)
    {
        using var stream = new MemoryStream(png);
        var data = TextureData.FromPng(texture, stream);
        var resized = data.ResizedForTheGame();
        if (resized != data)
        {
            var size = data.Bitmap!.PixelSize;
            Log.WriteLine($"Resized the {size.Width}x{size.Height} image of {name} made in Blender to {resized.Bitmap!.PixelSize.Width}x{resized.Bitmap.PixelSize.Height}, the biggest the game takes is {TextureData.MaxGameSize}x{TextureData.MaxGameSize}");
            data.Dispose();
        }

        return resized;
    }

    private static Texture CreateTexture(IAsset owner, string name, Byte[] png, params (string Parameter, string Value)[] parameters)
    {
        var packageFolder = AssetManager.Get().GetAsset<Package>(owner.Package).GetPackageFolder();
        return (Texture)AssetFactory.CreateAsset(typeof(Texture), TypeFolder(packageFolder, typeof(Texture)), UniqueName<Texture>(owner.Package, name), string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<Texture>(), asset =>
            {
                var data = GameTexture(asset, name, png);
                ((Texture)asset).UseGameLayout(data.Bitmap!.PixelSize.Width, data.Bitmap.PixelSize.Height);
                asset.SetData(data);
                foreach (var (parameter, value) in parameters)
                {
                    asset.Parameters[parameter] = value;
                }

                return AssetCreationStatus.Success;
            })!;
    }

    private LabURI CreateMaterial(IAsset owner, string name, JsonObject entry, MadeInBlender made, string? blenderId, TlmMaterialUse use)
    {
        var packageFolder = AssetManager.Get().GetAsset<Package>(owner.Package).GetPackageFolder();
        // A material of the add-on's settings has the textures of its pictures already
        var texture = made.Picture != null ? CreateTexture(owner, name, made.Picture.GetPngBytes()) : null;
        var material = AssetFactory.CreateAsset(typeof(Material), TypeFolder(packageFolder, typeof(Material)), UniqueName<Material>(owner.Package, name), string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<Material>(), asset =>
            {
                if (made.Settings != null)
                {
                    var materialData = new MaterialData(asset);
                    materialData.PopulateFrom(made.Settings.ToJsonString());
                    asset.SetData(materialData);
                }
                else
                {
                    asset.SetData(DefaultMaterial(asset, owner, name, entry, texture?.URI ?? LabURI.Empty, use));
                }

                if (blenderId != null)
                {
                    asset.Parameters[BlenderMaterialParameter] = blenderId;
                    asset.Parameters[BlenderMaterialUseParameter] = use.ToString();
                    asset.Parameters[BlenderMaterialHashParameter] = Hex(made.Hash);
                }

                return AssetCreationStatus.Success;
            })!;
        Log.WriteLine($"Added {name} made in Blender to the project as {material.URI}");
        return material.URI;
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
