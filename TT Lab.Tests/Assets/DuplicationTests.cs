using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using InstancePath = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.Tests.Assets;

// Ctrl+D copies an instance of a layout as a new instance with its data copied for it (AbstractAssetData.CopyFor, made the way loading
// makes data) and an element of a list (a link, a hull, an emitter, a particle system, a point, a light, a placement) as a deep clone put
// next to it: the copy has every value of the original and nothing that is the original's, so neither changes with the other, and it
// saves. A duplicated camera's trigger had no owner and saving the camera threw
[Collection(ProjectCollection.Name)]
public sealed class DuplicationTests : IDisposable
{
    private readonly TestProject _project = new();
    private UInt32 _nextId = 0x40;

    public void Dispose() => _project.Dispose();

    private T AddInstance<T>(int layout, Func<IAsset, AbstractAssetData> data, string chunk = "levels/test") where T : SerializableInstance, new()
    {
        var instance = _project.Add(new T { Chunk = chunk, LayoutID = layout }, $"{typeof(T).Name} {_nextId}", _nextId++);
        instance.SetData(data(instance));
        return instance;
    }

    // The viewport's copy: a new instance of the type in the same layout, its data copied for it
    private T Duplicate<T>(T original) where T : SerializableInstance, new()
    {
        var copy = _project.Add(new T { Chunk = original.Chunk, LayoutID = original.LayoutID }, $"{original.Alias} Copy", _nextId++);
        copy.SetData(original.GetData().CopyFor(copy));
        return copy;
    }

    private static T Data<T>(IAsset asset) where T : AbstractAssetData => asset.GetData<T>();

    private static void AssertDuplicated(SerializableAsset original, SerializableAsset copy)
    {
        var originalData = original.GetData();
        var copyData = copy.GetData();
        Assert.Same(copy, copyData.GetOwner());
        Assert.Equal(JsonConvert.SerializeObject(originalData), JsonConvert.SerializeObject(copyData));
        AssertNothingShared(originalData, copyData);
        // It saves, linking what the original links
        original.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        copy.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        Assert.Equal(original.References.Select(uri => (string)uri).Order(), copy.References.Select(uri => (string)uri).Order());
    }

    private static void AssertClonedWhole(object original)
    {
        var copy = CloneUtils.DeepClone(original, original.GetType());
        Assert.IsType(original.GetType(), copy);
        Assert.Equal(JsonConvert.SerializeObject(original), JsonConvert.SerializeObject(copy));
        AssertNothingShared(original, copy);
    }

    // Every object the copy is made of is its own: changing the copy can't change the original
    private static void AssertNothingShared(object original, object copy)
    {
        var originals = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Collect(original, originals);
        var copies = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Collect(copy, copies);
        Assert.Empty(copies.Where(originals.Contains).Select(shared => shared.GetType().Name));
    }

    private static void Collect(object? value, HashSet<object> seen)
    {
        if (value == null || value is string or IAsset or Type or Delegate || value.GetType().IsValueType || !seen.Add(value))
        {
            return;
        }

        if (value is IEnumerable items)
        {
            foreach (var item in items)
            {
                Collect(item, seen);
            }
        }

        foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.GetMethod == null)
            {
                continue;
            }

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            Collect(propertyValue, seen);
        }
    }

    [Fact]
    public void ADuplicatedCameraHasATriggerOfItsOwnAndSaves()
    {
        var camera = AddInstance<Camera>(4, asset => new CameraData(asset)
        {
            Flags = (ITwinCamera.CameraFlags)0x8A0,
            FovStart = 10923,
            FovEnd = 16384,
            MainCamera1 = new CameraPoint { Point = new Vector4(25, 1, 7, 0) },
            MainCamera2 = new CameraPoint { Point = new Vector4(26, 11, 29, 0) },
        });
        Data<CameraData>(camera).Trigger.Position = new Vector3(23, 4, 10);

        var copy = Duplicate(camera);

        Assert.Same(copy, Data<CameraData>(copy).Trigger.GetOwner());
        AssertDuplicated(camera, copy);
        // Moving the copy and its points leaves the original's where they are
        Data<CameraData>(copy).Trigger.Position.X = 40;
        ((CameraPoint)Data<CameraData>(copy).MainCamera2!).Point.Y = 20;
        Assert.Equal(23, Data<CameraData>(camera).Trigger.Position.X);
        Assert.Equal(11, ((CameraPoint)Data<CameraData>(camera).MainCamera2!).Point.Y);
    }

    [Fact]
    public void EveryKindOfInstanceIsDuplicatedWhole()
    {
        var gameObject = _project.Add(new GameObject(), "Crate", 0x10);
        gameObject.SetData(new GameObjectData(gameObject) { Name = "Crate" });
        var spot = AddInstance<Position>(0, asset => new PositionData(asset) { Coords = new Vector3(4, 5, 6) });
        var route = AddInstance<InstancePath>(0, asset => new PathData(asset)
        {
            Points = [new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 1, 0), new Vector3(3, 1, 1)],
        });
        var crate = AddInstance<ObjectInstance>(0, asset => new ObjectInstanceData(asset)
        {
            ObjectId = gameObject.URI,
            Position = new Vector3(1, 2, 3),
            Rotation = new Vector3(0, 90, 0),
            TaggedProperties = [new(1), new(2)],
            FloatProperties = [0.5f],
            IntProperties = [0, 255],
            Positions = [spot.URI],
            Paths = [route.URI],
        });
        var trigger = AddInstance<Trigger>(0, asset => new TriggerData(asset)
        {
            Position = new Vector3(1, 2, 3),
            Scale = new Vector3(2, 3, 4),
            Instances = [crate.URI],
            TriggerMessage1 = 7,
        });
        var start = AddInstance<AiPosition>(6, asset => new AiPositionData(asset) { Coords = new Vector3(1, 0, 1), Radius = 2 });
        var end = AddInstance<AiPosition>(6, asset => new AiPositionData(asset) { Coords = new Vector3(5, 0, 5), Radius = 1 });
        var way = AddInstance<AiPath>(6, asset => new AiPathData(asset) { PathBegin = start.URI, PathEnd = end.URI });
        var surface = new CollisionSurface { Chunk = "default", LayoutID = 7 };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", _nextId++);
        surface.SetData(new CollisionSurfaceData(surface));

        AssertDuplicated(spot, Duplicate(spot));
        AssertDuplicated(route, Duplicate(route));
        AssertDuplicated(crate, Duplicate(crate));
        AssertDuplicated(trigger, Duplicate(trigger));
        AssertDuplicated(start, Duplicate(start));
        AssertDuplicated(way, Duplicate(way));
        AssertDuplicated(surface, Duplicate(surface));
    }

    [Fact]
    public void EveryKindOfElementIsDuplicatedWhole()
    {
        var link = new ChunkLink();
        link.Hulls.Add(new ChunkLinkHull());
        AssertClonedWhole(link);
        AssertClonedWhole(new ChunkLinkHull());
        AssertClonedWhole(new ParticleSystemInstance { Name = "FIRE" });
        AssertClonedWhole(new ParticleSystem { Name = "FIRE" });
        AssertClonedWhole(new Vector3(1, 2, 3));
        AssertClonedWhole(new Vector4(1, 2, 3, 1));
        AssertClonedWhole(DefaultLights.Ambient());
        AssertClonedWhole(DefaultLights.Directional());
        AssertClonedWhole(DefaultLights.Point());
        AssertClonedWhole(DefaultLights.Spot());
        AssertClonedWhole(new SceneryPlacement { Matrix = new Matrix4 { Column4 = new Vector4(1, 2, 3, 1) }, Node = "01" });
    }
}
