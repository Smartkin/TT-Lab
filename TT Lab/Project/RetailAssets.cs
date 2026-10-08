using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TT_Lab.Assets;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Project;

/// <summary>
/// The game's own packages a project is made on (Global PS2, PS2, Global XBOX and XBOX), which a project shared without the game's assets
/// (a repository can't have them) doesn't have. Its packages need a version's when they link to its assets or depend on its packages,
/// TT Lab unpacks them from the game's files again the way creating the project did
/// </summary>
internal static class RetailAssets
{
    /// <summary>
    /// A version of the game: whether its packages are missing and whether the project's own packages need them
    /// </summary>
    public sealed record Version(GamePlatform Platform, bool Missing, bool Required);

    /// <param name="ProjectName">The project's name, which the game's packages' names end with</param>
    /// <param name="Versions">The PS2 and Xbox versions</param>
    /// <param name="Problem">Why the project can't get them back, null when it can</param>
    public sealed record Check(string ProjectName, IReadOnlyList<Version> Versions, string? Problem)
    {
        public bool IsMissingAny => Versions.Any(version => version.Missing);
    }

    public static string GlobalPackageName(GamePlatform platform, string project) => platform == GamePlatform.Xbox ? $"Global XBOX_{project}" : $"Global PS2_{project}";

    public static string PackageName(GamePlatform platform, string project) => platform == GamePlatform.Xbox ? $"XBOX_{project}" : $"PS2_{project}";

    private static bool HasPackage(string assets, string name) => File.Exists(Path.Combine(assets, name, $"{name}.json"));

    /// <summary>
    /// What the project in the folder of the project file lacks of the game's packages and what its own packages need
    /// </summary>
    public static Check Of(string projectFile, string projectName)
    {
        var assets = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(projectFile))!, "assets");
        var platforms = new[] { GamePlatform.PS2, GamePlatform.Xbox };
        var missing = platforms.ToDictionary(platform => platform, platform => (Global: !HasPackage(assets, GlobalPackageName(platform, projectName)),
            Package: !HasPackage(assets, PackageName(platform, projectName))));
        if (!missing.Values.Any(pair => pair.Global || pair.Package))
        {
            return new Check(projectName, platforms.Select(platform => new Version(platform, false, false)).ToList(), null);
        }

        foreach (var (platform, (global, package)) in missing)
        {
            if (global != package)
            {
                var (present, absent) = global ? (PackageName(platform, projectName), GlobalPackageName(platform, projectName)) : (GlobalPackageName(platform, projectName), PackageName(platform, projectName));
                return new Check(projectName, [], $"The project has the package {present} but not {absent}. Put {absent}'s folder back into the project's assets folder, or take " +
                                                  $"{present}'s out to have TT Lab unpack the {Describe(platform)} version's packages again.");
            }
        }

        if (!HasPackage(assets, projectName))
        {
            return new Check(projectName, [], $"The project has no package of its own, {projectName}, nor the game's packages.");
        }

        var required = RequiredVersions(assets, projectName);
        return new Check(projectName, platforms.Select(platform => new Version(platform, missing[platform].Global, required.Contains(platform))).ToList(), null);
    }

    public static string Describe(GamePlatform platform) => platform == GamePlatform.Xbox ? "Xbox" : "PS2";

    // The versions whose packages the project's own packages link to: a URI of an asset in one, or a package depending on one. The
    // project's own package depends on every one of them whatever it has
    private static HashSet<GamePlatform> RequiredVersions(string assets, string projectName)
    {
        var prefixes = new[] { GamePlatform.PS2, GamePlatform.Xbox }
            .SelectMany(platform => new[] { GlobalPackageName(platform, projectName), PackageName(platform, projectName) }.Select(name => (Platform: platform, Uri: $"res://{name}")))
            .ToList();
        var retail = prefixes.Select(prefix => prefix.Uri[AssetLinks.UriPrefix.Length..]).ToHashSet(StringComparer.Ordinal);
        var ownPackageFile = Path.Combine(assets, projectName, $"{projectName}.json");
        var required = new HashSet<GamePlatform>();
        foreach (var package in Directory.EnumerateDirectories(assets).Where(directory => !retail.Contains(Path.GetFileName(directory))))
        {
            foreach (var file in Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories))
            {
                if (required.Count == prefixes.Select(prefix => prefix.Platform).Distinct().Count())
                {
                    return required;
                }

                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(ownPackageFile), StringComparison.Ordinal))
                {
                    continue;
                }

                var text = ReadLinks(file);
                if (text == null)
                {
                    continue;
                }

                foreach (var (platform, uri) in prefixes)
                {
                    if (LinksTo(text, uri))
                    {
                        required.Add(platform);
                    }
                }
            }
        }

        return required;
    }

    // A URI of the package or of an asset in it, not one of another package whose name starts the same way
    private static bool LinksTo(string text, string packageUri)
    {
        for (var index = text.IndexOf(packageUri, StringComparison.Ordinal); index >= 0; index = text.IndexOf(packageUri, index + 1, StringComparison.Ordinal))
        {
            var end = index + packageUri.Length;
            if (end >= text.Length || text[end] is '/' or '"' or '\'')
            {
                return true;
            }
        }

        return false;
    }

    // The text that can have links: JSON, scripts, a model file's JSON. Pictures and sounds have none
    private static string? ReadLinks(string file)
    {
        var extension = Path.GetExtension(file).ToLowerInvariant();
        try
        {
            return extension switch
            {
                ".json" or ".data" or ".lab" => File.ReadAllText(file),
                ".tlm" => string.Join('\n', AssetLinks.UrisInModelFile(file)),
                _ => null
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.WriteLine($"Couldn't read {file}: {exception.Message}", Log.LogType.Warning);
            return null;
        }
    }

    /// <summary>
    /// Whether the folder has the game's files of the version the way creating a project takes them: the PS2 disc's SYSTEM.CNF, the
    /// Xbox disc's default.xbe
    /// </summary>
    public static bool IsDisc(GamePlatform platform, string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return false;
        }

        var file = platform == GamePlatform.Xbox ? "default.xbe" : "system.cnf";
        return Directory.EnumerateFiles(folder).Any(path => string.Equals(Path.GetFileName(path), file, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Where each missing version is unpacked from without asking: the folder of the game's files the preferences have, when it has them
    /// </summary>
    public static Dictionary<GamePlatform, string> FromPreferences(Check check, Func<GamePlatform, string?> preferred)
    {
        return check.Versions.Where(version => version.Missing && IsDisc(version.Platform, preferred(version.Platform)))
            .ToDictionary(version => version.Platform, version => preferred(version.Platform)!);
    }

    /// <summary>
    /// Whether the folders do without asking: every version the project's packages need, at least one when they need none
    /// </summary>
    public static bool Suffice(Check check, IReadOnlyDictionary<GamePlatform, string> folders)
    {
        var required = check.Versions.Where(version => version.Missing && version.Required).ToList();
        return required.Count > 0 ? required.All(version => folders.ContainsKey(version.Platform)) : folders.Count > 0;
    }
}
