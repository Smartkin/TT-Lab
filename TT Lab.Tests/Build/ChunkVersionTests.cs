using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Tests.Build;

// Objects that differ between chunks get a variant for each. High Seas' rat spawner got the first chunk's rats, which had no instance
// properties, and spawning them crashed the game
[Collection(ProjectCollection.Name)]
public sealed class ChunkVersionTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private GameObject AddObject(string name, UInt32 id, Action<GameObjectData> configure, string variation = "")
    {
        var gameObject = new GameObject { Variation = variation };
        _project.Add(gameObject, name, id, _project.Project.Ps2Package);
        var data = new GameObjectData(gameObject);
        configure(data);
        gameObject.SetData(data);
        gameObject.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        return gameObject;
    }

    [AvaloniaFact]
    public void ChunksGetTheirOwnVersionOfWhatObjectsReference()
    {
        var otherRat = AddObject("GLOBAL_RAT_LIGHTBROWN5", 0x54, data => data.Name = "|CavBridg|act_GLOBAL_RAT_LIGHTBROWN5");
        var ownRat = AddObject("GLOBAL_RAT_LIGHTBROWN", 0x54, data =>
        {
            data.Name = "GLOBAL_RAT_LIGHTBROWN";
            data.FloatProperties = [1, 25];
            data.IntProperties = [0, 255];
        }, "levels_ice_highseas_gpa04");
        var spawner = AddObject("GLOBAL_RAT_INTERMEDIATE19", 0x92, data =>
        {
            data.Name = "GLOBAL_RAT_INTERMEDIATE19";
            data.ObjectSlots = [otherRat.URI];
        });
        var factory = new PS2ItemFactory
        {
            GlobalPackage = _project.Project.GlobalPackagePS2,
            ChunkPath = "levels/ice/highseas/gpa04",
            ChunkVersions = new Dictionary<(Type, UInt32), LabURI> { [(typeof(GameObject), 0x54)] = ownRat.URI }
        };
        var code = factory.GenerateRM().GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var objects = code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);

        spawner.ResolveChunkResources(factory, objects);

        var rat = objects.GetItem<ITwinObject>(0x54);
        Assert.Equal("GLOBAL_RAT_LIGHTBROWN", rat.Name);
        Assert.Equal(new List<Single> { 1, 25 }, rat.FloatProperties);
        Assert.Contains((UInt16)0x54, objects.GetItem<ITwinObject>(0x92).RefObjects);
    }
}
