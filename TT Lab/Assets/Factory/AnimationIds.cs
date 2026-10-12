using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets.Code;
using TT_Lab.Project;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Assets.Factory;

/// <summary>
/// The IDs a build gives the animations it writes. The game keeps the animations of every chunk it has loaded in one table of
/// <see cref="TableSize"/> at their ID's low 15 bits (ResourceIndexMask, no check that it's in the table) and skips an animation of an ID
/// it already has (ResourceReaderFor): an animation has to have the same ID in every chunk and other animations other IDs, all below the
/// table's size. An animation keeps the ID its OGI has it by while no other animation took it, the others get the highest one no OGI's
/// animation has. The IDs given are kept in the build's cache, so the chunks built later give the same animations the same IDs
/// </summary>
/// <remarks>
/// TT Lab gave edited copies and animations made in Blender IDs from 0x8000 up, which the game took for the IDs from 0 up: the beach's
/// copy of Crash's idle (0x8000) took the place of a model's own idle (0), which the model then played on a skeleton it wasn't made for
/// </remarks>
public sealed class AnimationIds
{
    /// <summary>
    /// How many animations the game's table has room for (AnimationCapacity)
    /// </summary>
    public const Int32 TableSize = 2500;

    private const Int32 FileVersion = 1;
    private const string CachePrefix = "animation:";

    private readonly object _lock = new();
    private readonly string? _path;
    private readonly string _platform;
    private readonly Func<HashSet<UInt32>>? _reserve;
    private HashSet<UInt32>? _reserved;
    private readonly Dictionary<string, Given> _given = new();
    private readonly Dictionary<UInt32, string> _taken = new();
    private Dictionary<string, ScannedFile> _files = new();

    // An animation's ID: its own one (Kept) or one no OGI's animation has
    private sealed class Given
    {
        public string Content { get; set; } = string.Empty;
        public UInt32 Own { get; set; }
        public UInt32 Id { get; set; }
        public bool Kept { get; set; }
    }

    // The IDs of an OGI's animations its file had when it was this long and written then
    private sealed class ScannedFile
    {
        public long Length { get; set; }
        public long Ticks { get; set; }
        public List<UInt32> Ids { get; set; } = [];
    }

    private sealed class Stored
    {
        public Int32 Version { get; set; }
        public List<Given> Given { get; set; } = [];
        public Dictionary<string, ScannedFile> Files { get; set; } = new();
    }

    private AnimationIds(string? path, string platform, Func<HashSet<UInt32>>? reserve)
    {
        _path = path;
        _platform = platform;
        _reserve = reserve;
    }

    /// <summary>
    /// IDs only this factory's chunks get, for what isn't built into a project (previews, tests): the IDs the project's OGIs have their
    /// animations by are worked out when the first animation gets one
    /// </summary>
    public static AnimationIds ForOneBuild()
    {
        return new AnimationIds(null, string.Empty, () => ReservedIds(AssetManager.Get().GetAllAssetsOf<OGI>(), new Dictionary<string, ScannedFile>(), new Dictionary<string, ScannedFile>()));
    }

    /// <summary>
    /// The IDs the version's builds gave so far, without the ones an OGI's animation has by now
    /// </summary>
    /// <param name="projectPath">The project's folder, the IDs are kept in its build cache</param>
    /// <param name="ogis">Every OGI of the project, an animation keeps no ID one of their animations is picked by</param>
    public static AnimationIds Load(string projectPath, GamePlatform platform, IEnumerable<IAsset> ogis)
    {
        var name = platform == GamePlatform.Xbox ? "Xbox" : "PS2";
        var path = Path.Combine(projectPath, "build", "cache", $"animation_ids_{name.ToLowerInvariant()}.json");
        Stored? stored = null;
        if (File.Exists(path))
        {
            try
            {
                stored = JsonConvert.DeserializeObject<Stored>(File.ReadAllText(path));
            }
            catch (JsonException ex)
            {
                Log.WriteLine($"The {name} animations' IDs of the last builds can't be read and get worked out again, build every chunk again: {ex.Message}", Log.LogType.Warning);
            }
        }

        if (stored is { Version: not FileVersion })
        {
            stored = null;
        }

        var ids = new AnimationIds(path, name, null);
        var scanned = new Dictionary<string, ScannedFile>();
        ids._reserved = ReservedIds(ogis, stored?.Files ?? new Dictionary<string, ScannedFile>(), scanned);
        ids._files = scanned;
        foreach (var given in stored?.Given ?? [])
        {
            // An ID one of the animations has by now is that one's, an animation's own ID none of them has anymore can be given away
            var stillGiven = given.Id < TableSize && !ids._taken.ContainsKey(given.Id) && ids._reserved.Contains(given.Id) == given.Kept
                             && (!given.Kept || given.Own == given.Id);
            if (stillGiven && ids._given.TryAdd(given.Content, given))
            {
                ids._taken[given.Id] = given.Content;
            }
        }

        return ids;
    }

    /// <summary>
    /// The key the build cache keeps an animation's ID by, for the content the item of its bytes has
    /// </summary>
    public string KeyOf(ReadOnlySpan<Byte> bytes) => $"{CachePrefix}{_platform}:{Convert.ToHexString(SHA256.HashData(bytes))}";

    /// <summary>
    /// The ID the animation of the key (<see cref="KeyOf"/>) gets: the one it got before, its own when no other animation took it and
    /// no ID one of the OGIs' animations is picked by, else the highest no OGI's animation has and no other animation got
    /// </summary>
    /// <param name="own">The ID its OGI has the animation by</param>
    /// <param name="what">The animation, named when the table is full</param>
    public UInt32 IdOf(string key, UInt32 own, string what)
    {
        lock (_lock)
        {
            if (_given.TryGetValue(key, out var given))
            {
                return given.Id;
            }

            var reserved = Reserved();
            given = new Given { Content = key, Own = own };
            if (own < TableSize && reserved.Contains(own) && !_taken.ContainsKey(own))
            {
                given.Id = own;
                given.Kept = true;
            }
            else
            {
                var free = Enumerable.Range(0, TableSize).Select(id => (UInt32)(TableSize - 1 - id)).Where(id => !reserved.Contains(id) && !_taken.ContainsKey(id))
                    .Cast<UInt32?>().FirstOrDefault();
                given.Id = free ?? throw new InvalidOperationException($"{what} needs an ID of its own and the game's table of animations has room for {TableSize}, which " +
                                                                       "the project's animations and their edited copies take: build every chunk to let go of the IDs of " +
                                                                       "animations no chunk has anymore");
            }

            _given[key] = given;
            _taken[given.Id] = key;
            return given.Id;
        }
    }

    /// <summary>
    /// Whether the animation of the key still gets the ID the build cache has for it, the hex of the ID
    /// </summary>
    public bool StillGives(string key, string id)
    {
        lock (_lock)
        {
            return key.StartsWith($"{CachePrefix}{_platform}:", StringComparison.Ordinal) && _given.TryGetValue(key, out var given) && given.Id.ToString("X") == id;
        }
    }

    /// <summary>
    /// Whether the key is one of an animation's ID (<see cref="KeyOf"/>)
    /// </summary>
    public static bool IsKey(string key) => key.StartsWith(CachePrefix, StringComparison.Ordinal);

    /// <summary>
    /// Lets go of the IDs given to animations no chunk has anymore, once every chunk of the version got built
    /// </summary>
    /// <param name="keys">The keys of the animations the version's chunks have, other versions' ones are left out</param>
    public void KeepOnly(IEnumerable<string> keys)
    {
        lock (_lock)
        {
            var kept = keys.ToHashSet();
            foreach (var key in _given.Keys.Where(key => !kept.Contains(key)).ToList())
            {
                _taken.Remove(_given[key].Id);
                _given.Remove(key);
            }
        }
    }

    public void Save()
    {
        if (_path == null)
        {
            return;
        }

        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var stored = new Stored { Version = FileVersion, Given = _given.Values.OrderBy(given => given.Id).ToList(), Files = _files };
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(stored));
            File.Move(temporaryPath, _path, true);
        }
    }

    private HashSet<UInt32> Reserved()
    {
        return _reserved ??= _reserve?.Invoke() ?? [];
    }

    // The IDs the OGIs' animations have below the table's size, the loaded ones' as they are (they can have changes that aren't saved) and
    // the others' as their files have them, which are only read again when they changed
    private static HashSet<UInt32> ReservedIds(IEnumerable<IAsset> ogis, IReadOnlyDictionary<string, ScannedFile> known, Dictionary<string, ScannedFile> scanned)
    {
        var reserved = new HashSet<UInt32>();
        var hasProject = Locator.Current.GetService<ProjectManager>()?.OpenedProject != null;
        foreach (var ogi in ogis)
        {
            if (ogi is SerializableAsset { IsLoaded: true, AssetData: OGIData data })
            {
                reserved.UnionWith(data.Animations.Select(animation => animation.ID).Where(id => id < TableSize));
            }

            if (!hasProject)
            {
                continue;
            }

            var path = Path.GetFullPath(ogi.FullDataPath);
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                continue;
            }

            if (!known.TryGetValue(path, out var file) || file.Length != info.Length || file.Ticks != info.LastWriteTimeUtc.Ticks)
            {
                file = new ScannedFile { Length = info.Length, Ticks = info.LastWriteTimeUtc.Ticks, Ids = ReadIds(path, ogi) };
            }

            scanned[path] = file;
            reserved.UnionWith(file.Ids.Where(id => id < TableSize));
        }

        return reserved;
    }

    private static List<UInt32> ReadIds(string path, IAsset ogi)
    {
        try
        {
            return OGIData.AnimationIdsOf(TlmFile.ReadJson(path)).ToList();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            Log.WriteLine($"The animations of {ogi.Name} can't be read from {path}, the build leaves their IDs to them: {ex.Message}", Log.LogType.Warning);
            return [];
        }
    }
}
