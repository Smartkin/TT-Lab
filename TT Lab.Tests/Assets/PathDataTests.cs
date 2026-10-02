using Avalonia.Headless.XUnit;
using GlmSharp;
using Newtonsoft.Json;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using Path = TT_Lab.Assets.Instance.Path;
using Vector2 = Twinsanity.TwinsanityInterchange.Common.Vector2;
using Vector3 = Twinsanity.TwinsanityInterchange.Common.Vector3;

namespace TT_Lab.Tests.Assets;

// Paths store every segment's length and steps along with their points, which TT Lab keeps up to date instead of showing them
[Collection(ProjectCollection.Name)]
public sealed class PathDataTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    // Points 10 apart on a line make segments 10 long
    private static List<Vector3> Line(int points, float spacing = 10.0f)
    {
        return Enumerable.Range(0, points).Select(i => new Vector3(i * spacing, 0, 0)).ToList();
    }

    private static float[] Flatten(PathParameters.Values parameters) => [..parameters.ArcLengths, ..parameters.InverseSteps];

    private static float[] Flatten(ITwinPath path) => [..path.ArcLengths, ..path.InverseSteps];

    // The arc lengths, then as many inverse steps
    private static PathParameters.Values Halves(params float[] values)
    {
        return new PathParameters.Values([..values.Take(values.Length / 2)], [..values.Skip(values.Length / 2)]);
    }

    // Read the way a project loads the path's data file
    private PathData LoadPath(List<Vector3> points, PathParameters.Values parameters)
    {
        var path = _project.Add(new Path(), "Path");
        var data = new PathData(path);
        var json = JsonConvert.SerializeObject(new { Points = points, parameters.ArcLengths, parameters.InverseSteps });
        JsonConvert.PopulateObject(json, data, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
        path.SetData(data);
        return data;
    }

    [Fact]
    public void ParametersAreEverySegmentsLengthFromTheStartThenOneOverItsSteps()
    {
        var parameters = PathParameters.Create(Line(6).Select(point => new vec3(point.X, point.Y, point.Z)).ToList(), 1.5f);

        // 10 units take 7 steps of 1.5
        Assert.Equal([10.0f, 20.0f, 30.0f, 1 / 7.0f, 1 / 7.0f, 1 / 7.0f], Flatten(parameters), new FloatComparer(1e-4f));
    }

    [Fact]
    public void SegmentsTakeAtLeastFiveSteps()
    {
        Assert.Equal(5, PathParameters.GetSteps(1.0f, 2.5f));
        Assert.Equal(8, PathParameters.GetSteps(20.0f, 2.5f));
        Assert.Equal(9, PathParameters.GetSteps(20.1f, 2.5f));
    }

    // The step length isn't stored, 10 units in 5 steps, 20 in 8 and 7 in 5 took steps of 2.5 to 2.857
    [Fact]
    public void TheStepLengthIsWorkedOutFromTheParameters()
    {
        var (arcLengths, inverseSteps) = Halves(10.0f, 30.0f, 37.0f, 1 / 5.0f, 1 / 8.0f, 1 / 5.0f);
        var stepLength = PathParameters.FindStepLength(arcLengths, inverseSteps, 3)!.Value;

        Assert.InRange(stepLength, 2.5f, 20.0f / 7.0f);
        Assert.Null(PathParameters.FindStepLength([10.0f], [1 / 5.0f], 3));
    }

    [Fact]
    public void UneditedPathsKeepTheGamesParameters()
    {
        var games = Halves(10.0001f, 20.0002f, 30.0001f, 1 / 5.0f, 1 / 5.0f, 1 / 5.0f);
        var data = LoadPath(Line(6), games);

        var path = (ITwinPath)data.Export(new PS2ItemFactory());

        Assert.Equal(Flatten(games), Flatten(path));
    }

    // Saving the path's data brings them up to date
    [Fact]
    public void MovingPointsMakesTheParametersAgainWithThePathsStepLength()
    {
        // 10 units in 5 steps, steps of at least 2
        var data = LoadPath(Line(6), Halves(10.0f, 20.0f, 30.0f, 1 / 5.0f, 1 / 5.0f, 1 / 5.0f));
        foreach (var point in data.Points.Skip(3))
        {
            point.X += 10.0f;
        }

        JsonConvert.SerializeObject(data);

        // Segments 11.67, 16.67 and 11.67 long
        Assert.Equal([35.0f / 3.0f, 85.0f / 3.0f, 40.0f, 1 / 6.0f, 1 / 9.0f, 1 / 6.0f], Flatten(new PathParameters.Values(data.ArcLengths, data.InverseSteps)), new FloatComparer(1e-4f));
    }

    [Fact]
    public void AddedPointsGetTheirSegments()
    {
        var data = LoadPath(Line(4), Halves(10.0f, 1 / 5.0f));
        data.Points.Add(new Vector3(40, 0, 0));
        data.Points.Insert(0, new Vector3(-10, 0, 0));

        var path = (ITwinPath)data.Export(new PS2ItemFactory());

        Assert.Equal([10.0f, 20.0f, 30.0f, 1 / 5.0f, 1 / 5.0f, 1 / 5.0f], Flatten(path), new FloatComparer(1e-4f));
    }

    [AvaloniaFact]
    public void TheInspectorHasNoParameters()
    {
        var data = LoadPath(Line(4), Halves(10.0f, 1 / 5.0f));
        var document = new DocumentViewModel(data.GetOwner());
        document.Initialize();

        Assert.NotNull(document.PropertyGraph.Find("Root.AssetData.Points"));
        Assert.Null(document.PropertyGraph.Find("Root.AssetData.Parameters"));
    }

    public static TheoryData<Type, Type> Instances => new()
    {
        { typeof(Path), typeof(PathData) },
        { typeof(Position), typeof(PositionData) },
        { typeof(AiPath), typeof(AiPathData) },
        { typeof(AiPosition), typeof(AiPositionData) },
        { typeof(Camera), typeof(CameraData) },
        { typeof(ObjectInstance), typeof(ObjectInstanceData) },
        { typeof(Trigger), typeof(TriggerData) },
        { typeof(InstanceTemplate), typeof(InstanceTemplateData) },
    };

    // Duplicating an instance in the viewport copies its data
    [Theory]
    [MemberData(nameof(Instances))]
    public void InstancesDataCanBeCopied(Type assetType, Type dataType)
    {
        var asset = _project.Add((SerializableInstance)Activator.CreateInstance(assetType)!, "Instance");
        var data = (AbstractAssetData)Activator.CreateInstance(dataType, asset)!;
        var copyAsset = _project.Add((SerializableInstance)Activator.CreateInstance(assetType)!, "Copy");

        var copy = data.CopyFor(copyAsset);

        Assert.IsType(dataType, copy);
        Assert.Same(copyAsset, copy.GetOwner());
        Assert.Equal(JsonConvert.SerializeObject(data), JsonConvert.SerializeObject(copy));
    }

    private sealed class FloatComparer(float tolerance) : IEqualityComparer<float>
    {
        public bool Equals(float x, float y) => Math.Abs(x - y) <= tolerance;

        public int GetHashCode(float obj) => 0;
    }
}
