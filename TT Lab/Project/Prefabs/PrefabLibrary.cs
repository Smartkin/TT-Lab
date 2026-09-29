using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using GlmSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Attributes;
using TT_Lab.ServiceProviders;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Path = System.IO.Path;

namespace TT_Lab.Project.Prefabs;

/// <summary>
/// What of a chunk gets saved as a prefab: an instance of a layout, or an element of a list of a resource the chunk has once
/// </summary>
public sealed record PrefabSource(SerializableInstance Asset, PropertyNode? Element, string? ListPath, string DefaultName)
{
    public PrefabKind Kind => Element == null ? PrefabKind.Instance : PrefabKind.Element;
}

/// <summary>
/// The project's prefabs: instances and parts of resources saved to be placed in any chunk, one JSON file each in the project's
/// prefabs folder. What they link to among a chunk's instances (other instances, positions, paths) is left out, the rest is kept
/// </summary>
public sealed class PrefabLibrary
{
    public const string FolderName = "prefabs";
    private static readonly JsonSerializerSettings DataSettings = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };
    private static readonly Regex Words = new("(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);

    private readonly Project _project;

    public PrefabLibrary(Project project)
    {
        _project = project;
        Folder = Path.Combine(project.ProjectPath, FolderName);
    }

    public string Folder { get; }

    public static PrefabLibrary? ForOpenedProject()
    {
        return Locator.Current.GetService<ProjectManager>()?.OpenedProject is Project project ? new PrefabLibrary(project) : null;
    }

    public List<Prefab> Load()
    {
        var prefabs = new List<Prefab>();
        if (!Directory.Exists(Folder))
        {
            return prefabs;
        }

        foreach (var file in Directory.EnumerateFiles(Folder, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                var prefab = JsonConvert.DeserializeObject<Prefab>(File.ReadAllText(file));
                if (prefab == null)
                {
                    continue;
                }

                prefab.FilePath = file;
                var preview = PreviewPathOf(file);
                prefab.PreviewPath = File.Exists(preview) ? preview : null;
                prefabs.Add(prefab);
            }
            catch (Exception e)
            {
                Log.WriteLine($"Prefab {Path.GetFileName(file)} couldn't be read: {e.Message}", Log.LogType.Warning);
            }
        }

        return prefabs.OrderBy(prefab => prefab.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Writes the prefab's file, a prefab saved under the name of another replaces it. Its picture goes next to it as a PNG
    /// </summary>
    public void Save(Prefab prefab, Bitmap? preview = null)
    {
        Directory.CreateDirectory(Folder);
        prefab.FilePath ??= Path.Combine(Folder, $"{FileName(prefab.Name)}.json");
        File.WriteAllText(prefab.FilePath, JsonConvert.SerializeObject(prefab, Formatting.Indented));
        var previewPath = PreviewPathOf(prefab.FilePath);
        if (preview != null)
        {
            preview.Save(previewPath);
            prefab.PreviewPath = previewPath;
        }
        else if (File.Exists(previewPath))
        {
            prefab.PreviewPath = previewPath;
        }
    }

    public void Delete(Prefab prefab)
    {
        if (prefab.FilePath != null && File.Exists(prefab.FilePath))
        {
            File.Delete(prefab.FilePath);
            var preview = PreviewPathOf(prefab.FilePath);
            if (File.Exists(preview))
            {
                File.Delete(preview);
            }
        }

        prefab.FilePath = null;
        prefab.PreviewPath = null;
    }

    private static string PreviewPathOf(string filePath) => Path.ChangeExtension(filePath, ".png");

    /// <summary>
    /// What the selection in a chunk's document can be saved as: the resource's node in the document and the element of it the selection
    /// stands for, if any. The resources a chunk has once (scenery, dynamic scenery, collision, chunk links, particles) can't be prefabs
    /// themselves, the elements of their lists can. Elements of an instance or of another element are saved with what they belong to
    /// </summary>
    public static bool TryGetSource(PropertyNode resource, PropertyNode? element, out PrefabSource source, out string reason)
    {
        source = null!;
        var dataNode = resource.Path.EndsWith("[data]", StringComparison.Ordinal) ? resource : resource.Find("[data]");
        if (dataNode?.GetValue() is not SerializableInstance asset)
        {
            reason = "Only what a chunk holds can be a prefab";
            return false;
        }

        if (element == null)
        {
            if (asset.LayoutID == null)
            {
                reason = $"{Describe(asset.GetType())} exists once in a chunk, its parts can be prefabs";
                return false;
            }

            reason = string.Empty;
            source = new PrefabSource(asset, null, null, asset.Alias);
            return true;
        }

        if (asset.LayoutID != null)
        {
            reason = $"It's a part of {Describe(asset.GetType()).ToLowerInvariant()} {asset.Alias}, save that instead";
            return false;
        }

        if (element.Parent is not { } list || !list.Path.StartsWith($"{dataNode.Path}.", StringComparison.Ordinal) || element.GetValue() is not { } value)
        {
            reason = "Only the parts of the chunk's resources can be prefabs";
            return false;
        }

        var listPath = list.Path[(dataNode.Path.Length + 1)..];
        if (listPath.Contains('['))
        {
            reason = "It's a part of another part, save that one instead";
            return false;
        }

        reason = string.Empty;
        source = new PrefabSource(asset, element, listPath, (value as IDocumentModel)?.DocumentName ?? Describe(value.GetType()));
        return true;
    }

    /// <summary>
    /// The source's data as a prefab, without its links to the chunk's instances
    /// </summary>
    public Prefab Capture(PrefabSource source, string name)
    {
        var asset = source.Asset;
        var prefab = new Prefab
        {
            Name = name,
            Kind = source.Kind,
            Platform = _project.GetPlatform(asset.Package).ToString(),
            Package = asset.Package,
            MadeFrom = source.DefaultName,
            AssetType = asset.GetType().FullName!
        };

        if (source.Element == null)
        {
            var (dataType, data) = CaptureInstanceData(asset);
            prefab.DataType = dataType;
            prefab.LayoutID = asset.LayoutID;
            prefab.Data = data;
            return prefab;
        }

        var value = source.Element.GetValue()!;
        var element = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(value), value.GetType(), DataSettings)!;
        StripChunkLinks(element);
        prefab.ElementType = value.GetType().FullName;
        prefab.ListPath = source.ListPath;
        prefab.Data = JObject.FromObject(element);
        return prefab;
    }

    // The instance's data without its links to the chunk's instances, the data is let go of again when it wasn't loaded
    private static (string DataType, JObject Data) CaptureInstanceData(SerializableInstance asset)
    {
        var wasLoaded = asset.IsLoaded;
        var copy = asset.GetData().CopyFor(asset);
        StripChunkLinks(copy);
        var data = JObject.FromObject(copy);
        if (!wasLoaded)
        {
            asset.UnloadData();
        }

        return (copy.GetType().FullName!, data);
    }

    /// <summary>
    /// Several instances as one prefab, each kept where it stands from the first one
    /// </summary>
    public Prefab CaptureGroup(IReadOnlyList<(SerializableInstance Asset, vec3 Position)> members, string name)
    {
        if (members.Count == 0)
        {
            throw new ArgumentException("A group needs instances", nameof(members));
        }

        var first = members[0].Asset;
        var prefab = new Prefab
        {
            Name = name,
            Kind = PrefabKind.Group,
            Platform = _project.GetPlatform(first.Package).ToString(),
            Package = first.Package,
            MadeFrom = $"{members.Count} instances",
            AssetType = first.GetType().FullName!,
            Items = [],
        };
        var anchor = members[0].Position;
        foreach (var (asset, position) in members)
        {
            if (asset.LayoutID == null)
            {
                throw new ArgumentException($"{asset.Alias} isn't an instance of a layout", nameof(members));
            }

            var (dataType, data) = CaptureInstanceData(asset);
            var offset = position - anchor;
            prefab.Items.Add(new PrefabItem { AssetType = asset.GetType().FullName!, DataType = dataType, LayoutID = asset.LayoutID.Value, Data = data, Offset = [offset.x, offset.y, offset.z] });
        }

        return prefab;
    }

    /// <summary>
    /// Whether the prefab can go into the chunk: it has to be of the chunk's version of the game, and an element needs the chunk to have
    /// the resource it's a part of, which its open document tells
    /// </summary>
    public bool CanPlace(Prefab prefab, LevelChunk chunk, DocumentViewModel? document, out string reason)
    {
        if (prefab.Platform != _project.GetPlatform(chunk.Package).ToString())
        {
            reason = $"Made from the {prefab.Platform} version of the game";
            return false;
        }

        if (prefab.Kind == PrefabKind.Element && (document == null || FindResource(document, prefab) == null))
        {
            reason = $"The chunk has no {Describe(ResolveType(prefab.AssetType)).ToLowerInvariant()}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// A new instance of the chunk's layout the prefab was made from, with the prefab's data. It isn't among the chunk's resources yet
    /// </summary>
    public SerializableInstance PlaceInstance(Prefab prefab, LevelChunk chunk)
    {
        if (prefab.Kind != PrefabKind.Instance || prefab.LayoutID == null)
        {
            throw new InvalidOperationException($"Prefab {prefab.Name} isn't an instance");
        }

        return CreateInstance(chunk, prefab.Name, prefab.AssetType, prefab.DataType, prefab.LayoutID.Value, prefab.Data);
    }

    /// <summary>
    /// New instances of the chunk for a group prefab's items, each with where it goes from the group's first instance. They aren't among
    /// the chunk's resources yet
    /// </summary>
    public List<(SerializableInstance Instance, vec3 Offset)> PlaceGroup(Prefab prefab, LevelChunk chunk)
    {
        if (prefab.Kind != PrefabKind.Group || prefab.Items == null)
        {
            throw new InvalidOperationException($"Prefab {prefab.Name} isn't a group");
        }

        return prefab.Items.Select(item => (CreateInstance(chunk, prefab.Name, item.AssetType, item.DataType, item.LayoutID, item.Data),
                new vec3(item.Offset[0], item.Offset[1], item.Offset[2])))
            .ToList();
    }

    private static SerializableInstance CreateInstance(LevelChunk chunk, string prefabName, string assetType, string? dataTypeName, int layoutId, JObject data)
    {
        var type = ResolveType(assetType);
        var dataType = ResolveType(dataTypeName);
        if (!type.IsAssignableTo(typeof(SerializableInstance)) || !dataType.IsAssignableTo(typeof(AbstractAssetData)))
        {
            throw new InvalidDataException($"Prefab {prefabName} isn't made of an instance");
        }

        var layout = (Enums.Layouts)layoutId;
        var name = $"{prefabName} {(uint)Guid.NewGuid().GetHashCode():X8}";
        var instance = AssetFactory.CreateAsset(type, chunk.GetChunkFolder(), name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGeneratorForChunk(type, chunk.AdditionalPath!, layout),
            asset =>
            {
                var instanceAsset = (SerializableInstance)asset;
                instanceAsset.Chunk = chunk.AdditionalPath!;
                instanceAsset.AdditionalPath = chunk.AdditionalPath;
                instanceAsset.RegenerateLinks();
                var instanceData = (AbstractAssetData)Activator.CreateInstance(dataType, asset)!;
                JsonConvert.PopulateObject(data.ToString(), instanceData, DataSettings);
                asset.SetData(instanceData);
                return AssetCreationStatus.Success;
            }, layout);
        return (SerializableInstance)instance!;
    }

    /// <summary>
    /// Puts the prefab's element at the end of its list in the chunk's document, recorded in the document's history
    /// </summary>
    public PropertyNode PlaceElement(Prefab prefab, DocumentViewModel document)
    {
        if (prefab.Kind != PrefabKind.Element || prefab.ListPath == null)
        {
            throw new InvalidOperationException($"Prefab {prefab.Name} isn't a part of a resource");
        }

        var resource = FindResource(document, prefab) ?? throw new InvalidOperationException($"The chunk has no {Describe(ResolveType(prefab.AssetType)).ToLowerInvariant()}");
        var list = resource.Find(prefab.ListPath) ?? throw new InvalidDataException($"{Describe(ResolveType(prefab.AssetType))} has no {prefab.ListPath}");
        var value = JsonConvert.DeserializeObject(prefab.Data.ToString(), ResolveType(prefab.ElementType), DataSettings) ?? throw new InvalidDataException($"Prefab {prefab.Name} has no element");
        if (list.IsFull)
        {
            throw new InvalidOperationException($"{prefab.ListPath} has {list.MaxElements}, as many as the game takes");
        }

        var count = (list.GetValue() as IList)?.Count ?? list.Children.Count;
        return list.InsertElement(count, value) ?? throw new InvalidOperationException($"{prefab.ListPath} can't take elements");
    }

    /// <summary>
    /// The resource's data node in the chunk's document, the chunk's own version of it
    /// </summary>
    public static PropertyNode? FindResource(DocumentViewModel document, Prefab prefab)
    {
        var resources = document.PropertyGraph.Root.Find(nameof(LevelChunk.ChunkResources));
        return resources?.Children.Select(resource => resource.Find("[data]"))
            .FirstOrDefault(data => data?.GetValue()?.GetType().FullName == prefab.AssetType);
    }

    public static string Describe(Prefab prefab)
    {
        if (prefab.Kind == PrefabKind.Group)
        {
            return $"Group of {prefab.Items?.Count ?? 0} instances, {prefab.Platform}";
        }

        var what = prefab.Kind == PrefabKind.Instance
            ? $"{Describe(ResolveType(prefab.AssetType))} of the {ChunkLayouts.Find(prefab.LayoutID)?.Name.ToLowerInvariant() ?? $"layout {prefab.LayoutID}"} layout"
            : $"{Describe(ResolveType(prefab.ElementType))} of the {Describe(ResolveType(prefab.AssetType)).ToLowerInvariant()}";
        return string.IsNullOrEmpty(prefab.MadeFrom) ? $"{what}, {prefab.Platform}" : $"{what}, {prefab.Platform}, from {prefab.MadeFrom}";
    }

    // "DynamicScenery" reads as "Dynamic scenery"
    public static string Describe(Type type)
    {
        var words = Words.Replace(type.Name, " ").ToLowerInvariant();
        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static Type ResolveType(string? name)
    {
        if (name != null)
        {
            var type = typeof(PrefabLibrary).Assembly.GetType(name) ?? typeof(Enums).Assembly.GetType(name);
            if (type != null)
            {
                return type;
            }
        }

        throw new InvalidDataException($"Unknown prefab type {name}");
    }

    private static string FileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var fileName = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return fileName.Length == 0 ? "Prefab" : fileName;
    }

    /// <summary>
    /// Takes the links to a chunk's instances (instances of its layouts: object instances, positions, paths, triggers, cameras, ...)
    /// out of the data, following the members the way deleting an asset does. Lists lose them, single links become none
    /// </summary>
    internal static void StripChunkLinks(object? data, HashSet<object>? visited = null)
    {
        if (data?.GetType().GetCustomAttribute<ReferencesAssetsAttribute>() is null)
        {
            return;
        }

        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(data))
        {
            return;
        }

        foreach (var property in data.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            switch (property.GetValue(data))
            {
                case LabURI uri:
                    if (IsChunkInstance(uri) && property.CanWrite)
                    {
                        property.SetValue(data, LabURI.Empty);
                    }

                    break;
                case IList<LabURI> uris:
                    for (var i = uris.Count - 1; i >= 0; i--)
                    {
                        if (!IsChunkInstance(uris[i]))
                        {
                            continue;
                        }

                        if (uris.IsReadOnly)
                        {
                            uris[i] = LabURI.Empty;
                        }
                        else
                        {
                            uris.RemoveAt(i);
                        }
                    }

                    break;
                case IList items:
                    foreach (var item in items)
                    {
                        StripChunkLinks(item, visited);
                    }

                    break;
                case var value:
                    StripChunkLinks(value, visited);
                    break;
            }
        }
    }

    private static bool IsChunkInstance(LabURI uri)
    {
        var assetManager = AssetManager.Get();
        return uri != LabURI.Empty && assetManager.DoesAssetExist(uri) && assetManager.GetAsset(uri) is SerializableInstance { LayoutID: not null };
    }
}
