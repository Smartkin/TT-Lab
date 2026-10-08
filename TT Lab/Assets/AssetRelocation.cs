using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Splat;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Instance;
using TT_Lab.Controls;
using TT_Lab.Project;
using TT_Lab.Project.Build;
using TT_Lab.Project.Prefabs;
using TT_Lab.Util;

using Path = System.IO.Path;

namespace TT_Lab.Assets;

/// <summary>
/// Moving and duplicating what the project tree shows: assets, folders, chunks (their folders) and packages. Things go where the folders'
/// Create Asset would make them, links to what moves follow it and copies of a set link to each other. See <see cref="MoveAsync"/> and
/// <see cref="DuplicateAsync"/>
/// </summary>
public static partial class AssetRelocation
{
    /// <summary>
    /// Raised once assets moved or got copied, for what keeps their URIs elsewhere (the Prefabs panel's prefabs)
    /// </summary>
    public static event Action? Relocated;

    // What the dialogues ask, tests answer them
    internal static Func<string, string, IReadOnlyList<string>, Task<int?>> Ask { get; set; } = ChoiceDialogue.Ask;
    internal static Func<NameRequest, Task<NameAnswer?>> AskName { get; set; } = NameDialogue.Ask;
    internal static Func<IAsset, Task<Folder?>> AskFolder { get; set; } = FolderPickerDialogue.Ask;

    private static TT_Lab.Project.Project? OpenedProject => Locator.Current.GetService<ProjectManager>()?.OpenedProject as TT_Lab.Project.Project;

    private static AssetManager Assets => AssetManager.Get();

    // The game's own files of the startup folder, each the only one of its kind the game reads where it is
    private static string? FixedReason(IAsset asset)
    {
        return asset switch
        {
            SaveIcon => "the game has one memory card icon, Crash.ico of the startup folder",
            UiSoundLibrary => "the game has one library of the menus' sounds, the startup folder's",
            PTC when asset.InvariantName.Equals("Decal", StringComparison.OrdinalIgnoreCase) => "the game reads its decals from the startup folder's Decal.ptc",
            PSM when asset.InvariantName.Equals("Icons", StringComparison.OrdinalIgnoreCase) => "the game reads the HUD's icons from the startup folder's Icons.psm",
            TextFile when asset.InvariantName.Equals(LevelSelect.FileName, StringComparison.OrdinalIgnoreCase) => "the level select reads the startup folder's LevelSelect.txt",
            LevelChunk { IsGlobalDefaultChunk: true } => "the game can't start without the default chunk where it is",
            _ => null
        };
    }

    // The build writes the game's pictures, fonts and texts from where they are, the global package's Startup, Extras and Language
    // folders: one moved elsewhere dropped out of it. They're duplicated, not moved
    private static string? StaysReason(IAsset asset)
    {
        return asset switch
        {
            PSM => "the build only writes the game's pictures where they are (they can be duplicated)",
            Font => "the build only writes the game's fonts where they are (they can be duplicated)",
            TextFile => "the build only writes the game's texts where they are (they can be duplicated)",
            _ => null
        };
    }

    // Why the asset can't go anywhere on its own, whatever it's asked for
    private static string? WhyNotOnItsOwn(IAsset asset)
    {
        return asset switch
        {
            DiscFile => "it's a file of the disc, the twinstudio tools open it",
            Package => "packages stay in the project's assets folder",
            _ when asset.IsInternal => "it's part of another asset",
            SerializableInstance => "it belongs to its chunk: instances are copied in the chunk's scene with Ctrl+D and into other chunks as prefabs",
            _ => FixedReason(asset)
        };
    }

    /// <summary>
    /// The chunk a chunk's folder holds
    /// </summary>
    internal static LevelChunk? ChunkOf(Folder folder)
    {
        if (!folder.Mark.HasFlag(FolderMark.IsChunk))
        {
            return null;
        }

        return folder.Children.Where(Assets.DoesAssetExist).Select(Assets.GetAsset).OfType<LevelChunk>().FirstOrDefault();
    }

    internal static Package? PackageOf(Folder folder)
    {
        return folder.Mark.HasFlag(FolderMark.IsPackage) && Assets.DoesAssetExist(folder.Package) ? Assets.GetAsset(folder.Package) as Package : null;
    }

    // What a row of the tree stands for: a chunk is its folder, the tree shows that
    private static IAsset RowItem(IAsset item)
    {
        if (item is LevelChunk chunk)
        {
            try
            {
                return chunk.GetChunkFolder();
            }
            catch (Exception)
            {
                return item;
            }
        }

        return item;
    }

    /// <summary>
    /// Why the tree's item can't be moved, null when it can
    /// </summary>
    public static string? WhyNotMovable(IAsset item)
    {
        if (OpenedProject == null)
        {
            return "no project is open";
        }

        item = RowItem(item);
        if (item is not Folder folder)
        {
            return WhyNotOnItsOwn(item) ?? StaysReason(item);
        }

        if (folder.Mark.HasFlag(FolderMark.IsPackage))
        {
            return "packages stay in the project's assets folder";
        }

        if (WhyFolderStays(folder) is { } why)
        {
            return why;
        }

        if (ChunkOf(folder) is { } chunk)
        {
            return FixedReason(chunk);
        }

        return FixedInside(folder, moving: true);
    }

    /// <summary>
    /// Why the tree's item can't be duplicated, null when it can
    /// </summary>
    public static string? WhyNotDuplicable(IAsset item)
    {
        if (OpenedProject == null)
        {
            return "no project is open";
        }

        item = RowItem(item);
        if (item is Package package)
        {
            item = package.GetPackageFolder();
        }

        if (item is not Folder folder)
        {
            return WhyNotOnItsOwn(item);
        }

        if (PackageOf(folder) is { } folderPackage)
        {
            if (folderPackage.Dependencies.Count == 0)
            {
                return $"{folderPackage.Name} is a root package, every other package is made on it";
            }

            return FixedInside(folder);
        }

        if (WhyFolderStays(folder) is { } why)
        {
            return why;
        }

        if (ChunkOf(folder) is { } chunk)
        {
            return FixedReason(chunk);
        }

        return FixedInside(folder);
    }

    private static string? WhyFolderStays(Folder folder)
    {
        if (folder.Mark.HasFlag(FolderMark.Disc))
        {
            return "the disc's folders show the disc's files where they are";
        }

        if (folder.Mark.HasFlag(FolderMark.Locked) || folder.Parent == LabURI.Empty || PathInPackage(folder) is null or "")
        {
            return $"TT Lab keeps {folder.Alias} where it is";
        }

        return null;
    }

    private static string? FixedInside(Folder folder, bool moving = false)
    {
        var path = PathInPackage(folder);
        if (path == null)
        {
            return null;
        }

        foreach (var asset in AssetsUnder(folder.Package, path))
        {
            if ((FixedReason(asset) ?? (moving ? StaysReason(asset) : null)) is { } reason)
            {
                return $"{asset.Alias} is in it and {reason}";
            }
        }

        return null;
    }

    private enum Kind
    {
        Asset,
        Folder,
        Chunk
    }

    private static Kind KindOf(IAsset item)
    {
        return item switch
        {
            Folder folder when folder.Mark.HasFlag(FolderMark.IsChunk) => Kind.Chunk,
            Folder => Kind.Folder,
            LevelChunk => Kind.Chunk,
            _ => Kind.Asset
        };
    }

    private static bool IsInLevels(string path) => path == Folder.LevelsFolderName || path.StartsWith(Folder.LevelsFolderName + "/", StringComparison.Ordinal);

    /// <summary>
    /// Why the tree's item can't go into the folder, null when it can: where Create Asset would make it (chunks in a package's levels
    /// folder and the folders in it, folders in a package or a folder, the other assets in folders outside the levels folder), not where
    /// something has its name
    /// </summary>
    public static string? WhyNotInto(IAsset item, Folder target, string? name = null) => new Destinations(item).WhyNot(target, name);

    /// <summary>
    /// Where the tree's item can go, what's in a folder looked at once for the many folders a dialogue or a drag asks about
    /// </summary>
    internal sealed class Destinations
    {
        private readonly IAsset _item;
        private readonly Kind _kind;
        // A folder's assets with their directories under the folder's and whether they're a chunk's
        private readonly List<(IAsset Asset, string Below, bool IsChunks)> _contents = [];
        private readonly Dictionary<Folder, string?> _checked = new();
        // The folder listing an asset, looked for once
        private readonly Folder? _listing;

        public Destinations(IAsset item)
        {
            _item = RowItem(item);
            _kind = KindOf(_item);
            _listing = _item is Folder ? null : FolderListing(_item);
            if (_item is not Folder folder || _kind != Kind.Folder || PathInPackage(folder) is not { } path)
            {
                return;
            }

            var chunks = ChunkDirectories(folder.Package);
            foreach (var asset in AssetsUnder(folder.Package, path))
            {
                var directory = DirectoryOf(asset);
                _contents.Add((asset, directory[path.Length..], asset is LevelChunk || chunks.Any(chunk => IsUnder(directory, chunk))));
            }
        }

        public IAsset Item => _item;

        public string? WhyNot(Folder target, string? name = null)
        {
            if (name != null)
            {
                return Check(target, name);
            }

            if (!_checked.TryGetValue(target, out var why))
            {
                why = Check(target, null);
                _checked[target] = why;
            }

            return why;
        }

        private string? Check(Folder target, string? name)
        {
            var item = _item;
            if (target.Mark.HasFlag(FolderMark.Disc))
            {
                return "the disc's folders only show its files";
            }

            if (target.Mark.HasFlag(FolderMark.IsChunk) || target.Mark.HasFlag(FolderMark.InChunk))
            {
                return "a chunk's folder only holds its chunk";
            }

            var targetPath = PathInPackage(target);
            if (targetPath == null)
            {
                return "the project's assets folder only holds packages";
            }

            if (item is Folder folder)
            {
                if (folder == target || IsInside(target, folder))
                {
                    return "a folder can't go into itself";
                }

                if (folder.Parent == target.URI && name == null)
                {
                    return $"it's in {target.Alias} already";
                }
            }
            else if (_listing == target && name == null)
            {
                return $"it's in {target.Alias} already";
            }

            var isPackageRoot = target.Mark.HasFlag(FolderMark.IsPackage);
            switch (_kind)
            {
                case Kind.Chunk:
                    if (target.GetPathInLevels() == null)
                    {
                        return "chunks only go in a package's levels folder and the folders in it";
                    }

                    break;
                case Kind.Folder:
                    if (!isPackageRoot && !target.Mark.HasFlag(FolderMark.Normal))
                    {
                        return $"{target.Alias} doesn't take folders";
                    }

                    if (WhyNotContents(target, JoinPath(targetPath, name ?? item.Alias)) is { } why)
                    {
                        return why;
                    }

                    break;
                default:
                    if (isPackageRoot)
                    {
                        return "a package's own folder only takes folders, assets go into the folders in it";
                    }

                    if (!target.Mark.HasFlag(FolderMark.Normal))
                    {
                        return $"{target.Alias} doesn't take assets";
                    }

                    if (target.GetPathInLevels() != null)
                    {
                        return "the levels folders only hold chunks";
                    }

                    break;
            }

            var shownName = name ?? (item is Folder ? item.Alias : item.Name);
            if (HasEntry(target, shownName, item is not Folder))
            {
                return $"{target.Alias} already has {shownName}";
            }

            return null;
        }

        // What a folder holds has to end up where it could be made: chunks in the levels folders, the rest outside them
        private string? WhyNotContents(Folder target, string newPath)
        {
            foreach (var (asset, below, isChunks) in _contents)
            {
                var newDirectory = newPath + below;
                if (isChunks)
                {
                    if (!IsInLevels(newDirectory))
                    {
                        return $"{asset.Alias} is a chunk's, chunks only go in a package's levels folder and the folders in it";
                    }

                    continue;
                }

                if (IsInLevels(newDirectory))
                {
                    return $"{asset.Alias} would be in the levels folders, which only hold chunks";
                }
            }

            return null;
        }
    }

    private static bool IsInside(Folder folder, Folder ancestor)
    {
        for (var current = folder; current.Parent != LabURI.Empty && Assets.DoesAssetExist(current.Parent);)
        {
            current = Assets.GetAsset<Folder>(current.Parent);
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    // Whether the folder's directory has a file or folder of the name, without minding case: Windows doesn't tell them apart
    // What the folder lists by the name without minding case, or a file or folder of the name in its directory: dialogues and drags ask
    // about thousands of folders, a listing of each directory took seconds
    private static bool HasEntry(Folder folder, string name, bool isAsset)
    {
        var listed = folder.Children.Where(Assets.DoesAssetExist).Select(Assets.GetAsset)
            .Any(child => string.Equals(child is Folder ? child.Alias : child.Name, name, StringComparison.OrdinalIgnoreCase));
        var path = Path.Combine(AbsoluteDirectory(folder), isAsset ? name + ".json" : name);
        return listed || File.Exists(path) || Directory.Exists(path);
    }

    internal static string PackageName(LabURI package)
    {
        return Assets.DoesAssetExist(package) ? Assets.GetAsset(package).Name : package.GetPackageName();
    }

    // Paths in a package are kept with forward slashes, the way URIs have them
    internal static string NormalizePath(string? path) => (path ?? string.Empty).Replace('\\', '/').Trim('/');

    internal static string JoinPath(string parent, string name) => parent.Length == 0 ? name : $"{parent}/{name}";

    internal static bool IsUnder(string path, string directory)
    {
        return directory.Length == 0 || path == directory || path.StartsWith(directory + "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// A folder's place in its package, empty for the package's own folder and null outside packages
    /// </summary>
    internal static string? PathInPackage(Folder folder)
    {
        if (folder.Package == null || folder.Package == LabURI.Empty)
        {
            return null;
        }

        var prefix = $"/assets/{folder.Package.GetPackageName()}";
        var path = folder.GetPath();
        if (path == prefix)
        {
            return string.Empty;
        }

        return path.StartsWith(prefix + "/", StringComparison.Ordinal) ? path[(prefix.Length + 1)..] : null;
    }

    // Where an asset's files are in its package: the URI's path between the package and the name
    internal static string DirectoryOf(IAsset asset) => NormalizePath(asset.URI.GetFilePathInPackage());

    internal static string AbsoluteDirectory(string packageName, string path)
    {
        return Path.Combine(OpenedProject!.ProjectPath, "assets", packageName, path.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string AbsoluteDirectory(Folder folder)
    {
        return Path.Combine(OpenedProject!.ProjectPath, folder.GetPath().TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// The assets whose files are in the package's directory or the ones under it: what moves or gets copied with a folder or chunk
    /// </summary>
    internal static List<IAsset> AssetsUnder(LabURI package, string directory)
    {
        var name = package.GetPackageName();
        return Assets.GetAssets().Where(asset => asset is not (Folder or Package or DiscFile) && !asset.IsInternal && asset.URI.GetPackageName() == name
                                                 && IsUnder(DirectoryOf(asset), directory)).ToList();
    }

    // The directories of the package's chunks, what's under one is the chunk's
    private static List<string> ChunkDirectories(LabURI package)
    {
        return Assets.GetAllAssetsOf<LevelChunk>().Where(chunk => chunk.Package == package).Select(DirectoryOf).ToList();
    }

    /// <summary>
    /// The folder of the tree that lists the asset
    /// </summary>
    internal static Folder? FolderListing(IAsset asset)
    {
        return Assets.GetAllAssetsOf<Folder>().FirstOrDefault(folder => folder.Children.Contains(asset.URI));
    }

    private static readonly Regex CopyNumber = new(@"^(.*) \((\d+)\)$", RegexOptions.Compiled);

    /// <summary>
    /// The name of a copy the way Windows names them: the name with the lowest number from 2 that's free, a number the name has already
    /// counts on from its name without it
    /// </summary>
    internal static string SuggestName(string name, Func<string, bool> isFree)
    {
        var match = CopyNumber.Match(name);
        var stem = match.Success ? match.Groups[1].Value : name;
        for (var number = 2; ; number++)
        {
            var candidate = $"{stem} ({number})";
            if (isFree(candidate))
            {
                return candidate;
            }
        }
    }

    internal const int MaxNameLength = 64;

    // What Create Asset checks a new asset's name for, whether it's free is up to the caller
    internal static string? WhyNotName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "the name can't be empty";
        }

        if (name.Length > MaxNameLength)
        {
            return $"the name can't be longer than {MaxNameLength} characters";
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) != -1 || name.IndexOfAny(['/', '\\']) != -1 || name != name.Trim())
        {
            return "the name can't have characters file names can't have or spaces around it";
        }

        if (!NameRules.IsAscii(name))
        {
            return $"the name {NameRules.AsciiOnly}";
        }

        return null;
    }

    // Lists for messages: the first few names of many
    private static string Names(IReadOnlyList<string> names, int shown = 5)
    {
        if (names.Count <= shown)
        {
            return names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
        }

        return $"{string.Join(", ", names.Take(shown))} and {names.Count - shown} more";
    }

    private static string Counted(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";

    // The project's own files that keep links: the prefabs and the build profiles
    private static List<string> LinkingFiles(IReadOnlySet<string> uris)
    {
        var project = OpenedProject!;
        var files = new List<string>();
        foreach (var (folder, pattern) in new[] { (PrefabLibrary.FolderName, "*"), (BuildProfileLibrary.FolderName, "*.json") })
        {
            var directory = Path.Combine(project.ProjectPath, folder);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
            {
                try
                {
                    var found = AssetLinks.IsModelFile(file) ? AssetLinks.UrisInModelFile(file)
                        : file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? AssetLinks.UrisIn(AssetLinks.ReadText(file).Text) : [];
                    if (found.Any(uris.Contains))
                    {
                        files.Add(file);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    Log.WriteLine($"Couldn't read {file}: {exception.Message}", Log.LogType.Warning);
                }
            }
        }

        return files;
    }

    private static void RemapLinkingFiles(IEnumerable<string> files, IReadOnlyDictionary<string, string> map)
    {
        foreach (var file in files)
        {
            try
            {
                if (AssetLinks.IsModelFile(file))
                {
                    AssetLinks.RemapModelFile(file, map);
                }
                else
                {
                    AssetLinks.RemapJsonFile(file, map);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.WriteLine($"Couldn't give {file} the new links: {exception.Message}", Log.LogType.Error);
            }
        }
    }

    private static bool IsModelFile(IAsset asset) => AssetLinks.IsModelFile(asset.Data);

    private static bool HasJsonData(IAsset asset) => asset.Data.EndsWith(".data", StringComparison.OrdinalIgnoreCase);

    // The assets an asset links to: what it keeps in itself, what its data file has (its JSON, its model file's, the behaviours its
    // script names). Internal assets and links to nothing are left out
    private static IEnumerable<IAsset> DependenciesOf(IAsset asset)
    {
        var uris = new HashSet<LabURI>(AssetLinks.MetadataLinks(asset));
        var graphs = new List<IAsset>();
        var dataPath = asset is Folder or Package or LevelChunk or DiscFile ? null : asset.FullDataPath;
        if (dataPath != null && File.Exists(dataPath))
        {
            if (IsModelFile(asset))
            {
                uris.UnionWith(AssetLinks.UrisInModelFile(dataPath).Select(uri => new LabURI(uri)));
            }
            else if (HasJsonData(asset))
            {
                uris.UnionWith(AssetLinks.UrisIn(AssetLinks.ReadText(dataPath).Text).Select(uri => new LabURI(uri)));
            }
            else if (asset is BehaviourGraph)
            {
                graphs.AddRange(BehaviourScriptLinks.Find(AssetLinks.ReadText(dataPath).Text)
                    .Select(reference => BehaviourReferences.Find(asset, reference.Text)).OfType<BehaviourGraph>());
            }
        }

        foreach (var uri in uris)
        {
            if (uri == asset.URI || !Assets.DoesAssetExist(uri))
            {
                continue;
            }

            var linked = Assets.GetAsset(uri);
            if (!linked.IsInternal && linked is not (Folder or DiscFile))
            {
                graphs.Add(linked);
            }
        }

        return graphs.Where(linked => linked != asset).Distinct();
    }

    // Unloading the data of an asset lets go of the internal assets it made, they're made again when it's loaded
    private static void Release(IEnumerable<IAsset> assets)
    {
        var released = assets.ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var asset in released.Cast<IAsset>())
        {
            if (asset.IsLoaded)
            {
                asset.UnloadData();
            }
        }

        foreach (var owned in Assets.GetAssets().Where(asset => asset.IsInternal && asset.InternalOwner != null && released.Contains(asset.InternalOwner)).ToList())
        {
            Assets.RemoveAsset(owned);
        }

        Assets.RemoveOrphanedInternalAssets(new HashSet<LabURI>());
    }

    // A command sequence is also known by a URI per behaviour pack, objects' behaviour slots link to those
    private static IEnumerable<(LabURI Old, LabURI New)> PackLinks(IAsset asset, LabURI package, UInt32 id)
    {
        if (asset is not BehaviourCommandsSequence sequence)
        {
            yield break;
        }

        foreach (var (key, uri) in sequence.BehaviourGraphLinks)
        {
            yield return (uri, PackLink(package, key, id));
        }
    }

    private static LabURI PackLink(LabURI package, UInt32 key, UInt32 id) => new($"{package}/{nameof(BehaviourGraph)}/{key}/{id}");

    // Folders of the package down to the directory, made when they're missing, with their rows
    private static Folder EnsureFolder(LabURI package, string path)
    {
        var folder = Assets.GetAsset<Package>(package).GetPackageFolder();
        foreach (var name in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var existing = folder.Children.Where(Assets.DoesAssetExist).Select(Assets.GetAsset).OfType<Folder>().FirstOrDefault(child => child.Alias == name);
            if (existing == null)
            {
                existing = new Folder(name)
                {
                    Parent = folder.URI,
                    Package = folder.Package,
                    Mark = FolderMark.Normal
                };
                Directory.CreateDirectory(Path.Combine(AbsoluteDirectory(folder), name));
                Assets.AddAsset(existing);
                folder.AddChild(existing);
                var row = folder.GetResourceTreeElement();
                row.AddNewChild(existing.GetResourceTreeElement(row));
            }

            folder = existing;
        }

        return folder;
    }

    /// <summary>
    /// Where an asset of its type is: the fields its URI's path is made of
    /// </summary>
    internal readonly record struct Place(LabURI Package, string Directory, string? AdditionalPath, string? FolderInPackage, string? Chunk, string? GlobalPath);

    // Where the asset goes in the directory: a chunk's path is its directory, an instance's chunk path follows its chunk's, a game's file
    // keeps its place in the build, the rest are in their folder unless it's the one of their type. Paths of directories that move with
    // it (a chunk's) follow them
    internal static Place PlaceOf(IAsset asset, LabURI package, string directory, Func<string?, string?> mapPath)
    {
        switch (asset)
        {
            case LevelChunk:
                return new Place(package, directory, directory, null, asset.Chunk, null);
            case SerializableInstance:
                return new Place(package, directory, mapPath(asset.AdditionalPath), asset.FolderInPackage, mapPath(asset.Chunk), null);
            case GlobalAsset:
                return new Place(package, directory, mapPath(asset.AdditionalPath), null, asset.Chunk, directory);
            default:
                var additional = mapPath(asset.AdditionalPath);
                var typeDirectory = string.IsNullOrEmpty(additional) ? asset.Type.Name : $"{NormalizePath(additional)}/{asset.Type.Name}";
                return new Place(package, directory, additional, typeDirectory == directory ? null : directory, asset.Chunk, null);
        }
    }

    // The URI an asset gets where it goes, checked against what its type makes of the place
    private static LabURI UriAt(LabURI package, string directory, string name) => new($"{package}/{directory}/{name}");

    private static void Apply(IAsset asset, Place place, LabURI uri)
    {
        asset.Package = place.Package;
        asset.AdditionalPath = place.AdditionalPath;
        asset.FolderInPackage = place.FolderInPackage;
        if (asset is SerializableAsset serializable)
        {
            serializable.Chunk = place.Chunk!;
        }

        if (asset is GlobalAsset global)
        {
            global.GlobalPath = place.GlobalPath!;
        }

        asset.RegenerateLinks();
        if (asset.URI != uri)
        {
            Log.WriteLine($"{asset.Alias} would be at {asset.URI} rather than {uri}, it's kept at {uri}", Log.LogType.Warning);
            asset.URI = uri;
        }
    }

    // Maps the paths of the directories that move to where they go, the deepest first, other paths stay as they were written
    private sealed class PathMap
    {
        private readonly List<(string From, string To)> _moves = [];

        public void Add(string from, string to)
        {
            _moves.Add((from, to));
            _moves.Sort((a, b) => b.From.Length.CompareTo(a.From.Length));
        }

        public string Map(string path)
        {
            var normalized = NormalizePath(path);
            foreach (var (from, to) in _moves)
            {
                if (IsUnder(normalized, from))
                {
                    return to + normalized[from.Length..];
                }
            }

            return normalized;
        }

        public string? MapOrKeep(string? path)
        {
            if (path == null)
            {
                return null;
            }

            var normalized = NormalizePath(path);
            return _moves.Any(move => IsUnder(normalized, move.From)) ? Map(normalized) : path;
        }
    }

    // File moves and what was made for them, taken back when one fails
    private sealed class FileJournal
    {
        private readonly List<(string From, string To, bool IsDirectory)> _moves = [];
        private readonly List<string> _createdDirectories = [];
        private readonly List<string> _createdFiles = [];

        public void CreateDirectory(string path)
        {
            var missing = new Stack<string>();
            for (var directory = path; !string.IsNullOrEmpty(directory) && !Directory.Exists(directory); directory = Path.GetDirectoryName(directory))
            {
                missing.Push(directory);
            }

            while (missing.Count > 0)
            {
                var directory = missing.Pop();
                Directory.CreateDirectory(directory);
                _createdDirectories.Add(directory);
            }
        }

        public void MoveDirectory(string from, string to)
        {
            CreateDirectory(Path.GetDirectoryName(to)!);
            Directory.Move(from, to);
            _moves.Add((from, to, true));
        }

        public void MoveFile(string from, string to)
        {
            CreateDirectory(Path.GetDirectoryName(to)!);
            File.Move(from, to);
            _moves.Add((from, to, false));
        }

        public void Created(string file) => _createdFiles.Add(file);

        public void Undo()
        {
            foreach (var file in Enumerable.Reverse(_createdFiles))
            {
                TryUndo(() => File.Delete(file), file);
            }

            foreach (var (from, to, isDirectory) in Enumerable.Reverse(_moves))
            {
                TryUndo(() =>
                {
                    if (isDirectory)
                    {
                        Directory.Move(to, from);
                    }
                    else
                    {
                        File.Move(to, from);
                    }
                }, to);
            }

            foreach (var directory in Enumerable.Reverse(_createdDirectories))
            {
                TryUndo(() =>
                {
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                    }
                }, directory);
            }
        }

        private static void TryUndo(Action undo, string path)
        {
            try
            {
                undo();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.WriteLine($"Couldn't put {path} back: {exception.Message}", Log.LogType.Error);
            }
        }
    }
}
