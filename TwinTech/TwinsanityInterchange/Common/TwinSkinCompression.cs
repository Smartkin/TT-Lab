using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.PS2Hardware;

namespace Twinsanity.TwinsanityInterchange.Common;

/// <summary>
/// How the positions and UVs of a skin or a blend skin get packed into 16 bit integers on PS2
/// </summary>
/// <remarks>
/// VIF adds the offsets to the packed integers and the VU program multiplies the results by the scales.
/// The fourth position component is the X of the vertex's normal, so the position scale can't go below 1/16384 or normals don't fit
/// </remarks>
public class TwinSkinCompression
{
    /// <summary>
    /// Smallest position scale the game's tools used
    /// </summary>
    public const Single MinPositionScale = 1.0f / 16384.0f;

    /// <summary>
    /// Multiplier from packed integers to positions
    /// </summary>
    public Single PositionScale { get; set; } = MinPositionScale;
    /// <summary>
    /// Integers added to the packed position components before they get scaled
    /// </summary>
    public Int32[] PositionOffset { get; set; } = new Int32[4];
    /// <summary>
    /// Multiplier from packed integers to UVs
    /// </summary>
    public Single UvScale { get; set; } = TwinVIFCompiler.SkinUvScale;
    /// <summary>
    /// Integers added to the packed UV components before they get scaled
    /// </summary>
    public Int32[] UvOffset { get; set; } = new Int32[4];

    /// <summary>
    /// Creates settings that fit the given positions, centering them to keep as much precision as possible
    /// </summary>
    public static TwinSkinCompression FitTo(IEnumerable<Vector4> positions)
    {
        var min = new[] { Single.MaxValue, Single.MaxValue, Single.MaxValue };
        var max = new[] { Single.MinValue, Single.MinValue, Single.MinValue };
        var hasPositions = false;
        foreach (var position in positions)
        {
            hasPositions = true;
            var components = new[] { position.X, position.Y, position.Z };
            for (var i = 0; i < 3; i++)
            {
                min[i] = Math.Min(min[i], components[i]);
                max[i] = Math.Max(max[i], components[i]);
            }
        }

        var result = new TwinSkinCompression();
        if (!hasPositions)
        {
            return result;
        }

        var halfExtent = Enumerable.Range(0, 3).Max(i => (max[i] - min[i]) / 2.0);
        // Rounding the packed values and the offsets can each move a component by half a step, leave room for both
        result.PositionScale = (Single)Math.Max(MinPositionScale, halfExtent / 32000.0);
        for (var i = 0; i < 3; i++)
        {
            result.PositionOffset[i] = (Int32)Math.Round((min[i] + max[i]) / 2.0 / result.PositionScale);
        }

        return result;
    }

    /// <summary>
    /// Whether every position packs into 16 bits with these settings
    /// </summary>
    public Boolean Fits(IEnumerable<Vector4> positions, IEnumerable<Vector4> uvs)
    {
        return positions.All(p => FitsInShort(PackPosition(p))) && uvs.All(uv => FitsInShort(PackUv(uv)));
    }

    /// <summary>
    /// Packs a position and the normal X it holds in W
    /// </summary>
    public Int32[] PackPosition(Vector4 position)
    {
        return Pack(position, PositionScale, PositionOffset);
    }

    /// <summary>
    /// Packs UVs and the normal Y and Z held in Z and W
    /// </summary>
    public Int32[] PackUv(Vector4 uv)
    {
        return Pack(uv, UvScale, UvOffset);
    }

    /// <summary>
    /// Converts packed position components as VIF and the VU program do
    /// </summary>
    public Vector4 UnpackPosition(UInt32[] offsetComponents)
    {
        return Unpack(offsetComponents, PositionScale);
    }

    /// <summary>
    /// Converts packed UV components as VIF and the VU program do
    /// </summary>
    public Vector4 UnpackUv(UInt32[] offsetComponents)
    {
        return Unpack(offsetComponents, UvScale);
    }

    private static Int32[] Pack(Vector4 value, Single scale, Int32[] offset)
    {
        return new[]
        {
            (Int32)Math.Round(value.X / scale) - offset[0],
            (Int32)Math.Round(value.Y / scale) - offset[1],
            (Int32)Math.Round(value.Z / scale) - offset[2],
            (Int32)Math.Round(value.W / scale) - offset[3]
        };
    }

    private static Vector4 Unpack(UInt32[] offsetComponents, Single scale)
    {
        return new Vector4((Int32)offsetComponents[0] * scale, (Int32)offsetComponents[1] * scale, (Int32)offsetComponents[2] * scale, (Int32)offsetComponents[3] * scale);
    }

    private static Boolean FitsInShort(Int32[] packed)
    {
        return packed.All(p => p >= Int16.MinValue && p <= Int16.MaxValue);
    }

    /// <summary>
    /// Creates a copy of the settings
    /// </summary>
    public TwinSkinCompression Clone()
    {
        return new TwinSkinCompression
        {
            PositionScale = PositionScale,
            PositionOffset = (Int32[])PositionOffset.Clone(),
            UvScale = UvScale,
            UvOffset = (Int32[])UvOffset.Clone()
        };
    }
}
