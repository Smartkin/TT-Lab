using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.Tests.Build;

// The game numbers a layout's elements of a kind in the order it reads them (the decomp's instancesection.cpp) and the build
// writes them in the order of their IDs: references are an element's place among its kind of its layout, a taken out element
// made every reference past it point at the next one
[Collection(ProjectCollection.Name)]
public sealed class LayoutIndexesTests : IDisposable
{
    private const string Beach = "levels/earth/hub/beach";
    private readonly TestProject _project = new();
    private readonly List<IAsset> _resources = [];

    public void Dispose() => _project.Dispose();

    private T Add<T>(string name, UInt32 id, int layout, Func<IAsset, AbstractAssetData> data) where T : SerializableInstance, new()
    {
        var asset = _project.Add(new T { Chunk = Beach, LayoutID = layout }, name, id);
        asset.SetData(data(asset));
        _resources.Add(asset);
        return asset;
    }

    private GameObject Crate()
    {
        var crate = _project.Add(new GameObject(), "Crate", 0x10);
        crate.SetData(new GameObjectData(crate) { Type = ITwinObject.ObjectType.Crate });
        return crate;
    }

    private IDisposable Use() => new LayoutIndexes("beach", _resources).Use();

    private static ITwinInstance Export(ObjectInstance instance) => (ITwinInstance)((IAsset)instance).GetData<ObjectInstanceData>().Export(new PS2ItemFactory());

    private static List<Vector3> FourPoints() => [new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(3, 0, 0)];

    private static UInt16[] Ids(params UInt16[] ids) => ids;

    [Fact]
    public void ElementsAreFoundByTheirPlaceAmongTheirKind()
    {
        var crate = Crate();
        var first = Add<ObjectInstance>("Instance 0", 0, 0, asset => new ObjectInstanceData(asset) { ObjectId = crate.URI });
        // Instances 1, 3 and 4 were taken out
        var second = Add<ObjectInstance>("Instance 2", 2, 0, asset => new ObjectInstanceData(asset) { ObjectId = crate.URI });
        var third = Add<ObjectInstance>("Instance 5", 5, 0, asset => new ObjectInstanceData(asset) { ObjectId = crate.URI, Instances = [first.URI, second.URI] });
        var trigger = Add<Trigger>("Trigger 3", 3, 0, asset => new TriggerData(asset) { Instances = [third.URI, first.URI] });
        var camera = Add<Camera>("Camera 1", 1, 0, asset => new CameraData(asset));
        ((IAsset)camera).GetData<CameraData>().Trigger.Instances.Add(second.URI);

        using var indexes = Use();

        Assert.Equal(new[] { 0U, 1U, 2U }, new IAsset[] { first, second, third }.Select(asset => asset.ExportTwinID));
        Assert.Equal(0U, ((IAsset)trigger).ExportTwinID);
        var told = (ITwinTrigger)((IAsset)trigger).GetData<TriggerData>().Export(new PS2ItemFactory());
        Assert.Equal(Ids(2, 0), told.Trigger.Instances);
        Assert.Equal(Ids(0, 1), Export(third).Instances);
        var framed = (ITwinCamera)((IAsset)camera).GetData<CameraData>().Export(new PS2ItemFactory());
        Assert.Equal(Ids(1), framed.CamTrigger.Instances);
    }

    [Fact]
    public void OutsideABuildElementsKeepTheirIds()
    {
        var crate = Crate();
        var instance = Add<ObjectInstance>("Instance 5", 5, 0, asset => new ObjectInstanceData(asset) { ObjectId = crate.URI });

        Assert.Equal(5U, ((IAsset)instance).ExportTwinID);
    }

    // Triggers, cameras and instances find instances among their own layout's
    [Fact]
    public void InstancesOfAnotherLayoutAreRefused()
    {
        var crate = Crate();
        var instance = Add<ObjectInstance>("Instance 0", 0, 3, asset => new ObjectInstanceData(asset) { ObjectId = crate.URI });
        var trigger = Add<Trigger>("Trigger 0", 0, 0, asset => new TriggerData(asset) { Instances = [instance.URI] });
        var linking = Add<ObjectInstance>("Instance 1", 0, 0, asset => new ObjectInstanceData(asset) { ObjectId = crate.URI, Instances = [instance.URI] });

        using var indexes = Use();

        Assert.Contains("layout 3", Assert.Throws<InvalidOperationException>(() => ((IAsset)trigger).GetData<TriggerData>().Export(new PS2ItemFactory())).Message);
        Assert.Contains("layout 3", Assert.Throws<InvalidOperationException>(() => Export(linking)).Message);
    }

    // The chunk's own layouts (0-2 and 7) find positions from the start of the chunk's list, the others past the positions of the last
    // own layout that has any; paths are found from the start
    [Fact]
    public void PositionsAndPathsAreOneListOfTheChunk()
    {
        var crate = Crate();
        var own = Enumerable.Range(0, 2).Select(i => Add<Position>($"Position {i}", (UInt32)i, 0, asset => new PositionData(asset))).ToList();
        var other = Enumerable.Range(0, 3).Select(i => Add<Position>($"Other position {i}", (UInt32)i, 4, asset => new PositionData(asset))).ToList();
        var ownPath = Add<Path>("Path 0", 0, 0, asset => new PathData(asset) { Points = FourPoints() });
        var otherPath = Add<Path>("Other path 0", 0, 4, asset => new PathData(asset) { Points = FourPoints() });
        var ownInstance = Add<ObjectInstance>("Instance 0", 0, 0, asset => new ObjectInstanceData(asset) { ObjectId = crate.URI, Positions = [own[1].URI], Paths = [ownPath.URI] });
        var otherInstance = Add<ObjectInstance>("Other instance 0", 0, 4,
            asset => new ObjectInstanceData(asset) { ObjectId = crate.URI, Positions = [other[2].URI, other[0].URI], Paths = [otherPath.URI, ownPath.URI] });
        var unreachable = Add<ObjectInstance>("Other instance 1", 1, 4, asset => new ObjectInstanceData(asset) { ObjectId = crate.URI, Positions = [own[0].URI] });
        var tooEarly = Add<ObjectInstance>("Instance 1", 1, 0, asset => new ObjectInstanceData(asset) { ObjectId = crate.URI, Positions = [other[0].URI] });

        using var indexes = Use();

        Assert.Equal(Ids(1), Export(ownInstance).Positions);
        Assert.Equal(Ids(0), Export(ownInstance).Paths);
        var written = Export(otherInstance);
        // The game takes positionCount + the reference: layout 4's third position is the chunk's fifth
        Assert.Equal(Ids(2, 0), written.Positions);
        Assert.Equal(Ids(1, 0), written.Paths);
        Assert.Contains("past those of the chunk's own layouts", Assert.Throws<InvalidOperationException>(() => Export(unreachable)).Message);
        Assert.Contains("up to the instance's (0)", Assert.Throws<InvalidOperationException>(() => Export(tooEarly)).Message);
    }

    [Fact]
    public void AChunksAiNavigationIsInOneLayout()
    {
        var first = Add<AiPosition>("AI Navigation Position 0", 0, 6, asset => new AiPositionData(asset));
        var second = Add<AiPosition>("AI Navigation Position 3", 3, 6, asset => new AiPositionData(asset));
        var path = Add<AiPath>("AI Navigation Path 0", 0, 6, asset => new AiPathData(asset) { PathBegin = second.URI, PathEnd = first.URI });
        using (Use())
        {
            var written = (ITwinAIPath)((IAsset)path).GetData<AiPathData>().Export(new PS2ItemFactory());
            Assert.Equal((UInt16)1, written.PositionA);
            Assert.Equal((UInt16)0, written.PositionB);
        }

        var stray = Add<AiPosition>("AI Navigation Position 0", 0, 3, asset => new AiPositionData(asset));
        using (Use())
        {
            Assert.Contains("layouts 3, 6", Assert.Throws<InvalidOperationException>(() => ((IAsset)path).GetData<AiPathData>().Export(new PS2ItemFactory())).Message);
            Assert.Contains("layouts 3, 6", Assert.Throws<InvalidOperationException>(() => ((IAsset)stray).GetData<AiPositionData>().Export(new PS2ItemFactory())).Message);
        }
    }

    // Every chunk's collision finds the default chunk's surfaces by the ID it was written with, the game by their place in its table of 128
    [Fact]
    public void SurfacesAreNumberedWithoutGaps()
    {
        var surfaces = new[] { 0U, 1U, 3U }.Select(id => Add<CollisionSurface>($"Surface {id}", id, 7, asset => new CollisionSurfaceData(asset))).ToList();
        using (Use())
        {
            ((IAsset)surfaces[1]).GetData<CollisionSurfaceData>().Export(new PS2ItemFactory());
            Assert.Contains("fill the gap", Assert.Throws<InvalidOperationException>(() => ((IAsset)surfaces[2]).GetData<CollisionSurfaceData>().Export(new PS2ItemFactory())).Message);
        }

        _resources.Remove(surfaces[2]);
        var more = Enumerable.Range(2, CollisionSurfaceData.MaxSurfaces - 1).Select(id => Add<CollisionSurface>($"More {id}", (UInt32)id, 7, asset => new CollisionSurfaceData(asset))).ToList();
        using (Use())
        {
            ((IAsset)more[^2]).GetData<CollisionSurfaceData>().Export(new PS2ItemFactory());
            Assert.Contains("at most 128", Assert.Throws<InvalidOperationException>(() => ((IAsset)more[^1]).GetData<CollisionSurfaceData>().Export(new PS2ItemFactory())).Message);
        }
    }

    // A class keeps its own share of each kind of property and puts 7 more aside, the game writes the rest over its memory
    [Fact]
    public void InstancesKeepSevenPropertiesPastTheirClasss()
    {
        var crate = Crate();
        // Crates keep 3 floats and 2 integers
        var full = Add<ObjectInstance>("Instance 0", 0, 0, asset => new ObjectInstanceData(asset)
            { ObjectId = crate.URI, FloatProperties = [..Enumerable.Repeat(1.0f, 8)], IntProperties = [1, 2, 3, 4] });
        var over = Add<ObjectInstance>("Instance 1", 1, 0, asset => new ObjectInstanceData(asset)
            { ObjectId = crate.URI, FloatProperties = [..Enumerable.Repeat(1.0f, 9)], IntProperties = [1, 2, 3, 4] });

        Export(full);
        Assert.Contains("8 property values", Assert.Throws<InvalidOperationException>(() => Export(over)).Message);
    }

    [Fact]
    public void APathHasFourPointsAtLeast()
    {
        var path = Add<Path>("Path 0", 0, 0, asset => new PathData(asset) { Points = FourPoints().Take(3).ToList() });

        Assert.Contains("at least 4", Assert.Throws<InvalidOperationException>(() => ((IAsset)path).GetData<PathData>().Export(new PS2ItemFactory())).Message);
    }

    // The two versions' chunks of a path are numbered apart, an ID past the other version's elements left a gap
    [Fact]
    public void EachVersionsChunkNumbersItsOwnElements()
    {
        var xbox = _project.Project.GlobalPackageXbox;
        for (var id = 0U; id < 5; id++)
        {
            _project.Add(new CollisionSurface { Chunk = Beach, LayoutID = 7 }, $"Xbox surface {id}", id, xbox);
        }

        _project.Add(new CollisionSurface { Chunk = Beach, LayoutID = 7 }, "PS2 surface 0", 0);

        Assert.Equal(1U, TwinIdGeneratorServiceProvider.GetGeneratorForChunk<CollisionSurface>(Beach, _project.Project.GlobalPackagePS2.URI, Enums.Layouts.LAYER_8).GenerateTwinId());
        Assert.Equal(5U, TwinIdGeneratorServiceProvider.GetGeneratorForChunk<CollisionSurface>(Beach, xbox.URI, Enums.Layouts.LAYER_8).GenerateTwinId());
    }
}
