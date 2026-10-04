using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TT_Lab.Assets;

/// <summary>
/// The names the game's OGIs, textures, sounds, skydomes, behaviour command sequences, instance templates and animations get when a
/// project is made: twinsanity-editor's names of their IDs (TwinTech's <c>DefaultHashes</c>) without the folders that group them, since
/// TT Lab keeps an asset's name in its file name, and with them joined by underscores where two names of a kind would be the same. IDs
/// without a name keep the names TT Lab gives them
/// </summary>
public static class RetailNames
{
    private static readonly ConcurrentDictionary<IReadOnlyDictionary<UInt32, string>, Dictionary<UInt32, string>> FileNames = new();
    private static readonly char[] NotInFileNames = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public static string Of(IReadOnlyDictionary<UInt32, string> names, UInt32 id, string fallback)
    {
        return FileNames.GetOrAdd(names, ToFileNames).TryGetValue(id, out var name) ? name : fallback;
    }

    private static Dictionary<UInt32, string> ToFileNames(IReadOnlyDictionary<UInt32, string> names)
    {
        static string WithoutFolders(string name) => name[(name.LastIndexOf('/') + 1)..];
        var counts = names.Values.GroupBy(WithoutFolders, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var chosen = names.ToDictionary(pair => pair.Key, pair => counts[WithoutFolders(pair.Value)] == 1 ? WithoutFolders(pair.Value) : pair.Value.Replace('/', '_'));
        var taken = chosen.Values.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        return chosen.ToDictionary(pair => pair.Key, pair =>
        {
            var name = string.Concat(pair.Value.Select(character => character < ' ' || NotInFileNames.Contains(character) ? '_' : character)).Trim(' ', '.');
            return taken[pair.Value] > 1 || name.Length == 0 ? $"{(name.Length == 0 ? "Unnamed" : name)}_{pair.Key:X}" : name;
        });
    }
}
