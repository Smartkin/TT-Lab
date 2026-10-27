using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Project.Repair;

/// <summary>
/// A version of the game being repaired: its two game packages, and their level chunks' own values in a project's files
/// </summary>
internal sealed class RepairVersion
{
    public RepairVersion(GamePlatform platform, string projectName)
    {
        Platform = platform;
        Packages = new[] { RetailAssets.GlobalPackageName(platform, projectName), RetailAssets.PackageName(platform, projectName) }.Select(name => $"res://{name}")
            .ToHashSet(StringComparer.Ordinal);
    }

    public GamePlatform Platform { get; }

    // The packages' URIs, which the assets name
    public IReadOnlySet<string> Packages { get; }

    public string Describe() => RetailAssets.Describe(Platform);

    // A chunk's own values of an asset as the files have them, the entry of the chunk's overrides they're in
    public sealed record OwnValues(RepairFiles.Entry Chunk, JObject Entry)
    {
        public JObject Values => (JObject)Entry["Values"]!;
    }

    // The chunks' own values in the version's packages by the asset they're of
    public Dictionary<string, List<OwnValues>> OwnValuesOf(RepairFiles files)
    {
        var result = new Dictionary<string, List<OwnValues>>(StringComparer.Ordinal);
        foreach (var chunk in files.OfType(nameof(TT_Lab.Assets.LevelChunk), Packages))
        {
            if (files.Metadata(chunk)["Overrides"] is not JArray overrides)
            {
                continue;
            }

            foreach (var entry in overrides.OfType<JObject>().Where(entry => entry["Values"] is JObject))
            {
                var asset = RepairFiles.UriOf(entry["Asset"]);
                if (!result.TryGetValue(asset, out var list))
                {
                    result[asset] = list = [];
                }

                list.Add(new OwnValues(chunk, entry));
            }
        }

        return result;
    }

    // The variation a chunk's own variants are named with (its path, its folders between underscores)
    public static string VariationOf(RepairFiles files, RepairFiles.Entry chunk)
    {
        return ((string?)files.Metadata(chunk)["AdditionalPath"] ?? string.Empty).Replace('\\', '_').Replace('/', '_');
    }
}
