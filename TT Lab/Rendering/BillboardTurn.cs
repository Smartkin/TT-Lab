using GlmSharp;

namespace TT_Lab.Rendering;

/// <summary>
/// The game's turn of a placed model with a billboard's shader to face the camera (the decomp's Billboard in SetPlacedModelRenderDMA), which
/// <c>MainPass.vert</c>'s BillboardTurn does for instances that turn (InstanceBuffer.BillboardTurn): about the model's up axis, before its
/// placement's matrix, the camera taken into the model's space with the rigid inverse of the matrix. The turn's rows (columns here) are its
/// side and back of up and the way from the camera, a reflection that puts the model's local +X on the right of the game's screen
/// </summary>
public static class BillboardTurn
{
    // Bits of the game's constants (models.cpp)
    public const float Epsilon = 2.49999976e-09f;
    public static readonly vec3 TiltedUp = new(-0.0499299988f, 0.998749971f, 0.0f);

    public static mat3 Of(mat4 model, vec3 eye)
    {
        var rotation = new mat3(model);
        var way = -(rotation.Transposed * (eye - model.Column3.xyz));
        var up = vec3.UnitY;
        var toWay = up - way;
        if (vec3.Dot(toWay, toWay) <= vec3.Dot(up, way) * Epsilon)
        {
            up = TiltedUp;
        }

        var side = Normalized(vec3.Cross(up, way));
        var back = Normalized(vec3.Cross(up, side));
        return new mat3(side, up, back);
    }

    private static vec3 Normalized(vec3 vector)
    {
        var squared = vec3.Dot(vector, vector);
        return squared > Epsilon ? vector / (float)System.Math.Sqrt(squared) : vec3.Zero;
    }
}
