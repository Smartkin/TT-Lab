using GlmSharp;

namespace TT_Lab.Rendering.Lighting;

/// <summary>
/// What a lit object gets drawn with, the way the game hands it to the lit VU1 programs (<c>FUN_001c7d50</c>, <c>SetRigidModelRenderDMA</c>):
/// the ambient color and the three strongest lights at the object, each a direction and a color. The colors are already the light's color
/// times its intensity there times 0.5, the ambient the ambient lights' colors times their intensities times 0.5. Unused slots are black
/// </summary>
public readonly record struct LightSet(vec3 Ambient, vec3 Direction0, vec3 Color0, vec3 Direction1, vec3 Color1, vec3 Direction2, vec3 Color2)
{
    public const int Slots = 3;

    public static LightSet Dark { get; } = new(vec3.Zero, vec3.Zero, vec3.Zero, vec3.Zero, vec3.Zero, vec3.Zero, vec3.Zero);

    public (vec3 Direction, vec3 Color) this[int slot] => slot switch
    {
        0 => (Direction0, Color0),
        1 => (Direction1, Color1),
        _ => (Direction2, Color2),
    };

    /// <summary>
    /// What a vertex with the normal gets its color multiplied by: the ambient plus every light's color times the clamped dot product of its
    /// direction and the normal, neither normalized (the lit programs 0x2dbeb0 and 0x2e3b40, <c>MainPass.vert</c> does the same)
    /// </summary>
    public vec3 At(vec3 normal)
    {
        return Ambient + Color0 * glm.Max(0.0f, vec3.Dot(Direction0, normal)) + Color1 * glm.Max(0.0f, vec3.Dot(Direction1, normal)) +
               Color2 * glm.Max(0.0f, vec3.Dot(Direction2, normal));
    }

    /// <summary>
    /// A vertex's color lit, a vertex color of 1 being 255: the programs multiply the byte color and clamp it to 255, and the GS takes 128 as
    /// the texture as it is
    /// </summary>
    public static vec3 Shade(vec3 vertexColor, vec3 light)
    {
        return glm.Min(vertexColor * light, vec3.Ones) * (255.0f / 128.0f);
    }
}
