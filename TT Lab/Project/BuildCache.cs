using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;

namespace TT_Lab.Project;

// Remembers which assets every build output was made from, so outputs whose assets didn't change are reused by the next build.
// An asset's fingerprint is its metadata plus its data file's contents. Assets whose data is loaded when the build starts can have
// unsaved changes so they count as changed, but for what open editors show of their files (LoadedData), which counts as changed again once
// it's edited or let go of while the build runs. The tool's own fingerprint covers TT Lab's code and definition files, so updating TT Lab
// invalidates every output
public sealed class BuildCache
{
    private const int CacheVersion = 1;
    private const string VolatileFingerprint = "VOLATILE";

    private readonly string _projectPath;
    private readonly string _manifestPath;
    private readonly AssetManager _assetManager;
    private readonly Manifest _manifest;
    private readonly HashSet<LabURI> _volatileAssets;
    // Loaded data counted as its files, with the edits it had then (LoadedData)
    private readonly Dictionary<LabURI, (AbstractAssetData Data, long Edits)> _asFiles;
    private readonly Dictionary<LabURI, string> _fingerprints = new();
    // Chunks build in parallel and record what they read when they're done
    private readonly object _lock = new();

    private sealed class Manifest
    {
        public int Version { get; set; } = CacheVersion;
        public string ToolFingerprint { get; set; } = string.Empty;
        public Dictionary<string, Entry> Entries { get; set; } = new();
        public Dictionary<string, FileHash> FileHashes { get; set; } = new();
    }

    private sealed class Entry
    {
        public Dictionary<string, string> Dependencies { get; set; } = new();
        public Dictionary<string, string> Outputs { get; set; } = new();
    }

    private sealed class FileHash
    {
        public long Length { get; set; }
        public long LastWriteTicks { get; set; }
        public string Hash { get; set; } = string.Empty;
    }

    private BuildCache(string projectPath, AssetManager assetManager, Manifest manifest)
    {
        _projectPath = projectPath;
        _manifestPath = Path.Combine(projectPath, "build", "cache", "manifest.ttcache");
        _assetManager = assetManager;
        _manifest = manifest;
        (_volatileAssets, _asFiles) = LoadedData(assetManager);

        var toolFingerprint = ComputeToolFingerprint();
        if (_manifest.Version == CacheVersion && _manifest.ToolFingerprint == toolFingerprint)
        {
            return;
        }

        IsReset = true;
        _manifest.Version = CacheVersion;
        _manifest.ToolFingerprint = toolFingerprint;
        _manifest.Entries.Clear();
    }

    // Which of the loaded assets the open editors show, which TT Lab's editors answer once the app started (EditorsViewModel.ShowToBuilds): a
    // build counts the others' loaded data as changed, the editors' while it has an edit its files don't have, all of it without editors
    internal static Func<IReadOnlySet<LabURI>, IReadOnlySet<LabURI>>? EditorsShowing { get; set; }

    // Loaded data can have edits its files don't have, which the build reads: it counts as changed (volatile). What the open editors show has
    // what its files have while it has no edit since it was read or saved, an undone one included (another editor sharing the data can have
    // saved the edit before), and its data file is the one TT Lab read or wrote (another program's write isn't read yet): a chunk's scene open
    // for Play loads every resource of the chunk, and the start chunk was built again on every Play and Reload. Data no editor shows (whatever
    // loaded it can have changed it) and an asset never saved count as changed whatever the editors say. The edits are taken first, one made
    // later is made while the build runs (GetFingerprint)
    private static (HashSet<LabURI> Volatile, Dictionary<LabURI, (AbstractAssetData Data, long Edits)> AsFiles) LoadedData(AssetManager assetManager)
    {
        var loaded = assetManager.GetAssets().Where(asset => asset.IsLoaded && !asset.IsInternal).ToList();
        var asFiles = new Dictionary<LabURI, (AbstractAssetData Data, long Edits)>();
        if (loaded.Count > 0 && EditorsShowing is { } showing)
        {
            foreach (var asset in loaded)
            {
                if (asset is SerializableAsset { IsUnsaved: false, LoadedData: { Disposed: false } data } && data.Edits is var edits && !data.HasUnsavedEdits
                    && AssetFileStamps.IsAsRecorded(asset.FullDataPath))
                {
                    asFiles[asset.URI] = (data, edits);
                }
            }

            var shown = asFiles.Count == 0 ? new HashSet<LabURI>() : showing(asFiles.Keys.ToHashSet());
            foreach (var uri in asFiles.Keys.Where(uri => !shown.Contains(uri)).ToList())
            {
                asFiles.Remove(uri);
            }
        }

        return (loaded.Select(asset => asset.URI).Where(uri => !asFiles.ContainsKey(uri)).ToHashSet(), asFiles);
    }

    // Data counted as its files is still the asset's, has no edit made since the build started and its file is still the one TT Lab read or
    // wrote: what the build read of it is what they have. With another program writing the file while the build ran (Blender exporting the
    // model offline), the chunk built of the data was recorded by the new file and kept once the file was read again
    private bool IsStillAsFiles(IAsset asset)
    {
        return !_asFiles.TryGetValue(asset.URI, out var loaded)
               || asset is SerializableAsset { LoadedData: { } data } && ReferenceEquals(data, loaded.Data) && data.Edits == loaded.Edits
               && AssetFileStamps.IsAsRecorded(asset.FullDataPath);
    }

    public static BuildCache Load(string projectPath, AssetManager assetManager)
    {
        var manifestPath = Path.Combine(projectPath, "build", "cache", "manifest.ttcache");
        Manifest? manifest = null;
        if (File.Exists(manifestPath))
        {
            try
            {
                manifest = JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(manifestPath));
            }
            catch (JsonException ex)
            {
                Log.WriteLine($"Build cache is corrupted and will be rebuilt: {ex.Message}", Log.LogType.Warning);
            }
        }

        return new BuildCache(projectPath, assetManager, manifest ?? new Manifest());
    }

    // Dependencies on links: a chunk's file has the links to the chunks a build leaves out dropped
    private const string LinkPrefix = "link:";
    private const string LinkKept = "kept";
    private const string LinkDropped = "dropped";

    /// <summary>
    /// The chunks the build's profile leaves out. A chunk's file is only up to date while the same of its links are dropped
    /// </summary>
    public IReadOnlySet<LabURI> ExcludedChunks { get; set; } = new HashSet<LabURI>();

    /// <summary>
    /// The IDs the build gives animations. A chunk's file is only up to date while its animations get the IDs it has them by
    /// </summary>
    public AnimationIds? AnimationIds { get; set; }

    /// <summary>
    /// Whether the cache knows nothing of the outputs the build folder has: it's new, or TT Lab changed since they were built
    /// </summary>
    public bool IsReset { get; }

    public bool IsUpToDate(string key, IReadOnlyCollection<string> outputs)
    {
        lock (_lock)
        {
            return IsUpToDateLocked(key, outputs);
        }
    }

    private bool IsUpToDateLocked(string key, IReadOnlyCollection<string> outputs)
    {
        if (!_manifest.Entries.TryGetValue(key, out var entry) || entry.Outputs.Count != outputs.Count)
        {
            return false;
        }

        foreach (var output in outputs)
        {
            if (!entry.Outputs.TryGetValue(GetRelativePath(output), out var outputHash) || !File.Exists(output) || GetFileHash(output) != outputHash)
            {
                return false;
            }
        }

        foreach (var (uri, fingerprint) in entry.Dependencies)
        {
            if (uri.StartsWith(LinkPrefix, StringComparison.Ordinal))
            {
                if (LinkState(new LabURI(uri[LinkPrefix.Length..])) != fingerprint)
                {
                    return false;
                }

                continue;
            }

            if (AnimationIds.IsKey(uri))
            {
                if (AnimationIds?.StillGives(uri, fingerprint) != true)
                {
                    return false;
                }

                continue;
            }

            // What was built from loaded data may have had changes that weren't saved then or were saved since, a chunk built while
            // its scene was open looked up to date for as long as the scene stayed open
            var labUri = new LabURI(uri);
            if (fingerprint == VolatileFingerprint || !_assetManager.DoesAssetExist(labUri) || GetFingerprint(_assetManager.GetAsset(labUri)) != fingerprint)
            {
                return false;
            }
        }

        return true;
    }

    public void Record(string key, IEnumerable<IAsset> dependencies, IReadOnlyCollection<string> outputs, IEnumerable<LabURI>? linkedChunks = null,
        IReadOnlyDictionary<string, UInt32>? animations = null)
    {
        lock (_lock)
        {
            RecordLocked(key, dependencies, outputs, linkedChunks ?? [], animations ?? new Dictionary<string, UInt32>());
        }
    }

    private void RecordLocked(string key, IEnumerable<IAsset> dependencies, IReadOnlyCollection<string> outputs, IEnumerable<LabURI> linkedChunks,
        IReadOnlyDictionary<string, UInt32> animations)
    {
        var entry = new Entry();
        foreach (var dependency in dependencies.Select(GetFingerprintedAsset).Distinct())
        {
            entry.Dependencies[dependency.URI.ToString()] = GetFingerprint(dependency);
        }

        foreach (var linkedChunk in linkedChunks.Distinct())
        {
            entry.Dependencies[$"{LinkPrefix}{linkedChunk}"] = LinkState(linkedChunk);
        }

        foreach (var (animation, id) in animations)
        {
            entry.Dependencies[animation] = id.ToString("X");
        }

        foreach (var output in outputs)
        {
            entry.Outputs[GetRelativePath(output)] = GetFileHash(output);
        }

        _manifest.Entries[key] = entry;
    }

    private string LinkState(LabURI chunk) => ExcludedChunks.Contains(chunk) ? LinkDropped : LinkKept;

    /// <summary>
    /// The animations every output the cache knows has (<see cref="Assets.Factory.AnimationIds.DependencyOf"/>)
    /// </summary>
    public IReadOnlyCollection<string> RecordedAnimations()
    {
        lock (_lock)
        {
            return _manifest.Entries.Values.SelectMany(entry => entry.Dependencies.Keys).Where(AnimationIds.IsKey).ToHashSet();
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_manifestPath)!);
            var temporaryPath = _manifestPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(_manifest));
            File.Move(temporaryPath, _manifestPath, true);
        }
    }

    // Internal assets are recreated from their owner's data, so the owner is what they depend on
    private static IAsset GetFingerprintedAsset(IAsset asset)
    {
        while (asset is { IsInternal: true, InternalOwner: not null })
        {
            asset = asset.InternalOwner;
        }

        return asset;
    }

    private string GetFingerprint(IAsset asset)
    {
        asset = GetFingerprintedAsset(asset);
        // Edited, let go of or its file written by another program while the build runs (an instance dragged in the open scene, the scene
        // closed), it counts as changed from then on: what the build read of it can be what no file has, and recorded by its files a chunk was
        // kept with it once the edit was undone
        if (!IsStillAsFiles(asset))
        {
            _asFiles.Remove(asset.URI);
            _volatileAssets.Add(asset.URI);
            _fingerprints.Remove(asset.URI);
        }

        if (_fingerprints.TryGetValue(asset.URI, out var fingerprint))
        {
            return fingerprint;
        }

        if (_volatileAssets.Contains(asset.URI) || asset.IsInternal)
        {
            fingerprint = VolatileFingerprint;
        }
        else
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(asset)));
            var dataPath = asset is Folder or Package ? null : asset.FullDataPath;
            if (dataPath != null && File.Exists(dataPath))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(GetFileHash(dataPath)));
            }

            fingerprint = Convert.ToHexString(hash.GetHashAndReset());
        }

        _fingerprints[asset.URI] = fingerprint;
        return fingerprint;
    }

    private string GetFileHash(string path)
    {
        var info = new FileInfo(path);
        var key = Path.GetFullPath(path);
        if (_manifest.FileHashes.TryGetValue(key, out var cached) && cached.Length == info.Length && cached.LastWriteTicks == info.LastWriteTimeUtc.Ticks)
        {
            return cached.Hash;
        }

        using var stream = File.OpenRead(path);
        var fileHash = new FileHash
        {
            Length = info.Length,
            LastWriteTicks = info.LastWriteTimeUtc.Ticks,
            Hash = Convert.ToHexString(SHA256.HashData(stream))
        };
        _manifest.FileHashes[key] = fileHash;
        return fileHash.Hash;
    }

    private string GetRelativePath(string path) => Path.GetRelativePath(_projectPath, path);

    // Deterministic builds derive an assembly's module version ID from its contents, the definition files embedded in it included.
    // The assemblies' files can't be hashed, a single file build has none
    private static string ComputeToolFingerprint()
    {
        return string.Join("-", new[] { typeof(BuildCache), typeof(Twinsanity.AgentLab.AgentLabCompiler) }
            .Select(type => type.Assembly.ManifestModule.ModuleVersionId.ToString("N")));
    }
}
