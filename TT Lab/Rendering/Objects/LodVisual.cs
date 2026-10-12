using System;
using System.Collections.Generic;
using GlmSharp;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// A level of detail of the scenery drawn the way the game picks its mesh (<c>LodMeshAt</c>, from the culling's distance): the camera's
/// distance squared, here to the middle of the box the LOD is culled by, picks the first mesh up to the first switch distance and the next
/// one past each switch. The game draws nothing nearer than the nearest distance or farther than the farthest (-1 none), which the viewport
/// only does when its toolbar says so (<see cref="RenderContext.CullsLods"/>): the editor shows every LOD otherwise. Every level is a child,
/// the one of the distance shown every frame on the render thread before the batches gather their instances
/// </summary>
public sealed class LodVisual : Renderable, IInstancedRenderable
{
    private readonly List<Renderable?> _levels = [];
    private readonly int _nearest;
    private readonly int _farthest;
    private readonly int[] _switches;
    private readonly vec3 _boxCenter;

    public LodVisual(RenderContext context, MeshService meshService, LodModelData lod, vec3 boxCenter) : base(context)
    {
        _nearest = lod.MinDrawDistance;
        _farthest = lod.MaxDrawDistance;
        _switches = (int[])lod.ModelsDrawDistances.Clone();
        _boxCenter = boxCenter;
        foreach (var mesh in lod.Meshes)
        {
            var model = meshService.GetMesh(mesh, true).Model;
            if (model != null)
            {
                // Shown by the distance, not with the LOD
                model.SetInheritVisibility(false);
                AddChild(model);
            }

            _levels.Add(model);
        }
    }

    /// <summary>
    /// Which level the game draws at a squared distance (the game's integer of it, compared unsigned like the game's) by the switch distances
    /// </summary>
    public static int LevelAt(IReadOnlyList<int> switches, int count, float distanceSquared)
    {
        var distance = GameDistance(distanceSquared);
        var last = count - 1;
        if (last <= 0 || distance < (uint)switches[0])
        {
            return 0;
        }

        var level = 1;
        for (var index = 1; index < last && index < switches.Count && distance >= (uint)switches[index]; index++)
        {
            level = index + 1;
        }

        return level;
    }

    /// <summary>
    /// Whether the game draws the LOD at a squared distance: from its nearest distance to its farthest, -1 having no limit
    /// </summary>
    public static bool IsDrawnAt(int nearest, int farthest, float distanceSquared)
    {
        var distance = GameDistance(distanceSquared);
        return distance >= (uint)nearest && distance <= (uint)farthest;
    }

    private static uint GameDistance(float distanceSquared) => (uint)Math.Clamp(distanceSquared, 0.0f, uint.MaxValue);

    public void PrepareInstances(InstanceBuffer instances)
    {
        var camera = Context.FrameCamera;
        var level = 0;
        if (camera.IsValid)
        {
            var center = (RenderTransform * new vec4(_boxCenter, 1.0f)).xyz;
            var distanceSquared = (center - camera.Position).LengthSqr;
            level = Context.CullsLods && !IsDrawnAt(_nearest, _farthest, distanceSquared) ? -1 : LevelAt(_switches, _levels.Count, distanceSquared);
        }

        for (var i = 0; i < _levels.Count; i++)
        {
            if (_levels[i] is { } mesh)
            {
                mesh.IsVisible = IsVisible && i == level;
            }
        }
    }
}
