using GlmSharp;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// The boxes of a chunk's dynamic scenery models. A box is in its model's space, so it moves and turns with the model's animation
/// </summary>
public sealed class DynamicSceneryBounds(RenderContext context, DynamicScenery scenery) : Renderable(context, "DYNAMIC_SCENERY_BOUNDS"), IPrimitiveRenderable
{
    private static readonly vec4 Color = new(0.35f, 0.9f, 1.0f, 0.9f);

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        foreach (var model in scenery.Models)
        {
            renderer.DrawWireBox(model.ModelTransform * BoxTransform(model.BoundsMin, model.BoundsMax), Color, 1.5f, PrimitiveLayer.WorldXRay);
        }
    }

    /// <summary>
    /// What turns the cube from -1 to 1 into the box between the corners
    /// </summary>
    public static mat4 BoxTransform(vec3 min, vec3 max) => mat4.Translate((min + max) * 0.5f) * mat4.Scale((max - min) * 0.5f);
}
