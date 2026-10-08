using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Splat;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Global;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;

using Path = System.IO.Path;

namespace TT_Lab.Assets;

public static partial class AssetRelocation
{
    // A folder (a chunk's as well) or an asset and where it goes
    internal sealed class Unit
    {
        public required IAsset Item { get; init; }
        public required LabURI OldPackage { get; init; }
        // A folder's path, an asset's directory
        public required string OldDirectory { get; init; }
        public required LabURI NewPackage { get; init; }
        // The folder's new path, the directory the asset goes into
        public required string NewDirectory { get; init; }
        // The folder it goes into, what comes along finds or makes its own
        public Folder? Target { get; set; }
        // A folder and every folder in it
        public List<Folder> Folders { get; init; } = [];
    }

    // Who links to what moves, by where they keep the links
    internal sealed class Linking
    {
        // In themselves, made from their data: their data files have the links as well
        public HashSet<IAsset> Metadata { get; } = [];
        public HashSet<IAsset> ModelFiles { get; } = [];
        public HashSet<IAsset> Scripts { get; } = [];

        public List<IAsset> All => Metadata.Concat(ModelFiles).Concat(Scripts).Distinct().ToList();
    }

    // A behaviour graph's script with the references that matter and what each found before anything moved
    internal sealed record Script(BehaviourGraph Graph, string Text, bool Bom, List<(BehaviourScriptLinks.Reference Reference, BehaviourGraph? Before)> References);

    internal sealed class MovePlan
    {
        public required IAsset Item { get; init; }
        public required Folder Target { get; init; }
        public required LabURI Package { get; init; }
        // A folder going into the folder it's in under another name
        public bool IsRename { get; init; }
        public List<string> Errors { get; } = [];
        public List<Unit> Units { get; } = [];
        // Every asset that moves: where it was and where it goes
        public Dictionary<IAsset, (LabURI OldUri, Place Place, LabURI NewUri)> Moved { get; } = new();
        // Old URIs to new ones, the folders' and command sequences' behaviour packs' as well
        public Dictionary<LabURI, LabURI> Map { get; } = new();
        // What comes along from packages the target package can't link to
        public List<IAsset> Brought { get; } = [];
        public Linking Linking { get; } = new();
        public List<(Package Package, Package Dependency)> NewDependencies { get; } = [];
        // Prefabs and build profiles
        public List<string> LinkingFiles { get; set; } = [];
        public List<Script> Scripts { get; } = [];
        public List<(LevelChunk Chunk, string OldPath)> Chunks { get; } = [];
    }

    /// <summary>
    /// What moving the tree's item into the folder takes, the errors say why it can't
    /// </summary>
    internal static MovePlan PlanMove(IAsset item, Folder target, string? newName = null)
    {
        item = RowItem(item);
        var plan = new MovePlan { Item = item, Target = target, Package = target.Package, IsRename = newName != null && item is Folder folder && folder.Parent == target.URI };
        if (WhyNotMovable(item) is { } whyNot)
        {
            plan.Errors.Add($"{item.Alias} can't move: {whyNot}.");
            return plan;
        }

        if (WhyNotInto(item, target, newName) is { } whyNotInto)
        {
            plan.Errors.Add($"{item.Alias} can't go into {target.Alias}: {whyNotInto}.");
            return plan;
        }

        var targetPath = PathInPackage(target)!;
        var unit = AddUnit(plan, item, item is Folder ? JoinPath(targetPath, newName ?? item.Alias) : targetPath);
        unit.Target = target;
        if (unit.OldPackage != plan.Package)
        {
            BringAlong(plan);
        }

        if (plan.Errors.Count > 0)
        {
            return plan;
        }

        CheckPlaces(plan);
        if (plan.Errors.Count > 0)
        {
            return plan;
        }

        FindLinking(plan);
        plan.LinkingFiles = LinkingFiles(plan.Map.Keys.Select(uri => uri.ToString()).ToHashSet());
        return plan;
    }

    private static Unit AddUnit(MovePlan plan, IAsset item, string newDirectory)
    {
        var folder = item as Folder;
        var unit = new Unit
        {
            Item = item,
            OldPackage = folder?.Package ?? item.Package,
            OldDirectory = folder != null ? PathInPackage(folder)! : DirectoryOf(item),
            NewPackage = plan.Package,
            NewDirectory = newDirectory,
            Folders = folder != null ? FoldersIn(folder) : []
        };
        plan.Units.Add(unit);
        var paths = new PathMap();
        if (folder != null)
        {
            paths.Add(unit.OldDirectory, newDirectory);
        }

        var newPackageName = plan.Package.GetPackageName();
        foreach (var moved in unit.Folders)
        {
            var path = paths.Map(PathInPackage(moved)!);
            plan.Map[moved.URI] = new LabURI($"res://__GLOBAL_FOLDER__/assets/{newPackageName}/{path}");
        }

        foreach (var asset in folder != null ? AssetsUnder(unit.OldPackage, unit.OldDirectory) : [item])
        {
            var directory = folder != null ? paths.Map(DirectoryOf(asset)) : newDirectory;
            var place = PlaceOf(asset, plan.Package, directory, paths.MapOrKeep);
            var uri = UriAt(plan.Package, directory, asset.Name);
            plan.Moved[asset] = (asset.URI, place, uri);
            plan.Map[asset.URI] = uri;
            foreach (var (old, packLink) in PackLinks(asset, plan.Package, asset.ID))
            {
                plan.Map[old] = packLink;
            }

            if (asset is LevelChunk chunk && NormalizePath(chunk.AdditionalPath) != directory)
            {
                plan.Chunks.Add((chunk, chunk.AdditionalPath ?? string.Empty));
            }
        }

        return unit;
    }

    private static List<Folder> FoldersIn(Folder folder)
    {
        var folders = new List<Folder> { folder };
        for (var i = 0; i < folders.Count; i++)
        {
            folders.AddRange(folders[i].Children.Where(Assets.DoesAssetExist).Select(Assets.GetAsset).OfType<Folder>());
        }

        return folders;
    }

    // What moves to another package takes along what it links to that the package can't link to, to the same place in the package, and
    // what that links to in turn
    private static void BringAlong(MovePlan plan)
    {
        var chunkDirectories = new Dictionary<LabURI, List<string>>();
        var queue = new Queue<IAsset>(plan.Moved.Keys);
        while (queue.Count > 0)
        {
            var asset = queue.Dequeue();
            foreach (var dependency in DependenciesOf(asset).ToList())
            {
                // The other version's game objects, instances and behaviours come along whatever the package depends on
                if (plan.Moved.ContainsKey(dependency)
                    || (Assets.IsOwnOrDependency(plan.Package, dependency.Package) && AssetVersions.WhyNotUsableBy(plan.Package, dependency) == null))
                {
                    continue;
                }

                var link = $"{asset.Alias} links to {dependency.Alias} of {PackageName(dependency.Package)}, which {PackageName(plan.Package)} can't link to";
                if ((WhyNotOnItsOwn(dependency) ?? StaysReason(dependency)) is { } why)
                {
                    plan.Errors.Add($"{link}, and it can't come along: {why}.");
                    continue;
                }

                if (!chunkDirectories.TryGetValue(dependency.Package, out var chunks))
                {
                    chunks = ChunkDirectories(dependency.Package);
                    chunkDirectories[dependency.Package] = chunks;
                }

                var directory = DirectoryOf(dependency);
                if (dependency is not LevelChunk && chunks.FirstOrDefault(chunk => IsUnder(directory, chunk)) is { } chunkDirectory)
                {
                    plan.Errors.Add($"{link}, and it's part of the chunk at {chunkDirectory}.");
                    continue;
                }

                var item = RowItem(dependency);
                if (dependency is LevelChunk && item is not Folder)
                {
                    plan.Errors.Add($"{link}, and the project tree has no folder of it.");
                    continue;
                }

                var before = plan.Moved.Count;
                AddUnit(plan, item, item is Folder folder ? PathInPackage(folder)! : directory);
                plan.Brought.Add(dependency);
                foreach (var added in plan.Moved.Keys.Skip(before))
                {
                    queue.Enqueue(added);
                }
            }
        }
    }

    private static void CheckPlaces(MovePlan plan)
    {
        var target = PackageName(plan.Package);
        var moving = plan.Moved.Values.Select(moved => moved.OldUri).ToHashSet();
        foreach (var (asset, (oldUri, _, newUri)) in plan.Moved)
        {
            if (newUri != oldUri && Assets.DoesAssetExist(newUri) && !moving.Contains(newUri))
            {
                plan.Errors.Add($"{target} already has {newUri.GetFilePathInPackage()}/{asset.Name}.");
            }
        }

        // What comes along goes to its place in the package, which has to be free
        foreach (var unit in plan.Units.Skip(1))
        {
            var place = AbsoluteDirectory(plan.Package.GetPackageName(), unit.NewDirectory);
            var taken = unit.Item is Folder ? Directory.Exists(place) : File.Exists(Path.Combine(place, $"{unit.Item.Name}.json"));
            if (taken)
            {
                plan.Errors.Add($"{target} already has {JoinPath(unit.NewDirectory, unit.Item is Folder ? string.Empty : unit.Item.Name).TrimEnd('/')}.");
            }
        }

        var movingChunks = plan.Chunks.Select(moved => moved.Chunk).ToHashSet();
        foreach (var (chunk, _) in plan.Chunks)
        {
            var path = plan.Moved[chunk].Place.AdditionalPath!;
            if (ChunkAt(path, plan.Package, movingChunks) is { } other)
            {
                plan.Errors.Add($"{PackageName(other.Package)} already has a chunk at {path}, the game has one file of a path.");
            }
        }
    }

    // The chunk of a version of the game at the path, besides the ones left out
    private static LevelChunk? ChunkAt(string path, LabURI package, IReadOnlySet<LevelChunk> except)
    {
        var project = OpenedProject!;
        var platform = project.GetPlatform(package);
        return Assets.GetAllAssetsOf<LevelChunk>().FirstOrDefault(chunk => !except.Contains(chunk) && project.GetPlatform(chunk.Package) == platform
                                                                          && string.Equals(NormalizePath(chunk.AdditionalPath), path, StringComparison.OrdinalIgnoreCase));
    }

    private static void FindLinking(MovePlan plan)
    {
        var oldUris = plan.Map.Keys.ToHashSet();
        var oldStrings = oldUris.Select(uri => uri.ToString()).ToHashSet();
        foreach (var asset in Assets.GetAssets())
        {
            if (asset.IsInternal || plan.Moved.ContainsKey(asset) || asset is Folder or DiscFile or Package)
            {
                continue;
            }

            if (AssetLinks.MetadataLinks(asset).Any(oldUris.Contains))
            {
                plan.Linking.Metadata.Add(asset);
            }

            if (IsModelFile(asset) && File.Exists(asset.FullDataPath) && AssetLinks.UrisInModelFile(asset.FullDataPath).Any(oldStrings.Contains))
            {
                plan.Linking.ModelFiles.Add(asset);
            }
        }

        var movedGraphs = plan.Moved.Keys.OfType<BehaviourGraph>().ToHashSet();
        var movedNames = movedGraphs.Select(graph => graph.InvariantName).ToHashSet();
        var scripts = movedGraphs.Count > 0 ? ReadScripts(Assets.GetAllAssetsOf<BehaviourGraph>().Distinct()) : null;
        if (scripts != null)
        {
            foreach (var (graph, _, _, references) in scripts.Where(script => !plan.Moved.ContainsKey(script.Graph)))
            {
                var links = references.Any(entry => oldStrings.Contains(entry.Reference.Text)
                                                    || entry.Reference is { IsChild: true, IsName: true } && movedNames.Contains(entry.Reference.Text)
                                                                                                         && BehaviourReferences.Find(graph, entry.Reference.Text) is { } found && movedGraphs.Contains(found));
                if (links)
                {
                    plan.Linking.Scripts.Add(graph);
                }
            }
        }

        CheckVersions(plan);
        AddDependencies(plan);
        // A package's scripts name behaviours in its scope, which grows with a dependency
        var widened = plan.NewDependencies.Select(dependency => dependency.Package.URI).ToHashSet();
        if (scripts == null && widened.Count > 0)
        {
            scripts = ReadScripts(Assets.GetAllAssetsOf<BehaviourGraph>().Distinct().Where(graph => widened.Contains(graph.Package)));
        }

        if (scripts == null)
        {
            return;
        }

        foreach (var script in scripts)
        {
            var everything = plan.Moved.ContainsKey(script.Graph) || widened.Contains(script.Graph.Package);
            var references = script.References.Where(entry => everything || oldStrings.Contains(entry.Reference.Text) || entry.Reference.IsName && movedNames.Contains(entry.Reference.Text))
                .Select(entry => (entry.Reference, entry.Reference.IsChild ? BehaviourReferences.Find(script.Graph, entry.Reference.Text) : null)).ToList();
            if (references.Count > 0)
            {
                plan.Scripts.Add(script with { References = references });
            }
        }
    }

    // Every graph's script with its references, what they find is worked out for the ones that matter
    private static List<Script> ReadScripts(IEnumerable<BehaviourGraph> graphs)
    {
        var scripts = new List<Script>();
        foreach (var graph in graphs.Where(graph => !graph.IsInternal))
        {
            var path = graph.FullDataPath;
            if (!File.Exists(path))
            {
                continue;
            }

            var (text, bom) = AssetLinks.ReadText(path);
            var references = BehaviourScriptLinks.Find(text);
            if (references.Count > 0)
            {
                scripts.Add(new Script(graph, text, bom, references.Select(reference => (reference, (BehaviourGraph?)null)).ToList()));
            }
        }

        return scripts;
    }

    // Game objects, their instances and behaviours that move to the other version can't be used by what stays in theirs
    private static void CheckVersions(MovePlan plan)
    {
        if (OpenedProject is not { } project)
        {
            return;
        }

        var version = project.GetPlatform(plan.Package);
        var rebound = plan.Moved.Keys.Where(asset => AssetVersions.IsVersionBound(asset) && project.GetPlatform(asset.Package) != version).ToList();
        if (rebound.Count == 0)
        {
            return;
        }

        var uris = rebound.Select(asset => asset.URI).ToHashSet();
        var graphs = rebound.OfType<BehaviourGraph>().Any();
        foreach (var linker in plan.Linking.All)
        {
            if (project.GetPlatform(linker.Package) == version || !(AssetLinks.MetadataLinks(linker).Any(uris.Contains) || graphs && plan.Linking.Scripts.Contains(linker)))
            {
                continue;
            }

            plan.Errors.Add($"{linker.Alias} of {PackageName(linker.Package)} uses what moves, which would be the {AssetVersions.Describe(version)} version's: " +
                            "game objects, their instances and behaviours are only used by their own version.");
        }
    }

    // What links to what moves keeps its links once its package depends on the target's. Root packages depend on nothing
    private static void AddDependencies(MovePlan plan)
    {
        var target = Assets.GetAsset<Package>(plan.Package);
        foreach (var group in plan.Linking.All.GroupBy(asset => asset.Package))
        {
            if (Assets.IsOwnOrDependency(group.Key, plan.Package))
            {
                continue;
            }

            var linking = Names(group.Select(asset => asset.Alias).ToList(), 3);
            if (!Assets.DoesAssetExist(group.Key) || Assets.GetAsset(group.Key) is not Package package)
            {
                plan.Errors.Add($"{linking} link to what moves, and their package isn't in the project.");
                continue;
            }

            if (package.Dependencies.Count == 0)
            {
                plan.Errors.Add($"{linking} of {package.Name} link to what moves, and {package.Name} is a root package that depends on no other.");
                continue;
            }

            plan.NewDependencies.Add((package, target));
        }
    }

    // What has to be closed: editors of what moves or gets its links changed
    private static HashSet<LabURI> Affected(MovePlan plan)
    {
        var affected = plan.Map.Keys.ToHashSet();
        affected.UnionWith(plan.Linking.All.Select(asset => asset.URI));
        affected.UnionWith(plan.Scripts.Select(script => script.Graph.URI));
        return affected;
    }

    private static void ExecuteMove(MovePlan plan)
    {
        var timer = Stopwatch.StartNew();
        foreach (var (package, dependency) in plan.NewDependencies)
        {
            PackageDependencies.Add(package, dependency);
            Log.WriteLine($"{package.Name} depends on {dependency.Name} now");
        }

        Release(plan.Moved.Keys.Concat(plan.Linking.All).Concat(plan.Scripts.Select(script => script.Graph)));
        var journal = new FileJournal();
        try
        {
            foreach (var unit in plan.Units)
            {
                MoveFiles(plan, unit, journal);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            journal.Undo();
            throw new IOException($"The files of {plan.Item.Alias} couldn't be moved, they're where they were: {exception.Message}", exception);
        }

        foreach (var (asset, _) in plan.Moved)
        {
            foreach (var (packLink, _) in PackLinks(asset, asset.Package, asset.ID))
            {
                Assets.RemoveUri(packLink);
            }

            Assets.RemoveAsset(asset);
        }

        foreach (var (asset, (_, place, newUri)) in plan.Moved)
        {
            Apply(asset, place, newUri);
            Assets.AddAsset(asset.URI, asset);
            if (asset is BehaviourCommandsSequence sequence)
            {
                foreach (var key in sequence.BehaviourGraphLinks.Keys.ToList())
                {
                    sequence.BehaviourGraphLinks[key] = PackLink(asset.Package, key, asset.ID);
                    Assets.TryAddAsset(sequence.BehaviourGraphLinks[key], asset);
                }
            }
        }

        MoveFolders(plan);
        foreach (var asset in Assets.GetAssets())
        {
            if (asset.IsInternal)
            {
                continue;
            }

            var changed = AssetLinks.RemapMetadata(asset, plan.Map);
            if (asset is Folder or DiscFile || !changed && !plan.Moved.ContainsKey(asset))
            {
                continue;
            }

            asset.Serialize();
        }

        var map = plan.Map.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString());
        foreach (var asset in plan.Moved.Keys.Concat(plan.Linking.Metadata).Concat(plan.Linking.ModelFiles).Distinct())
        {
            RemapDataFile(asset, map);
        }

        RewriteScripts(plan.Scripts, plan.Map, graph => graph, graph => graph);
        foreach (var (chunk, oldPath) in plan.Chunks)
        {
            LevelSelect.MoveChunk(chunk, oldPath);
        }

        RemapLinkingFiles(plan.LinkingFiles, map);
        MoveRows(plan);
        Locator.Current.GetService<EditorsViewModel>()?.FollowMovedAssets(plan.Map);
        Relocated?.Invoke();
        Log.WriteLine($"{(plan.IsRename ? $"Renamed {LastSegment(plan.Units[0].OldDirectory)} to {plan.Item.Alias}" : $"Moved {plan.Item.Alias} into {plan.Target.Alias}")} in {timer.Elapsed}: {Counted(plan.Moved.Count, "asset")} moved{(plan.Brought.Count > 0 ? $" ({plan.Brought.Count} came along)" : string.Empty)}, "
                      + $"links given to {Counted(plan.Linking.All.Count + plan.Scripts.Count(script => !plan.Linking.Scripts.Contains(script.Graph)), "asset")} and {Counted(plan.LinkingFiles.Count, "file")}");
    }

    private static void MoveFiles(MovePlan plan, Unit unit, FileJournal journal)
    {
        var from = AbsoluteDirectory(unit.OldPackage.GetPackageName(), unit.OldDirectory);
        var to = AbsoluteDirectory(unit.NewPackage.GetPackageName(), unit.NewDirectory);
        if (unit.Item is Folder)
        {
            journal.MoveDirectory(from, to);
            return;
        }

        var asset = unit.Item;
        journal.MoveFile(Path.Combine(from, $"{asset.Name}.json"), Path.Combine(to, $"{asset.Name}.json"));
        var data = Path.Combine(from, asset.Data);
        if (File.Exists(data))
        {
            journal.MoveFile(data, Path.Combine(to, asset.Data));
        }
    }

    // A moved folder and the folders in it under their new URIs, listed in the folder they went into. What the generic pass over the
    // links doesn't know: an asset or folder leaves the folder it was in
    private static void MoveFolders(MovePlan plan)
    {
        foreach (var unit in plan.Units)
        {
            var target = unit.Target ??= EnsureFolder(unit.NewPackage, unit.Item is Folder ? ParentPath(unit.NewDirectory) : unit.NewDirectory);
            if (unit.Item is not Folder folder)
            {
                var oldUri = plan.Moved[unit.Item].OldUri;
                foreach (var listing in Assets.GetAllAssetsOf<Folder>().Where(listing => listing.Children.Contains(oldUri)))
                {
                    listing.Children.Remove(oldUri);
                }

                if (!target.Children.Contains(unit.Item.URI))
                {
                    target.Children.Add(unit.Item.URI);
                }

                continue;
            }

            if (Assets.DoesAssetExist(folder.Parent) && Assets.GetAsset(folder.Parent) is Folder parent)
            {
                parent.Children.Remove(folder.URI);
            }

            foreach (var moved in unit.Folders)
            {
                Assets.RemoveAsset(moved);
            }

            foreach (var moved in unit.Folders)
            {
                moved.Package = unit.NewPackage;
                moved.URI = plan.Map[moved.URI];
                Assets.AddAsset(moved.URI, moved);
            }

            folder.Alias = LastSegment(unit.NewDirectory);
            folder.InvariantName = folder.Alias;
            folder.Parent = target.URI;
            target.Children.Add(folder.URI);
        }
    }

    private static string ParentPath(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }

    private static string LastSegment(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static void MoveRows(MovePlan plan)
    {
        foreach (var unit in plan.Units)
        {
            var row = unit.Item.GetResourceTreeElement();
            var targetRow = unit.Target!.GetResourceTreeElement();
            row.Parent?.RemoveChild(row);
            targetRow.AddNewChild(row);
            row.NotifyOfPropertyChange(nameof(row.Alias));
        }

        Locator.Current.GetService<TT_Lab.Project.ProjectManager>()?.RefreshTreeSearch();
    }

    private static void RemapDataFile(IAsset asset, IReadOnlyDictionary<string, string> map)
    {
        if (asset is Folder or Package or LevelChunk or DiscFile)
        {
            return;
        }

        var path = asset.FullDataPath;
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            if (IsModelFile(asset))
            {
                AssetLinks.RemapModelFile(path, map);
            }
            else if (HasJsonData(asset))
            {
                AssetLinks.RemapJsonFile(path, map);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.WriteLine($"Couldn't give {asset.Alias}'s data file the new links: {exception.Message}", Log.LogType.Error);
        }
    }

    // The scripts' references to what moved or got copied: a URI string gets the new URI, a name that finds something else from where its
    // script is now names what it found before (by its URI when its name can't). Copies name the copies of what's copied with them
    private static void RewriteScripts(IEnumerable<Script> scripts, IReadOnlyDictionary<LabURI, LabURI> map, Func<BehaviourGraph, BehaviourGraph> requesterOf,
        Func<BehaviourGraph, BehaviourGraph> targetOf)
    {
        foreach (var script in scripts)
        {
            var requester = requesterOf(script.Graph);
            var replacements = new List<(BehaviourScriptLinks.Reference, string)>();
            foreach (var (reference, before) in script.References)
            {
                var isUri = BehaviourReferences.IsUri(reference.Text);
                if (!reference.IsChild || before == null)
                {
                    if (isUri && map.TryGetValue(new LabURI(reference.Text), out var uri))
                    {
                        replacements.Add((reference, BehaviourScriptLinks.Quote(uri)));
                    }

                    continue;
                }

                var target = targetOf(before);
                if (!reference.IsName)
                {
                    if (target.URI.ToString() != reference.Text)
                    {
                        replacements.Add((reference, BehaviourScriptLinks.Quote(target.URI)));
                    }

                    continue;
                }

                if (BehaviourReferences.Find(requester, reference.Text) != target)
                {
                    replacements.Add((reference, BehaviourScriptLinks.Write(requester, target, false)));
                }
            }

            var path = requester.FullDataPath;
            if (replacements.Count == 0 && File.Exists(path))
            {
                continue;
            }

            try
            {
                AssetLinks.WriteText(path, BehaviourScriptLinks.Replace(script.Text, replacements), script.Bom);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.WriteLine($"Couldn't give {requester.Alias}'s script the new links: {exception.Message}", Log.LogType.Error);
            }
        }
    }

    /// <summary>
    /// Renames the folder of the tree: its directory and everything in it get the new name, what links to them the new URIs, after asking.
    /// Whether it got renamed
    /// </summary>
    public static async Task<bool> RenameAsync(Folder folder, string name)
    {
        if (name == folder.Alias)
        {
            return true;
        }

        var why = WhyNotName(name) ?? (Assets.DoesAssetExist(folder.Parent) ? null : "it's in no folder");
        if (why != null)
        {
            await Ask("Can't rename", $"{folder.Alias} can't be renamed {name}: {why}.", []);
            return false;
        }

        return await MoveAsync(folder, Assets.GetAsset<Folder>(folder.Parent), name);
    }

    private static string WhatMoves(MovePlan plan) => plan.Item is Folder { Mark: var mark } && mark.HasFlag(FolderMark.IsChunk) ? $"the chunk {plan.Item.Alias}" : plan.Item.Alias;

    /// <summary>
    /// Moves the tree's item into the folder (a folder under a new name when one's given) after saying what it takes, with everything in
    /// it, and gives what links to it the new URIs. Whether it moved
    /// </summary>
    public static async Task<bool> MoveAsync(IAsset item, Folder target, string? newName = null)
    {
        item = RowItem(item);
        var projectManager = Locator.Current.GetService<TT_Lab.Project.ProjectManager>();
        if (projectManager is { WorkableProject: false })
        {
            await Ask("Can't move", "The project is busy, a build or something else is working on it. Try again once it's done.", []);
            return false;
        }

        var plan = PlanMove(item, target, newName);
        if (!await Confirm(plan))
        {
            return false;
        }

        // There are no editors to close without the UI
        var editors = Locator.Current.GetService<EditorsViewModel>();
        if (editors != null && !await editors.CloseEditorsReferencing(Affected(plan)))
        {
            Log.WriteLine($"Moving {item.Alias} was cancelled");
            return false;
        }

        // What closing the editors saved or let go of is part of it now
        var again = PlanMove(item, target, newName);
        if (again.Errors.Count > 0 || again.Brought.Any(asset => !plan.Brought.Contains(asset))
                                   || again.NewDependencies.Any(added => !plan.NewDependencies.Any(asked => asked.Package == added.Package)))
        {
            await Ask("Can't move", again.Errors.Count > 0 ? string.Join("\n", again.Errors.Take(10)) : "Closing the editors changed what has to come along, move it again.", []);
            return false;
        }

        try
        {
            ExecuteMove(again);
        }
        catch (IOException exception)
        {
            Log.WriteLine(exception.Message, Log.LogType.Error);
            await Ask("Can't move", exception.Message, []);
            return false;
        }

        return true;
    }

    // Says what can't be, what has to come along and the packages that need another dependency, then what moving takes
    private static async Task<bool> Confirm(MovePlan plan)
    {
        if (plan.Errors.Count > 0)
        {
            var errors = plan.Errors.Distinct().ToList();
            await Ask("Can't move", string.Join("\n", errors.Take(10)) + (errors.Count > 10 ? $"\nand {errors.Count - 10} more" : string.Empty), []);
            return false;
        }

        var target = PackageName(plan.Package);
        if (plan.Brought.Count > 0)
        {
            var message = $"{WhatMoves(plan)} links to {Counted(plan.Brought.Count, "asset")} that {target} can't link to: {Names(plan.Brought.Select(asset => $"{asset.Alias} ({asset.Type.Name})").ToList())}. "
                          + $"They move to {target} with it, each to the same place in the package, and keep their links.";
            if (await Ask("Move along", message, ["Move them along"]) != 0)
            {
                return false;
            }
        }

        if (plan.NewDependencies.Count > 0)
        {
            var packages = plan.NewDependencies.Select(dependency => dependency.Package.Name).ToList();
            var one = packages.Count == 1;
            var message = $"{Names(packages)} {(one ? "has" : "have")} assets linking to what moves into {target}. They keep their links once {Names(packages)} "
                          + $"{(one ? "depends" : "depend")} on {target}: add {target} to {(one ? "its" : "their")} dependencies? {(one ? "It's" : "They're")} saved right away.";
            if (await Ask("Package dependencies", message, ["Add the dependencies"]) != 0)
            {
                return false;
            }
        }

        var linking = plan.Linking.All.Count + plan.Scripts.Count(script => !plan.Linking.All.Contains(script.Graph) && !plan.Moved.ContainsKey(script.Graph));
        var files = plan.LinkingFiles.Count > 0 ? $" and {Counted(plan.LinkingFiles.Count, "prefab or build profile")}" : string.Empty;
        if (plan.IsRename)
        {
            var renaming = $"Renaming {plan.Item.Alias} to {LastSegment(plan.Units[0].NewDirectory)} moves the {Counted(plan.Moved.Count, "asset")} in it and updates {Counted(linking, "asset")}{files} "
                           + "linking to them. It can't be undone. It can take a long time with a lot of assets.";
            return await Ask("Rename", renaming, ["Rename"]) == 0;
        }

        var confirm = $"Moving {WhatMoves(plan)} into {plan.Target.Alias} moves {Counted(plan.Moved.Count, "asset")} and updates {Counted(linking, "asset")}{files} linking to "
                      + $"{(plan.Moved.Count == 1 ? "it" : "them")}. It can't be undone." + (plan.Item is Folder ? " It can take a long time with a lot of assets." : string.Empty);
        return await Ask("Move", confirm, ["Move"]) == 0;
    }

    /// <summary>
    /// Asks which folder the tree's item goes into and moves it there
    /// </summary>
    public static async Task<bool> MoveToAsync(IAsset item)
    {
        item = RowItem(item);
        if (WhyNotMovable(item) is { } why)
        {
            await Ask("Can't move", $"{item.Alias} can't move: {why}.", []);
            return false;
        }

        var target = await AskFolder(item);
        return target != null && await MoveAsync(item, target);
    }
}
