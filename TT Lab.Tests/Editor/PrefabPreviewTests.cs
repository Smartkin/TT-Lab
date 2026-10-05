using GlmSharp;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Project.Prefabs;
using TT_Lab.Tests.Support;
using PrefabPreview = TT_Lab.Rendering.Objects.PrefabPreview;
using Vector3 = Twinsanity.TwinsanityInterchange.Common.Vector3;

namespace TT_Lab.Tests.Editor;

// A prefab dragged over a scene is drawn where letting it go puts it: its object instances with their objects' models, turned like
// them and where they stand from the first, a marker for the rest. Drawing it needs GL, what goes where doesn't
[Collection(ProjectCollection.Name)]
public sealed class PrefabPreviewTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private GameObject AddObject(string name, params LabURI[] models)
    {
        var gameObject = _project.Add(new GameObject(), name);
        gameObject.SetData(new GameObjectData(gameObject) { Name = name, ModelSlots = models.Select(model => new ModelSlot { Ogi = model }).ToList() });
        return gameObject;
    }

    private static JObject InstanceData(LabURI objectUri, Vector3 rotation) => new()
    {
        [nameof(ObjectInstanceData.ObjectId)] = JObject.FromObject(objectUri),
        [nameof(ObjectInstanceData.Rotation)] = JObject.FromObject(rotation),
    };

    private Prefab Prefab(PrefabKind kind, string assetType, JObject data, List<PrefabItem>? items = null) => new()
    {
        Name = "Preview", Kind = kind, Platform = "PS2", Package = _project.Project.GlobalPackagePS2.URI, LayoutID = 0, AssetType = assetType,
        Data = data, Items = items
    };

    private static void AssertTurnsXTo(vec3 expected, quat rotation)
    {
        var turned = rotation * vec3.UnitX;
        Assert.Equal(expected.x, turned.x, 4);
        Assert.Equal(expected.y, turned.y, 4);
        Assert.Equal(expected.z, turned.z, 4);
    }

    [Fact]
    public void AnObjectInstanceIsShownWithItsObjectsModelTurnedLikeIt()
    {
        var model = _project.Add(new OGI(), "WumpaModel");
        var wumpa = AddObject("WUMPA", LabURI.Empty, model.URI);

        var part = Assert.Single(PrefabPreview.PartsOf(Prefab(PrefabKind.Instance, typeof(ObjectInstance).FullName!, InstanceData(wumpa.URI, new Vector3(0, 90, 0)))));

        Assert.Equal(model.URI, part.Model);
        Assert.Equal(vec3.Zero, part.Offset);
        AssertTurnsXTo(new vec3(0, 0, -1), part.Rotation);
    }

    // The objects without a model are drawn as the scene's box, the ones the project doesn't have and what isn't an object instance are
    // markers where they go
    [Fact]
    public void AGroupsInstancesGoWhereTheyStoodFromTheFirst()
    {
        var crabModel = _project.Add(new OGI(), "CrabModel");
        var crab = AddObject("CRAB", crabModel.URI);
        var sound = AddObject("IMPACT_SOUND");
        var objectInstance = typeof(ObjectInstance).FullName!;
        List<PrefabItem> items =
        [
            new() { AssetType = objectInstance, LayoutID = 0, Offset = [0, 0, 0], Data = InstanceData(crab.URI, new Vector3(0, 0, 0)) },
            new() { AssetType = objectInstance, LayoutID = 0, Offset = [2, 0, 1], Data = InstanceData(sound.URI, new Vector3(0, 0, 0)) },
            new() { AssetType = objectInstance, LayoutID = 0, Offset = [0, 3, 0], Data = InstanceData(new LabURI("res://Global PS2_Test/GameObject/Gone"), new Vector3(0, 0, 0)) },
            new() { AssetType = typeof(Trigger).FullName!, LayoutID = 0, Offset = [-1, 0, -4], Data = new JObject() },
        ];

        var parts = PrefabPreview.PartsOf(Prefab(PrefabKind.Group, string.Empty, new JObject(), items));

        Assert.Equal([crabModel.URI, LabURI.Empty, null, null], parts.Select(part => part.Model));
        Assert.Equal([vec3.Zero, new vec3(2, 0, 1), new vec3(0, 3, 0), new vec3(-1, 0, -4)], parts.Select(part => part.Offset));
    }

    [Fact]
    public void APartOfAResourceIsAMarkerOnTheDropPoint()
    {
        var part = Assert.Single(PrefabPreview.PartsOf(Prefab(PrefabKind.Element, typeof(ChunkLinks).FullName!, new JObject())));

        Assert.Null(part.Model);
        Assert.Equal(vec3.Zero, part.Offset);
    }
}
