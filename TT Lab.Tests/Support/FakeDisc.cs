using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TT_Lab.Assets.Factory;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Tests.Support;

/// <summary>
/// A folder of made-up game files laid out the way creating a project takes them: the PS2 version's SYSTEM.CNF and its archive (Crash6/Crash.BH
/// and Crash.BD, empty until files are added), the Xbox version's default.xbe and its files loose in their folders. Deleted with it
/// </summary>
public sealed class FakeDisc : IDisposable
{
    private readonly List<(string Path, byte[] Content)> _files = [];

    public FakeDisc(GamePlatform platform)
    {
        Platform = platform;
        Folder = Path.Combine(Path.GetTempPath(), "TT Lab Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(Path.Combine(Folder, platform == GamePlatform.Xbox ? "default.xbe" : "SYSTEM.CNF"), []);
        if (platform == GamePlatform.PS2)
        {
            WriteArchive();
        }
    }

    public GamePlatform Platform { get; }

    /// <summary>
    /// The disc's folder, what creating a project and the repair take
    /// </summary>
    public string Folder { get; }

    /// <summary>
    /// The archive's files' SHA-256s, what a release's record has (the PS2 version's)
    /// </summary>
    public (string Header, string Archive) ArchiveSha256 => (Sha256(Path.Combine(Folder, "Crash6", "Crash.BH")), Sha256(Path.Combine(Folder, "Crash6", "Crash.BD")));

    /// <summary>
    /// A file of the game at its path on the disc ('\' or '/' between its folders): the PS2 version's go into its archive in the order
    /// they're added (the game's has the startup's files first), the Xbox version's are loose on the disc
    /// </summary>
    public FakeDisc Add(string path, byte[] content)
    {
        var archivePath = path.Replace('/', '\\');
        _files.Add((archivePath, content));
        if (Platform == GamePlatform.Xbox)
        {
            var file = Path.Combine(Folder, archivePath.Replace('\\', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, content);
        }
        else
        {
            WriteArchive();
        }

        return this;
    }

    /// <summary>
    /// The PS2 disc's release as its SYSTEM.CNF names it, its boot file and version
    /// </summary>
    public FakeDisc OfRelease(string executable, string version)
    {
        File.WriteAllText(Path.Combine(Folder, "SYSTEM.CNF"), $"BOOT2 = cdrom0:\\{executable};1\r\nVER = {version}\r\nVMODE = PAL\r\n");
        return this;
    }

    /// <summary>
    /// The Xbox disc's executable with the certificate of a release: its title, regions and version
    /// </summary>
    public FakeDisc OfXboxRelease(UInt32 titleId, UInt32 regions, UInt32 version)
    {
        const UInt32 baseAddress = 0x10000;
        const int certificate = 0x200;
        var bytes = new byte[0x1000];
        "XBEH"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x104), baseAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x118), baseAddress + certificate);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(certificate + 8), titleId);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(certificate + 0xA0), regions);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(certificate + 0xAC), version);
        File.WriteAllBytes(Path.Combine(Folder, "default.xbe"), bytes);
        return this;
    }

    /// <summary>
    /// A text file of the game, a byte per character like the game's
    /// </summary>
    public FakeDisc AddText(string path, string text) => Add(path, Encoding.Latin1.GetBytes(text));

    /// <summary>
    /// A game item's or section's bytes as a file of the game (a chunk, a save icon)
    /// </summary>
    public FakeDisc Add(string path, ITwinSerializable item) => Add(path, TestGeometry.Serialize(item));

    /// <summary>
    /// A level chunk's resource file (the PS2 version's .rm2, the Xbox version's .rmx) of the game objects, of the disc's version's classes,
    /// every other section empty and no scenery file
    /// </summary>
    public FakeDisc AddChunk(string path, params ITwinObject[] objects)
    {
        var isXbox = Platform == GamePlatform.Xbox;
        var chunk = (isXbox ? new XboxItemFactory() : new PS2ItemFactory()).GenerateRM();
        var gameObjects = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        foreach (var gameObject in objects)
        {
            gameObject.BehaviourPack ??= isXbox ? new XboxBehaviourCommandPack() : new PS2BehaviourCommandPack();
            gameObjects.AddItem(gameObject);
        }

        return Add(path, WithCollisionAndParticles(chunk, isXbox));
    }

    /// <summary>
    /// The default chunk (the PS2 version's Startup\Default.rm2, the Xbox version's Startup\Default.rmx) of the behaviours and game objects,
    /// its collision and particles empty
    /// </summary>
    public FakeDisc AddDefaultChunk(string path, IEnumerable<ITwinItem> behaviours, params ITwinObject[] objects)
    {
        var isXbox = Platform == GamePlatform.Xbox;
        var chunk = (isXbox ? new XboxItemFactory() : new PS2ItemFactory()).GenerateDefault();
        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        foreach (var behaviour in behaviours)
        {
            code.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION).AddItem(behaviour);
        }

        foreach (var gameObject in objects)
        {
            gameObject.BehaviourPack ??= isXbox ? new XboxBehaviourCommandPack() : new PS2BehaviourCommandPack();
            code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION).AddItem(gameObject);
        }

        var particles = isXbox ? new XboxDefaultParticleData() : new PS2DefaultParticleData();
        particles.SetID(Constants.LEVEL_PARTICLES_ITEM);
        chunk.AddItem(particles);
        return Add(path, chunk);
    }

    /// <summary>
    /// The chunk's collision and particles, empty: the unpack reads them of every chunk
    /// </summary>
    public static ITwinSection WithCollisionAndParticles(ITwinSection chunk, bool isXbox)
    {
        var collision = isXbox ? new XboxAnyCollisionData { Version = PS2AnyCollisionData.GameVersion } : new PS2AnyCollisionData { Version = PS2AnyCollisionData.GameVersion };
        collision.SetID(Constants.LEVEL_COLLISION_ITEM);
        chunk.AddItem(collision);
        var particles = isXbox ? new XboxAnyParticleData() : new PS2AnyParticleData();
        particles.SetID(Constants.LEVEL_PARTICLES_ITEM);
        chunk.AddItem(particles);
        return chunk;
    }

    // The archive's header has a record of every file's path, offset and length, its data has the files one after the other
    private void WriteArchive()
    {
        var crash6 = Path.Combine(Folder, "Crash6");
        Directory.CreateDirectory(crash6);
        using var header = new BinaryWriter(File.Create(Path.Combine(crash6, "Crash.BH")));
        using var data = File.Create(Path.Combine(crash6, "Crash.BD"));
        header.Write(0x501);
        var offset = 0;
        foreach (var (path, content) in _files)
        {
            new BHRecord { Path = path, Offset = offset, Length = content.Length }.Write(header);
            data.Write(content);
            offset += content.Length;
        }
    }

    private static string Sha256(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    public void Dispose()
    {
        try
        {
            Directory.Delete(Folder, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file still held open only leaves some garbage in the temporary directory
        }
    }
}
