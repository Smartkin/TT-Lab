using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using Avalonia.Headless.XUnit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;

namespace TT_Lab.Tests.Assets;

// A camera's subtypes carry values the game works out from their geometry (a path's parameters, a spline's tangents and lengths,
// the boss camera's two matrices), which follow the geometry edited in TT Lab, and the files of older projects keep loading
[Collection(ProjectCollection.Name)]
public sealed class CameraDataTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public CameraDataTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    private (Camera Asset, CameraData Data) AddCamera(CameraSubBase? first, CameraSubBase? second = null)
    {
        var camera = _project.Add(new Camera { Chunk = "default", LayoutID = 4 }, "Camera");
        var data = new CameraData(camera) { MainCamera1 = first, MainCamera2 = second, Flags = (ITwinCamera.CameraFlags)0x118, BlendTime = 1.0f };
        camera.SetData(data);
        return (camera, data);
    }

    private static CameraPath Path()
    {
        var path = new CameraPath { Offset = -9 };
        for (var i = 0; i < 6; i++)
        {
            path.PathPoints.Add(new Vector4(i * 10, 0, 0, 1));
        }

        path.Parameters = PathParameters.Create(path.PathPoints.Select(point => new GlmSharp.vec3(point.X, point.Y, point.Z)).ToList(), 0.5f);
        return path;
    }

    private static CameraSpline Spline()
    {
        var spline = new CameraSpline { StepLength = 2, SplineFlags = 15, Offset = 6 };
        for (var i = 0; i < 4; i++)
        {
            spline.PathPoints.Add(new Vector4(i * 2, 0, 0, 0));
            spline.Tangents.Add(new Vector4(1, 0, 0, 1));
        }

        // The lengths from the start then 1 over the steps, pair after pair: 2, 4, 6, then 1/31 three times
        spline.Parameters.Add(new Vector2 { X = 2, Y = 4 });
        spline.Parameters.Add(new Vector2 { X = 6, Y = 1 / 31.0f });
        spline.Parameters.Add(new Vector2 { X = 1 / 31.0f, Y = 1 / 31.0f });

        return spline;
    }

    [Fact]
    public void ParametersStayTheGamesUntilAPointMoves()
    {
        var (camera, data) = AddCamera(Path(), Spline());
        var before = _assets.Export(camera);

        var unchanged = _assets.Reload<CameraData>(camera);
        Assert.Equal(before, _assets.Export(camera));

        ((CameraPath)unchanged.MainCamera1!).PathPoints[2] = new Vector4(20, 5, 0, 1);
        ((CameraSpline)unchanged.MainCamera2!).PathPoints[1] = new Vector4(2, 2, 0, 0);
        var moved = _assets.Reload<CameraData>(camera);

        Assert.NotEqual(before, _assets.Export(camera));
        var path = (CameraPath)moved.MainCamera1!;
        // The parameters are the segments' lengths from the start then 1 over their steps, pair after pair: the segments got longer
        // through the raised point and every one keeps taking steps of 0.5
        Assert.Equal(3, path.Parameters.Count);
        Assert.True(path.Parameters[0].X > 10.0f);
        Assert.True(path.Parameters[0].Y > path.Parameters[0].X + 10.0f);
        Assert.Equal(1 / 21.0f, path.Parameters[1].Y, 1e-4f);
        var spline = (CameraSpline)moved.MainCamera2!;
        Assert.Equal(3, spline.Parameters.Count);
        // Lengths from the start then 1 over the steps, pair after pair: the two segments around the raised sample got longer
        Assert.Equal(MathF.Sqrt(8), spline.Parameters[0].X, 1e-4f);
        Assert.Equal(MathF.Sqrt(8) * 2, spline.Parameters[0].Y, 1e-4f);
        Assert.Equal(MathF.Sqrt(8) * 2 + 2, spline.Parameters[1].X, 1e-4f);
        // A segment 2 long taking 31 steps had steps of 2/31 to 2/30, the longer ones take more of them and the unchanged one its 31
        Assert.InRange(1 / spline.Parameters[1].Y, 42, 45);
        Assert.Equal(1 / 31.0f, spline.Parameters[2].Y, 1e-6f);
        // The tangent at the moved sample points from the one before to the one after
        Assert.Equal((1f, 0f, 0f, 1f), (spline.Tangents[1].X, spline.Tangents[1].Y, spline.Tangents[1].Z, spline.Tangents[1].W));
        Assert.True(spline.Tangents[0].Y > 0.5f);
        Assert.Equal((MathF.Sqrt(8) * 2 + 2) / 3, spline.StepLength, 1e-4f);
    }

    [Fact]
    public void BossCamerasKeepTheirMatricesInverse()
    {
        var boss = new BossCamera { Orbit = new Vector4(35, 65, 2, 0) };
        boss.ArenaToWorld = GlmSharp.mat4.Translate(new GlmSharp.vec3(10, 0, 5)).ToTwin();
        boss.WorldToArena = GlmSharp.mat4.Translate(new GlmSharp.vec3(-10, 0, -5)).ToTwin();
        var (camera, data) = AddCamera(boss);
        var before = _assets.Export(camera);

        var unchanged = _assets.Reload<CameraData>(camera);
        Assert.Equal(before, _assets.Export(camera));

        ((BossCamera)unchanged.MainCamera1!).ArenaToWorld = GlmSharp.mat4.Translate(new GlmSharp.vec3(0, 0, 20)).ToTwin();
        var moved = (BossCamera)_assets.Reload<CameraData>(camera).MainCamera1!;

        Assert.Equal((0f, 0f, -20f), (moved.WorldToArena.Column4.X, moved.WorldToArena.Column4.Y, moved.WorldToArena.Column4.Z));
    }

    // The game uses each boss camera matrix as the other's inverse (BossCameraAt), so the inspector edits the arena's own and World To
    // Arena follows it, like a chunk link's object matrix follows its chunk matrix
    [AvaloniaFact]
    public void WorldToArenaFollowsArenaToWorldInTheInspector()
    {
        var arena = GlmSharp.mat4.Translate(new GlmSharp.vec3(10, 0, 5)) * GlmSharp.mat4.RotateY(0.3f);
        var boss = new BossCamera { Orbit = new Vector4(35, 65, 2, 0), ArenaToWorld = arena.ToTwin(), WorldToArena = arena.Inverse.ToTwin() };
        var (camera, _) = AddCamera(null, boss);
        var stored = (boss.WorldToArena.ToGlm(), boss.ArenaToWorld.ToGlm());
        var document = new DocumentViewModel(camera);
        document.Initialize();
        var arenaToWorld = document.PropertyGraph.Find("Root.AssetData.MainCamera2.ArenaToWorld")!;
        var worldToArena = document.PropertyGraph.Find("Root.AssetData.MainCamera2.WorldToArena")!;

        // Opening keeps the game's values
        Assert.Equal(stored, (boss.WorldToArena.ToGlm(), boss.ArenaToWorld.ToGlm()));
        Assert.True(EditorDescRegistry.GetDesc(document, worldToArena).Construct().IsReadOnly);
        Assert.False(EditorDescRegistry.GetDesc(document, arenaToWorld).Construct().IsReadOnly);

        var moved = GlmSharp.mat4.Translate(new GlmSharp.vec3(-4, 2, 30)) * GlmSharp.mat4.RotateY(1.2f);
        arenaToWorld.SetValue(moved.ToTwin());

        var product = moved * boss.WorldToArena.ToGlm();
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(i % 5 == 0 ? 1.0f : 0.0f, product[i / 4, i % 4], 1e-5f);
        }

        // One step takes both back
        document.Undo();
        Assert.Equal(stored, (boss.WorldToArena.ToGlm(), boss.ArenaToWorld.ToGlm()));
    }
}
