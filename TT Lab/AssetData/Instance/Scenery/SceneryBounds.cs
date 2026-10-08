using GlmSharp;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance.Scenery;

/// <summary>
/// The root cell of a scenery's tree, the box the game keeps the chunk's objects in. It places every instance in the deepest tree node
/// whose cell holds the instance's box (<c>FUN_001eb250</c>, <c>FUN_001fa4b0</c> with 5e-05 of play), and an instance in none is left
/// out of the level's collision checks: on a new chunk whose root was the cell of its flat ground Crash crept along without jumping and
/// stopped at the edge. Every retail root is a box around the origin well past its level. Edited as its middle and half size, kept as its
/// corners (<see cref="SceneryData.BoundsMin"/>, <see cref="SceneryData.BoundsMax"/>): a value of the corners, which the history keeps,
/// so undo puts them back to the bit. Undone through the middle's or the half size's setter they were only as close as floats get, and a
/// middle typed far enough out to round the half size away lost it for good
/// </summary>
public sealed record SceneryBounds
{
    public SceneryBounds(System.Numerics.Vector3 min, System.Numerics.Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public System.Numerics.Vector3 Min { get; private set; }

    public System.Numerics.Vector3 Max { get; private set; }

    /// <summary>
    /// What new chunks get, about the size of the game's levels
    /// </summary>
    public static readonly vec3 DefaultHalfSize = new(200.0f, 100.0f, 200.0f);

    // A cell thinner than this on an axis holds no object
    private const float FlatHalfSize = 0.5f;
    // Roots made from what's in them get this much room around it, like the game's
    private const float Room = 1.5f;

    [Editable(IsComputed = true, Hint = "The middle of the box the game keeps the chunk's objects in. An object outside of it has nothing under it: it has to hold every place objects go")]
    public Vector3 Center
    {
        get => ToTwin(CenterOf(Min, Max));
        set => (Min, Max) = Corners(ToGlm(value), HalfSizeOf(Min, Max));
    }

    [Editable(Caption = "Half Size", IsComputed = true, Hint = "How far the box reaches from its middle along each axis, the scale tool of the viewport sets it")]
    public Vector3 HalfSize
    {
        get => ToTwin(HalfSizeOf(Min, Max));
        set => (Min, Max) = Corners(CenterOf(Min, Max), vec3.Abs(ToGlm(value)));
    }

    // The middle ± the half size, what the history shows of a step
    public override string ToString()
    {
        var center = CenterOf(Min, Max);
        var halfSize = HalfSizeOf(Min, Max);
        return $"({center.x}, {center.y}, {center.z}) ± ({halfSize.x}, {halfSize.y}, {halfSize.z})";
    }

    public static vec3 CenterOf(SceneryData scenery) => CenterOf(scenery.BoundsMin, scenery.BoundsMax);

    public static vec3 HalfSizeOf(SceneryData scenery) => HalfSizeOf(scenery.BoundsMin, scenery.BoundsMax);

    private static vec3 CenterOf(System.Numerics.Vector3 min, System.Numerics.Vector3 max) => (ToGlm(min) + ToGlm(max)) * 0.5f;

    private static vec3 HalfSizeOf(System.Numerics.Vector3 min, System.Numerics.Vector3 max) => (ToGlm(max) - ToGlm(min)) * 0.5f;

    private static (System.Numerics.Vector3 Min, System.Numerics.Vector3 Max) Corners(vec3 center, vec3 halfSize)
    {
        return (new System.Numerics.Vector3(center.x - halfSize.x, center.y - halfSize.y, center.z - halfSize.z),
            new System.Numerics.Vector3(center.x + halfSize.x, center.y + halfSize.y, center.z + halfSize.z));
    }

    public static void SetCell(SceneryData scenery, vec3 center, vec3 halfSize)
    {
        (scenery.BoundsMin, scenery.BoundsMax) = Corners(center, halfSize);
    }

    /// <summary>
    /// Whether the cell can hold an object at all
    /// </summary>
    public static bool HoldsAnything(SceneryData scenery)
    {
        var halfSize = HalfSizeOf(scenery);
        return float.IsFinite(halfSize.x) && float.IsFinite(halfSize.y) && float.IsFinite(halfSize.z) &&
               halfSize.x >= FlatHalfSize && halfSize.y >= FlatHalfSize && halfSize.z >= FlatHalfSize;
    }

    /// <summary>
    /// A box around the origin like the game's roots: the default, or more when the level reaches further, with room to spare
    /// </summary>
    public static void Widen(SceneryData scenery, vec3 levelMin, vec3 levelMax)
    {
        var reach = vec3.Zero;
        if (float.IsFinite(levelMin.x) && float.IsFinite(levelMax.x))
        {
            reach = vec3.Max(vec3.Abs(levelMin), vec3.Abs(levelMax)) * Room;
        }

        SetCell(scenery, vec3.Zero, vec3.Max(DefaultHalfSize, reach));
    }

    private static Vector3 ToTwin(vec3 vector) => new(vector.x, vector.y, vector.z);

    private static vec3 ToGlm(Vector3 vector) => new(vector.X, vector.Y, vector.Z);

    private static vec3 ToGlm(System.Numerics.Vector3 vector) => new(vector.X, vector.Y, vector.Z);
}
