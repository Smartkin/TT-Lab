using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Newtonsoft.Json;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// The materials and pictures made in Blender for a model: found again by their Blender IDs among the ones made for that model (a blend
/// file copied and exported over another model makes that model's own), changed in place, and deleted once the model's file doesn't use
/// them any more and nothing else does
/// </summary>
public static class BlenderAssets
{
    /// <summary>
    /// Parameter of the materials and textures made of Blender's, the URI of the model they were made for
    /// </summary>
    public const string OwnerParameter = "BlenderOwner";

    // The Blender IDs TT Lab gives the materials it makes itself (the placeholder shapes' checker, a new chunk's ground): every model's
    // related packages share them, no model owns them
    private static readonly string[] SharedIdPrefixes = ["TTLab_", "tt-lab-"];

    private static readonly string[] PictureExtensions = [".png", ".jpg", ".jpeg", ".tga", ".bmp", ".tif", ".tiff", ".exr", ".hdr", ".webp"];

    public static bool IsShared(string blenderId) => SharedIdPrefixes.Any(prefix => blenderId.StartsWith(prefix, StringComparison.Ordinal));

    public static bool IsMadeInBlender(IAsset asset) => asset.Parameters.ContainsKey(OwnerParameter);

    public static bool IsOwnedBy(IAsset asset, IAsset owner) => owner.URI != null && Is(asset, OwnerParameter, owner.URI.ToString());

    /// <summary>
    /// The material made of the Blender material for the model, for the kind of part (a Blender material a rigid part and a skin shared
    /// became one of each). One an add-on made before materials were kept per model, of the kind and in a package the model can draw,
    /// is the model's from now on (adopted, unless it's only looked for)
    /// </summary>
    public static Material? FindMaterial(IAsset owner, string blenderId, TlmMaterialUse use, bool adopt = true)
    {
        var assetManager = AssetManager.Get();
        var candidates = assetManager.GetAllAssetsOf<Material>().Where(material => !material.IsInternal && Is(material, TlmMaterials.BlenderMaterialParameter, blenderId)).ToList();
        if (IsShared(blenderId))
        {
            return candidates.FirstOrDefault(material => assetManager.IsRelated(owner.Package, material.Package));
        }

        var owned = candidates.FirstOrDefault(material => IsOwnedBy(material, owner) && UseOf(material) == use);
        if (owned != null)
        {
            return owned;
        }

        var earlier = candidates.FirstOrDefault(material => !IsMadeInBlender(material) && assetManager.IsRelated(owner.Package, material.Package) && UseOf(material) == use);
        if (earlier != null && adopt)
        {
            Adopt(earlier, owner);
        }

        return earlier;
    }

    /// <summary>
    /// The texture made of the Blender picture for the model, one an add-on made before pictures were kept per model is the model's
    /// from now on
    /// </summary>
    public static Texture? FindTexture(IAsset owner, string blenderId)
    {
        var assetManager = AssetManager.Get();
        var candidates = assetManager.GetAllAssetsOf<Texture>().Where(texture => !texture.IsInternal && Is(texture, TlmMaterials.BlenderImageParameter, blenderId)).ToList();
        var owned = candidates.FirstOrDefault(texture => IsOwnedBy(texture, owner));
        if (owned != null)
        {
            return owned;
        }

        var earlier = candidates.FirstOrDefault(texture => !IsMadeInBlender(texture) && assetManager.IsRelated(owner.Package, texture.Package));
        if (earlier != null)
        {
            Adopt(earlier, owner);
        }

        return earlier;
    }

    private static void Adopt(IAsset asset, IAsset owner)
    {
        if (owner.URI == null)
        {
            return;
        }

        OnUiThread(() =>
        {
            asset.Parameters[OwnerParameter] = owner.URI.ToString();
            asset.Serialize();
            return true;
        });
    }

    // Made for the kind of part, by the shader it was given before the kind was kept
    private static TlmMaterialUse UseOf(Material material)
    {
        if (material.Parameters.TryGetValue(TlmMaterials.BlenderMaterialUseParameter, out var kind) && kind != null && Enum.TryParse<TlmMaterialUse>(kind.ToString(), out var use))
        {
            return use;
        }

        return TlmMaterials.DrawsSkins(((IAsset)material).GetData<MaterialData>()) ? TlmMaterialUse.Skin : TlmMaterialUse.Rigid;
    }

    /// <summary>
    /// A material of the model's package made of Blender's, in the package's materials folder, the model's own but for one of TT Lab's
    /// shared ones
    /// </summary>
    public static Material CreateMaterial(IAsset owner, string name, Func<IAsset, MaterialData> data, string? blenderId, TlmMaterialUse use, bool unsaved = false)
    {
        return OnUiThread(() =>
        {
            var package = AssetManager.Get().GetAsset<Package>(owner.Package);
            var material = (Material)AssetFactory.CreateAsset(typeof(Material), TypeFolder(package.GetPackageFolder(), typeof(Material)), UniqueName<Material>(owner.Package, name),
                string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<Material>(), asset =>
                {
                    asset.SetData(data(asset));
                    if (blenderId != null)
                    {
                        asset.Parameters[TlmMaterials.BlenderMaterialParameter] = blenderId;
                        asset.Parameters[TlmMaterials.BlenderMaterialUseParameter] = use.ToString();
                    }

                    if ((blenderId == null || !IsShared(blenderId)) && owner.URI != null)
                    {
                        asset.Parameters[OwnerParameter] = owner.URI.ToString();
                    }

                    return AssetCreationStatus.Success;
                }, unsaved: unsaved)!;
            Log.WriteLine($"Added {name} made in Blender to the project as {material.URI}");
            return material;
        });
    }

    /// <summary>
    /// A texture of the model's package made of a Blender picture, at a size the game takes, the model's own unless it's the picture of one
    /// of TT Lab's shared materials
    /// </summary>
    public static Texture CreateTexture(IAsset owner, string name, Byte[] png, string? blenderId, bool owned = true, bool unsaved = false)
    {
        return OnUiThread(() =>
        {
            var package = AssetManager.Get().GetAsset<Package>(owner.Package);
            var texture = (Texture)AssetFactory.CreateAsset(typeof(Texture), TypeFolder(package.GetPackageFolder(), typeof(Texture)), UniqueName<Texture>(owner.Package, name, true),
                string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<Texture>(), asset =>
                {
                    var data = GameTexture(asset, name, png);
                    ((Texture)asset).UseGameLayout(data.Bitmap!.PixelSize.Width, data.Bitmap.PixelSize.Height);
                    asset.SetData(data);
                    if (blenderId != null)
                    {
                        asset.Parameters[TlmMaterials.BlenderImageParameter] = blenderId;
                    }

                    if (owned && owner.URI != null)
                    {
                        asset.Parameters[OwnerParameter] = owner.URI.ToString();
                    }

                    return AssetCreationStatus.Success;
                }, unsaved: unsaved)!;
            Log.WriteLine($"Added the picture {name} made in Blender to the project as {texture.URI}");
            return texture;
        });
    }

    /// <summary>
    /// Gives the texture the picture's pixels, at a size the game takes. A picture of another size gets the layout the game's tools gave
    /// pictures of its size
    /// </summary>
    /// <returns>Whether the texture's pixels changed</returns>
    public static bool ReplacePicture(Texture texture, string name, Byte[] png)
    {
        return OnUiThread(() =>
        {
            var current = ((IAsset)texture).GetData<TextureData>();
            var made = GameTexture(texture, name, png);
            var size = made.Bitmap!.PixelSize;
            var sameSize = current.Bitmap != null && current.Bitmap.PixelSize == size;
            if (sameSize && made.GetPixels().AsSpan().SequenceEqual(current.GetPixels()))
            {
                made.Dispose();
                return false;
            }

            if (!sameSize)
            {
                texture.UseGameLayout(size.Width, size.Height);
            }

            texture.SetData(made);
            texture.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData | SerializationFlags.FixReferences);
            Log.WriteLine($"{texture.Alias} took the picture {name} painted in Blender");
            return true;
        });
    }

    /// <summary>
    /// Gives the material Blender's settings: every value Blender changed since it last had the material's (the paths in changed, its shaders
    /// in Blender's order from the material's at their sources, -1 for one added in Blender), the values TT Lab changed meanwhile staying.
    /// Without changes all of Blender's
    /// </summary>
    /// <returns>Whether the material changed</returns>
    public static bool ApplySettings(Material material, JsonObject settings, JsonArray? changed, JsonArray? sources)
    {
        return OnUiThread(() =>
        {
            var data = ((IAsset)material).GetData<MaterialData>();
            var before = JsonConvert.SerializeObject(data);
            var result = changed == null ? (JsonObject)settings.DeepClone() : WithChanges(JsonNode.Parse(before)!.AsObject(), settings, changed, sources);
            data.PopulateFrom(result.ToJsonString());
            if (JsonConvert.SerializeObject(data) == before)
            {
                return false;
            }

            material.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData | SerializationFlags.FixReferences);
            Log.WriteLine($"{material.Alias} took the settings changed in Blender");
            return true;
        });
    }

    internal static JsonObject WithChanges(JsonObject current, JsonObject settings, JsonArray changed, JsonArray? sources)
    {
        if (sources != null && current["Shaders"] is JsonArray currentShaders && settings["Shaders"] is JsonArray blenderShaders)
        {
            var shaders = new JsonArray();
            for (var i = 0; i < sources.Count && i < blenderShaders.Count; i++)
            {
                var source = sources[i] is JsonValue value && value.TryGetValue<Int32>(out var index) ? index : -1;
                shaders.Add((source >= 0 && source < currentShaders.Count ? currentShaders[source] : blenderShaders[i])?.DeepClone());
            }

            current["Shaders"] = shaders;
        }

        foreach (var path in changed.OfType<JsonArray>())
        {
            if (Find(settings, path) is var (found, value) && found)
            {
                Set(current, path, value?.DeepClone());
            }
        }

        return current;
    }

    private static (bool Found, JsonNode? Value) Find(JsonNode root, JsonArray path)
    {
        JsonNode? node = root;
        foreach (var step in path)
        {
            switch (node)
            {
                case JsonObject container when step is JsonValue key && key.TryGetValue<string>(out var name) && container.ContainsKey(name):
                    node = container[name];
                    break;
                case JsonArray list when step is JsonValue position && position.TryGetValue<Int32>(out var index) && index >= 0 && index < list.Count:
                    node = list[index];
                    break;
                default:
                    return (false, null);
            }
        }

        return (true, node);
    }

    private static void Set(JsonNode root, JsonArray path, JsonNode? value)
    {
        if (path.Count == 0)
        {
            return;
        }

        var parent = Find(root, new JsonArray(path.Take(path.Count - 1).Select(step => step?.DeepClone()).ToArray()));
        if (!parent.Found)
        {
            return;
        }

        var last = path[^1];
        switch (parent.Value)
        {
            case JsonObject container when last is JsonValue key && key.TryGetValue<string>(out var name):
                container[name] = value;
                break;
            case JsonArray list when last is JsonValue position && position.TryGetValue<Int32>(out var index) && index >= 0 && index < list.Count:
                list[index] = value;
                break;
        }
    }

    /// <summary>
    /// Deletes the model's materials and textures made in Blender that its file doesn't use any more, once nothing else uses them either:
    /// another model drawing a material, a material drawing a texture. Builds read the project, the deleting waits for them
    /// </summary>
    public static void DeleteUnused(IAsset owner, IReadOnlySet<LabURI> used)
    {
        var assetManager = AssetManager.Get();
        var unused = assetManager.GetAllAssetsOf<Material>().Cast<IAsset>().Concat(assetManager.GetAllAssetsOf<Texture>())
            .Where(asset => !asset.IsInternal && IsOwnedBy(asset, owner) && !used.Contains(asset.URI)).ToList();
        if (unused.Count == 0)
        {
            return;
        }

        if (Application.Current == null)
        {
            DeleteUnusedAsync(owner, unused).GetAwaiter().GetResult();
            return;
        }

        Dispatcher.UIThread.Post(() => _ = DeleteUnusedAsync(owner, unused));
    }

    private static async Task DeleteUnusedAsync(IAsset owner, List<IAsset> unused)
    {
        while (AssetManager.Get().AnyBuilding)
        {
            await Task.Delay(500);
        }

        // The materials first, the textures only they drew go with them. What an open editor shows (showed) stays until it's closed: deleting
        // it closed the editor, asking about its unsaved changes in the middle of a Blender save
        var editors = Locator.Current.GetService<EditorsViewModel>();
        foreach (var kind in new List<IAsset>[] { unused.OfType<Material>().Cast<IAsset>().ToList(), unused.OfType<Texture>().Cast<IAsset>().ToList() })
        {
            var assetManager = AssetManager.Get();
            var left = kind.Where(asset => assetManager.DoesAssetExist(asset.URI) && editors?.IsShown(asset.URI) != true).ToList();
            var used = UsersOf(left, owner);
            foreach (var asset in left.Where(asset => !used(asset)))
            {
                if (await AssetDeletion.DeleteAsync(asset))
                {
                    Log.WriteLine($"Deleted {asset.Alias} made in Blender, {owner.Alias} doesn't draw it any more");
                }
            }
        }
    }

    internal static bool IsUsedByOthers(IAsset asset, IAsset? owner) => UsersOf([asset], owner)(asset);

    /// <summary>
    /// Whether something links to each of the assets besides a model's new data (owner): what assets keep in themselves, models' files (the
    /// model's own too: unsaved changes discarded, its file draws it again), materials' data, and what the open documents' steps keep (undoing
    /// a model Blender wrote draws the one before it again, undoing a picture picked the picture before). The project is gone over once for
    /// all of them (every model file was read once for each), what links the ones ignored doesn't count
    /// </summary>
    internal static Func<IAsset, bool> UsersOf(IReadOnlyCollection<IAsset> assets, IAsset? owner, IReadOnlyCollection<IAsset>? ignoring = null)
    {
        var asked = assets.Select(asset => asset.URI).ToHashSet();
        var texts = asked.Select(uri => uri.ToString()).ToHashSet();
        var ignored = (ignoring ?? []).Select(asset => asset.URI).ToHashSet();
        var textures = assets.Any(asset => asset is Texture);
        var linked = new HashSet<LabURI>();
        var unreadable = false;
        foreach (var other in asked.Count == 0 ? [] : AssetManager.Get().GetAssets())
        {
            if (other.IsInternal || other is Folder or Package or DiscFile || ignored.Contains(other.URI))
            {
                continue;
            }

            // The model's own references are what it had, its file says what it draws
            if (other != owner)
            {
                linked.UnionWith(AssetLinks.MetadataLinks(other).Where(uri => asked.Contains(uri) && uri != other.URI));
            }

            try
            {
                var path = other.FullDataPath;
                if (AssetLinks.IsModelFile(other.Data) && File.Exists(path))
                {
                    linked.UnionWith(AssetLinks.UrisInModelFile(path).Where(texts.Contains).Select(text => new LabURI(text)).Where(uri => uri != other.URI));
                }

                if (textures && other is Material && File.Exists(path))
                {
                    linked.UnionWith(AssetLinks.UrisIn(AssetLinks.ReadText(path).Text).Where(texts.Contains).Select(text => new LabURI(text)));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Can't tell, so they're used
                Log.WriteLine($"Couldn't read {other.Alias} to tell whether it uses what Blender made: {exception.Message}", Log.LogType.Warning);
                unreadable = true;
            }
        }

        var kept = UndoHistory.LinksKept(texts);
        return asset => unreadable || linked.Contains(asset.URI) || kept.Contains(asset.URI.ToString());
    }

    /// <summary>
    /// The name of a new asset of Blender's: a picture's without its file's extension, plain ASCII, the package's assets of the kind having
    /// no other of the name in any case (the files of two named apart only by case are the same file on Windows)
    /// </summary>
    public static string UniqueName<T>(LabURI package, string name, bool picture = false) where T : IAsset
    {
        var cleaned = picture ? WithoutPictureExtension(name) : name.Trim();

        cleaned = RigidModelData.SanitizeName(NameRules.ToAscii(cleaned).Replace('?', '_')).Trim();
        if (cleaned.Length == 0)
        {
            cleaned = picture ? "Blender picture" : "Blender material";
        }

        var taken = AssetManager.Get().GetAllAssetsOf<T>().Where(asset => asset.Package == package).Select(asset => asset.InvariantName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = cleaned;
        for (var number = 2; taken.Contains(candidate); number++)
        {
            candidate = $"{cleaned} {number}";
        }

        return candidate;
    }

    /// <summary>
    /// A picture's name without its file's extensions (Blender names pictures after their files)
    /// </summary>
    public static string WithoutPictureExtension(string name)
    {
        var cleaned = name.Trim();
        while (PictureExtensions.Any(extension => cleaned.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            cleaned = cleaned[..^Path.GetExtension(cleaned).Length].TrimEnd();
        }

        return cleaned;
    }

    /// <summary>
    /// Blender's pictures come in any size, the game's textures are powers of two of at most 256
    /// </summary>
    public static TextureData GameTexture(IAsset texture, string name, Byte[] png)
    {
        using var stream = new MemoryStream(png);
        var data = TextureData.FromPng(texture, stream);
        var resized = data.ResizedForTheGame();
        if (resized != data)
        {
            var size = data.Bitmap!.PixelSize;
            Log.WriteLine($"Resized the {size.Width}x{size.Height} picture {name} from Blender to {resized.Bitmap!.PixelSize.Width}x{resized.Bitmap.PixelSize.Height}, " +
                          $"the biggest the game takes is {TextureData.MaxGameSize}x{TextureData.MaxGameSize}");
            data.Dispose();
        }

        return resized;
    }

    /// <summary>
    /// The editors showing what Blender changed show it, asking first about unsaved changes to it like for any file changed outside TT Lab
    /// </summary>
    public static void TellEditors(IReadOnlyCollection<IAsset> changed)
    {
        if (changed.Count == 0 || Application.Current == null || Locator.Current.GetService<EditorsViewModel>() is not { } editors)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _ = editors.ShowChangedAssets(changed));
    }

    private static Folder TypeFolder(Folder packageFolder, Type type)
    {
        var assetManager = AssetManager.Get();
        var typeFolder = packageFolder.Children.FirstOrDefault(child => assetManager.GetAsset(child) is Folder { Name: var name } && name == type.Name);
        return typeFolder == null ? packageFolder : assetManager.GetAsset<Folder>(typeFolder);
    }

    private static bool Is(IAsset asset, string parameter, string value)
    {
        return asset.Parameters.TryGetValue(parameter, out var recorded) && recorded?.ToString() == value;
    }

    private static T OnUiThread<T>(Func<T> action)
    {
        return Application.Current != null && !Dispatcher.UIThread.CheckAccess() ? Dispatcher.UIThread.Invoke(action) : action();
    }
}
