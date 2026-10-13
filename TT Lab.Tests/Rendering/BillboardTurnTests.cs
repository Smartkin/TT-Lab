using GlmSharp;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Rendering;

// The game turns a placed model with a billboard's shader toward the camera's position, about its up axis and before its placement's
// matrix, as a reflection (the decomp's Billboard). TT Lab turned every billboard part to the view's axes, a rotation: roof05's landscape
// billboards came out mirrored and all parallel to the screen
public class BillboardTurnTests
{
    private static void Near(vec3 expected, vec3 actual)
    {
        Assert.Equal(expected.x, actual.x, 4);
        Assert.Equal(expected.y, actual.y, 4);
        Assert.Equal(expected.z, actual.z, 4);
    }

    // Seen from +Z the game's screen has world -X on its right: the model's local +X goes there, its picture the right way round
    [Fact]
    public void AModelSeenFromItsFrontKeepsItsPictureTheRightWayRound()
    {
        var model = mat4.Translate(10.0f, 30.5f, 0.0f);
        var turn = BillboardTurn.Of(model, new vec3(10.0f, 30.5f, 50.0f));

        Near(new vec3(-1.0f, 0.0f, 0.0f), turn.Column0);
        Near(vec3.UnitY, turn.Column1);
        Near(vec3.UnitZ, turn.Column2);
        Assert.True(turn.Determinant < 0.0f);
    }

    // The model's back points at the camera's place, whatever way the camera looks
    [Fact]
    public void AModelFacesTheCamerasPlace()
    {
        var model = mat4.Translate(100.0f, 0.0f, 200.0f);
        var eye = new vec3(0.0f, 40.0f, 0.0f);
        var turn = BillboardTurn.Of(model, eye);

        var towardCamera = eye - model.Column3.xyz;
        towardCamera.y = 0.0f;
        Near(towardCamera.Normalized, turn.Column2);
        Assert.Equal(0.0f, vec3.Dot(turn.Column0, vec3.UnitY), 5);
    }

    // The camera is taken into the model's space, the placement's own turn applying over the billboard's
    [Fact]
    public void ThePlacementsTurnComesAfter()
    {
        var model = mat4.Translate(0.0f, 0.0f, 0.0f) * mat4.RotateY(MathF.PI / 2.0f);
        var eye = new vec3(50.0f, 0.0f, 0.0f);
        var turn = BillboardTurn.Of(model, eye);

        // In the world the model's back points at the camera all the same
        Near(vec3.UnitX, new mat3(model) * turn.Column2);
    }

    // Scenery mode picks a placed billboard where it's drawn, its box turned with it
    [Fact]
    public void APlacedBillboardIsPickedWhereItsDrawn()
    {
        var material = new MaterialData(null!) { Shaders = [new LabShader { ShaderType = TwinShader.Type.UnlitBillboard }] };
        var placement = new EditableObject(null!, new Mesh(null!, [new ModelBuffer(null!, null!, null!, material)]) { IsPlaced = true }, "card");
        var eye = new vec3(0.0f, 0.0f, 50.0f);

        Assert.True(placement.TurnsToCamera);
        Assert.Equal(new mat4(BillboardTurn.Of(placement.WorldTransform, eye)), placement.DrawnTransformFrom(eye));
        // An object's part with the shader doesn't turn
        var part = new EditableObject(null!, new Mesh(null!, [new ModelBuffer(null!, null!, null!, material)]), "part");
        Assert.False(part.TurnsToCamera);
        Assert.Equal(part.WorldTransform, part.DrawnTransformFrom(eye));
    }
}
