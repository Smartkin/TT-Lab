using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TT_Lab.Tools.Pcsx2;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Project.Repair;

/// <summary>
/// Whether a folder has a version's game files the repair can read its packages from again. TT Lab 1.1.0's builds wrote the built archive over
/// the PS2 disc folder's Crash6/Crash.BH and Crash.BD (a project's own copy, or the user's folder when the disc wasn't copied), so a PS2 folder
/// is taken by its archive's hashes: a release's, or one the user says is the original disc. The Xbox builds never wrote the disc folder
/// </summary>
public static class RepairDiscs
{
    // Crash.BH's and Crash.BD's SHA-256s
    public sealed record Release(string Name, string Header, string Archive);

    // NTSC 1.00's and 2.00's copies here couldn't be told from built ones: their folders are taken once the user says they're the original disc
    public static readonly IReadOnlyList<Release> KnownPs2Archives =
    [
        new("PAL 1.01", "5ccfd726313b5dff4742f3daa40a3526a848187f609f1a1ed569697b4c093371", "b835ca8528b5c42d5a7fa81775f147011dafa4e6028446332f6821b94e4e2841"),
    ];

    public enum Verdict
    {
        // Not the version's game files
        Missing,
        // A release TT Lab knows (PS2), or the Xbox version's files
        Known,
        // A PS2 archive of other hashes: the original disc of another release, or one TT Lab 1.1.0 built
        Unknown
    }

    public sealed record Check(Verdict Verdict, string Text, string? Release = null)
    {
        public bool Takes(bool confirmed) => Verdict == Verdict.Known || (Verdict == Verdict.Unknown && confirmed);
    }

    // Whether the folder looks like the version's game files without reading them through: what the folder picker says as it's typed
    public static Check Glance(GamePlatform platform, string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return new Check(Verdict.Missing, "The folder doesn't exist.");
        }

        if (platform == GamePlatform.Xbox)
        {
            return XboxFiles(folder);
        }

        return DiscPath.Find(folder, "Crash6", "Crash.BH") == null || DiscPath.Find(folder, "Crash6", "Crash.BD") == null
            ? new Check(Verdict.Missing, "The folder has no Crash6/Crash.BH and Crash.BD, the PS2 disc's archive.")
            : new Check(Verdict.Unknown, "The folder has the PS2 disc's archive.");
    }

    // A PS2 folder's archive checked against the releases' (KnownPs2Archives unless tests give others), an Xbox folder's files
    public static Check Of(GamePlatform platform, string? folder, IReadOnlyList<Release>? known = null)
    {
        var glance = Glance(platform, folder);
        if (platform == GamePlatform.Xbox || glance.Verdict == Verdict.Missing)
        {
            return glance;
        }

        try
        {
            var header = Sha256(DiscPath.Find(folder!, "Crash6", "Crash.BH")!);
            // Crash.BD is most of the disc, it's only read through when the header is a release's
            var candidates = (known ?? KnownPs2Archives).Where(release => release.Header.Equals(header, StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count > 0)
            {
                var archive = Sha256(DiscPath.Find(folder!, "Crash6", "Crash.BD")!);
                if (candidates.FirstOrDefault(release => release.Archive.Equals(archive, StringComparison.OrdinalIgnoreCase)) is { } release)
                {
                    return new Check(Verdict.Known, $"The archive is {release.Name}'s.", release.Name);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Check(Verdict.Missing, $"The folder's archive can't be read: {exception.Message}");
        }

        return new Check(Verdict.Unknown, $"The archive isn't one TT Lab knows ({string.Join(", ", (known ?? KnownPs2Archives).Select(release => release.Name))}): it may be the original " +
                                          "disc of another release, or one a build of TT Lab 1.1.0 wrote over the project's disc folder.");
    }

    // Whether the folder's game files are of the release the project was made from, its disc folder's (the same folder, or files of the same
    // release): the PS2 disc's SYSTEM.CNF boot file and version, the Xbox executable's certificate, which builds never write. Same is none
    // when either can't be read
    public sealed record ReleaseCheck(bool? Same, string Text);

    public static ReleaseCheck ProjectsRelease(GamePlatform platform, string folder, string? projectDisc)
    {
        if (!string.IsNullOrEmpty(projectDisc) && AssetFileStamps.PathComparer.Equals(Path.GetFullPath(folder).TrimEnd('/', '\\'), Path.GetFullPath(projectDisc).TrimEnd('/', '\\')))
        {
            return new ReleaseCheck(true, "The folder is the project's disc folder.");
        }

        var release = ReleaseOf(platform, folder);
        var projects = string.IsNullOrEmpty(projectDisc) ? null : ReleaseOf(platform, projectDisc);
        if (release == null)
        {
            return new ReleaseCheck(null, $"Which release the folder's game files are of can't be read ({(platform == GamePlatform.Xbox ? "default.xbe" : "SYSTEM.CNF")}).");
        }

        if (projects == null)
        {
            return new ReleaseCheck(null, $"The folder's game files are {release}, which release the project was made from can't be read from its disc folder " +
                                          $"({(string.IsNullOrEmpty(projectDisc) ? "it has none" : projectDisc)}).");
        }

        return release == projects
            ? new ReleaseCheck(true, $"The folder's game files are {release}, the release the project was made from.")
            : new ReleaseCheck(false, $"The folder's game files are {release}, the project was made from {projects}: repaired from them, its packages take another " +
                                      "release's values.");
    }

    // The release a folder's game files are of, none when it can't be read
    private static string? ReleaseOf(GamePlatform platform, string folder)
    {
        try
        {
            if (platform == GamePlatform.PS2)
            {
                if (DiscPath.Find(folder, "SYSTEM.CNF") is not { } systemCnf || !File.Exists(systemCnf))
                {
                    return null;
                }

                var (executable, version) = GameRelease.ReadSystemCnf(File.ReadAllText(systemCnf));
                var known = GameRelease.All.FirstOrDefault(release => release.Executable.Equals(executable, StringComparison.OrdinalIgnoreCase) && release.Version == version);
                return known != null ? $"{known.Name}'s ({executable} {version})" : $"{executable} {version}'s";
            }

            return DiscPath.Find(folder, "default.xbe") is { } xbe && File.Exists(xbe) && XboxCertificate(xbe) is { } certificate
                ? $"those of the Xbox executable of title {certificate.Title:X8}, regions {certificate.Regions:X} and version {certificate.Version:X8}"
                : null;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // An Xbox executable's certificate tells its releases apart: the header ("XBEH") has the image's base address at 0x104 and the
    // certificate's at 0x118, the certificate the title ID at 8, the regions at 0xA0 and the version at 0xAC
    private static (UInt32 Title, UInt32 Regions, UInt32 Version)? XboxCertificate(string xbe)
    {
        const int certificateLength = 0xB0;
        using var stream = File.OpenRead(xbe);
        var header = new byte[Math.Min(stream.Length, 1 << 20)];
        stream.ReadExactly(header);
        if (header.Length < 0x11C || !header.AsSpan().StartsWith("XBEH"u8))
        {
            return null;
        }

        var baseAddress = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x104));
        var offset = (long)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x118)) - baseAddress;
        if (offset < 0 || offset > header.Length - certificateLength)
        {
            return null;
        }

        var certificate = header.AsSpan((int)offset, certificateLength);
        return (BinaryPrimitives.ReadUInt32LittleEndian(certificate[8..]), BinaryPrimitives.ReadUInt32LittleEndian(certificate[0xA0..]),
            BinaryPrimitives.ReadUInt32LittleEndian(certificate[0xAC..]));
    }

    // The Xbox disc's executable and its default chunk, found whatever their case
    private static Check XboxFiles(string folder)
    {
        if (DiscPath.Find(folder, "default.xbe") is not { } executable || !File.Exists(executable))
        {
            return new Check(Verdict.Missing, "The folder has no default.xbe, the Xbox disc's executable.");
        }

        return DiscPath.Find(folder, "Startup", "Default.rmx") is { } chunk && File.Exists(chunk)
            ? new Check(Verdict.Known, "The folder has the Xbox disc's files.")
            : new Check(Verdict.Missing, "The folder has no Startup/Default.rmx, the Xbox disc's default chunk.");
    }

    private static string Sha256(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
