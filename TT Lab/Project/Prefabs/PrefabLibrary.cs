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
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Attributes;
using TT_Lab.ServiceProviders;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Path = System.IO.Path;
using Scenery = TT_Lab.Assets.Instance.Scenery;

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

    public const char FolderSeparator = '/';

    /// <summary>
    /// Every prefab of the library, in every folder
    /// </summary>
    public List<Prefab> Load()
    {
        return Directory.Exists(Folder) ? Read(Directory.EnumerateFiles(Folder, "*.json", SearchOption.AllDirectories)) : [];
    }

    /// <summary>
    /// The prefabs right in the folder, not in the folders in it
    /// </summary>
    public List<Prefab> Load(string folder)
    {
        var directory = DirectoryOf(folder);
        return Directory.Exists(directory) ? Read(Directory.EnumerateFiles(directory, "*.json")) : [];
    }

    private List<Prefab> Read(IEnumerable<string> files)
    {
        var prefabs = new List<Prefab>();
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            try
            {
                var prefab = JsonConvert.DeserializeObject<Prefab>(File.ReadAllText(file));
                if (prefab == null)
                {
                    continue;
                }

                prefab.FilePath = file;
                prefab.Folder = FolderOf(Path.GetDirectoryName(file)!);
                var preview = PreviewPathOf(file);
                prefab.PreviewPath = File.Exists(preview) ? preview : null;
                var model = ModelPathOf(file);
                prefab.ModelPath = File.Exists(model) ? model : null;
                prefabs.Add(prefab);
            }
            catch (Exception e)
            {
                Log.WriteLine($"Prefab {Path.GetFileName(file)} couldn't be read: {e.Message}", Log.LogType.Warning);
            }
        }

        return prefabs.OrderBy(prefab => prefab.Name, NaturalStringComparer.Instance).ToList();
    }

    /// <summary>
    /// The prefabs of the files
    /// </summary>
    public List<Prefab> Load(IEnumerable<string> files)
    {
        return Read(files);
    }

    /// <summary>
    /// The folders and prefabs in the folder and every folder in it that have every term in them (without case): a folder in its name, a
    /// prefab in its folders and its name. Only the first prefabs up to the limit are read, how many there are comes back too
    /// </summary>
    public (List<string> Folders, List<Prefab> Prefabs, int Matched) Search(string folder, IReadOnlyCollection<string> terms, int limit)
    {
        var directory = DirectoryOf(folder);
        if (terms.Count == 0 || !Directory.Exists(directory))
        {
            return ([], [], 0);
        }

        bool Matches(string text) => terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
        var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
            .Where(path => Matches(Path.GetFileName(path))).Select(FolderOf).Order(NaturalStringComparer.Instance).ToList();
        // Found by their files' names, which are their names, without reading every one
        var files = Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
            .Where(file => Matches(Join(FolderOf(Path.GetDirectoryName(file)!), Path.GetFileNameWithoutExtension(file))))
            .Order(NaturalStringComparer.Instance).ToList();
        return (folders, Read(files.Take(limit)), files.Count);
    }

    /// <summary>
    /// The files of every prefab without a picture, found without reading them
    /// </summary>
    public List<string> FilesWithoutPicture()
    {
        return Directory.Exists(Folder)
            ? Directory.EnumerateFiles(Folder, "*.json", SearchOption.AllDirectories).Where(file => !File.Exists(PreviewPathOf(file))).Order(StringComparer.Ordinal).ToList()
            : [];
    }

    /// <summary>
    /// The names of the folders in the folder
    /// </summary>
    public List<string> Folders(string folder)
    {
        var directory = DirectoryOf(folder);
        return Directory.Exists(directory)
            ? Directory.EnumerateDirectories(directory).Select(Path.GetFileName).OfType<string>().Order(NaturalStringComparer.Instance).ToList()
            : [];
    }

    /// <summary>
    /// A new folder in the folder, numbered when one of the name is there. Its path comes back
    /// </summary>
    public string CreateFolder(string parent, string name)
    {
        var baseName = FileName(name);
        var candidate = baseName;
        for (var number = 2; Directory.Exists(DirectoryOf(Join(parent, candidate))); number++)
        {
            candidate = $"{baseName} {number}";
        }

        var folder = Join(parent, candidate);
        Directory.CreateDirectory(DirectoryOf(folder));
        return folder;
    }

    /// <summary>
    /// The folder under another name in the same place, its new path comes back. A folder of that name already there keeps it
    /// </summary>
    public string RenameFolder(string folder, string name)
    {
        var parent = ParentOf(folder);
        var renamed = Join(parent, FileName(name));
        if (string.Equals(renamed, folder, StringComparison.Ordinal) || Directory.Exists(DirectoryOf(renamed)))
        {
            return folder;
        }

        Directory.Move(DirectoryOf(folder), DirectoryOf(renamed));
        return renamed;
    }

    /// <summary>
    /// Deletes the folder with every prefab and folder in it
    /// </summary>
    public void DeleteFolder(string folder)
    {
        if (folder.Length > 0 && Directory.Exists(DirectoryOf(folder)))
        {
            Directory.Delete(DirectoryOf(folder), true);
        }
    }

    /// <summary>
    /// Moves the prefab and its picture into the folder, replacing one of its name there
    /// </summary>
    public void Move(Prefab prefab, string folder)
    {
        if (prefab.FilePath == null || !File.Exists(prefab.FilePath) || string.Equals(prefab.Folder, folder, StringComparison.Ordinal))
        {
            return;
        }

        var directory = DirectoryOf(folder);
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, Path.GetFileName(prefab.FilePath));
        File.Move(prefab.FilePath, target, true);
        var preview = PreviewPathOf(prefab.FilePath);
        if (File.Exists(preview))
        {
            File.Move(preview, PreviewPathOf(target), true);
            prefab.PreviewPath = PreviewPathOf(target);
        }

        var model = ModelPathOf(prefab.FilePath);
        if (File.Exists(model))
        {
            File.Move(model, ModelPathOf(target), true);
            prefab.ModelPath = ModelPathOf(target);
        }

        prefab.FilePath = target;
        prefab.Folder = folder;
    }

    public bool FolderExists(string folder) => Directory.Exists(DirectoryOf(folder));

    /// <summary>
    /// How many prefabs the folder has with the ones of the folders in it
    /// </summary>
    public int CountPrefabs(string folder)
    {
        var directory = DirectoryOf(folder);
        return Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories).Count() : 0;
    }

    public static string Join(string parent, string name) => parent.Length == 0 ? name : $"{parent}{FolderSeparator}{name}";

    /// <summary>
    /// The names the prefabs have in every folder, with their files' names, which new prefabs made in them don't take
    /// </summary>
    internal static Dictionary<string, HashSet<string>> NamesByFolder(IEnumerable<Prefab> prefabs)
    {
        return prefabs.GroupBy(prefab => prefab.Folder, StringComparer.Ordinal).ToDictionary(folder => folder.Key,
            folder => folder.SelectMany(prefab => new[] { prefab.Name, Path.GetFileNameWithoutExtension(prefab.FilePath) ?? prefab.Name })
                .ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.Ordinal);
    }

    public static string ParentOf(string folder)
    {
        var separator = folder.LastIndexOf(FolderSeparator);
        return separator < 0 ? string.Empty : folder[..separator];
    }

    // The folder's directory, only ever within the library's: the folder's names are file names
    private string DirectoryOf(string folder)
    {
        var names = folder.Split(FolderSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (names.Any(name => name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new ArgumentException($"{folder} isn't a folder of the prefabs", nameof(folder));
        }

        return names.Length == 0 ? Folder : Path.Combine([Folder, .. names]);
    }

    private string FolderOf(string directory)
    {
        var relative = Path.GetRelativePath(Folder, directory);
        return relative == "." ? string.Empty : relative.Replace(Path.DirectorySeparatorChar, FolderSeparator);
    }

    /// <summary>
    /// Writes the prefab's file into its folder, a prefab saved under the name of another there replaces it. Its picture goes next to it
    /// as a PNG, a scenery prefab's model file as a .tlm
    /// </summary>
    public void Save(Prefab prefab, Bitmap? preview = null)
    {
        var directory = DirectoryOf(prefab.Folder);
        Directory.CreateDirectory(directory);
        prefab.FilePath ??= Path.Combine(directory, $"{FileName(prefab.Name)}.json");
        if (prefab.Model != null)
        {
            // Written first, a prefab's file without it can't be placed
            File.WriteAllBytes(ModelPathOf(prefab.FilePath), prefab.Model);
            prefab.ModelPath = ModelPathOf(prefab.FilePath);
        }

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

    /// <summary>
    /// Puts the picture next to the prefab's file, whether the prefab is still there to have it comes back
    /// </summary>
    public bool SavePicture(Prefab prefab, byte[] png)
    {
        if (prefab.FilePath == null || !File.Exists(prefab.FilePath))
        {
            return false;
        }

        var path = PreviewPathOf(prefab.FilePath);
        File.WriteAllBytes(path, png);
        // Moved or deleted meanwhile, the picture would be another prefab's of the name
        if (!File.Exists(prefab.FilePath))
        {
            File.Delete(path);
            return false;
        }

        prefab.PreviewPath = path;
        return true;
    }

    public void Delete(Prefab prefab)
    {
        if (prefab.FilePath != null && File.Exists(prefab.FilePath))
        {
            File.Delete(prefab.FilePath);
            foreach (var sidecar in new[] { PreviewPathOf(prefab.FilePath), ModelPathOf(prefab.FilePath) }.Where(File.Exists))
            {
                File.Delete(sidecar);
            }
        }

        prefab.FilePath = null;
        prefab.PreviewPath = null;
        prefab.ModelPath = null;
    }

    private static string PreviewPathOf(string filePath) => Path.ChangeExtension(filePath, ".png");

    private static string ModelPathOf(string filePath) => Path.ChangeExtension(filePath, ".tlm");

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
    internal static (string DataType, JObject Data) CaptureInstanceData(SerializableInstance asset)
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

    // A scenery prefab keeps its meshes as a model file of their own next to it, the way the scenery keeps them
    internal const string SceneryCountKey = "Count";
    // What the scenery prefabs made of the chunks' scenery are made of, they aren't made again
    internal const string SceneryContentKey = "Content";

    /// <summary>
    /// Placed meshes of a scenery as one prefab, its meshes and LODs with it, each where it stands from the origin
    /// </summary>
    public Prefab CaptureScenery(Scenery scenery, IReadOnlyList<SceneryPlacement> placements, System.Numerics.Vector3 origin, string name)
    {
        if (placements.Count == 0)
        {
            throw new ArgumentException("A scenery prefab needs meshes", nameof(placements));
        }

        var file = ((IAsset)scenery).GetData<SceneryData>().WritePlacements(placements, origin);
        using var stream = new MemoryStream();
        file.WriteTo(stream);
        return new Prefab
        {
            Name = name,
            Kind = PrefabKind.Scenery,
            Platform = _project.GetPlatform(scenery.Package).ToString(),
            Package = scenery.Package,
            MadeFrom = scenery.Chunk,
            AssetType = typeof(Scenery).FullName!,
            Data = new JObject { [SceneryCountKey] = placements.Count },
            Model = stream.ToArray(),
        };
    }

    /// <summary>
    /// The scenery prefab's meshes and LODs made into meshes and LODs of the scenery, moved by the offset. They aren't among its placed
    /// ones yet
    /// </summary>
    public List<SceneryPlacement> PlaceScenery(Prefab prefab, SceneryData scenery, System.Numerics.Vector3 offset)
    {
        if (prefab.Kind != PrefabKind.Scenery || ModelOf(prefab) is not { } model)
        {
            throw new InvalidOperationException($"Prefab {prefab.Name} isn't made of scenery meshes, or its model file is gone");
        }

        using var stream = new MemoryStream(model);
        return scenery.ReadPlacements(TlmFile.Read(stream), offset);
    }

    /// <summary>
    /// A scenery prefab's model file, none when it has none
    /// </summary>
    public static byte[]? ModelOf(Prefab prefab)
    {
        return prefab.Model ?? (prefab.ModelPath != null && File.Exists(prefab.ModelPath) ? File.ReadAllBytes(prefab.ModelPath) : null);
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

        if (prefab.Kind is PrefabKind.Element or PrefabKind.Scenery && (document == null || FindResource(document, prefab) == null))
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
            TwinIdGeneratorServiceProvider.GetGeneratorForChunk(type, chunk.AdditionalPath!, chunk.Package, layout),
            asset =>
            {
                var instanceAsset = (SerializableInstance)asset;
                instanceAsset.Chunk = chunk.AdditionalPath!;
                instanceAsset.AdditionalPath = chunk.AdditionalPath;
                instanceAsset.RegenerateLinks();
                var instanceData = (AbstractAssetData)Activator.CreateInstance(dataType, asset)!;
                instanceData.PopulateFrom(data.ToString());
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

        if (prefab.Kind == PrefabKind.Scenery)
        {
            var count = prefab.Data[SceneryCountKey]?.Value<int>() ?? 0;
            var meshes = count == 1 ? "1 scenery mesh" : $"{count} scenery meshes";
            return string.IsNullOrEmpty(prefab.MadeFrom) ? $"{meshes}, {prefab.Platform}" : $"{meshes}, {prefab.Platform}, from {prefab.MadeFrom}";
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
            // What isn't stored is made of what is
            if (!property.CanRead || property.GetIndexParameters().Length > 0 || property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
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
