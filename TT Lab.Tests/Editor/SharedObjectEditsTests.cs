using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// An object every instance of it shares, changed anywhere, is what an open chunk's instances show: changed through one instance's link the
// others show it too, changed in its own editor the chunks' versions of it take it where they have no value of their own
[Collection(ProjectCollection.Name)]
public sealed class SharedObjectEditsTests : IDisposable
{
    private const string Slot = "[data].AssetData.ObjectId[data].AssetData.ModelSlots[0].Ogi";

    private readonly TestProject _project = new();
    private readonly Package _package;
    private readonly GameObject _box;
    private readonly OGI _plain;
    private readonly OGI _fancy;

    public SharedObjectEditsTests()
    {
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "levels"), Path.Combine(_package.Name, "Graphics"));
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);

        _plain = AddOgi("Plain box");
        _fancy = AddOgi("Fancy box");
        _box = _project.Add(new GameObject(), "Box", 0x5);
        var box = new GameObjectData(_box) { Name = "Box" };
        box.ModelSlots.Add(new ModelSlot { Ogi = _plain.URI });
        box.FloatProperties.Add(1.5f);
        _box.SetData(box);
        _box.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
    }

    public void Dispose() => _project.Dispose();

    private OGI AddOgi(string name)
    {
        var ogi = _project.Add(new OGI(), name);
        ogi.SetData(new OGIData(ogi)
        {
            Joints = [new TwinJoint { Index = 0, ParentIndex = 255, LocalRotation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) }],
        });
        return ogi;
    }

    // A new chunk with two instances of the box after the ones every new chunk has
    private LevelChunk CreateChunk(string name)
    {
        var folder = _project.GetFolder(_package, "levels");
        var chunk = (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), folder, name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(), asset => AssetDataFactory.CreateChunkData(folder, asset))!;
        for (var i = 0; i < 2; i++)
        {
            var instance = _project.Add(new ObjectInstance { Chunk = chunk.AdditionalPath!, AdditionalPath = chunk.AdditionalPath, LayoutID = 0 }, $"{name} box {i}", (uint)(0x10 + i));
            instance.SetData(new ObjectInstanceData(instance) { ObjectId = _box.URI });
            chunk.ChunkResources.Add(instance.URI);
        }

        return chunk;
    }

    private static DocumentViewModel Open(IAsset asset)
    {
        var document = new DocumentViewModel(asset);
        document.Initialize();
        return document;
    }

    private static List<PropertyNode> Boxes(DocumentViewModel document) => document.PropertyGraph.Find("Root.ChunkResources")!.Children
        .Where(resource => resource.Find("[data]")!.Target is ObjectInstance instance && ((IAsset)instance).GetData<ObjectInstanceData>().ObjectId != LabURI.Empty &&
                           AssetManager.Get().GetAsset(((IAsset)instance).GetData<ObjectInstanceData>().ObjectId).Name == "Box")
        .ToList();

    // The model the scene draws the instance with
    private static LabURI ModelOf(PropertyNode resource) => ((IAsset)resource.Find("[data]")!.Target).GetData<ObjectInstanceData>().ModelOf(resource);

    // What a link under the instance's object leads to, the asset a chunk's view is of
    private static IAsset LinkedBy(PropertyNode link)
    {
        var target = (IAsset)link.Find("[data]")!.Target;
        return target is SerializableAsset { OverriddenAsset: { } asset } ? asset : target;
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void AModelPickedInTheObjectsOwnEditorIsTheModelOfItsInstancesInTheChunk()
    {
        var chunk = CreateChunk("hub");
        var scene = Open(chunk);
        var followed = new List<IAsset>();
        scene.PropertyGraph.Followed += followed.Add;
        var editor = Open(_box);

        editor.PropertyGraph.Find("Root.AssetData.ModelSlots[0].Ogi")!.SetValue(_fancy.URI);
        Pump();

        var boxes = Boxes(scene);
        Assert.Equal(2, boxes.Count);
        foreach (var box in boxes)
        {
            Assert.Equal(_fancy.URI, box.Find(Slot)!.GetValue());
            Assert.Same(_fancy, LinkedBy(box.Find(Slot)!));
            Assert.Equal(_fancy.URI, ModelOf(box));
        }

        // What's drawn of the object's instances gets told once, by the chunk's version of the object
        var view = Assert.IsAssignableFrom<SerializableAsset>(Assert.Single(followed));
        Assert.Same(_box, view.OverriddenAsset);
        Assert.True(new ViewportObject(null!, "box", boxes[1]) { RenderDependencies = [boxes[1].Find("[data].AssetData.ObjectId")!] }.Links(view.URI));
        // The chunk took the object as it is, it has nothing of its own to save or undo
        Assert.False(scene.IsDirty);
        Assert.False(scene.History.CanUndo);
        scene.Save();
        Assert.Empty(chunk.Overrides);
    }

    [AvaloniaFact]
    public void TheChunksOwnValuesStayWhenTheObjectChanges()
    {
        var chunk = CreateChunk("hub");
        var hub = Open(chunk);
        var other = Open(CreateChunk("beach"));
        Boxes(hub)[0].Find("[data].AssetData.ObjectId[data].AssetData.FloatProperties[0]")!.SetValue(2.5f);
        Boxes(hub)[0].Find(Slot)!.SetValue(_fancy.URI);
        var editor = Open(_box);
        var third = AddOgi("Third box");

        editor.PropertyGraph.Find("Root.AssetData.ModelSlots[0].Ogi")!.SetValue(third.URI);
        editor.PropertyGraph.Find("Root.AssetData.FloatProperties")!.AddElement()!.SetValue(4.0f);
        Pump();

        // The hub's own model and number stay, what it didn't change is the object's
        Assert.All(Boxes(hub), box => Assert.Same(_fancy, LinkedBy(box.Find(Slot)!)));
        Assert.Equal([2.5f], ((IAsset)Boxes(hub)[1].Find("[data].AssetData.ObjectId[data]")!.Target).GetData<GameObjectData>().FloatProperties);
        Assert.All(Boxes(other), box => Assert.Equal(third.URI, ModelOf(box)));
        Assert.Equal([1.5f, 4.0f], ((IAsset)Boxes(other)[0].Find("[data].AssetData.ObjectId[data]")!.Target).GetData<GameObjectData>().FloatProperties);
        // Its list's nodes follow the list
        Assert.Equal(2, Boxes(other)[1].Find("[data].AssetData.ObjectId[data].AssetData.FloatProperties")!.Children.Count);

        hub.Save();
        var @override = Assert.Single(chunk.Overrides);
        Assert.Equal(["AssetData.FloatProperties", "AssetData.ModelSlots[0].Ogi"], @override.Values.Keys);
        Assert.Equal(_fancy.URI.ToString(), (string)@override.Values["AssetData.ModelSlots[0].Ogi"]["_uri"]!);
    }

    [AvaloniaFact]
    public void AModelPickedThroughOneInstanceIsTheModelOfTheOthers()
    {
        var scene = Open(CreateChunk("hub"));
        var (first, second) = (Boxes(scene)[0], Boxes(scene)[1]);

        first.Find(Slot)!.SetValue(_fancy.URI);

        // The object is under the link of each instance, the other's leads to the new model as well
        Assert.Same(_fancy, LinkedBy(second.Find(Slot)!));
        Assert.Equal(_fancy.URI, ModelOf(second));
        var changedAsset = DocumentViewModel.GetOwningAsset(first.Find(Slot))!.URI;
        Assert.True(new ViewportObject(null!, "second", second) { RenderDependencies = [second.Find("[data].AssetData.ObjectId")!] }.Links(changedAsset));

        scene.Undo();
        Assert.Same(_plain, LinkedBy(second.Find(Slot)!));
        Assert.Equal(_plain.URI, ModelOf(second));

        // Lists taken from under one link have as many nodes under the others
        scene.Redo();
        var slots = first.Find("[data].AssetData.ObjectId[data].AssetData.ModelSlots")!;
        slots.RemoveElement(slots.Children[0]);
        Assert.Empty(second.Find("[data].AssetData.ObjectId[data].AssetData.ModelSlots")!.Children);
        Assert.Equal(LabURI.Empty, ModelOf(second));
        scene.Undo();
        Assert.Same(_fancy, LinkedBy(second.Find(Slot)!));
    }

    [AvaloniaFact]
    public void DiscardingTheObjectsChangesPutsTheChunksVersionBack()
    {
        var scene = Open(CreateChunk("hub"));
        var editor = Open(_box);
        editor.PropertyGraph.Find("Root.AssetData.ModelSlots[0].Ogi")!.SetValue(_fancy.URI);
        Pump();
        Assert.All(Boxes(scene), box => Assert.Equal(_fancy.URI, ModelOf(box)));

        // What closing its editor without saving does: its data read again from its file
        ((IAsset)_box).UnloadData();
        DocumentViewModel.TellChanged(_box);
        Pump();

        Assert.All(Boxes(scene), box => Assert.Same(_plain, LinkedBy(box.Find(Slot)!)));
        Assert.False(scene.IsDirty);
    }

    [AvaloniaFact]
    public void ValuesSharedOrPutBackFollowTheObjectAgain()
    {
        var hub = Open(CreateChunk("hub"));
        var beach = Open(CreateChunk("beach"));
        var hubFloat = Boxes(hub)[0].Find("[data].AssetData.ObjectId[data].AssetData.FloatProperties[0]")!;
        hubFloat.SetValue(2.5f);
        var editor = EditorDescRegistry.GetDesc(hub, hubFloat).Construct();
        new Window { Content = new ContentControl { Content = editor }, Width = 600, Height = 300 }.Show();
        Pump();

        // Shared with the object, the other chunk takes it
        editor.ApplyOverrideToAsset();
        Pump();
        Assert.Equal([2.5f], ((IAsset)Boxes(beach)[0].Find("[data].AssetData.ObjectId[data]")!.Target).GetData<GameObjectData>().FloatProperties);

        // Changed in the object's editor after, both chunks take the change: neither has a value of its own there any more
        var objectEditor = Open(_box);
        var objectFloat = objectEditor.PropertyGraph.Find("Root.AssetData.FloatProperties[0]")!;
        objectFloat.SetValue(3.0f);
        Pump();
        Assert.Equal(3.0f, hubFloat.GetValue());
        Assert.Equal(3.0f, Boxes(beach)[1].Find("[data].AssetData.ObjectId[data].AssetData.FloatProperties[0]")!.GetValue());

        // The chunk's own value stays while the object changes, put back it's the object's and follows it again
        hubFloat.SetValue(7.0f);
        objectFloat.SetValue(5.0f);
        Pump();
        Assert.Equal(7.0f, hubFloat.GetValue());
        editor.RevertOverride();
        Assert.Equal(5.0f, hubFloat.GetValue());
        objectFloat.SetValue(8.0f);
        Pump();
        Assert.Equal(8.0f, hubFloat.GetValue());
    }

    [AvaloniaFact]
    public void ClosedChunksDontFollowTheirObjects()
    {
        var scene = Open(CreateChunk("hub"));
        var editor = Open(_box);
        scene.Close();

        editor.PropertyGraph.Find("Root.AssetData.ModelSlots[0].Ogi")!.SetValue(_fancy.URI);
        Pump();

        Assert.All(Boxes(scene), box => Assert.Same(_plain, LinkedBy(box.Find(Slot)!)));
    }

    // In place, the chunk's nodes keep showing the view's objects, at the paths its JSON has them: a dictionary's key isn't a name
    [AvaloniaFact]
    public void ValuesTakenFromTheAssetAreSetWhereItsJsonHasThem()
    {
        var view = AssetOverrides.CreateView(_box, new Dictionary<string, Newtonsoft.Json.Linq.JToken>());
        var slot = ((GameObjectData)view.GetData()).ModelSlots[0];
        _box.Parameters["new key"] = 3L;
        ((IAsset)_box).GetData<GameObjectData>().ModelSlots[0].Ogi = _fancy.URI;
        var values = AssetOverrides.Diff(AssetOverrides.GetDocument(view, view.GetData()), AssetOverrides.GetDocument(_box, ((IAsset)_box).GetData<AbstractAssetData>()));

        var set = AssetOverrides.Take(view, values.ToList());

        Assert.Equal(["AssetData.ModelSlots[0].Ogi", "Parameters['new key']"], set.Order());
        Assert.Same(slot, ((GameObjectData)view.GetData()).ModelSlots[0]);
        Assert.Equal(_fancy.URI, slot.Ogi);
        Assert.Equal(3L, view.Parameters["new key"]);
    }

    [AvaloniaFact]
    public void AKeyTheObjectGetsIsFollowedFromThenOn()
    {
        var scene = Open(CreateChunk("hub"));
        var view = (SerializableAsset)Boxes(scene)[0].Find("[data].AssetData.ObjectId[data]")!.Target;

        _box.Parameters["new key"] = 3L;
        DocumentViewModel.TellChanged(_box);
        Pump();
        Assert.Equal(3L, view.Parameters["new key"]);

        _box.Parameters["new key"] = 4L;
        DocumentViewModel.TellChanged(_box);
        Pump();
        Assert.Equal(4L, view.Parameters["new key"]);
        Assert.False(scene.PropertyGraph.Overrides!.HasOwnValues(view));
    }

    [AvaloniaFact]
    public void PathsInTheJsonAreTheNodesSegments()
    {
        Assert.Equal([".AssetData", ".ModelSlots", "[0]", ".Ogi"], AssetOverrides.Segments("AssetData.ModelSlots[0].Ogi"));
        Assert.Equal([".Parameters", ".some key", "[12]"], AssetOverrides.Segments("Parameters['some key'][12]"));
        Assert.Equal([".AssetData", ".Name"], AssetOverrides.Segments(".AssetData.Name"));
    }

    [AvaloniaFact]
    public void AnInstanceWithoutAnObjectIsABox()
    {
        var chunk = CreateChunk("hub");
        ((IAsset)AssetManager.Get().GetAsset(chunk.ChunkResources[^1])).GetData<ObjectInstanceData>().ObjectId = LabURI.Empty;
        var scene = Open(chunk);

        Assert.Equal(LabURI.Empty, ModelOf(scene.PropertyGraph.Find("Root.ChunkResources")!.Children[^1]));
    }
}
