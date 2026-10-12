using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Tests.Assets;

// The game turns a trigger's and a camera's box by its quaternion (MatrixFromRotation, x, y, z, then w): TT Lab shows the rotation as
// degrees about X, then Y, then Z like an object instance's, draws the box where the game has it and builds the game's quaternion back
[Collection(ProjectCollection.Name)]
public sealed class TriggerRotationTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;
    private UInt32 _triggers;

    public TriggerRotationTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    // Turns of the kinds the game's boxes have, in degrees about an axis: none, about Y, a quarter turn about Y, about a slanted axis,
    // half a turn about a slanted axis (its middle angle a quarter turn, gimbal lock), more than half a turn about Y
    public static readonly TheoryData<Single, Single, Single, Single> Turns = new()
    {
        { 0, 0, 1, 0 },
        { 23.6f, 0, 1, 0 },
        { -90, 0, 1, 0 },
        { 90.3f, -0.19f, -0.98f, -0.1f },
        { 179.9f, 0, 1, 1 },
        { 180, 1, 0, 1 },
        { 182.3f, 0, 1, 0 },
    };

    private static Vector4 Quaternion(Single degrees, Single x, Single y, Single z)
    {
        var turn = quat.FromAxisAngle(glm.Radians(degrees), new vec3(x, y, z).Normalized);
        return new Vector4(turn.x, turn.y, turn.z, turn.w);
    }

    // A point of the box turned the way the game does: its vectors are rows times the rows MatrixFromRotation makes of the quaternion
    private static vec3 GameTurns(Vector4 rotation, vec3 point)
    {
        Single x = rotation.X, y = rotation.Y, z = rotation.Z, w = rotation.W;
        var row0 = new vec3(1 - 2 * y * y - 2 * z * z, 2 * x * y + 2 * z * w, 2 * z * x - 2 * y * w);
        var row1 = new vec3(2 * x * y - 2 * z * w, 1 - 2 * x * x - 2 * z * z, 2 * y * z + 2 * x * w);
        var row2 = new vec3(2 * z * x + 2 * y * w, 2 * y * z - 2 * x * w, 1 - 2 * x * x - 2 * y * y);
        return point.x * row0 + point.y * row1 + point.z * row2;
    }

    // The box's corners where the game has them and where the viewport draws them (its transform of the angles, the cube from -1 to 1)
    private static void AssertBoxWhereTheGameHasIt(TriggerData data, Vector4 rotation)
    {
        var transform = mat4.Translate(data.Position.ToGlm()) * new quat(data.Rotation.ToRadiansGlm()).ToMat4 * mat4.Scale(data.Scale.ToGlm());
        for (var corner = 0; corner < 8; corner++)
        {
            var unit = new vec3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
            var game = data.Position.ToGlm() + GameTurns(rotation, unit * data.Scale.ToGlm());
            var drawn = (transform * new vec4(unit, 1)).xyz;
            Assert.True((game - drawn).Length < 1e-3f, $"Corner {unit} of a box turned by {rotation}: the game has it at {game}, the viewport draws it at {drawn}");
        }
    }

    private (Trigger Asset, TriggerData Data) AddTrigger(Vector4 rotation)
    {
        var id = _triggers++;
        var trigger = _project.Add(new Trigger { Chunk = "default", LayoutID = 0 }, $"Trigger {id}", id);
        var item = new TwinTrigger
        {
            Position = new Vector4(12.5f, -3, 40, 1),
            Rotation = rotation,
            Scale = new Vector4(15, 3, 2.5f, 1),
        };
        var data = new TriggerData(trigger, _project.Project.GlobalPackagePS2.URI, null, item, 0);
        trigger.SetData(data);
        return (trigger, data);
    }

    private Vector4 Built(IAsset trigger) => ((ITwinTrigger)trigger.GetData<TriggerData>().Export(_assets.Factory)).Trigger.Rotation;

    private static void AssertSameBits(Vector4 expected, Vector4 actual)
    {
        Assert.Equal(
            (BitConverter.SingleToInt32Bits(expected.X), BitConverter.SingleToInt32Bits(expected.Y), BitConverter.SingleToInt32Bits(expected.Z), BitConverter.SingleToInt32Bits(expected.W)),
            (BitConverter.SingleToInt32Bits(actual.X), BitConverter.SingleToInt32Bits(actual.Y), BitConverter.SingleToInt32Bits(actual.Z), BitConverter.SingleToInt32Bits(actual.W)));
    }

    [Theory]
    [MemberData(nameof(Turns))]
    public void TheBoxIsWhereTheGameHasIt(Single degrees, Single x, Single y, Single z)
    {
        var rotation = Quaternion(degrees, x, y, z);
        var (_, data) = AddTrigger(rotation);

        AssertBoxWhereTheGameHasIt(data, rotation);
    }

    // No angles give the quaternion's bits back, the game's is built while the angles are the ones worked out of it, saved and read again
    // too. Angles changed build their own quaternion, the one the viewport turns the box by
    [Theory]
    [MemberData(nameof(Turns))]
    public void TheGamesQuaternionIsBuiltUntilTheRotationChanges(Single degrees, Single x, Single y, Single z)
    {
        var rotation = Quaternion(degrees, x, y, z);
        var (trigger, data) = AddTrigger(rotation);

        AssertSameBits(rotation, Built(trigger));
        _assets.Reload<TriggerData>(trigger);
        AssertSameBits(rotation, Built(trigger));

        data = ((IAsset)trigger).GetData<TriggerData>();
        data.Rotation = new Vector3(data.Rotation.X + 10, data.Rotation.Y, data.Rotation.Z - 5);
        var built = Built(trigger);
        AssertSameBits(data.Rotation.ToQuaternion(), built);
        AssertBoxWhereTheGameHasIt(data, built);
    }

    // The angles say what the box is turned by, a quarter turn as the tools' floats have it isn't 89.98
    [Fact]
    public void TheAnglesAreTheTurnsTheGameHas()
    {
        var (_, data) = AddTrigger(new Vector4(0, 0.7071068f, 0, 0.7071068f));
        Assert.Equal((0.0f, 90.0f, 0.0f), (data.Rotation.X, MathF.Round(data.Rotation.Y, 4), data.Rotation.Z));

        (_, data) = AddTrigger(Quaternion(-30, 1, 0, 0));
        Assert.Equal((-30.0f, 0.0f, 0.0f), (MathF.Round(data.Rotation.X, 4), data.Rotation.Y, data.Rotation.Z));
    }
}
