using System;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance.Scenery;

/// <summary>
/// The values a node of the game's scenery tree had that aren't what the build works out (the tools' rounding, light bits nothing reads),
/// kept for the node at <see cref="Path"/> while they're still within a hair of what's worked out
/// </summary>
public class SceneryTreeNode
{
    public String Path { get; set; } = String.Empty;

    public Vector4 BoundsCenter { get; set; } = new();

    public Vector4 BoundsMin { get; set; } = new();

    public Vector4 BoundsMax { get; set; } = new();

    public Vector4 BoundsHalfSize { get; set; } = new();

    /// <summary>
    /// The node's own light bits, null when they're the root's. The game only reads the root's
    /// </summary>
    public Boolean[]? LightsEnabler { get; set; }
}
