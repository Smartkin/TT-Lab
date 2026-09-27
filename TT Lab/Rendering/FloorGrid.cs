using System;
using GlmSharp;
using TT_Lab.Rendering.Objects.Gizmo;

namespace TT_Lab.Rendering;

/// <summary>
/// Grid on the ground of viewers that show a single model, with cells picked to suit the model's size
/// </summary>
public sealed class FloorGrid
{
    private const int CellsPerMajorLine = 10;
    // How many cells there are at least between the center and the farthest part of the model
    private const float CellsAcrossModel = 8.0f;
    private static readonly vec4 LineColor = new(0.62f, 0.62f, 0.66f, 0.45f);
    private static readonly float[] CellSizes = [0.01f, 0.025f, 0.05f, 0.1f, 0.25f, 0.5f, 1.0f, 2.5f, 5.0f, 10.0f, 25.0f, 50.0f, 100.0f, 250.0f, 500.0f, 1000.0f];

    // Replaced as a whole since it gets fitted on the render thread while it's being drawn
    private volatile Layout _layout = new(1.0f, 10.0f);

    public float CellSize => _layout.CellSize;
    public float Radius => _layout.Radius;

    /// <summary>
    /// Sizes the grid to reach well past the model, extent being the farthest the model goes from the origin along any axis
    /// </summary>
    public void FitTo(float extent)
    {
        if (!(extent > 0.0f) || !float.IsFinite(extent))
        {
            _layout = new Layout(1.0f, 10.0f);
            return;
        }

        var cellSize = CellSizes[^1];
        foreach (var size in CellSizes)
        {
            if (size * CellsAcrossModel >= extent)
            {
                cellSize = size;
                break;
            }
        }

        _layout = new Layout(cellSize, MathF.Max(extent * 2.5f, cellSize * 10.0f));
    }

    public void Draw(PrimitiveRenderer renderer, FrameCamera camera)
    {
        var layout = _layout;
        renderer.DrawGrid(vec3.Zero, vec3.UnitX, vec3.UnitZ, layout.Radius, layout.CellSize, CellsPerMajorLine, LineColor,
            TransformGizmo.GetAxisColor(0), TransformGizmo.GetAxisColor(2));
    }

    private sealed record Layout(float CellSize, float Radius);
}
