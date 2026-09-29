using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Util;

namespace TT_Lab.AssetData.Instance.Particle;

/// <summary>
/// What the game does with a particle system's blend mode and draw list, from the PAL executable. Loading a texture page
/// (<c>FUN_0019d9d8</c>) makes a shader for each of the modes 0 to 3, all in render bucket 22: 0 and 1 are new shaders with the GS
/// alpha presets 1 and 2, 2 is the page material's own first shader and 3 one without blending. Each drops the pixels whose alpha is
/// under 5/128, tests the depth, and all but 3 leave the depth buffer as it is. Mode 7 draws hexagons through a shader of its own in
/// bucket 23 that copies the frame first. The game draws list 0 every frame going through the modes 3, 2, 1, 0, then list 2 with only
/// mode 7 (<c>DrawParticleList</c>), and puts every mode 7 system in list 2 whatever it says; list 1 is never drawn
/// </summary>
public static class ParticleBlendModes
{
    public const Byte Additive = 0;
    public const Byte Subtractive = 1;
    public const Byte PageMaterial = 2;
    public const Byte Cutout = 3;
    public const Byte Distortion = 7;

    public const Byte DrawnList = 0;
    public const Byte DistortionList = 2;

    /// <summary>
    /// The page shaders' alpha test passes 5 and above, in the GS's alpha where 128 is 1
    /// </summary>
    public const float AlphaTestReference = 5.0f / 128.0f;

    public static readonly IReadOnlyList<NamedChoice> Modes =
    [
        new(Additive, "Additive", "Adds the particles' color times their alpha to what's behind them. Fire, sparks and glows, most of the game's particles"),
        new(Subtractive, "Subtractive", "Takes the particles' color times their alpha away from what's behind them. The skid marks and the warp splash"),
        new(PageMaterial, "Page material", "Drawn with the texture page's material's own blending, the retail pages blend normally (color times alpha over what's behind). Smoke, dirt and feathers"),
        new(Cutout, "Cutout", "No blending: every pixel with an alpha of 5/128 or more is drawn solid and hides what's behind it. No particle of the game uses it"),
        new(Distortion, "Distortion", "Hexagons bending what's behind them by the Distortion values, drawn after every other particle. At most 384 at a time, no particle of the game uses it"),
    ];

    public static readonly IReadOnlyList<NamedChoice> DrawLists =
    [
        new(DrawnList, "Drawn", "The list the game draws every frame, the blend modes in the order 3, 2, 1, 0"),
        new(1, "Never drawn", "The retail game never draws this list"),
        new(DistortionList, "Distortion", "Only its Distortion (7) systems get drawn, the game puts every one of them here whatever this says"),
    ];

    public static NamedChoice FindMode(int value)
    {
        return Modes.FirstOrDefault(mode => mode.Value == value)
               ?? new NamedChoice(value, "Not a mode", "The game reads what follows the page's shaders (the next page's texture and material IDs) as the shader, which draws nothing that works");
    }

    public static NamedChoice FindDrawList(int value)
    {
        return DrawLists.FirstOrDefault(list => list.Value == value) ?? new NamedChoice(value, "Not a list", "The game only has the lists 0 to 2");
    }

    /// <summary>
    /// Whether the game draws a system at all
    /// </summary>
    public static bool IsDrawn(Byte blendMode, Byte drawList)
    {
        return blendMode == Distortion || (drawList == DrawnList && blendMode <= Cutout);
    }

    /// <summary>
    /// When the particles of a mode are drawn among the others
    /// </summary>
    public static int DrawOrder(Byte blendMode)
    {
        return blendMode switch
        {
            Cutout => 0,
            PageMaterial => 1,
            Subtractive => 2,
            Additive => 3,
            _ => 4
        };
    }
}
