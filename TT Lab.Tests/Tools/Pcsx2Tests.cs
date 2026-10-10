using System.Buffers.Binary;
using System.Text;
using TT_Lab.Tools.Pcsx2;

namespace TT_Lab.Tests.Tools;

// Playing a chunk in PCSX2: the executable patched to read a folder of the PC, PINE's messages and the game's states, and the folder
// the game reads, with made-up data in place of the PAL release's
public sealed class Pcsx2Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"TTLabPcsx2_{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private const int SegmentOffset = 0x1000;
    private const uint SegmentAddress = 0x100000;

    // One loaded segment from 0x100000 covering every address the patch touches, with the release's bytes at them
    private static byte[] MakeRetailLikeElf(GameRelease release)
    {
        var elf = new byte[SegmentOffset + 0x20b000];
        "\u007fELF"u8.CopyTo(elf);
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(0x1c), 0x34);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(0x2a), 0x20);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(0x2c), 1);
        var header = elf.AsSpan(0x34);
        BinaryPrimitives.WriteUInt32LittleEndian(header, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], SegmentOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], SegmentAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], (uint)(elf.Length - SegmentOffset));
        "cdrom0:\\"u8.CopyTo(At(elf, release.LoosePrefix));
        "rb batch=Crash6\\Crash"u8.CopyTo(At(elf, release.LaunchArguments));
        BinaryPrimitives.WriteUInt32LittleEndian(At(elf, release.StartChunkLoad), 0x3c050000 | (release.BeachLiteral + 0x8000) >> 16);
        BinaryPrimitives.WriteUInt32LittleEndian(At(elf, release.StartChunkLoad + 8), 0x24a50000 | release.BeachLiteral & 0xffff);
        new Random(5).NextBytes(At(elf, 0x200000)[..64]);
        return elf;
    }

    // The retail executable's CRC, with a word appended
    private static byte[] MakeRetailElf(GameRelease release)
    {
        var elf = MakeRetailLikeElf(release);
        return [.. elf, .. BitConverter.GetBytes(GameExecutable.Crc(elf) ^ release.Crc)];
    }

    private static Span<byte> At(byte[] elf, uint address) => elf.AsSpan((int)(SegmentOffset + address - SegmentAddress));

    private static string CString(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes[..bytes.IndexOf((byte)0)]);

    public static TheoryData<string> Releases => new(GameRelease.All.Select(release => release.Name));

    private static GameRelease Release(string name) => GameRelease.All.First(release => release.Name == name);

    [Theory]
    [MemberData(nameof(Releases))]
    public void TheExecutableReadsLooseFilesOfTheHostAndStartsInTheChunk(string name)
    {
        var release = Release(name);
        var retail = MakeRetailLikeElf(release);

        var patched = GameExecutable.Patch(release, retail, "Levels\\Earth\\Totem\\l03beach");

        Assert.Equal("host0:\\", CString(At(patched, release.LoosePrefix)));
        Assert.Equal("rb", CString(At(patched, release.LaunchArguments)));
        Assert.Equal("Levels\\Earth\\Totem\\l03beach", CString(At(patched, release.StartChunkGap)));
        // lui a1 and addiu a1, a1: CopyToString gets the chunk's path in place of the Beach literal
        var lui = BinaryPrimitives.ReadUInt32LittleEndian(At(patched, release.StartChunkLoad));
        var addiu = BinaryPrimitives.ReadUInt32LittleEndian(At(patched, release.StartChunkLoad + 8));
        Assert.Equal(release.StartChunkGap, ((lui & 0xffff) << 16) + (uint)(short)(addiu & 0xffff));
        Assert.Equal(0x3c05u, lui >> 16);
        Assert.Equal(0x24a5u, addiu >> 16);
        Assert.Equal(retail.Length + 4, patched.Length);
        Assert.True(patched.AsSpan(0, 0x3000).SequenceEqual(retail.AsSpan(0, 0x3000)));
    }

    [Theory]
    [MemberData(nameof(Releases))]
    public void EveryChunkGivesTheSameCrcSoOneGameSettingsFileServesThemAll(string name)
    {
        var release = Release(name);
        var retail = MakeRetailLikeElf(release);

        Assert.Equal(release.PatchedCrc, GameExecutable.Crc(GameExecutable.Patch(release, retail, "Levels\\Earth\\Hub\\Beach")));
        Assert.Equal(release.PatchedCrc, GameExecutable.Crc(GameExecutable.Patch(release, retail, "Levels\\AltEarth\\RockSlid\\l10chasb")));
        Assert.Equal(GameRelease.All.Count, GameRelease.All.Select(item => item.PatchedCrc).Distinct().Count());
    }

    [Fact]
    public void OnlyTheReleasesOwnBytesAndPathsThatFitArePatched()
    {
        var other = MakeRetailLikeElf(GameRelease.Pal);
        "cdrom1:\\"u8.CopyTo(At(other, GameRelease.Pal.LoosePrefix));

        Assert.Throws<InvalidDataException>(() => GameExecutable.Patch(GameRelease.Pal, other, "Levels\\Earth\\Hub\\Beach"));
        Assert.Throws<InvalidDataException>(() => GameExecutable.Patch(GameRelease.Ntsc200, MakeRetailLikeElf(GameRelease.Pal), "Levels\\Earth\\Hub\\Beach"));
        Assert.Throws<ArgumentException>(() => GameExecutable.Patch(GameRelease.Pal, MakeRetailLikeElf(GameRelease.Pal), new string('a', GameExecutable.MaxChunkPathLength + 1)));
        Assert.Equal("Levels\\Earth\\Hub\\Beach", GameExecutable.ChunkPath("Levels/Earth/Hub/Beach"));
    }

    [Fact]
    public void SystemCnfNamesTheExecutableAndVersion()
    {
        Assert.Equal(("SLES_525.68", "1.01"), GameRelease.ReadSystemCnf("BOOT2 = cdrom0:\\SLES_525.68;1 \r\nVER = 1.01\r\nVMODE = PAL\r\n"));
        Assert.Equal(("SLUS_209.09", "2.00"), GameRelease.ReadSystemCnf("BOOT2 = cdrom0:\\SLUS_209.09;1\r\nVER = 2.00\r\nVMODE = NTSC\r\n"));
        Assert.Throws<InvalidDataException>(() => GameRelease.ReadSystemCnf("VMODE = NTSC"));
    }

    private string MakeDisc(string systemCnf, string executable, byte[] elf, bool upperCase)
    {
        var disc = Path.Combine(_root, $"disc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(disc);
        File.WriteAllText(Path.Combine(disc, upperCase ? "SYSTEM.CNF" : "System.cnf"), systemCnf);
        File.WriteAllBytes(Path.Combine(disc, executable), elf);
        return disc;
    }

    [Fact]
    public void TheDiscsReleaseComesFromSystemCnfAndTheExecutablesCrc()
    {
        var ntsc2 = MakeDisc("BOOT2 = cdrom0:\\SLUS_209.09;1\r\nVER = 2.00\r\nVMODE = NTSC\r\n", "SLUS_209.09", MakeRetailElf(GameRelease.Ntsc200), true);
        var ntsc1 = MakeDisc("BOOT2 = cdrom0:\\SLUS_209.09;1 \r\nVER = 1.00\r\nVMODE = NTSC\r\n", "SLUS_209.09", MakeRetailElf(GameRelease.Ntsc100), true);
        var pal = MakeDisc("BOOT2 = cdrom0:\\SLES_525.68;1 \r\nVER = 1.01\r\nVMODE = PAL\r\n", "SLES_525.68", MakeRetailElf(GameRelease.Pal), false);

        Assert.Same(GameRelease.Ntsc200, GameRelease.Detect(ntsc2));
        Assert.Same(GameRelease.Ntsc100, GameRelease.Detect(ntsc1));
        Assert.Same(GameRelease.Pal, GameRelease.Detect(pal));
    }

    [Fact]
    public void AModifiedExecutableOrAnotherReleaseIsTurnedDown()
    {
        var modified = MakeDisc("BOOT2 = cdrom0:\\SLUS_209.09;1\r\nVER = 2.00\r\n", "SLUS_209.09", MakeRetailLikeElf(GameRelease.Ntsc200), true);
        var palOld = MakeDisc("BOOT2 = cdrom0:\\SLES_525.68;1 \r\nVER = 1.00\r\nVMODE = PAL\r\n", "SLES_525.68", MakeRetailElf(GameRelease.Pal), false);

        Assert.Contains("isn't the NTSC 2.00 release's executable", Assert.Throws<InvalidDataException>(() => GameRelease.Detect(modified)).Message);
        Assert.Contains("SLES_525.68 version 1.00", Assert.Throws<InvalidDataException>(() => GameRelease.Detect(palOld)).Message);
    }

    // PCSX2's side of PINE over a memory of words: reads answer from it, writes change it, a request for an unknown opcode fails
    private sealed class FakePcsx2 : Stream
    {
        private readonly MemoryStream _answers = new();
        public Dictionary<uint, uint> Memory { get; } = [];
        public List<byte[]> Requests { get; } = [];
        public PineStatus Status { get; set; } = PineStatus.Running;
        // PCSX2 closed the connection: nothing more to read
        public bool Closed { get; set; }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> request)
        {
            Requests.Add(request.ToArray());
            Assert.Equal(request.Length, (int)BinaryPrimitives.ReadUInt32LittleEndian(request));
            var address = request.Length >= 9 ? BinaryPrimitives.ReadUInt32LittleEndian(request[5..]) : 0;
            byte[] answer = request[4] switch
            {
                0 => [0, (byte)(Memory.GetValueOrDefault(address & ~3u) >> (int)(address % 4 * 8))],
                2 => [0, .. BitConverter.GetBytes(Memory.GetValueOrDefault(address))],
                3 => [0, .. BitConverter.GetBytes(Memory.GetValueOrDefault(address)), .. BitConverter.GetBytes(Memory.GetValueOrDefault(address + 4))],
                6 => Store(address, BinaryPrimitives.ReadUInt32LittleEndian(request[9..])),
                15 => [0, .. BitConverter.GetBytes((uint)Status)],
                _ => [0xff]
            };
            var position = _answers.Position;
            _answers.Seek(0, SeekOrigin.End);
            _answers.Write(BitConverter.GetBytes(answer.Length + 4));
            _answers.Write(answer);
            _answers.Position = position;
        }

        private byte[] Store(uint address, uint value)
        {
            Memory[address] = value;
            return [0];
        }

        public void Poke(uint address, string text)
        {
            var bytes = Encoding.Latin1.GetBytes(text + "\0\0\0\0");
            for (var i = 0; i + 4 <= bytes.Length; i += 4)
            {
                Memory[address + (uint)i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i));
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => Closed ? 0 : _answers.Read(buffer, offset, count);
        public override void Flush() { }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    [Fact]
    public void PineRequestsAreTheirSizeAnOpcodeAndArguments()
    {
        var pcsx2 = new FakePcsx2();
        pcsx2.Memory[0x309c60] = 1234;
        using var pine = new PineClient(pcsx2);

        Assert.Equal(1234u, pine.ReadUInt32(0x309c60));
        pine.WriteUInt32(0x1000, 0xdeadbeef);

        Assert.Equal<byte>([9, 0, 0, 0, 2, 0x60, 0x9c, 0x30, 0], pcsx2.Requests[0]);
        Assert.Equal<byte>([13, 0, 0, 0, 6, 0, 0x10, 0, 0, 0xef, 0xbe, 0xad, 0xde], pcsx2.Requests[1]);
        Assert.Equal(0xdeadbeefu, pcsx2.Memory[0x1000]);
        Assert.Equal(PineStatus.Running, pine.GetStatus());
    }

    // PCSX2's shutdown waits for its PINE server, which waits for its client's next request: a session asking nothing once the game
    // played kept PCSX2 running without a window after its game window got closed. Sessions ask for the status every second, a paused
    // game still plays, a shut down one or a connection PCSX2 closed doesn't
    [Fact]
    public void ASessionTellsItsGameStoppedByThePineStatus()
    {
        var pcsx2 = new FakePcsx2();
        var game = new RunningGame(new PineClient(pcsx2), GameRelease.Pal);

        Assert.False(Pcsx2Session.HasStopped(game));
        pcsx2.Status = PineStatus.Paused;
        Assert.False(Pcsx2Session.HasStopped(game));
        pcsx2.Status = PineStatus.Shutdown;
        Assert.True(Pcsx2Session.HasStopped(game));
        pcsx2.Status = PineStatus.Running;
        pcsx2.Closed = true;
        Assert.True(Pcsx2Session.HasStopped(game));
    }

    [Fact]
    public void PineReadsStringsAWordAtATimeUpToTheirEnd()
    {
        var pcsx2 = new FakePcsx2();
        pcsx2.Poke(0x2000, "levels\\earth\\hub\\beach");
        using var pine = new PineClient(pcsx2);

        Assert.Equal("levels\\earth\\hub\\beach", pine.ReadString(0x2000));
        Assert.Equal("levels", pine.ReadString(0x2000, 6));
    }

    [Fact]
    public void TheGameMovesToTheStateAskedForThroughTheControllersNextState()
    {
        var pcsx2 = new FakePcsx2();
        const uint controller = 0x9870c0;
        pcsx2.Memory[0x309888] = controller;
        // Playing (12) now, after the title (7), no next state (0x18), and a bit of the low word the game keeps
        pcsx2.Memory[controller + 8] = 0x80000000;
        pcsx2.Memory[controller + 0xC] = 0x18u << 18 | 12u << 12 | 7u << 6 | 5;
        var game = new RunningGame(new PineClient(pcsx2), GameRelease.Pal);

        Assert.Equal(GameState.Playing, game.State);
        game.Request(GameState.LoadStartChunk);

        Assert.Equal(6u << 18 | 12u << 12 | 7u << 6 | 5, pcsx2.Memory[controller + 0xC]);
        Assert.Equal(0x80000000u, pcsx2.Memory[controller + 8]);
    }

    // The title's character slot 4 plays its cinematic: a title skipped straight to playing kept it running, and the beach never got
    // past its "THREE YEARS AGO..."
    [Fact]
    public void TheGameGoesToPlayingThroughItsOwnNewGameWithoutTheMovie()
    {
        Assert.Equal(GameState.LoadStartChunk, Pcsx2Session.NextOnTheWayToPlaying(GameState.Logos, true));
        Assert.Null(Pcsx2Session.NextOnTheWayToPlaying(GameState.Logos, false));
        Assert.Equal(GameState.NewGame, Pcsx2Session.NextOnTheWayToPlaying(GameState.Title, true));
        Assert.Equal(GameState.StartPlaying, Pcsx2Session.NextOnTheWayToPlaying(GameState.Movie, false));
        foreach (var state in new[] { GameState.LoadStartChunk, GameState.NewGame, GameState.StartPlaying, GameState.Playing, GameState.None })
        {
            Assert.Null(Pcsx2Session.NextOnTheWayToPlaying(state, false));
        }
    }

    // The NTSC releases' game controller lacks the PAL one's first 8 bytes: its states are at +0 and the start's timer at +0xC
    [Fact]
    public void TheNtscReleasesStatesAndTimerAreEightBytesEarlier()
    {
        var pcsx2 = new FakePcsx2();
        const uint controller = 0x986000;
        pcsx2.Memory[GameRelease.Ntsc200.GameControllerPointer] = controller;
        pcsx2.Memory[controller + 4] = 0x18u << 18 | 12u << 12 | 11u << 6;
        pcsx2.Memory[controller + 0xC] = 9;
        var game = new RunningGame(new PineClient(pcsx2), GameRelease.Ntsc200);

        Assert.Equal(GameState.Playing, game.State);
        game.Request(GameState.LoadStartChunk);
        game.SkipStartNotices();

        Assert.Equal(6u << 18 | 12u << 12 | 11u << 6, pcsx2.Memory[controller + 4]);
        Assert.Equal(0u, pcsx2.Memory[controller + 0xC]);
    }

    [Fact]
    public void TheStartsNoticesAreSkippedThroughTheControllersTimer()
    {
        var pcsx2 = new FakePcsx2();
        const uint controller = 0x9870c0;
        pcsx2.Memory[0x309888] = controller;
        pcsx2.Memory[controller + 0x14] = 12;
        var game = new RunningGame(new PineClient(pcsx2), GameRelease.Pal);

        game.SkipStartNotices();

        Assert.Equal(0u, pcsx2.Memory[controller + 0x14]);
    }

    // Reloading only set the count to 0, the third slot still pointed at the chunk loaded before and was listed with it
    [Fact]
    public void TheGameListsTheChunksItsManagerHasLoaded()
    {
        var pcsx2 = new FakePcsx2();
        pcsx2.Memory[0x30a0a8] = 0x5000;
        pcsx2.Memory[0x5000] = 0x12340002;
        pcsx2.Memory[0x5004] = 0x6000;
        pcsx2.Memory[0x5008] = 0x6100;
        pcsx2.Memory[0x500c] = 0x6200;
        pcsx2.Memory[0x6000] = 0x7000;
        pcsx2.Memory[0x6004] = 22;
        pcsx2.Memory[0x6100] = 0x7100;
        pcsx2.Memory[0x6104] = 21;
        pcsx2.Memory[0x6200] = 0x7200;
        pcsx2.Memory[0x6204] = 21;
        pcsx2.Poke(0x7000, "levels\\earth\\hub\\beach");
        pcsx2.Poke(0x7100, "levels\\earth\\hub\\huba");
        pcsx2.Poke(0x7200, "levels\\earth\\hub\\hubc");
        var game = new RunningGame(new PineClient(pcsx2), GameRelease.Pal);

        Assert.Equal(["levels\\earth\\hub\\beach", "levels\\earth\\hub\\huba"], game.LoadedChunks());
        Assert.Equal(GameState.None, game.State);
    }

    private static void WriteArchive(string discPath, params (string Path, byte[] Bytes)[] files)
    {
        var crash6 = Path.Combine(discPath, "Crash6");
        Directory.CreateDirectory(crash6);
        using var header = new BinaryWriter(File.Create(Path.Combine(crash6, "Crash.BH")));
        using var data = new BinaryWriter(File.Create(Path.Combine(crash6, "Crash.BD")));
        header.Write(0x501);
        foreach (var (path, bytes) in files)
        {
            header.Write(path.Length);
            header.Write(Encoding.Latin1.GetBytes(path));
            header.Write((int)data.BaseStream.Position);
            header.Write(bytes.Length);
            data.Write(bytes);
        }
    }

    [Fact]
    public void TheFolderHasTheArchivesFilesInUpperCaseTheBuildsOverThemAndTheSoundBanks()
    {
        var disc = Path.Combine(_root, "disc");
        var build = Path.Combine(_root, "build", "archives");
        var folder = new Pcsx2DevFolder(Path.Combine(_root, "build", "pcsx2"));
        WriteArchive(disc, ("Startup\\Default.rm2", [1, 2, 3]), ("Levels\\Earth\\Hub\\Beach.rm2", [4, 5]), ("Levels\\Earth\\Hub\\Beach.sm2", [6]));
        File.WriteAllBytes(Path.Combine(disc, "Crash6", "Music.mh"), [7]);
        File.WriteAllBytes(Path.Combine(disc, "Crash6", "Music.mb"), [8, 9]);
        Directory.CreateDirectory(Path.Combine(build, "Levels", "earth", "hub"));
        File.WriteAllBytes(Path.Combine(build, "Levels", "earth", "hub", "beach.rm2"), [10, 11, 12]);

        Assert.Equal(3, folder.ExtractArchive(disc));
        Assert.Equal(1, folder.CopyBuiltFiles(build));
        Assert.Equal(2, folder.CopySoundBanks(disc));

        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(folder.FolderPath, "STARTUP", "DEFAULT.RM2")));
        Assert.Equal([10, 11, 12], File.ReadAllBytes(Path.Combine(folder.FolderPath, "LEVELS", "EARTH", "HUB", "BEACH.RM2")));
        Assert.Equal([6], File.ReadAllBytes(Path.Combine(folder.FolderPath, "LEVELS", "EARTH", "HUB", "BEACH.SM2")));
        Assert.Equal([8, 9], File.ReadAllBytes(Path.Combine(folder.FolderPath, "CRASH6", "MUSIC.MB")));
        Assert.Empty(Directory.EnumerateFiles(folder.FolderPath, "*.tmp", SearchOption.AllDirectories));
        // Nothing changed, nothing's written again
        Assert.Equal(0, folder.ExtractArchive(disc));
        Assert.Equal(0, folder.CopyBuiltFiles(build));
        Assert.Equal(0, folder.CopySoundBanks(disc));
    }

    [Fact]
    public void AnNtscDiscsUpperCaseFoldersAreFound()
    {
        var disc = Path.Combine(_root, "ntsc");
        var folder = new Pcsx2DevFolder(Path.Combine(_root, "build", "pcsx2"));
        WriteArchive(disc, ("Levels\\Earth\\Hub\\Beach.rm2", [4, 5]));
        Directory.Move(Path.Combine(disc, "Crash6"), Path.Combine(disc, "CRASH6"));
        File.Move(Path.Combine(disc, "CRASH6", "Crash.BH"), Path.Combine(disc, "CRASH6", "CRASH.BH"));
        File.Move(Path.Combine(disc, "CRASH6", "Crash.BD"), Path.Combine(disc, "CRASH6", "CRASH.BD"));
        File.WriteAllBytes(Path.Combine(disc, "CRASH6", "AMERICAN.MB"), [1]);

        Assert.Equal(1, folder.ExtractArchive(disc));
        Assert.Equal(1, folder.CopySoundBanks(disc));
        Assert.True(File.Exists(Path.Combine(folder.FolderPath, "LEVELS", "EARTH", "HUB", "BEACH.RM2")));
        Assert.True(File.Exists(Path.Combine(folder.FolderPath, "CRASH6", "AMERICAN.MB")));
    }

    [Fact]
    public void AChunkBuiltAgainIsCopiedAgain()
    {
        var build = Path.Combine(_root, "build", "archives");
        var folder = new Pcsx2DevFolder(Path.Combine(_root, "build", "pcsx2"));
        var chunk = Path.Combine(build, "Levels", "earth", "hub", "beach.rm2");
        Directory.CreateDirectory(Path.GetDirectoryName(chunk)!);
        File.WriteAllBytes(chunk, [1, 2]);
        folder.CopyBuiltFiles(build);

        File.WriteAllBytes(chunk, [3, 4]);
        File.SetLastWriteTimeUtc(chunk, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(1, folder.CopyBuiltFiles(build));
        Assert.Equal([3, 4], File.ReadAllBytes(Path.Combine(folder.FolderPath, "LEVELS", "EARTH", "HUB", "BEACH.RM2")));
    }

    [Fact]
    public void TheGameSettingsGetTTLabsKeysAndKeepTheUsers()
    {
        var keys = new Dictionary<string, string> { ["HostFs"] = "true", ["EnablePINE"] = "true" };

        Assert.Equal("[EmuCore]\nHostFs = true\nEnablePINE = true\n", Pcsx2Install.WithKeys("", "EmuCore", keys));
        Assert.Equal("[EmuCore/GS]\nupscale_multiplier = 3\n\n[EmuCore]\nHostFs = true\nEnablePINE = true\n",
            Pcsx2Install.WithKeys("[EmuCore/GS]\nupscale_multiplier = 3\n", "EmuCore", keys));
        Assert.Equal("[EmuCore]\nEnableCheats = true\nHostFs = true\nEnablePINE = true\n\n[Patches]\nEnable = Widescreen\n",
            Pcsx2Install.WithKeys("[EmuCore]\nEnableCheats = true\nHostFs = false\n\n[Patches]\nEnable = Widescreen\n", "EmuCore", keys));
    }

    // The PAL release's level select and save state (Ghidra Stuff/Engine_RE/pnach) goes in PCSX2's patches under the patched
    // executable's CRC, outside a group so it's always on. The other releases have none
    [Fact]
    public void ThePalReleasePlaysWithTheLevelSelectPatch()
    {
        var folder = Path.Combine(_root, "pcsx2");
        Directory.CreateDirectory(folder);
        var executable = Path.Combine(folder, "pcsx2-qt");
        File.WriteAllText(executable, "");
        File.WriteAllText(Path.Combine(folder, "portable.ini"), "");
        var install = Pcsx2Install.Find(executable)!;

        foreach (var release in GameRelease.All)
        {
            install.WriteGameSettings(release);
            install.WritePatches(release);
        }

        var lines = File.ReadAllLines(install.PatchesPath(GameRelease.Pal));
        Assert.StartsWith("gametitle=", lines[0]);
        Assert.DoesNotContain(lines, line => line.StartsWith('['));
        var patches = lines.Where(line => line.StartsWith("patch=")).Select(line => line.Split(',')).ToList();
        Assert.All(patches, fields => Assert.Equal(["patch=2", "EE", "word"], [fields[0], fields[1], fields[3]]));
        // The frame, the pause menu's options item and state 11's respawn call the patch's code, which is from 0xA0000 on
        Assert.Equal(["00100bf8", "00164094", "001746bc"], patches.Take(3).Select(fields => fields[2]));
        Assert.All(patches.Take(3), fields => Assert.InRange(Convert.ToUInt32(fields[4], 16) ^ 0x0C000000u, 0xA0000u >> 2, 0xAFFFFu >> 2));
        Assert.All(patches.Skip(3), fields => Assert.InRange(Convert.ToUInt32(fields[2], 16), 0xA0000u, 0xAFFFFu));
        Assert.Contains("EnablePatches = true", File.ReadAllText(install.GameSettingsPath(GameRelease.Pal)));
        // Closing the game's window ends the run without PCSX2 asking first, and without saving a state to resume it
        Assert.All(GameRelease.All, release =>
        {
            var settings = File.ReadAllText(install.GameSettingsPath(release));
            Assert.Contains("[UI]\nConfirmShutdown = false\n", settings);
            Assert.Contains("SaveStateOnShutdown = false", settings);
        });
        Assert.False(File.Exists(install.PatchesPath(GameRelease.Ntsc100)));
        Assert.DoesNotContain("EnablePatches", File.ReadAllText(install.GameSettingsPath(GameRelease.Ntsc200)));
        // Written once
        var written = File.GetLastWriteTimeUtc(install.PatchesPath(GameRelease.Pal));
        File.SetLastWriteTimeUtc(install.PatchesPath(GameRelease.Pal), written.AddHours(-1));
        install.WritePatches(GameRelease.Pal);
        Assert.Equal(written.AddHours(-1), File.GetLastWriteTimeUtc(install.PatchesPath(GameRelease.Pal)));
    }

    [Fact]
    public void TheProjectsLevelSelectGoesOverTheArchives()
    {
        var disc = Path.Combine(_root, "disc");
        var folder = new Pcsx2DevFolder(Path.Combine(_root, "build", "pcsx2"));
        WriteArchive(disc, ("Startup\\LevelSelect.txt", [1]), ("Levels\\Earth\\Hub\\Beach.rm2", [4, 5]));
        folder.ExtractArchive(disc);

        Assert.Equal(["STARTUP/LEVELSELECT.TXT", "LEVELS/EARTH/HUB/BEACH.RM2"], Pcsx2DevFolder.ArchiveFiles(disc).Order().Reverse());
        Assert.True(folder.WriteFile("Startup/LevelSelect.txt", [2, 3]));
        Assert.False(folder.WriteFile("Startup/LevelSelect.txt", [2, 3]));
        Assert.Equal([2, 3], File.ReadAllBytes(Path.Combine(folder.FolderPath, "STARTUP", "LEVELSELECT.TXT")));
    }

    // The preferences' extra arguments are one line split like a shell's, quotes keep spaces in, and Windows' backslashes stay
    [Fact]
    public void ExtraArgumentsAreSplitLikeAShellsButForBackslashes()
    {
        Assert.Empty(TT_Lab.Util.CommandLineArguments.Split("  "));
        Assert.Equal(["-fullscreen", "-bios", "C:\\BIOS\\scph 10000.bin", "it's", ""],
            TT_Lab.Util.CommandLineArguments.Split(" -fullscreen  -bios \"C:\\BIOS\\scph 10000.bin\" \"it's\" ''"));
        Assert.Equal(["say \"hi\"", "/home/a b/c"], TT_Lab.Util.CommandLineArguments.Split("\"say \\\"hi\\\"\" '/home/a b/c'"));
    }

    // They go after TT Lab's own and before the disc image, which PCSX2 takes after its options
    [Fact]
    public void ExtraArgumentsGoBeforeTheDiscImage()
    {
        var folder = Path.Combine(_root, "pcsx2");
        Directory.CreateDirectory(folder);
        var executable = Path.Combine(folder, "pcsx2-qt");
        File.WriteAllText(executable, "");
        File.WriteAllText(Path.Combine(folder, "portable.ini"), "");
        var install = Pcsx2Install.Find(executable)!;

        var info = install.StartInfo("/dev/SLES_525.68", "/games/Crash.iso", ["-fullscreen", "-bios", "a b.bin"]);

        Assert.Equal(executable, info.FileName);
        Assert.Equal(["-nogui", "-fastboot", "-elf", "/dev/SLES_525.68", "-fullscreen", "-bios", "a b.bin", "--", "/games/Crash.iso"], info.ArgumentList);
        Assert.Equal(["-nogui", "-fastboot", "-elf", "/dev/SLES_525.68", "--", "/games/Crash.iso"], install.StartInfo("/dev/SLES_525.68", "/games/Crash.iso").ArgumentList);
    }

    // PCSX2 2.0 and 2.2 open PINE on its default slot whatever the game settings say: a session takes the game there only when it carries the
    // session's start chunk, which the patched executable keeps in the gap after .vutext, so another PCSX2 with PINE on is left alone
    [Fact]
    public void TheDefaultPineSlotsGameIsTakenWhenItPlaysTheSession()
    {
        var pcsx2 = new FakePcsx2();
        using var pine = new PineClient(pcsx2);
        Assert.False(Pcsx2Session.PlaysSession(pine, GameRelease.Pal, "Levels\\Earth\\Hub\\Beach"));

        pcsx2.Poke(GameRelease.Pal.StartChunkGap, "Levels\\Earth\\Hub\\Beach");
        Assert.True(Pcsx2Session.PlaysSession(pine, GameRelease.Pal, "Levels\\Earth\\Hub\\Beach"));
        Assert.False(Pcsx2Session.PlaysSession(pine, GameRelease.Pal, "Levels\\Earth\\Hub\\Beach2"));
        Assert.False(Pcsx2Session.PlaysSession(pine, GameRelease.Ntsc100, "Levels\\Earth\\Hub\\Beach"));
    }

    private string MakeExecutable(string folderName, string name, byte[]? content = null)
    {
        var folder = Path.Combine(_root, folderName);
        Directory.CreateDirectory(folder);
        var executable = Path.Combine(folder, name);
        File.WriteAllBytes(executable, content ?? []);
        return executable;
    }

    // PCSX2's Path::Combine: the Windows build takes either slash and writes backslashes, every build makes a run of separators one and
    // leaves none at the end
    [Theory]
    [InlineData(@"C:\PCSX2", "/elsewhere", true, @"C:\PCSX2\elsewhere")]
    [InlineData(@"C:\PCSX2\", @"data//inner\", true, @"C:\PCSX2\data\inner")]
    [InlineData(@"C:\PCSX2", "/", true, @"C:\PCSX2")]
    [InlineData("/opt/pcsx2", "/elsewhere", false, "/opt/pcsx2/elsewhere")]
    [InlineData("/opt/pcsx2/", "data//inner/", false, "/opt/pcsx2/data/inner")]
    [InlineData("/opt/pcsx2", @"back\slash", false, @"/opt/pcsx2/back\slash")]
    public void PortableFoldersAreJoinedLikePcsx2Does(string folder, string named, bool windows, string joined)
    {
        Assert.Equal(joined, Pcsx2Install.JoinLikePcsx2(folder, named, windows));
    }

    // Where PCSX2 keeps its data (EmuFolders::SetDataDirectory): portable next to itself with a portable.ini or portable.txt there or
    // told -portable, in the folder portable.txt names under it (2.2.0 on), an AppImage only when told so and in a PCSX2 folder next to it
    [Fact]
    public void ThePortableSettingsAreWherePcsx2KeepsThem()
    {
        var installed = MakeExecutable("installed", "pcsx2-qt");
        var user = Pcsx2Install.NativeSettingsFolder(installed, false);
        Assert.EndsWith("PCSX2", user);
        Assert.NotEqual(Path.GetDirectoryName(installed), user);
        Assert.Equal(Path.GetDirectoryName(installed), Pcsx2Install.NativeSettingsFolder(installed, true));
        Assert.Equal(Path.GetDirectoryName(installed), Pcsx2Install.Find(installed, ["-fullscreen", "-portable"])!.SettingsFolder);
        Assert.Equal(user, Pcsx2Install.Find(installed, ["-fullscreen"])!.SettingsFolder);

        var named = MakeExecutable("named", "pcsx2-qt");
        File.WriteAllText(Path.Combine(_root, "named", "portable.txt"), "  data\n");
        Assert.Equal(Path.Combine(_root, "named", "data"), Pcsx2Install.NativeSettingsFolder(named, false));
        // Appended to its folder even when absolute
        File.WriteAllText(Path.Combine(_root, "named", "portable.txt"), "/elsewhere");
        Assert.Equal(Path.Combine(_root, "named", "elsewhere"), Pcsx2Install.NativeSettingsFolder(named, false));
        // 2.0.x keep their data next to the executable whatever it names, where they made their inis folder
        Directory.CreateDirectory(Path.Combine(_root, "named", "inis"));
        Assert.Equal(Path.Combine(_root, "named"), Pcsx2Install.NativeSettingsFolder(named, false));
        Directory.CreateDirectory(Path.Combine(_root, "named", "elsewhere", "inis"));
        Assert.Equal(Path.Combine(_root, "named", "elsewhere"), Pcsx2Install.NativeSettingsFolder(named, false));

        var ini = MakeExecutable("ini", "pcsx2-qt");
        File.WriteAllText(Path.Combine(_root, "ini", "portable.ini"), "");
        Assert.Equal(Path.Combine(_root, "ini"), Pcsx2Install.NativeSettingsFolder(ini, false));

        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var appImage = MakeExecutable("appimage", "pcsx2-v2.4.0-linux-appimage-x64-Qt.AppImage", [0x7f, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0, (byte)'A', (byte)'I', 2, 0, 0, 0, 0, 0]);
        Assert.True(Pcsx2Install.IsAppImage(appImage));
        Assert.False(Pcsx2Install.IsAppImage(installed));
        // Its own folder is inside its mount: a portable.txt next to it changes nothing
        File.WriteAllText(Path.Combine(_root, "appimage", "portable.txt"), "");
        Assert.Equal(user, Pcsx2Install.NativeSettingsFolder(appImage, false));
        Assert.Equal(Path.Combine(_root, "appimage", "PCSX2"), Pcsx2Install.NativeSettingsFolder(appImage, true));
    }
}

