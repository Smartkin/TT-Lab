using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Splat;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;

using Path = System.IO.Path;

namespace TT_Lab.Assets;

public static partial class AssetRelocation
{
    // What duplicating the tree's item copies: the item and what's in it, the parts the game's files write themselves along with them
    internal sealed class CopySet
    {
        public required IAsset Item { get; init; }
        // The package the copies go into, a new one's when a package gets copied
        public required LabURI Package { get; init; }
        public Package? CopiedPackage { get; init; }
        public List<IAsset> Assets { get; } = [];
        // The ones named one by one besides the item: what the tree shows, chunks as well, not what's in chunks or the parts
        public List<IAsset> Named { get; } = [];
        // The parts of the game's files the set doesn't have, by the directory they're in
        public Dictionary<string, List<IAsset>> Parts { get; } = new();
    }

    internal sealed class AssetCopy
    {
        public required IAsset Original { get; init; }
        public required LabURI Package { get; init; }
        public required string Directory { get; init; }
        public required string InvariantName { get; init; }
        public required string Alias { get; init; }
        public required string Variation { get; init; }
        public required UInt32 Id { get; init; }
        public required Place Place { get; init; }
        public required LabURI Uri { get; init; }
        public IAsset? Made { get; set; }

        public string Name => string.IsNullOrEmpty(Variation) ? InvariantName : $"{InvariantName}_{Variation}";
    }

    internal sealed class CopyPlan
    {
        public required CopySet Set { get; init; }
        public required string TopName { get; init; }
        public List<string> Errors { get; } = [];
        public List<AssetCopy> Copies { get; } = [];
        // Originals' URIs to their copies', their behaviour packs' as well
        public Dictionary<LabURI, LabURI> Map { get; } = new();
        // Directories the copies of folders, chunks and parts are made in, read into the tree under their folders
        public List<(string Package, string Path)> Directories { get; } = [];
        public List<Script> Scripts { get; } = [];
        public Package? PackageCopy { get; set; }
        // A copied asset's folder, where the copy is listed
        public Folder? Listing { get; set; }
    }

    /// <summary>
    /// What duplicating the tree's item copies
    /// </summary>
    internal static CopySet CopySetOf(IAsset item)
    {
        item = RowItem(item);
        if (item is Package itemPackage)
        {
            item = itemPackage.GetPackageFolder();
        }

        var folder = item as Folder;
        var package = folder != null && PackageOf(folder) is { } copied ? copied : null;
        var set = new CopySet { Item = item, Package = folder?.Package ?? item.Package, CopiedPackage = package };
        set.Assets.AddRange(folder != null ? AssetsUnder(folder.Package, PathInPackage(folder)!) : [item]);
        var chunks = ChunkDirectories(set.Package);
        var chunk = folder != null ? ChunkOf(folder) : null;
        set.Named.AddRange(set.Assets.Where(asset => asset != chunk && !asset.SkipExport
                                                     && (asset is LevelChunk || !chunks.Any(directory => IsUnder(DirectoryOf(asset), directory))))
            .OrderBy(asset => DirectoryOf(asset), StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.Alias, StringComparer.OrdinalIgnoreCase));
        // A package has every part of its files already
        if (package != null)
        {
            return set;
        }

        var inSet = set.Assets.ToHashSet();
        var parted = new HashSet<IAsset>();
        foreach (var owner in set.Assets.OfType<GlobalAsset>().ToList())
        {
            var parts = new List<IAsset>();
            var pending = new Queue<IAsset>([owner]);
            while (pending.Count > 0)
            {
                foreach (var uri in pending.Dequeue().References.Where(Assets.DoesAssetExist))
                {
                    var part = Assets.GetAsset(uri);
                    if (part.SkipExport && !part.IsInternal && part is not (Folder or DiscFile) && !inSet.Contains(part) && parted.Add(part))
                    {
                        parts.Add(part);
                        pending.Enqueue(part);
                    }
                }
            }

            if (parts.Count == 0)
            {
                continue;
            }

            // Next to their owner's copy when their folder is the owner's or holds it, else in a copy of their folder
            var directory = parts.Select(DirectoryOf).Aggregate(CommonDirectory);
            var key = directory.Length == 0 || IsUnder(DirectoryOf(owner), directory) ? string.Empty : directory;
            if (!set.Parts.TryGetValue(key, out var group))
            {
                group = [];
                set.Parts[key] = group;
            }

            group.AddRange(parts);
            set.Assets.AddRange(parts);
        }

        return set;
    }

    private static string CommonDirectory(string first, string second)
    {
        var a = first.Split('/');
        var b = second.Split('/');
        var common = a.Zip(b).TakeWhile(pair => pair.First == pair.Second).Count();
        return string.Join('/', a.Take(common));
    }

    private static string? WhyNotPackageName(string name)
    {
        if (WhyNotName(name) is { } why)
        {
            return why;
        }

        var directory = Path.Combine(OpenedProject!.ProjectPath, "assets", name);
        return Assets.GetAllAssetsOf<Package>().Any(package => package.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) || Directory.Exists(directory)
            ? $"the project has a package {name}"
            : null;
    }

    // A directory's entry of the name, without minding case
    private static bool HasEntry(string directory, string name)
    {
        return Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName)
            .Any(entry => string.Equals(entry, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The copies of the set, the item's named <paramref name="topName"/> and the named ones as the names say
    /// </summary>
    internal static CopyPlan PlanCopy(CopySet set, string topName, IReadOnlyDictionary<IAsset, string> names)
    {
        var plan = new CopyPlan { Set = set, TopName = topName };
        var project = OpenedProject!;
        var item = set.Item;
        var folder = item as Folder;
        var package = set.CopiedPackage != null ? new LabURI($"res://{topName}") : set.Package;
        var packageName = package.GetPackageName();
        var paths = new PathMap();
        var chunkFolder = folder != null ? ChunkOf(folder) : null;
        if (set.CopiedPackage != null)
        {
            plan.Map[set.CopiedPackage.URI] = package;
            plan.Directories.Add((string.Empty, string.Empty));
        }
        else if (folder != null)
        {
            var path = PathInPackage(folder)!;
            var copyPath = JoinPath(ParentPath(path), topName);
            paths.Add(path, copyPath);
            plan.Directories.Add((packageName, copyPath));
            if (Directory.Exists(AbsoluteDirectory(packageName, copyPath)))
            {
                plan.Errors.Add($"{PackageName(package)} already has {copyPath}.");
            }
        }

        foreach (var named in set.Named.OfType<LevelChunk>().Where(names.ContainsKey))
        {
            var directory = DirectoryOf(named);
            paths.Add(directory, JoinPath(ParentPath(paths.Map(directory)), names[named]));
        }

        foreach (var (directory, _) in set.Parts.Where(group => group.Key.Length > 0))
        {
            var parent = ParentPath(directory);
            var copyName = SuggestName(LastSegment(directory), name => !HasEntry(AbsoluteDirectory(packageName, parent), name)
                                                                      && !plan.Directories.Contains((packageName, JoinPath(parent, name))));
            paths.Add(directory, JoinPath(parent, copyName));
            plan.Directories.Add((packageName, JoinPath(parent, copyName)));
        }

        // Parts next to their owner's copy get names of their own
        var partNames = new Dictionary<IAsset, string>();
        if (set.Parts.TryGetValue(string.Empty, out var nextToOwner))
        {
            foreach (var part in nextToOwner)
            {
                var directory = AbsoluteDirectory(packageName, DirectoryOf(part));
                partNames[part] = SuggestName(part.InvariantName, name => !HasEntry(directory, Copied(part, name) + ".json")
                                                                          && !partNames.Any(other => other.Value == name && DirectoryOf(other.Key) == DirectoryOf(part)));
            }
        }

        var chunks = ChunkDirectories(set.Package);
        var ids = new IdAllocator();
        var variation = project.BasePackage.ID.ToString();
        foreach (var original in set.Assets)
        {
            if (!File.Exists(Path.Combine(original.FullPath, $"{original.Name}.json")))
            {
                Log.WriteLine($"{original.Alias} isn't saved, it isn't copied", Log.LogType.Warning);
                continue;
            }

            var inChunk = original is not LevelChunk && chunks.Any(directory => IsUnder(DirectoryOf(original), directory));
            string invariantName;
            string alias;
            string copyVariation;
            if (original == item || original == chunkFolder || names.ContainsKey(original))
            {
                invariantName = original == item || original == chunkFolder ? topName : names[original];
                alias = invariantName;
                copyVariation = original is LevelChunk ? string.Empty : variation;
            }
            else
            {
                invariantName = partNames.GetValueOrDefault(original, original.InvariantName);
                alias = partNames.ContainsKey(original) ? invariantName : original.Alias;
                copyVariation = original.Variation ?? string.Empty;
            }

            var directoryOfCopy = paths.Map(DirectoryOf(original));
            // What's in a chunk keeps its ID, the chunk's elements are numbered within it
            var id = inChunk ? original.ID : ids.Next(original.GetType());
            var copyName = string.IsNullOrEmpty(copyVariation) ? invariantName : $"{invariantName}_{copyVariation}";
            var uri = UriAt(package, directoryOfCopy, copyName);
            var copy = new AssetCopy
            {
                Original = original,
                Package = package,
                Directory = directoryOfCopy,
                InvariantName = invariantName,
                Alias = alias,
                Variation = copyVariation,
                Id = id,
                Place = PlaceOf(original, package, directoryOfCopy, paths.MapOrKeep),
                Uri = uri
            };
            plan.Copies.Add(copy);
            plan.Map[original.URI] = uri;
            foreach (var (old, packLink) in PackLinks(original, package, id))
            {
                plan.Map[old] = packLink;
            }

            if (Assets.DoesAssetExist(uri))
            {
                plan.Errors.Add($"{PackageName(package)} already has {copy.Directory}/{copy.Name}.");
            }
        }

        if (folder == null)
        {
            plan.Listing = FolderListing(item);
            var copy = plan.Copies.FirstOrDefault(copy => copy.Original == item);
            if (copy != null && File.Exists(Path.Combine(AbsoluteDirectory(packageName, copy.Directory), $"{copy.Name}.json")))
            {
                plan.Errors.Add($"{copy.Directory} already has {copy.Name}.");
            }
        }

        foreach (var copy in plan.Copies.Where(copy => copy.Original is LevelChunk))
        {
            if (ChunkAt(copy.Directory, package, new HashSet<LevelChunk>()) is { } other)
            {
                plan.Errors.Add($"{PackageName(other.Package)} already has a chunk at {copy.Directory}, the game has one file of a path.");
            }
        }

        var graphs = plan.Copies.Select(copy => copy.Original).OfType<BehaviourGraph>().ToList();
        foreach (var script in ReadScripts(graphs))
        {
            plan.Scripts.Add(script with
            {
                References = script.References.Select(entry => (entry.Reference, entry.Reference.IsChild ? BehaviourReferences.Find(script.Graph, entry.Reference.Text) : null)).ToList()
            });
        }

        return plan;

        static string Copied(IAsset part, string name) => string.IsNullOrEmpty(part.Variation) ? name : $"{name}_{part.Variation}";
    }

    // The lowest IDs free among the assets of a kind (a sound's languages are one), behaviour graphs' odd ones because their starter has the
    // ID before theirs, random ones for chunks and packages like new ones get
    private sealed class IdAllocator
    {
        private readonly Dictionary<Type, HashSet<UInt32>> _taken = new();

        public UInt32 Next(Type type)
        {
            if (type == typeof(LevelChunk) || type == typeof(Package) || type == typeof(Folder))
            {
                return (UInt32)Guid.NewGuid().GetHashCode();
            }

            var kind = KindRoot(type);
            if (!_taken.TryGetValue(kind, out var taken))
            {
                taken = Assets.GetAssets().Where(asset => KindRoot(asset.GetType()) == kind).Select(asset => asset.ID).ToHashSet();
                _taken[kind] = taken;
            }

            var step = kind == typeof(BehaviourGraph) ? 2U : 1U;
            var id = 1U;
            while (taken.Contains(id))
            {
                id += step;
            }

            taken.Add(id);
            return id;
        }

        private static Type KindRoot(Type type)
        {
            var root = type;
            while (root.BaseType != null && root.BaseType != typeof(SerializableAsset) && root.BaseType != typeof(GlobalAsset)
                   && root.BaseType != typeof(SerializableInstance) && !root.BaseType.IsAbstract)
            {
                root = root.BaseType;
            }

            return root;
        }
    }

    private static IAsset ExecuteCopy(CopyPlan plan)
    {
        var timer = Stopwatch.StartNew();
        var project = OpenedProject!;
        var map = plan.Map.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString());
        var scripted = plan.Scripts.Select(script => script.Graph).ToHashSet();
        var journal = new FileJournal();
        try
        {
            if (plan.Set.CopiedPackage is { } original)
            {
                plan.PackageCopy = CopyPackage(original, plan.TopName, journal);
            }

            foreach (var copy in plan.Copies)
            {
                CopyFiles(copy, map, scripted.Contains(copy.Original), journal);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            journal.Undo();
            throw new IOException($"{plan.Set.Item.Alias} couldn't be copied, nothing was made: {exception.Message}", exception);
        }

        if (plan.PackageCopy != null)
        {
            Assets.AddAsset(plan.PackageCopy.URI, plan.PackageCopy);
        }

        foreach (var copy in plan.Copies)
        {
            Assets.AddAsset(copy.Made!.URI, copy.Made);
            copy.Made.PostDeserialize();
        }

        var copies = plan.Copies.ToDictionary(copy => copy.Original, copy => copy.Made!);
        RewriteScripts(plan.Scripts, plan.Map, graph => (BehaviourGraph)copies[graph], graph => copies.TryGetValue(graph, out var copy) ? (BehaviourGraph)copy : graph);
        if (plan.PackageCopy != null)
        {
            PackageDependencies.Add(project.BasePackage, plan.PackageCopy);
        }

        var top = AddCopiesToTree(plan, copies);
        foreach (var chunk in plan.Copies.Select(copy => copy.Made).OfType<LevelChunk>())
        {
            LevelSelect.AddChunk(chunk);
        }

        Relocated?.Invoke();
        Log.WriteLine($"Duplicated {plan.Set.Item.Alias} as {plan.TopName} in {timer.Elapsed}: {Counted(plan.Copies.Count, "asset")} copied");
        return top;
    }

    private static Package CopyPackage(Package original, string name, FileJournal journal)
    {
        var package = new Package(name)
        {
            Enabled = original.Enabled,
            Variant = original.Variant
        };
        package.Variation = string.Empty;
        package.RegenerateUri();
        package.Package = package.URI;
        foreach (var dependency in original.Dependencies)
        {
            package.AddDependency(dependency);
        }

        var directory = Path.Combine(OpenedProject!.ProjectPath, "assets", package.Name);
        journal.CreateDirectory(directory);
        package.Serialize();
        journal.Created(Path.Combine(directory, $"{package.Name}.json"));
        return package;
    }

    // The copy's data file with the copies' links and its asset from the original's file under its own name and place
    private static void CopyFiles(AssetCopy copy, IReadOnlyDictionary<string, string> map, bool isScripted, FileJournal journal)
    {
        var original = copy.Original;
        var directory = AbsoluteDirectory(copy.Package.GetPackageName(), copy.Directory);
        journal.CreateDirectory(directory);
        var data = original.FullDataPath;
        var extension = original.Data[original.Name.Length..];
        var copyData = Path.Combine(directory, copy.Name + extension);
        if (File.Exists(data) && !isScripted)
        {
            journal.Created(copyData);
            if (IsModelFile(original))
            {
                AssetLinks.RemapModelFile(data, map, copyData);
            }
            else if (HasJsonData(original))
            {
                AssetLinks.RemapJsonFile(data, map, copyData);
            }
            else
            {
                File.Copy(data, copyData);
            }
        }

        var (json, _) = AssetLinks.ReadText(Path.Combine(original.FullPath, $"{original.Name}.json"));
        var asset = (IAsset)Activator.CreateInstance(original.GetType())!;
        asset.Deserialize(AssetLinks.Remap(json, map, AssetLinks.QuoteNewtonsoft) ?? json);
        asset.InvariantName = copy.InvariantName;
        asset.Alias = copy.Alias;
        asset.Variation = copy.Variation;
        asset.ID = copy.Id;
        Apply(asset, copy.Place, copy.Uri);
        if (asset is BehaviourCommandsSequence sequence)
        {
            foreach (var key in sequence.BehaviourGraphLinks.Keys.ToList())
            {
                sequence.BehaviourGraphLinks[key] = PackLink(asset.Package, key, asset.ID);
            }
        }

        var file = Path.Combine(directory, $"{copy.Name}.json");
        journal.Created(file);
        asset.Serialize();
        copy.Made = asset;
    }

    // The copies of folders, chunks and packages are read into the tree like the project's opening reads directories, a copied asset goes
    // next to its original. The item's copy comes back
    private static IAsset AddCopiesToTree(CopyPlan plan, Dictionary<IAsset, IAsset> copies)
    {
        var manager = Locator.Current.GetService<ProjectManager>()!;
        IAsset? top = null;
        foreach (var (packageName, path) in plan.Directories)
        {
            if (plan.PackageCopy != null && packageName.Length == 0)
            {
                var assetsFolder = Assets.GetAsset<Folder>(plan.Set.CopiedPackage!.GetPackageFolder().Parent);
                top = manager.AddDirectoryToTree(assetsFolder, Path.Combine(OpenedProject!.ProjectPath, "assets", plan.PackageCopy.Name));
                continue;
            }

            var package = new LabURI($"res://{packageName}");
            var parent = EnsureFolder(package, ParentPath(path));
            var added = manager.AddDirectoryToTree(parent, AbsoluteDirectory(packageName, path));
            top ??= added;
        }

        if (plan.Set.Item is not Folder && copies.TryGetValue(plan.Set.Item, out var copy))
        {
            var listing = plan.Listing ?? EnsureFolder(copy.Package, DirectoryOf(copy));
            listing.AddChild(copy);
            var row = listing.GetResourceTreeElement();
            row.AddNewChild(copy.GetResourceTreeElement(row));
            manager.RefreshTreeSearch();
            top = copy;
        }

        return top ?? plan.Set.Item;
    }

    /// <summary>
    /// Duplicates the tree's item after asking what the copy is named, a folder's or package's copies as well, one by one or all at once.
    /// The copies of what's copied together link to each other. The item's copy comes back
    /// </summary>
    public static async Task<IAsset?> DuplicateAsync(IAsset item)
    {
        item = RowItem(item);
        if (item is Package itemPackage)
        {
            item = itemPackage.GetPackageFolder();
        }

        if (WhyNotDuplicable(item) is { } why)
        {
            await Ask("Can't duplicate", $"{item.Alias} can't be duplicated: {why}.", []);
            return null;
        }

        var set = CopySetOf(item);
        var folder = item as Folder;
        var chunk = folder != null ? ChunkOf(folder) : null;
        var unsaved = UnsavedEditorsOf(set);
        var count = $"{Counted(set.Assets.Count, "asset")}{(unsaved.Count > 0 ? $". {Names(unsaved, 3)} {(unsaved.Count == 1 ? "has" : "have")} unsaved changes, the copies are made of what's saved" : string.Empty)}";
        var allAtOnce = true;
        if (folder != null && chunk == null && set.Named.Count > 0)
        {
            var example = Naming(set.Named[0], set, TopNaming(item, set, chunk, count).Suggested, new Dictionary<IAsset, string>()).Suggested;
            var message = $"Duplicating {item.Alias} copies {count}. It can't be undone and can take a long time with a lot of assets. "
                          + $"Name the copies of the {Counted(set.Named.Count, "asset")} in it one by one, or all at once the way {set.Named[0].Alias}'s copy is named, {example}?";
            var answer = await Ask("Duplicate", message, ["One by one", "All at once"]);
            if (answer == null)
            {
                return null;
            }

            allAtOnce = answer == 1;
        }

        var (title, prompt, suggested, validate) = TopNaming(item, set, chunk, count);
        var top = await AskName(new NameRequest(title, prompt, suggested, validate, false));
        if (top == null)
        {
            return null;
        }

        var names = new Dictionary<IAsset, string>();
        foreach (var named in set.Named)
        {
            var (namedSuggestion, namedValidate) = Naming(named, set, top.Name, names);
            if (allAtOnce)
            {
                names[named] = namedSuggestion;
                continue;
            }

            var answer = await AskName(new NameRequest("Duplicate", $"The copy of {named.Alias} ({DirectoryOf(named)}) is named", namedSuggestion, namedValidate, true));
            if (answer == null)
            {
                return null;
            }

            names[named] = answer.Name;
            allAtOnce = answer.AllAtOnce;
        }

        var plan = PlanCopy(set, top.Name, names);
        if (plan.Errors.Count > 0)
        {
            await Ask("Can't duplicate", string.Join("\n", plan.Errors.Distinct().Take(10)), []);
            return null;
        }

        try
        {
            return ExecuteCopy(plan);
        }
        catch (IOException exception)
        {
            Log.WriteLine(exception.Message, Log.LogType.Error);
            await Ask("Can't duplicate", exception.Message, []);
            return null;
        }
    }

    private static List<string> UnsavedEditorsOf(CopySet set)
    {
        var editors = Locator.Current.GetService<EditorsViewModel>();
        if (editors == null)
        {
            return [];
        }

        var uris = set.Assets.Select(asset => asset.URI).ToHashSet();
        return editors.GetUnsavedEditors().Where(editor => editor.GetReferencedAssets().Any(uris.Contains)).Select(editor => editor.AssetName).ToList();
    }

    // What the item's copy is named: free in its folder, a chunk's path free in its version, a package's name free in the project
    private static (string Title, string Message, string Suggested, Func<string, string?> Validate) TopNaming(IAsset item, CopySet set, LevelChunk? chunk, string count)
    {
        if (set.CopiedPackage is { } package)
        {
            return ("Duplicate", $"Duplicating {package.Name} copies {count} into a new package, the project depends on it like on a package made in it. It can't be undone and "
                                 + "can take a long time with a lot of assets. The new package is named", SuggestName(package.Name, name => WhyNotPackageName(name) == null), WhyNotPackageName);
        }

        if (item is Folder folder)
        {
            var path = PathInPackage(folder)!;
            var parent = AbsoluteDirectory(folder.Package.GetPackageName(), ParentPath(path));
            Func<string, string?> validate = name => WhyNotName(name) ?? (HasEntry(parent, name) ? $"{ParentPath(path)} already has {name}" : null)
                                                                       ?? (chunk != null && ChunkAt(JoinPath(ParentPath(path), name), folder.Package, new HashSet<LevelChunk>()) is { } other
                                                                           ? $"{PackageName(other.Package)} has a chunk at {JoinPath(ParentPath(path), name)}, the game has one file of a path"
                                                                           : null);
            var message = chunk != null
                ? $"Duplicating the chunk {chunk.Alias} copies {count}, the copy is a chunk of its own at another path. It can't be undone. The copy is named"
                : $"The copy of {folder.Alias} is named";
            return ("Duplicate", message, SuggestName(folder.Alias, name => validate(name) == null), validate);
        }

        var (suggested, validateAsset) = Naming(item, set, null, new Dictionary<IAsset, string>());
        return ("Duplicate", $"The copy of {item.Alias} is named", suggested, validateAsset);
    }

    // What a copy of an asset is named: free among its folder's files like Create Asset checks, a chunk's path free in its version. The
    // copies named already count
    private static (string Suggested, Func<string, string?> Validate) Naming(IAsset asset, CopySet set, string? topName, IReadOnlyDictionary<IAsset, string> named)
    {
        var item = set.Item;
        var package = set.CopiedPackage != null ? new LabURI($"res://{topName}") : set.Package;
        var directory = DirectoryOf(asset);
        if (item is Folder folder && set.CopiedPackage == null && topName != null)
        {
            var path = PathInPackage(folder)!;
            directory = JoinPath(ParentPath(path), topName) + directory[path.Length..];
        }

        var absolute = AbsoluteDirectory(package.GetPackageName(), directory);
        var variation = asset is LevelChunk ? string.Empty : OpenedProject!.BasePackage.ID.ToString();
        Func<string, string?> validate = name =>
        {
            if (WhyNotName(name) is { } why)
            {
                return why;
            }

            if (named.Any(other => other.Value.Equals(name, StringComparison.OrdinalIgnoreCase) && other.Key.GetType() == asset.GetType() && DirectoryOf(other.Key) == DirectoryOf(asset)))
            {
                return $"another copy in {directory} is named {name}";
            }

            if (asset is LevelChunk)
            {
                var chunkPath = JoinPath(ParentPath(directory), name);
                return ChunkAt(chunkPath, package, new HashSet<LevelChunk>()) is { } other || named.Any(pair => pair.Key is LevelChunk && pair.Value == name && ParentPath(DirectoryOf(pair.Key)) == ParentPath(DirectoryOf(asset)))
                    ? $"there's a chunk at {chunkPath} already, the game has one file of a path"
                    : HasEntry(AbsoluteDirectory(package.GetPackageName(), ParentPath(directory)), name) ? $"{ParentPath(directory)} already has {name}" : null;
            }

            if (HasEntry(absolute, (string.IsNullOrEmpty(variation) ? name : $"{name}_{variation}") + ".json"))
            {
                return $"{directory} already has {name}";
            }

            return null;
        };
        return (SuggestName(asset.Alias, name => validate(name) == null), validate);
    }

    /// <summary>
    /// What the name dialogue asks: a copy's name, its check and whether the rest can be named all at once from it
    /// </summary>
    public sealed record NameRequest(string Title, string Message, string Suggested, Func<string, string?> Validate, bool OffersAllAtOnce);

    /// <summary>
    /// The name given, and whether the rest are named the way their copies are suggested
    /// </summary>
    public sealed record NameAnswer(string Name, bool AllAtOnce);
}
