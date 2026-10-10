using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TT_Lab.Assets.Instance;
using TT_Lab.Util;

namespace TT_Lab.Assets;

/// <summary>
/// The assets Open Asset (Ctrl+Shift+O) offers and the fuzzy search over them: every asset of the project but internal ones, folders, the
/// disc's files and the resources of level chunks, which are edited in their chunk's scene; the default chunk's have editors of their own
/// </summary>
public sealed class AssetSearch
{
    // Where a word of what's typed counts as much as in the name, the path only half
    private const int PathShare = 2;

    public sealed record Entry(IAsset Asset, string Name, string Where)
    {
        internal string NameLower { get; } = Name.ToLowerInvariant();
        internal string WhereLower { get; } = Where.ToLowerInvariant();
    }

    public sealed record Found(Entry Entry, int Score);

    private readonly List<Entry> _entries;

    private AssetSearch(List<Entry> entries)
    {
        _entries = entries;
    }

    public int Count => _entries.Count;

    public static AssetSearch Of(IEnumerable<IAsset> assets)
    {
        var all = assets.ToList();
        var levelChunks = all.OfType<LevelChunk>().Where(chunk => !chunk.IsGlobalDefaultChunk)
            .Select(chunk => (chunk.Package, chunk.AdditionalPath)).ToHashSet();
        var entries = all.Where(asset => !asset.IsInternal && asset is not Folder and not DiscFile
                                         && !(asset is SerializableInstance { Chunk: { } path } instance && levelChunks.Contains((instance.Package, path))))
            .Select(asset => new Entry(asset, asset.Alias ?? asset.Name, WhereOf(asset)))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new AssetSearch(entries);
    }

    // The asset's place in the project: its URI's package and folders
    private static string WhereOf(IAsset asset)
    {
        var uri = asset.URI.ToString();
        var start = uri.IndexOf("://", StringComparison.Ordinal);
        var path = start >= 0 ? uri[(start + 3)..] : uri;
        var last = path.LastIndexOf('/');
        return last > 0 ? path[..last] : path;
    }

    /// <summary>
    /// The best matches of what's typed, every word of it found in the name or the place, best first
    /// </summary>
    public IReadOnlyList<Found> Search(string query, int most, CancellationToken token = default)
    {
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(word => word.ToLowerInvariant()).ToArray();
        if (words.Length == 0)
        {
            return [];
        }

        var found = new List<Found>();
        for (var index = 0; index < _entries.Count; index++)
        {
            if ((index & 0xFFF) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var entry = _entries[index];
            var total = 0;
            var matches = true;
            foreach (var word in words)
            {
                var inName = FuzzyMatch.Score(word, entry.Name, entry.NameLower);
                var inWhere = FuzzyMatch.Score(word, entry.Where, entry.WhereLower) / PathShare;
                if (inName == null && inWhere == null)
                {
                    matches = false;
                    break;
                }

                total += Math.Max(inName ?? int.MinValue, inWhere ?? int.MinValue);
            }

            if (matches)
            {
                found.Add(new Found(entry, total));
            }
        }

        // The entries are in name order already, a stable sort keeps it among equal scores
        return found.OrderByDescending(match => match.Score).Take(most).ToList();
    }
}
