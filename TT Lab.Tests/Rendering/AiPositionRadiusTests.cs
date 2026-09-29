using Avalonia.Headless.XUnit;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Rendering;

// An AI position's radius is dragged by a handle on the +X side of its ring, the radius is how far from the position the handle goes
[Collection(ProjectCollection.Name)]
public sealed class AiPositionRadiusTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [AvaloniaFact]
    public void TheHandleStandsOnTheRingAndItsDistanceIsTheRadius()
    {
        var chunk = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/beach" }, "beach");
        var position = _project.Add(new AiPosition { Chunk = chunk.AdditionalPath!, AdditionalPath = chunk.AdditionalPath, LayoutID = 6 }, "AI 0");
        var data = new AiPositionData(position) { Coords = new Vector3(1, 2, 3), Radius = 1.5f };
        position.SetData(data);
        var document = new DocumentViewModel(position);
        document.Initialize();
        var coords = document.PropertyGraph.Find("Root.AssetData.Coords")!;
        var radius = document.PropertyGraph.Find("Root.AssetData.Radius")!;
        var handle = new AiPositionRadiusHandle(coords);

        Assert.Equal(new vec3(2.5f, 2, 3), handle.ToPosition(radius.GetValue()));

        // Dragged off the ring's axis the radius is still the distance, and the handle goes back onto the axis
        radius.SetValue(handle.ToData(new vec3(1, 2, 3) + new vec3(0, 3, 4)));
        Assert.Equal(5.0f, data.Radius, 4);
        Assert.Equal(new vec3(6, 2, 3), handle.ToPosition(radius.GetValue()));

        // It follows the position around
        coords.SetValue(new Vector3(-1, 0, 0));
        Assert.Equal(new vec3(4, 0, 0), handle.ToPosition(radius.GetValue()));

        document.Undo();
        document.Undo();
        Assert.Equal(1.5f, data.Radius);
    }
}
