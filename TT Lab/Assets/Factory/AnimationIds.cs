using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
/// table's size. An animation keeps each ID an OGI has it by, the way the disc's chunks have one spin by CrashSpin's 3 and by
/// CrashSpinWithCortex's 0xA8. An ID OGIs have other animations by stays the first one's, the others get the ID their animation got first or
/// else the highest one no OGI's animation has, worked out for them and given once a chunk asks for them: only what chunks have takes the
/// table's room. First is a fixed order of every animation of the OGIs, taken at once when the IDs are worked out: the version's OGIs before
/// the other version's, the game's before the ones the project made, then by the OGI's URI and the ID; past the room for every animation of
/// the OGIs the ones game objects play come first, and an animation left without room fails whenever a chunk asks for it. The chunks of a
/// build ask in the order its memory gate starts them, which decided before: two builds of a project gave the spin 3 or 0xA8 in every chunk.
/// The IDs given are kept in the build's cache, so the chunks built later give the same animations the same IDs
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

    private const Int32 FileVersion = 2;
    private const string CachePrefix = "animation:";

    private readonly object _lock = new();
    private readonly string? _path;
    private readonly string _platform;
    private readonly Int32 _tableSize;
    private Scan? _scan;
    // The IDs builds gave and the animation (its key) each one was given to, which the builds after keep while it's still one it can have
    private readonly Dictionary<UInt32, string> _given = new();
    private Plan? _plan;
    private Dictionary<string, ScannedFile> _files = new();

    // An animation an OGI has: the OGI's URI, whether the OGI is of the version the IDs are for, whether it's the game's, the ID the OGI has
    // the animation by and the animation's key
    private readonly record struct Claim(string Ogi, bool OfVersion, bool OfTheGame, UInt32 Own, string Key);

    // The animations the OGIs have and the IDs below the table's size they have them by
    private sealed class Scan(Int32 tableSize)
    {
        public HashSet<UInt32> Reserved { get; } = [];
        public List<Claim> Claims { get; } = [];

        public void Add(Claim claim)
        {
            Claims.Add(claim);
            Reserve(claim.Own);
        }

        public void Reserve(UInt32 id)
        {
            if (id < tableSize)
            {
                Reserved.Add(id);
            }
        }
    }

    // Every animation's ID by its key and the ID its OGI has it by, worked out for the OGIs' animations at once and for the others as they ask
    private sealed class Plan
    {
        private readonly HashSet<UInt32> _reserved;
        private readonly Int32 _tableSize;
        // The ID worked out in the fixed order for each animation of the OGIs that can't keep its own and got none, given once a chunk asks
        // for the animation: only what chunks have takes the table's room
        private readonly Dictionary<string, UInt32> _slots = new();
        private readonly HashSet<UInt32> _slotted = [];
        // Where the next one is looked for, every ID above it is taken or worked out for an animation
        private UInt32 _next;

        public Plan(HashSet<UInt32> reserved, Int32 tableSize)
        {
            _reserved = reserved;
            _tableSize = tableSize;
            _next = (UInt32)tableSize;
        }

        public Dictionary<(string Key, UInt32 Own), UInt32> Ids { get; } = new();
        // The animation every ID is
        public Dictionary<UInt32, string> Holders { get; } = new();
        // The ID an animation got first, which it gets by the IDs it can't keep
        public Dictionary<string, UInt32> Homes { get; } = new();

        public bool CanKeep(string key, UInt32 own) => own < _tableSize && _reserved.Contains(own) && (!Holders.TryGetValue(own, out var holder) || holder == key);

        // The ID worked out for the animation, while no other animation got it
        public UInt32? SlotOf(string key) => _slots.TryGetValue(key, out var slot) && (!Holders.TryGetValue(slot, out var holder) || holder == key) ? slot : null;

        // Works out the ID an animation of the OGIs gets by the IDs it can't keep, when it got none: the highest one no OGI's animation has, no
        // animation got and none was worked out for. False once the table has no room left
        public bool AddSlot(string key)
        {
            if (Homes.ContainsKey(key) || _slots.ContainsKey(key))
            {
                return true;
            }

            while (_next > 0)
            {
                var id = --_next;
                if (IsFree(id))
                {
                    _slots[key] = id;
                    _slotted.Add(id);
                    return true;
                }
            }

            return false;
        }

        // The ID of an animation that can't keep the one its OGI has it by: the one it got first, the one worked out for it, else the highest
        // one no OGI's animation has, no animation got and none was worked out for. None once the table has no room left, an animation never
        // takes the one worked out for another
        public UInt32? Other(string key)
        {
            if (Homes.TryGetValue(key, out var home))
            {
                return home;
            }

            if (SlotOf(key) is { } slot)
            {
                return slot;
            }

            for (var id = (UInt32)_tableSize; id-- > 0;)
            {
                if (IsFree(id))
                {
                    return id;
                }
            }

            return null;
        }

        // An ID one of the OGIs' animations has is its own, a home is what its animation gets by the IDs it can't keep
        public void Hold(string key, UInt32 id, bool isHome)
        {
            Debug.Assert(!Holders.TryGetValue(id, out var holder) || holder == key, $"ID {id:X} is another animation's");
            Holders[id] = key;
            if (isHome)
            {
                Homes.TryAdd(key, id);
            }
        }

        public void Give(string key, UInt32 own, UInt32 id)
        {
            Ids[(key, own)] = id;
            Hold(key, id, true);
        }

        private bool IsTaken(UInt32 id) => _reserved.Contains(id) || Holders.ContainsKey(id);

        private bool IsFree(UInt32 id) => !IsTaken(id) && !_slotted.Contains(id);
    }

    // An OGI's file's animations when it was this long and written then
    private sealed class ScannedFile
    {
        public long Length { get; set; }
        public long Ticks { get; set; }
        public List<ScannedAnimation> Animations { get; set; } = [];
    }

    // An animation of a file: the ID the OGI has it by and the SHA-256 of what it's made of (AnimationData.GetItemSource), empty when the
    // file has none of the game's bytes of it
    private sealed class ScannedAnimation
    {
        public UInt32 Id { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    // An ID builds gave and the animation (its key) it was given to
    private sealed class Given
    {
        public UInt32 Id { get; set; }
        public string Animation { get; set; } = string.Empty;
    }

    private sealed class Stored
    {
        public Int32 Version { get; set; }
        public List<Given> Given { get; set; } = [];
        public Dictionary<string, ScannedFile> Files { get; set; } = new();
    }

    private AnimationIds(string? path, string platform, Int32 tableSize)
    {
        _path = path;
        _platform = platform;
        _tableSize = tableSize;
    }

    /// <summary>
    /// IDs only this factory's chunks get, for what isn't built into a project (previews, tests): the project's OGIs' animations are taken
    /// when the first animation gets one
    /// </summary>
    public static AnimationIds ForOneBuild()
    {
        return new AnimationIds(null, string.Empty, TableSize);
    }

    /// <summary>
    /// The IDs the version's builds gave so far that are still the animations' they were given to, and the IDs every animation of the
    /// OGIs gets
    /// </summary>
    /// <param name="projectPath">The project's folder, the IDs are kept in its build cache</param>
    /// <param name="ogis">Every OGI of the project: their animations keep the IDs they have them by, no other animation gets one of them</param>
    /// <param name="isOfVersion">Whether an OGI is of the version, the others' animations come after its own (its chunks can draw the other
    /// version's models); every OGI without it</param>
    /// <param name="tableSize">How many animations the table has room for, the game's but in tests</param>
    public static AnimationIds Load(string projectPath, GamePlatform platform, IEnumerable<IAsset> ogis, Func<IAsset, bool>? isOfVersion = null,
        Int32 tableSize = TableSize)
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

        var ids = new AnimationIds(path, name, tableSize);
        var scanned = new Dictionary<string, ScannedFile>();
        ids._scan = ids.ScanOgis(ogis, isOfVersion, stored?.Files ?? new Dictionary<string, ScannedFile>(), scanned);
        ids._files = scanned;
        foreach (var given in stored?.Given ?? [])
        {
            ids._given.TryAdd(given.Id, given.Animation);
        }

        return ids;
    }

    /// <summary>
    /// The key of what an animation is made of, which <see cref="KeyOf(AnimationData)"/> gives of its item's source
    /// </summary>
    public string KeyOf(ReadOnlySpan<Byte> bytes) => KeyOfHash(HashOf(bytes));

    /// <summary>
    /// The key of what the animation is made of (<see cref="AnimationData.GetItemSource"/>), every copy's whatever its ID
    /// </summary>
    public string KeyOf(AnimationData animation) => KeyOf(animation.GetItemSource());

    /// <summary>
    /// What the build cache keeps the ID of the animation of the key by, its OGI having it by the ID given
    /// </summary>
    public static string DependencyOf(string key, UInt32 own) => $"{key}:{own:X}";

    /// <summary>
    /// Whether the build cache's key is an animation's ID (<see cref="DependencyOf"/>)
    /// </summary>
    public static bool IsKey(string key) => key.StartsWith(CachePrefix, StringComparison.Ordinal);

    /// <summary>
    /// The ID the animation of the key (<see cref="KeyOf(AnimationData)"/>) gets by the ID its OGI has it by: the one it got before, the
    /// OGI's own when the animation keeps it, else the one it got by another ID, the one worked out for it in the fixed order or the highest no
    /// OGI's animation has and no other animation got
    /// </summary>
    /// <param name="own">The ID its OGI has the animation by</param>
    /// <param name="what">The animation, named when the table is full</param>
    public UInt32 IdOf(string key, UInt32 own, string what)
    {
        lock (_lock)
        {
            var plan = Planned();
            if (!plan.Ids.TryGetValue((key, own), out var id))
            {
                // What the OGIs didn't have when the IDs were worked out (data changed since) goes by the same rules
                id = plan.CanKeep(key, own)
                    ? own
                    : plan.Other(key) ?? throw new InvalidOperationException($"{what} needs an ID of its own and the game's table of animations has room for " +
                                                                            $"{_tableSize}, which the IDs the OGIs have their animations by, the IDs " +
                                                                            "the builds gave and the animations game objects play before it take: take " +
                                                                            "the animations nothing plays out of the OGIs, or build every chunk to let go " +
                                                                            "of the IDs of animations no chunk has anymore");
                plan.Give(key, own, id);
            }

            _given[id] = key;
            return id;
        }
    }

    /// <summary>
    /// Whether the animation of the build cache's key (<see cref="DependencyOf"/>) still gets the ID it has, the hex of the ID
    /// </summary>
    public bool StillGives(string dependency, string id)
    {
        if (!TryParse(dependency, out var key, out var own) || !key.StartsWith($"{CachePrefix}{_platform}:", StringComparison.Ordinal))
        {
            return false;
        }

        lock (_lock)
        {
            var plan = Planned();
            // An animation of the OGIs gets its ID or the one worked out for it, one no OGI has what it would be given but a new ID
            UInt32? given = plan.Ids.TryGetValue((key, own), out var planned) ? planned
                : plan.CanKeep(key, own) ? own
                : plan.Homes.TryGetValue(key, out var home) ? home
                : plan.SlotOf(key);
            return given?.ToString("X") == id;
        }
    }

    /// <summary>
    /// Lets go of the IDs given to animations no chunk has anymore, once every chunk of the version got built
    /// </summary>
    /// <param name="dependencies">The animations the version's chunks have (<see cref="DependencyOf"/>), other versions' ones are left out</param>
    public void KeepOnly(IEnumerable<string> dependencies)
    {
        lock (_lock)
        {
            var kept = new HashSet<string>();
            foreach (var dependency in dependencies)
            {
                if (TryParse(dependency, out var key, out _))
                {
                    kept.Add(key);
                }
            }

            foreach (var id in _given.Where(given => !kept.Contains(given.Value)).Select(given => given.Key).ToList())
            {
                _given.Remove(id);
            }

            _plan = null;
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
            var stored = new Stored
            {
                Version = FileVersion,
                Given = _given.OrderBy(given => given.Key).Select(given => new Given { Id = given.Key, Animation = given.Value }).ToList(),
                Files = _files
            };
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(stored));
            File.Move(temporaryPath, _path, true);
        }
    }

    private Plan Planned() => _plan ??= MakePlan();

    private Scan Scanned() => _scan ??= ScanOgis(AssetManager.Get().GetAllAssetsOf<OGI>(), null, new Dictionary<string, ScannedFile>(), new Dictionary<string, ScannedFile>());

    // Past the room for every animation of the OGIs, the animations game objects play get it first, in the fixed order: the same IDs whatever
    // order the chunks ask in, and an animation of no room fails whenever a chunk asks for it. Given as the chunks asked, which animation
    // found the table full followed the order the chunks of a build were built in
    private Plan MakePlan()
    {
        var plan = MakePlan(null, out var full);
        return full ? MakePlan(PlayedKeys(Scanned()), out _) : plan;
    }

    // An ID builds gave stays the animation's while an OGI has it by that ID, or while no OGI's animation has the ID (the chunks the build
    // folder has keep theirs until they're built again), one no OGI's animation has being what the animation gets by the IDs it can't keep.
    // Then every animation of the OGIs in the fixed order keeps its OGI's ID unless an animation before it took it, the first one it keeps
    // being what it gets by the IDs it can't keep (taken in the given IDs' order it was the lowest, and a copy moved to it in the next build),
    // and the ones that can't keep theirs and have none get one worked out in the fixed order, the played ones' first when they're given,
    // given when a chunk asks for them. Whether some got none
    private Plan MakePlan(IReadOnlySet<string>? played, out bool full)
    {
        var scan = Scanned();
        var plan = new Plan(scan.Reserved, _tableSize);
        var claimed = scan.Claims.Select(claim => (claim.Own, claim.Key)).ToHashSet();
        foreach (var (id, key) in _given.OrderBy(given => given.Key).ToList())
        {
            if (id >= _tableSize || (scan.Reserved.Contains(id) && !claimed.Contains((id, key))))
            {
                _given.Remove(id);
                continue;
            }

            plan.Hold(key, id, !scan.Reserved.Contains(id));
        }

        var claims = scan.Claims.OrderBy(claim => claim.OfVersion ? 0 : 1).ThenBy(claim => claim.OfTheGame ? 0 : 1).ThenBy(claim => claim.Ogi, StringComparer.Ordinal)
            .ThenBy(claim => claim.Own).ThenBy(claim => claim.Key, StringComparer.Ordinal).ToList();
        foreach (var claim in claims)
        {
            if (plan.CanKeep(claim.Key, claim.Own))
            {
                plan.Give(claim.Key, claim.Own, claim.Own);
            }
        }

        full = false;
        IEnumerable<Claim> others = claims.Where(claim => !plan.Ids.ContainsKey((claim.Key, claim.Own))).ToList();
        foreach (var claim in played == null ? others : others.OrderBy(claim => played.Contains(claim.Key) ? 0 : 1))
        {
            if (plan.Homes.TryGetValue(claim.Key, out var home))
            {
                plan.Give(claim.Key, claim.Own, home);
            }
            else if (!plan.AddSlot(claim.Key))
            {
                full = true;
            }
        }

        return plan;
    }

    // The keys of the OGIs' animations game objects play
    private static HashSet<string> PlayedKeys(Scan scan)
    {
        var played = PlayedAnimations();
        return scan.Claims.Where(claim => played.Contains((claim.Ogi, claim.Own))).Select(claim => claim.Key).ToHashSet();
    }

    // What the game objects' model slots play, by the OGI's URI and the ID it has the animation by: an object's data where it's loaded (what
    // the build reads of it), else its file's, and what the chunks' own values of objects give them
    private static HashSet<(string Ogi, UInt32 Animation)> PlayedAnimations()
    {
        var played = new HashSet<(string, UInt32)>();
        if (Locator.Current.GetService<ProjectManager>()?.OpenedProject == null)
        {
            return played;
        }

        // What the project has, which a build working it out for a chunk mustn't take for what the chunk is made of: listing every asset
        // records nothing, looking them up by their type does
        var assets = AssetManager.Get().GetAssets();
        var slotsPath = $"{AssetOverrides.DataProperty}.{nameof(GameObjectData.ModelSlots)}";
        var chunksValues = assets.OfType<LevelChunk>().SelectMany(chunk => chunk.Overrides)
            .Where(values => values.Values.Keys.Any(path => path.StartsWith(slotsPath, StringComparison.Ordinal)))
            .ToLookup(values => values.Asset);
        foreach (var gameObject in assets.OfType<GameObject>())
        {
            JToken data;
            try
            {
                data = gameObject is { IsLoaded: true, AssetData: GameObjectData loaded } ? JObject.FromObject(loaded) : JToken.Parse(File.ReadAllText(gameObject.FullDataPath));
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                continue;
            }

            Add(data);
            foreach (var values in chunksValues[gameObject.URI])
            {
                var document = new JObject { [AssetOverrides.DataProperty] = data.DeepClone() };
                AssetOverrides.Put(document, values.Values.Where(value => AssetOverrides.IsDataPath(value.Key)));
                Add(document[AssetOverrides.DataProperty]!);
            }
        }

        return played;

        void Add(JToken data)
        {
            foreach (var slot in data[nameof(GameObjectData.ModelSlots)] as JArray ?? [])
            {
                if (slot[nameof(ModelSlot.Ogi)]?["_uri"] is JValue { Type: JTokenType.String } ogi && slot[nameof(ModelSlot.Animation)] is JValue { Type: JTokenType.Integer } animation)
                {
                    played.Add(((string)ogi!, (UInt32)(long)animation));
                }
            }
        }
    }

    // What the OGIs' animations are and the IDs below the table's size they have them by, the loaded ones' as they are (they can have changes
    // that aren't saved) and the others' as their files have them, which are only read again when they changed; the IDs a loaded OGI's file
    // has are kept from the others too
    private Scan ScanOgis(IEnumerable<IAsset> ogis, Func<IAsset, bool>? isOfVersion, IReadOnlyDictionary<string, ScannedFile> known,
        Dictionary<string, ScannedFile> scanned)
    {
        var scan = new Scan(_tableSize);
        var project = Locator.Current.GetService<ProjectManager>()?.OpenedProject;
        var gamePackages = GamePackagesOf(project);
        foreach (var ogi in ogis)
        {
            var uri = ogi.URI?.ToString() ?? string.Empty;
            var ofVersion = isOfVersion?.Invoke(ogi) ?? true;
            var ofTheGame = ogi.URI != null && gamePackages.Contains(ogi.URI.GetPackageName());
            var loaded = ogi is SerializableAsset { IsLoaded: true, AssetData: OGIData data } ? data : null;
            foreach (var animation in loaded?.Animations ?? [])
            {
                scan.Add(new Claim(uri, ofVersion, ofTheGame, animation.ID, KeyOf(animation)));
            }

            if (project == null)
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
                file = new ScannedFile { Length = info.Length, Ticks = info.LastWriteTimeUtc.Ticks, Animations = ReadAnimations(path, ogi) };
            }

            scanned[path] = file;
            foreach (var animation in file.Animations)
            {
                if (loaded == null && animation.Content.Length > 0)
                {
                    scan.Add(new Claim(uri, ofVersion, ofTheGame, animation.Id, KeyOfHash(animation.Content)));
                }
                else
                {
                    scan.Reserve(animation.Id);
                }
            }
        }

        return scan;
    }

    // The game's packages of the project, named after it: their OGIs' animations come before the ones of the packages the project made
    private static HashSet<string> GamePackagesOf(IProject? project)
    {
        if (project == null)
        {
            return [];
        }

        return [RetailAssets.GlobalPackageName(GamePlatform.PS2, project.Name), RetailAssets.PackageName(GamePlatform.PS2, project.Name),
            RetailAssets.GlobalPackageName(GamePlatform.Xbox, project.Name), RetailAssets.PackageName(GamePlatform.Xbox, project.Name)];
    }

    // An OGI's file's animations: the game's bytes a file TT Lab wrote keeps are them. A file the add-on wrote that TT Lab hasn't read since
    // can have keys edited after the bytes were made and animations made in Blender, which get their IDs as TT Lab reads the file: it's read
    // the way TT Lab reads it, in a scope of its own that takes nothing of it
    private static List<ScannedAnimation> ReadAnimations(string path, IAsset ogi)
    {
        try
        {
            var file = TlmFile.Load(path);
            if (file.Json[TlmMaterials.ExportedByKey] == null)
            {
                return OGIData.StoredAnimationsOf(file).Select(animation => Scanned(animation.Id, animation.Stored)).ToList();
            }

            // Reading the file records it as read by TT Lab, the project's watcher then takes the add-on's write for TT Lab's own: the editors
            // showing the model would keep what they have, and saving them would write it over the export
            var recorded = AssetFileStamps.IsAsRecorded(path);
            try
            {
                using var scope = new AssetDataScope { IsDryRead = true };
                scope.ReadAgain(ogi);
                return ogi.GetData<OGIData>().Animations.Select(animation => Scanned(animation.ID, animation)).ToList();
            }
            finally
            {
                if (!recorded)
                {
                    AssetFileStamps.Forget(path);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.WriteLine($"The animations of {ogi.Name} can't be read from {path}, the build leaves their IDs to them: {ex.Message}", Log.LogType.Warning);
            return [];
        }

        static ScannedAnimation Scanned(UInt32 id, AnimationData? animation) =>
            new() { Id = id, Content = animation == null ? string.Empty : HashOf(animation.GetItemSource()) };
    }

    private string KeyOfHash(string hash) => $"{CachePrefix}{_platform}:{hash}";

    private static string HashOf(ReadOnlySpan<Byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static bool TryParse(string dependency, out string key, out UInt32 own)
    {
        var separator = dependency.LastIndexOf(':');
        key = separator > 0 ? dependency[..separator] : string.Empty;
        own = 0;
        return separator > 0 && IsKey(dependency) && UInt32.TryParse(dependency.AsSpan(separator + 1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out own);
    }
}
