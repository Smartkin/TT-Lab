using System.Linq;

namespace TT_Lab.Util;

/// <summary>
/// Names typed into TT Lab are plain ASCII: the game keeps its names and paths a byte per character (TwinTech's <c>GameText</c>), the
/// characters past that became '?' in its files and two names differing only in them got the same path
/// </summary>
public static class NameRules
{
    public const string AsciiOnly = "can only have plain ASCII characters (letters, digits, spaces and the keyboard's symbols)";

    public static bool IsAscii(string? text) => text == null || text.All(character => character is >= ' ' and <= '~');

    /// <summary>
    /// The text with every character past plain ASCII as '?', what the game's files get of it
    /// </summary>
    public static string ToAscii(string text) => new(text.Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray());
}
