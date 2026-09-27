using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Extensions;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// Connection between the two AI positions of an AI path. Their positions are read every frame, so moving either of them moves the line
/// </summary>
public class AiPathVisual(RenderContext context, string name) : EditableObject(context, null, name), IPrimitiveRenderable
{
    private static readonly vec4 Color = new(1.0f, 0.86f, 0.25f, 0.9f);

    private AiPositionData? _begin;
    private AiPositionData? _end;

    public override bool IsSelectable => false;

    public void SetEnds(AiPositionData? begin, AiPositionData? end)
    {
        _begin = begin;
        _end = end;
    }

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        var begin = _begin?.Coords;
        var end = _end?.Coords;
        if (begin == null || end == null)
        {
            return;
        }

        var start = begin.ToGlm();
        var finish = end.ToGlm();
        renderer.DrawLine(start, finish, Color, 2.5f, PrimitiveLayer.WorldXRay);
        // Shows which way the path goes without covering the positions at its ends
        PolylineVisual.DrawArrowhead(renderer, camera, start, vec3.Lerp(start, finish, 0.6f), Color);
    }
}
