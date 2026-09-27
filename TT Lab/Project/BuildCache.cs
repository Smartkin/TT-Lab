using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using TT_Lab.Assets;

namespace TT_Lab.Project;

// Remembers which assets every build output was made from, so outputs whose assets didn't change are reused by the next build.
// An asset's fingerprint is its metadata plus its data file's contents. Assets whose data is loaded when the build starts can have
// unsaved changes so they always count as changed. The tool's own fingerprint covers TT Lab's code and definition files, so updating
// TT Lab invalidates every output
public sealed class BuildCache
{
    private const int CacheVersion = 1;
    private const string VolatileFingerprint = "VOLATILE";
    private static readonly string[] ToolDataFiles =
    [
        "AgentLabDefsPS2.json",
        "AgentLabDefsXbox.json",
        "TextureDescriptionHelper.json"
    ];

    private readonly string _projectPath;
    private readonly string _manifestPath;
    private readonly AssetManager _assetManager;
    private readonly Manifest _manifest;
    private readonly HashSet<LabURI> _volatileAssets;
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
        _volatileAssets = assetManager.GetAssets().Where(asset => asset.IsLoaded && !asset.IsInternal).Select(asset => asset.URI).ToHashSet();

        var toolFingerprint = ComputeToolFingerprint();
        if (_manifest.Version == CacheVersion && _manifest.ToolFingerprint == toolFingerprint)
        {
            return;
        }

        _manifest.Version = CacheVersion;
        _manifest.ToolFingerprint = toolFingerprint;
        _manifest.Entries.Clear();
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
            var labUri = new LabURI(uri);
            if (!_assetManager.DoesAssetExist(labUri) || GetFingerprint(_assetManager.GetAsset(labUri)) != fingerprint)
            {
                return false;
            }
        }

        return true;
    }

    public void Record(string key, IEnumerable<IAsset> dependencies, IReadOnlyCollection<string> outputs)
    {
        lock (_lock)
        {
            RecordLocked(key, dependencies, outputs);
        }
    }

    private void RecordLocked(string key, IEnumerable<IAsset> dependencies, IReadOnlyCollection<string> outputs)
    {
        var entry = new Entry();
        foreach (var dependency in dependencies.Select(GetFingerprintedAsset).Distinct())
        {
            entry.Dependencies[dependency.URI.ToString()] = GetFingerprint(dependency);
        }

        foreach (var output in outputs)
        {
            entry.Outputs[GetRelativePath(output)] = GetFileHash(output);
        }

        _manifest.Entries[key] = entry;
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

    private string ComputeToolFingerprint()
    {
        var toolFiles = new List<string>
        {
            typeof(BuildCache).Assembly.Location,
            typeof(Twinsanity.AgentLab.AgentLabCompiler).Assembly.Location
        };
        toolFiles.AddRange(ToolDataFiles.Select(file => Path.Combine(AppContext.BaseDirectory, file)));
        var agentLabDirectory = Path.Combine(AppContext.BaseDirectory, "AgentLab");
        if (Directory.Exists(agentLabDirectory))
        {
            toolFiles.AddRange(Directory.GetFiles(agentLabDirectory).Order());
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in toolFiles.Where(File.Exists))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(GetFileHash(file)));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
