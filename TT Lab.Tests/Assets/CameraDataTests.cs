using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets.Instance;
using TT_Lab.Controls;
using TT_Lab.Extensions;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.Views.Editors;
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

        (path.ArcLengths, path.InverseSteps) = PathParameters.Create(path.PathPoints.Select(point => new GlmSharp.vec3(point.X, point.Y, point.Z)).ToList(), 0.5f);
        return path;
    }

    private static CameraSpline Spline()
    {
        var spline = new CameraSpline { StepLength = 2, SplineFlags = (ITwinCamera.SplineCameraFlags)15, Offset = 6 };
        for (var i = 0; i < 4; i++)
        {
            // The ends have values, what's between passes over
            spline.PathPoints.Add(new Vector4(i * 2, 0, 0, Word(i is 0 or 3 ? CameraGeometry.NeutralKeySample : CameraGeometry.InBetweenSample)));
            spline.Tangents.Add(new Vector4(1, 0, 0, 1));
        }

        // The lengths from the start, then 1 over the steps
        spline.ArcLengths = [2, 4, 6];
        spline.InverseSteps = [1 / 31.0f, 1 / 31.0f, 1 / 31.0f];

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
        // The segments' lengths from the start and 1 over their steps: the segments got longer through the raised point and every one
        // keeps taking steps of 0.5
        Assert.Equal(3, path.ArcLengths.Count);
        Assert.True(path.ArcLengths[0] > 10.0f);
        Assert.True(path.ArcLengths[1] > path.ArcLengths[0] + 10.0f);
        Assert.Equal(1 / 21.0f, path.InverseSteps[0], 1e-4f);
        var spline = (CameraSpline)moved.MainCamera2!;
        Assert.Equal(3, spline.ArcLengths.Count);
        // The two segments around the raised sample got longer
        Assert.Equal(MathF.Sqrt(8), spline.ArcLengths[0], 1e-4f);
        Assert.Equal(MathF.Sqrt(8) * 2, spline.ArcLengths[1], 1e-4f);
        Assert.Equal(MathF.Sqrt(8) * 2 + 2, spline.ArcLengths[2], 1e-4f);
        // A segment 2 long taking 31 steps had steps of 2/31 to 2/30, the longer ones take more of them and the unchanged one its 31
        Assert.InRange(1 / spline.InverseSteps[0], 42, 45);
        Assert.Equal(1 / 31.0f, spline.InverseSteps[2], 1e-6f);
        // The tangent at the moved sample points from the one before to the one after
        Assert.Equal((1f, 0f, 0f, 1f), (spline.Tangents[1].X, spline.Tangents[1].Y, spline.Tangents[1].Z, spline.Tangents[1].W));
        Assert.True(spline.Tangents[0].Y > 0.5f);
        Assert.Equal((MathF.Sqrt(8) * 2 + 2) / 3, spline.StepLength, 1e-4f);
    }

    private static float Word(UInt32 bits) => BitConverter.UInt32BitsToSingle(bits);

    // A sample's W is a word of values (the decomp's CameraSplineCamera::SampleWord): samples made in TT Lab had 0, which the game reads
    // as an offset of -50 units along the spline and -5 times the way toward the target, and an end passing over reads the same past it
    [Fact]
    public void NewSplineSamplesPassOverAndTheEndsHaveValues()
    {
        var spline = Spline();
        spline.PathPoints.Insert(2, new Vector4(3, 1, 0, 0));
        spline.PathPoints.Add(new Vector4(8, 0, 0, 0));
        spline.PathPoints[0].W = Word(CameraGeometry.InBetweenSample);
        // 11.5 units further along, as on the game's cogpa01 cameras
        spline.PathPoints[1].W = Word(0x9D7080);

        CameraGeometry.UpdateDerived(spline, null);

        Assert.Equal(new[] { 0x7FFF80U, 0x9D7080U, 0x1000000U, 0x1000000U, 0x7FFF80U, 0x7FFF80U }, spline.PathPoints.Select(sample => BitConverter.SingleToUInt32Bits(sample.W)));
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

    // The camera controller only reads most values with their flags (FollowCamera taking a camera), without them they're the tools'
    // memory: l03stock's first camera has 0.5's bits as its blend in yaw. Those fields are grayed out while their flags are off
    [AvaloniaFact]
    public void ValuesOnlyReadWithTheirFlagsAreGrayedOutWithout()
    {
        var (camera, data) = AddCamera(null);
        data.Flags = ITwinCamera.CameraFlags.SetsPitch | ITwinCamera.CameraFlags.SetsFov;
        var document = new DocumentViewModel(camera);
        document.Initialize();
        var flags = document.PropertyGraph.Find("Root.AssetData.Flags")!;
        bool Grayed(string name) => document.PropertyGraph.Find($"Root.AssetData.{name}")!.IsReadOnly;

        Assert.False(Grayed(nameof(CameraData.PitchStart)));
        Assert.False(Grayed(nameof(CameraData.FovEnd)));
        Assert.True(Grayed(nameof(CameraData.YawStart)));
        Assert.True(Grayed(nameof(CameraData.BlendInYaw)));
        Assert.True(Grayed(nameof(CameraData.BlendInPitch)));
        Assert.True(Grayed(nameof(CameraData.TargetBoxMin)));
        Assert.True(Grayed(nameof(CameraData.FramingShare)));
        Assert.True(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.YawSpeed")!).Construct().IsReadOnly);

        // Blending the pitch in reads the blend in pitch, and the blend in yaw and yaw start the yaw is compared with
        flags.SetValue(data.Flags | ITwinCamera.CameraFlags.BlendsInFromPitch);
        Assert.False(Grayed(nameof(CameraData.BlendInPitch)));
        Assert.False(Grayed(nameof(CameraData.BlendInYaw)));
        Assert.False(Grayed(nameof(CameraData.YawStart)));
        Assert.True(Grayed(nameof(CameraData.YawEnd)));
        Assert.True(Grayed(nameof(CameraData.BlendInDistance)));

        // Undo grays them out again
        document.Undo();
        Assert.True(Grayed(nameof(CameraData.BlendInPitch)));
        Assert.True(Grayed(nameof(CameraData.YawStart)));
        document.Redo();
        Assert.False(Grayed(nameof(CameraData.BlendInYaw)));
    }

    // The game only reads bit 0 of a spline's flags (CameraSplineCamera::Read), the retail ones have the tools' 0xCDCD: the inspector
    // shows that bit and keeps the others
    [AvaloniaFact]
    public void SplineFlagsShowTheOnlyBitTheGameReads()
    {
        var spline = Spline();
        spline.SplineFlags = (ITwinCamera.SplineCameraFlags)0xCDCD;
        var (camera, _) = AddCamera(null, spline);
        var document = new DocumentViewModel(camera);
        document.Initialize();
        var flags = document.PropertyGraph.Find("Root.AssetData.MainCamera2.SplineFlags")!;

        var takesOffset = Assert.Single(flags.Children);
        Assert.True(takesOffset.GetValue<bool>());
        takesOffset.SetValue(false);
        Assert.Equal((ITwinCamera.SplineCameraFlags)0xCDCC, spline.SplineFlags);
        document.Undo();
        Assert.Equal((ITwinCamera.SplineCameraFlags)0xCDCD, spline.SplineFlags);
    }

    // A sample's W is a word of the game's (CameraSplineCamera::SampleWord), which the inspector edits as whether the sample is a key and
    // a key's offset along the curve and share of the way toward the target, keeping the bits nothing reads
    [AvaloniaFact]
    public void SplineSamplesAreEditedAsTheValuesOfTheirWord()
    {
        Assert.Equal(0f, SplineSampleWord.Share(CameraGeometry.NeutralKeySample));
        Assert.Equal(-50f / 32768, SplineSampleWord.Offset(CameraGeometry.NeutralKeySample));
        Assert.Equal(0x800080u, SplineSampleWord.WithOffset(CameraGeometry.NeutralKeySample, 0));
        Assert.Equal(0xFE000000u | 0x123, SplineSampleWord.WithKey(0xFE000000 | SplineSampleWord.Passes | 0x123, true));
        Assert.Equal(0xFFu, SplineSampleWord.WithShare(0, 100));
        // A sample passing over without values becomes a key that changes nothing, and a key of the lowest values isn't a new sample's W
        Assert.Equal(CameraGeometry.NeutralKeySample, SplineSampleWord.WithKey(CameraGeometry.InBetweenSample, true));
        Assert.NotEqual(0u, SplineSampleWord.Stored(SplineSampleWord.WithShare(SplineSampleWord.WithOffset(0, -50), -5)));

        var spline = Spline();
        var (camera, _) = AddCamera(spline);
        var document = new DocumentViewModel(camera);
        document.Initialize();
        Assert.IsType<CollectionEditorDesc>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.MainCamera1.PathPoints")!));
        var sample = Assert.IsType<SplineSampleFieldViewModel>(
            EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.MainCamera1.PathPoints[1]")!).Construct());
        sample.Activator.Activate();
        UInt32 WordOf(int index) => SplineSampleWord.Of(spline.PathPoints[index].W);

        Assert.False(sample.IsKey);
        sample.IsKey = true;
        Assert.Equal(CameraGeometry.NeutralKeySample, WordOf(1));
        Assert.Equal("-0.0015", sample.OffsetText);
        Assert.Equal("0", sample.ShareText);

        // What's typed stays while it means the value
        sample.OffsetText = "10";
        Assert.Equal("10", sample.OffsetText);
        Assert.Equal(10.0f, SplineSampleWord.Offset(WordOf(1)), 0.001f);
        sample.ShareText = "0.5";
        Assert.Equal(0.5f, SplineSampleWord.Share(WordOf(1)), 0.02f);
        Assert.Equal(2f, sample.X.Property.GetValue<Single>());

        document.Undo();
        Assert.Equal("0", sample.ShareText);
        document.Undo();
        Assert.Equal("-0.0015", sample.OffsetText);
        document.Undo();
        Assert.False(sample.IsKey);
        Assert.Equal(CameraGeometry.InBetweenSample, WordOf(1));
    }

    // The inspector shows the samples as rows of their place and their word's values, a passing sample's values grayed out
    [AvaloniaFact]
    public void TheInspectorShowsSplineSamplesAsTheirValues()
    {
        var spline = Spline();
        var (camera, _) = AddCamera(spline);
        var document = new DocumentViewModel(camera);
        document.Initialize();
        var samples = document.PropertyGraph.Find("Root.AssetData.MainCamera1.PathPoints")!;
        document.OpenInspector(document.PropertyGraph.Find("Root.AssetData.MainCamera1"), samples.Children[1]);
        var window = new Window { Content = new DocumentScrollViewer { Content = document.Inspector }, Width = 800, Height = 700 };
        window.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var rows = window.GetVisualDescendants().OfType<SplineSampleFieldView>().ToList();
        Assert.Equal(4, rows.Count);
        var row = rows.Single(view => view.ViewModel!.Property == samples.Children[1]);
        Assert.False(row.OffsetField.IsEnabled);
        Assert.True(rows.Single(view => view.ViewModel!.Property == samples.Children[0]).OffsetField.IsEnabled);

        row.KeyField.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(row.OffsetField.IsEnabled);
        row.OffsetField.Text = "3";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3.0f, SplineSampleWord.Offset(SplineSampleWord.Of(spline.PathPoints[1].W)), 0.001f);
        Assert.True(SplineSampleWord.IsKey(SplineSampleWord.Of(spline.PathPoints[1].W)));
        window.Close();
    }
}
