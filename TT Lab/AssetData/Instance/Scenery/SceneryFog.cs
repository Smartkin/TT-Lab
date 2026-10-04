using System.Collections.Generic;
using System.Linq;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance.Scenery;

/// <summary>
/// The fog a scenery's <see cref="SceneryData.FogColor"/> picks, one of the PAL executable's 8 tables of 256 colors (0x2f6ff8, set up by
/// <c>FUN_001aeab8</c>). Every frame the game draws the screen again over itself through the table (<c>FUN_001aee28</c>): the top byte
/// of each pixel's depth picks a color and its alpha mixes it in, up to 30% far away (and on the sky) down to nothing up close. The player
/// entering a chunk switches to its table (<c>FUN_0013be10</c>, <c>FUN_00177ba8</c> where the player is made)
/// </summary>
public static class SceneryFog
{
    public static readonly IReadOnlyList<NamedChoice> Tables =
    [
        new(0, "Violet", "Blue-violet in the distance, purple at middle distances"),
        new(1, "Black", "Darkens the distance"),
        new(2, "Light blue", "Light blue in the distance"),
        new(3, "Green", "Green in the distance"),
        new(4, "White", "Whitens the distance"),
        new(5, "Light orange", "Light orange in the distance"),
        new(6, "None", "Leaves the picture as it is, no chunk of the game uses it"),
        new(7, "Blue to red", "Blue-grey in the distance, red at middle distances, no chunk of the game uses it"),
    ];

    /// <summary>
    /// The color of each table far away, where it's the strongest
    /// </summary>
    public static readonly Color[] Colors =
    [
        new(52, 0, 247, 255),
        new(0, 0, 0, 255),
        new(165, 235, 255, 255),
        new(0, 255, 18, 255),
        new(255, 255, 255, 255),
        new(255, 200, 120, 255),
        new(0, 0, 0, 0),
        new(131, 147, 202, 255),
    ];

    public static NamedChoice Find(int value)
    {
        return Tables.FirstOrDefault(table => table.Value == value) ?? new NamedChoice(value, "Not a table", "The game only has the tables 0 to 7 and reads whatever follows them as one");
    }
}
