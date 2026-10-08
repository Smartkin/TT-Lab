using Avalonia.Headless.XUnit;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Object;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Tests.Build;

// What the game makes of an object of each type and reads with no check (the decomp's instancefactory.cpp, properties.cpp,
// charactermovement.cpp): the build refuses what it can't take and writes what every object and instance needs
[Collection(ProjectCollection.Name)]
public sealed class ObjectTypeBuildTests : IDisposable
{
    private readonly TestProject _project = new();
    private UInt32 _nextId = 0x40;

    public void Dispose() => _project.Dispose();

    private GameObject AddObject(GameObjectData values)
    {
        var gameObject = _project.Add(new GameObject(), $"Object {_nextId}", _nextId++);
        gameObject.SetData(new GameObjectData(gameObject)
        {
            Type = values.Type,
            SubType = values.SubType,
            ExitPointAmount = values.ExitPointAmount,
            CameraReactJointAmount = values.CameraReactJointAmount,
            TaggedProperties = values.TaggedProperties,
            FloatProperties = values.FloatProperties,
            IntProperties = values.IntProperties,
            ModelSlots = values.ModelSlots,
        });
        return gameObject;
    }

    private ObjectInstance AddInstance(GameObject gameObject, Func<IAsset, ObjectInstanceData> data)
    {
        var instance = _project.Add(new ObjectInstance { Chunk = "levels/test", LayoutID = 0 }, $"Instance {_nextId}", _nextId++);
        var instanceData = data(instance);
        instanceData.ObjectId = gameObject.URI;
        instance.SetData(instanceData);
        return instance;
    }

    private static ITwinInstance Export(ObjectInstance instance) => (ITwinInstance)((IAsset)instance).GetData<ObjectInstanceData>().Export(new PS2ItemFactory());

    private static string Refused(ObjectInstance instance) => Assert.Throws<InvalidOperationException>(() => Export(instance)).Message;

    private ITwinObject ExportObject(GameObject gameObject)
    {
        gameObject.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        var factory = new PS2ItemFactory { GlobalPackage = _project.Project.GlobalPackagePS2, ChunkPath = "levels/test" };
        var objects = factory.GenerateRM().GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        gameObject.ResolveChunkResources(factory, objects);
        return objects.GetItem<ITwinObject>(gameObject.ID);
    }

    // The class keeps its share of each kind and puts what's past them aside, counting each kind's difference in a byte: a grabbable's
    // instance made a graple's has a tagged value past the graple's none and 14 floats short of its 18, which went round to 242
    [AvaloniaFact]
    public void AKindPastItsShareWhileAnotherIsShortIsRefused()
    {
        var graple = AddObject(new GameObjectData(null!) { Type = ITwinObject.ObjectType.Graple, CameraReactJointAmount = 2 });
        var grabbable = AddInstance(graple, asset => new ObjectInstanceData(asset)
            { TaggedProperties = [new TaggedProperty(0)], FloatProperties = [1, 1, 0.1f, 0], IntProperties = [1, 255] });
        // The game's graples are spawned with 1 float and 2 integers, short of the class's without anything past them
        var spawned = AddInstance(graple, asset => new ObjectInstanceData(asset) { FloatProperties = [1], IntProperties = [0, 255] });

        Assert.Contains("fewer of others", Refused(grabbable));
        Export(spawned);
    }

    [AvaloniaFact]
    public void InstancesOfTypesWithoutWaypointsHaveNoPositionsOrPaths()
    {
        var crate = AddObject(new GameObjectData(null!) { Type = ITwinObject.ObjectType.Crate });
        var position = _project.Add(new Position { Chunk = "levels/test", LayoutID = 0 }, "Position", 0);
        position.SetData(new PositionData(position));
        var placed = AddInstance(crate, asset => new ObjectInstanceData(asset) { FloatProperties = [1, 50, 0], IntProperties = [0, 0], Positions = [position.URI] });

        Assert.Contains("makes no waypoints for", Refused(placed));
    }

    // Every instance of a character is a playable character the game puts into its table by the first integer, and the character code
    // reads exit points 0 to 10, joint IDs up to 28 for Crash and its model's animator with no check
    [AvaloniaFact]
    public void ACharacterIsAPlayableCharacterTheCharacterCodeCanMove()
    {
        var model = _project.Add(new OGI(), "Body", 0x1);
        var body = new OGIData(model);
        body.Joints.Add(new TwinJoint { Index = 1, ParentIndex = 0, Id = 0xFF, LocalRotation = new Vector4(0, 0, 0, 1), LocalTranslation = new Vector4(0, 1, 0, 1) });
        model.SetData(body);
        var still = _project.Add(new OGI(), "Still", 0x2);
        still.SetData(new OGIData(still));
        ObjectInstance Character(OGI ogi, Byte exitPoints, Byte jointIds, params Int32[] ints)
        {
            var character = AddObject(new GameObjectData(null!)
            {
                Type = ITwinObject.ObjectType.Character, ExitPointAmount = exitPoints, CameraReactJointAmount = jointIds, ModelSlots = [new ModelSlot { Ogi = ogi.URI }],
            });
            return AddInstance(character, asset => new ObjectInstanceData(asset)
            {
                TaggedProperties = [..ObjectTypes.CrashTaggedValues.Select(bits => new TaggedProperty(bits))], FloatProperties = [..ObjectTypes.CrashFloats], IntProperties = [..ints],
            });
        }

        Export(Character(model, 11, 29, 0, 255, 2));
        Assert.Contains("isn't a playable character", Refused(Character(model, 11, 29, 7, 255, 2)));
        Assert.Contains("reads 11 exit points and 29 joint IDs", Refused(Character(model, 6, 29, 0, 255, 2)));
        // The character none has no look or procedural joints, the Mecha-Bandicoot's probes reach exit point 16
        Export(Character(model, 11, 0, ObjectTypes.CharacterNone, 255, 2));
        Assert.Contains("reads 17 exit points", Refused(Character(model, 11, 33, ObjectTypes.CharacterMecha, 255, 2)));
        Assert.Contains("doesn't animate", Refused(Character(still, 11, 29, 0, 255, 2)));
    }

    [AvaloniaFact]
    public void ObjectsTheGameCantMakeAreRefused()
    {
        var customCrate = AddObject(new GameObjectData(null!) { Type = ITwinObject.ObjectType.Crate, SubType = ObjectTypes.CustomPickupSubType });
        Assert.Contains("only pickups can have", Assert.Throws<InvalidOperationException>(() => ExportObject(customCrate)).Message);

        var rope = AddObject(new GameObjectData(null!) { Type = ITwinObject.ObjectType.Graple, CameraReactJointAmount = 1 });
        Assert.Contains("joints 0 and 1", Assert.Throws<InvalidOperationException>(() => ExportObject(rope)).Message);

        // The template values instances its scripts spawn get
        var creature = AddObject(new GameObjectData(null!) { Type = ITwinObject.ObjectType.Creature, FloatProperties = [..Enumerable.Repeat(1.0f, 8)], IntProperties = [0, 255] });
        Assert.Contains("template values", Assert.Throws<InvalidOperationException>(() => ExportObject(creature)).Message);
    }

    // Every instance reads its object's first model slot with no check
    [AvaloniaFact]
    public void AnObjectWithoutModelSlotsGetsOneOfNone()
    {
        var empty = AddObject(new GameObjectData(null!) { Type = ITwinObject.ObjectType.GenericObject });

        var written = ExportObject(empty);

        Assert.Equal([(UInt16)0xFFFF], written.OGISlots);
        Assert.Equal([ModelSlot.NoAnimation], written.AnimationSlots);
    }

    // The game reads every instance's second integer with no check: an instance made in TT Lab without values gets its object's when
    // it's built, the game's instances stay as they are (some have no float, all have two integers)
    [AvaloniaFact]
    public void AnInstanceWithoutANearDistanceGetsItsObjectsValues()
    {
        var prop = AddObject(new GameObjectData(null!) { Type = ITwinObject.ObjectType.GenericObject, FloatProperties = [2.0f], IntProperties = [0, 255] });
        var bare = AddInstance(prop, asset => new ObjectInstanceData(asset));
        var retail = AddInstance(prop, asset => new ObjectInstanceData(asset) { IntProperties = [3, 0] });

        var written = Export(bare);
        Assert.Equal([2.0f], written.FloatProperties);
        Assert.Equal([0, 255], written.IntProperties);
        Assert.Empty(((IAsset)bare).GetData<ObjectInstanceData>().IntProperties);
        var kept = Export(retail);
        Assert.Empty(kept.FloatProperties);
        Assert.Equal([3, 0], kept.IntProperties);
    }

    // A new object is a generic object an instance can be placed of right away: a model slot and the type's values, which its instances
    // start with
    [AvaloniaFact]
    public void ANewObjectCanBePlacedRightAway()
    {
        var gameObject = _project.Add(new GameObject(), "Mine", 0x60);
        AssetDataFactory.CreateGameObjectData(gameObject);
        var data = ((IAsset)gameObject).GetData<GameObjectData>();

        Assert.Equal(ITwinObject.ObjectType.GenericObject, data.Type);
        Assert.Equal(ObjectTypes.PlainSubType, data.SubType);
        Assert.Single(data.ModelSlots);
        Assert.Equal([1.0f], data.FloatProperties);
        Assert.Equal([0, 255], data.IntProperties);
        Assert.Equal((Enums.InstanceState)0x7D36, data.InstanceStateFlags);
        var instance = AddInstance(gameObject, asset =>
        {
            var instanceData = new ObjectInstanceData(asset);
            instanceData.TakeValuesOf(data);
            return instanceData;
        });
        var written = Export(instance);
        Assert.Equal([0, 255], written.IntProperties);
        Assert.Equal((Enums.InstanceState)0x7D36, written.StateFlags);
        Assert.Single(ExportObject(gameObject).OGISlots);
    }
}
