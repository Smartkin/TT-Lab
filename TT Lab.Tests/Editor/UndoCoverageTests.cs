using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.Tests.Editor;

// Every kind of value the inspector edits goes back and forth in its document's history: flags, enums, links, matrices, points of
// nested game structs and elements of lists
[Collection(ProjectCollection.Name)]
public sealed class UndoCoverageTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public UndoCoverageTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    private static DocumentViewModel Open(IAsset asset)
    {
        var document = new DocumentViewModel(asset);
        document.Initialize();
        return document;
    }

    private static IEnumerable<string> Steps(UndoHistory.Entry entry) => new[] { entry.Description }.Concat(entry.Children.SelectMany(Steps));

    private static void Change(DocumentViewModel document, string path, object value)
    {
        using (document.History.BeginGroup())
        {
            document.PropertyGraph.Find(path)!.SetValue(value);
        }
    }

    [AvaloniaFact]
    public void FlagsAndLinksOfAnInstanceGoBack()
    {
        var instance = _project.Add(new ObjectInstance { Chunk = "levels/test", LayoutID = 0 }, "Instance");
        var data = new ObjectInstanceData(instance) { StateFlags = Enums.InstanceState.Visible };
        instance.SetData(data);
        var graph = _project.Add(new BehaviourGraph(), "COM_TEST", 5, _project.Project.Ps2Package);
        graph.SetData(new TT_Lab.AssetData.Code.Behaviour.BehaviourGraphData(graph) { Graph = "graph COM_TEST { state Start { } }" });
        var document = Open(instance);

        Change(document, "Root.AssetData.StateFlags.SnapToGround", true);
        Change(document, "Root.AssetData.StateFlags.Visible", false);
        Change(document, "Root.AssetData.SpawnScript", graph.URI);
        Assert.Equal(Enums.InstanceState.SnapToGround, data.StateFlags);
        Assert.Equal(graph.URI, data.SpawnScript);

        document.Undo();
        Assert.Equal(LabURI.Empty, data.SpawnScript);
        document.Undo();
        Assert.Equal(Enums.InstanceState.Visible | Enums.InstanceState.SnapToGround, data.StateFlags);
        document.Undo();
        Assert.Equal(Enums.InstanceState.Visible, data.StateFlags);
        Assert.False(document.CanUndo);
        document.Redo();
        document.Redo();
        document.Redo();
        Assert.Equal(graph.URI, data.SpawnScript);
        Assert.Equal(Enums.InstanceState.SnapToGround, data.StateFlags);
    }

    [AvaloniaFact]
    public void EnumsAndNamedValuesOfASurfaceGoBack()
    {
        var surface = _project.Add(new CollisionSurface { Chunk = "default", LayoutID = ChunkLayouts.CollisionSurfaces }, "Ice");
        var data = new CollisionSurfaceData(surface) { SurfaceID = Enums.SurfaceType.SURF_ICE, Friction = 0.05f };
        surface.SetData(data);
        var document = Open(surface);

        Change(document, "Root.AssetData.SurfaceID", Enums.SurfaceType.SURF_LAVA);
        Change(document, "Root.AssetData.Friction", 0.7f);
        Change(document, "Root.AssetData.CollisionMask.Sticky", true);

        document.Undo();
        Assert.False(data.CollisionMask.HasFlag(Enums.SurfaceCollisionFlags.Sticky));
        Assert.Equal(0.7f, data.Friction);
        document.Undo();
        Assert.True(0.05f == data.Friction, string.Join(" | ", Steps(document.History.Root)));
        Assert.Equal(Enums.SurfaceType.SURF_LAVA, data.SurfaceID);
        document.Undo();
        Assert.Equal(Enums.SurfaceType.SURF_ICE, data.SurfaceID);
        document.Redo();
        document.Redo();
        Assert.Equal(0.7f, data.PhysicsParameters[Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout.SurfacePhysics.Friction]);
    }

    // A camera's subtypes are game structs edited in place: their points, matrices and list elements
    [AvaloniaFact]
    public void PointsMatricesAndSamplesOfACameraGoBack()
    {
        var camera = _project.Add(new Camera { Chunk = "default", LayoutID = 4 }, "Camera");
        var zone = new CameraZone();
        var spline = new CameraSpline { PathPoints = [new Vector4(0, 0, 0, 0), new Vector4(1, 0, 0, 0)], Tangents = [new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1)], ArcLengths = [1], InverseSteps = [0.2f] };
        camera.SetData(new CameraData(camera) { MainCamera1 = zone, MainCamera2 = spline });
        var document = Open(camera);

        Change(document, "Root.AssetData.MainCamera2.PathPoints[1]", new Vector4(5, 0, 0, 0));
        var moved = document.PropertyGraph.Find("Root.AssetData.MainCamera1.CameraBoxTransform")!;
        var box = new Matrix4 { Column1 = new Vector4(2, 0, 0, 0), Column2 = new Vector4(0, 2, 0, 0), Column3 = new Vector4(0, 0, 2, 0), Column4 = new Vector4(9, 9, 9, 1) };
        Change(document, "Root.AssetData.MainCamera1.CameraBoxTransform", box);
        using (document.History.BeginGroup())
        {
            document.PropertyGraph.Find("Root.AssetData.MainCamera2.PathPoints")!.AddElement();
        }

        Assert.Equal(3, spline.PathPoints.Count);
        Assert.Equal(9.0f, zone.CameraBox[CameraZone.Origin].X);
        Assert.Equal(5.0f, spline.PathPoints[1].X);

        document.Undo();
        Assert.Equal(2, spline.PathPoints.Count);
        document.Undo();
        Assert.Equal(0.0f, zone.CameraBox[CameraZone.Origin].X);
        Assert.Equal(1.0f, zone.CameraBox[CameraZone.Size].X);
        document.Undo();
        Assert.Equal(1.0f, spline.PathPoints[1].X);
        Assert.False(document.CanUndo);

        document.Redo();
        document.Redo();
        Assert.Equal(2.0f, zone.CameraBox[CameraZone.Size].X);
        Assert.True(document.IsDirty);
    }

    // A material's shader animation is a game struct the inspector constructs and fills
    [AvaloniaFact]
    public void ConstructedStructsGoBack()
    {
        var material = _assets.AddMaterial("Water");
        var data = ((IAsset)material).GetData<MaterialData>();
        var document = Open(material);
        var animation = document.PropertyGraph.Find("Root.AssetData.Shaders[0].Animation")!;

        using (document.History.BeginGroup())
        {
            animation.SetValue(new Twinsanity.TwinsanityInterchange.Common.ShaderAnimation.TwinShaderAnimation { TotalFrames = 3 });
        }

        Assert.NotNull(data.Shaders[0].Animation);
        Assert.Equal(3, data.Shaders[0].Animation!.TotalFrames);
        document.Undo();
        Assert.Null(data.Shaders[0].Animation);
        document.Redo();
        Assert.Equal(3, data.Shaders[0].Animation!.TotalFrames);
    }

    private static (T Editor, Avalonia.Controls.Window Window) Show<T>(DocumentViewModel document, string path) where T : DocumentNodeViewModel
    {
        var editor = Assert.IsType<T>(TT_Lab.ViewModels.Editors.Descs.EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct());
        var window = new Avalonia.Controls.Window { Content = new Avalonia.Controls.ContentControl { Content = editor }, Width = 500, Height = 600 };
        window.Show();
        Pump();
        return (editor, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }

    private static IEnumerable<string> Names(DocumentCompositeViewModel editor) => editor.Nodes.Select(node => node.Property.Name).Order();

    // The editor shows its node's children, the ones of the subtype the camera has
    private static void AssertShows(DocumentCompositeViewModel editor, string has, string hasNot)
    {
        Assert.Equal(editor.Property.Children.Select(child => child.Name).Order(), Names(editor));
        Assert.Contains(has, Names(editor));
        Assert.DoesNotContain(hasNot, Names(editor));
    }

    // Picking a camera's subtype and taking it back with undo showed the old subtype's values under a camera of none
    [AvaloniaFact]
    public void SubtypesPickedAndTakenBackShowTheirOwnValues()
    {
        var camera = _project.Add(new Camera { Chunk = "default", LayoutID = 4 }, "Camera");
        var data = new CameraData(camera);
        camera.SetData(data);
        var document = Open(camera);
        var (editor, window) = Show<DocumentModelViewModel>(document, "Root.AssetData.MainCamera1");
        editor.IsExpanded = true;
        Pump();
        Assert.Empty(editor.Nodes);

        editor.ConstructType(typeof(CameraPoint));
        Pump();
        var point = Assert.IsType<CameraPoint>(data.MainCamera1);
        Assert.Equal("CameraPoint", editor.TypeConstructed);
        AssertShows(editor, "Point", "LineStart");

        document.Undo();
        Pump();
        Assert.Null(data.MainCamera1);
        Assert.Equal("null", editor.TypeConstructed);
        Assert.Empty(document.PropertyGraph.Find("Root.AssetData.MainCamera1")!.Children);
        Assert.Empty(editor.Nodes);

        document.Redo();
        Pump();
        Assert.Same(point, data.MainCamera1);
        AssertShows(editor, "Point", "LineStart");

        // From one subtype to another and back, the values shown are the ones the camera has
        editor.ConstructType(typeof(CameraLine));
        Pump();
        Assert.IsType<CameraLine>(data.MainCamera1);
        AssertShows(editor, "LineStart", "Point");
        Change(document, "Root.AssetData.MainCamera1.LineStart", new Vector4(1, 2, 3, 0));
        document.Undo();
        document.Undo();
        Pump();
        Assert.Same(point, data.MainCamera1);
        AssertShows(editor, "Point", "LineStart");
        // The nodes read and write the value the camera has now
        Change(document, "Root.AssetData.MainCamera1.Point", new Vector4(7, 0, 0, 0));
        Assert.Equal(7.0f, point.Point.X);
        window.Close();
    }

    // Linked fields follow the edited value, the edit is one step and undoing it puts them all back: one undo of a trigger's header only
    // took back the last of its linked fields
    [AvaloniaFact]
    public void LinkedFieldsFollowTheirValueBackAndForth()
    {
        var trigger = _project.Add(new Trigger { Chunk = "levels/test", LayoutID = 0 }, "Trigger");
        var data = new TriggerData(trigger) { Header = 0x832, Kind = 0x32, TriggerArgument1Enabled = true };
        trigger.SetData(data);
        var document = Open(trigger);
        Assert.Equal((0x32, true), (data.Kind, data.TriggerArgument1Enabled));

        document.PropertyGraph.Find("Root.AssetData.Header")!.SetValue(1u);
        Assert.Equal((1u, (byte)1, false), (data.Header, data.Kind, data.TriggerArgument1Enabled));
        Assert.Same(document.History.Root, document.History.Current.Parent);

        document.Undo();
        Assert.Equal((0x832u, (byte)0x32, true), (data.Header, data.Kind, data.TriggerArgument1Enabled));
        Assert.False(document.CanUndo);
        document.Redo();
        Assert.Equal((1u, (byte)1, false), (data.Header, data.Kind, data.TriggerArgument1Enabled));

        // And the other way round
        document.Undo();
        document.PropertyGraph.Find("Root.AssetData.Kind")!.SetValue((byte)7);
        Assert.Equal(0x807u, data.Header);
        document.Undo();
        Assert.Equal((0x832u, (byte)0x32), (data.Header, data.Kind));

        // New triggers are the game's kind of trigger, not the sound code's plain boxes, and their fields agree with their header
        var made = new TriggerData(trigger);
        Assert.Equal((0x32u, (byte)0x32, 0.3f), (made.Header, made.Kind, made.CheckInterval));
    }

    // Linked fields of nodes made after the document opened follow each other too: a trigger placed in a chunk's document, or put back
    // by undo, had fields that followed nothing
    [AvaloniaFact]
    public void LinkedFieldsOfResourcesPlacedLaterFollowEachOther()
    {
        var chunk = _project.Add(new LevelChunk { AdditionalPath = "levels/test" }, "test");
        var document = Open(chunk);
        var trigger = _project.Add(new Trigger { Chunk = "levels/test", LayoutID = 0 }, "Trigger");
        trigger.SetData(new TriggerData(trigger));
        var element = document.PropertyGraph.Find("Root.ChunkResources")!.AddElement()!;
        element.SetValue(trigger.URI);
        var data = ((IAsset)trigger).GetData<TriggerData>();

        document.PropertyGraph.Find("Root.ChunkResources[0][data].AssetData.Kind")!.SetValue((byte)9);
        Assert.Equal(9u, data.Header);
        document.PropertyGraph.Find("Root.ChunkResources[0][data].AssetData.TriggerArgument1Enabled")!.SetValue(true);
        Assert.Equal(0x809u, data.Header);
    }

    // A camera keeps its trigger in its own file, the trigger's fields shown of its header were never worked out from it and opening the
    // camera wrote them over the header: the kind and message bits of every camera opened and saved were gone
    [AvaloniaFact]
    public void OpeningAndCopyingKeepsTriggerHeaders()
    {
        var camera = _project.Add(new Camera { Chunk = "levels/test", LayoutID = 4 }, "Camera");
        var original = new CameraData(camera);
        original.Trigger.SetHeader(0x140846);
        var data = (CameraData)original.CopyFor(camera);
        camera.SetData(data);
        Assert.Equal(((byte)0x46, true), (data.Trigger.Kind, data.Trigger.TriggerArgument1Enabled));
        Open(camera);
        Assert.Equal(0x140846u, data.Trigger.Header);

        // Copies, like duplicated instances, are made the same way
        var trigger = _project.Add(new Trigger { Chunk = "levels/test", LayoutID = 0 }, "Trigger");
        var triggerData = new TriggerData(trigger);
        triggerData.SetHeader(0x1832);
        var copy = (TriggerData)triggerData.CopyFor(trigger);
        Assert.Equal(((byte)0x32, true, true), (copy.Kind, copy.TriggerArgument1Enabled, copy.NotPolled));
        // New cameras have what most of the game's have
        Assert.Equal((0x140000u, 0.0f), (new CameraData(camera).Trigger.Header, new CameraData(camera).Trigger.CheckInterval));
    }

    // A link's object matrix follows its chunk matrix when that changes, opening the links recomputed it and every link saved again got
    // other floats than the game's
    [AvaloniaFact]
    public void OpeningLinksKeepsTheirMatrices()
    {
        var links = _project.Add(new ChunkLinks { Chunk = "levels/test" }, "Links");
        var objectMatrix = new Matrix4 { Column1 = new Vector4(1, 0, 0, 0), Column2 = new Vector4(0, 0.99999976f, 0, 0), Column3 = new Vector4(0, 0, 1, 0), Column4 = new Vector4(0, -40.424225f, 0, 1) };
        var chunkMatrix = new Matrix4 { Column1 = new Vector4(1, 0, 0, 0), Column2 = new Vector4(0, 1, 0, 0), Column3 = new Vector4(0, 0, 1, 0), Column4 = new Vector4(0, 40.424244f, 0, 1) };
        var data = new ChunkLinksData(links) { Links = [new ChunkLink { ObjectMatrix = objectMatrix, ChunkMatrix = chunkMatrix }] };
        links.SetData(data);
        var document = Open(links);
        Assert.Same(objectMatrix, data.Links[0].ObjectMatrix);
        Assert.Equal(0.99999976f, data.Links[0].ObjectMatrix.Column2.Y);

        Change(document, "Root.AssetData.Links[0].ChunkMatrix", new Matrix4 { Column1 = new Vector4(1, 0, 0, 0), Column2 = new Vector4(0, 1, 0, 0), Column3 = new Vector4(0, 0, 1, 0), Column4 = new Vector4(2, 0, 0, 1) });
        Assert.Equal(-2.0f, data.Links[0].ObjectMatrix.Column4.X);

        // Undoing puts back the game's floats, not an inverse of the chunk matrix made again
        document.Undo();
        Assert.Same(chunkMatrix, data.Links[0].ChunkMatrix);
        Assert.Same(objectMatrix, data.Links[0].ObjectMatrix);
        document.Redo();
        Assert.Equal(-2.0f, data.Links[0].ObjectMatrix.Column4.X);

        // A link without a wall is stored without one, read back it got a zero wall every save wrote
        Assert.DoesNotContain(nameof(ChunkLink.LoadingWall), Newtonsoft.Json.JsonConvert.SerializeObject(new ChunkLink { LoadingWall = new Matrix4() }));
    }

    // Shaders keep bits of the tools' memory as NaNs, float equality took them all for the same value and undo left another NaN
    [AvaloniaFact]
    public void UndoPutsBackTheBitsOfNaNs()
    {
        var links = _project.Add(new ChunkLinks { Chunk = "levels/test" }, "Links");
        var leftover = BitConverter.Int32BitsToSingle(unchecked((int)0xFFFFFFFF));
        var data = new ChunkLinksData(links) { Links = [new ChunkLink { ObjectMatrix = new Matrix4 { Column1 = new Vector4(leftover, 0, 0, 0) } }] };
        links.SetData(data);
        var document = Open(links);
        var node = document.PropertyGraph.Find("Root.AssetData.Links[0].ObjectMatrix.Column1.X")!;

        node.SetValue(5.0f);
        node.SetValue(float.NaN);
        Assert.True(float.IsNaN(data.Links[0].ObjectMatrix.Column1.X));
        while (document.CanUndo)
        {
            document.Undo();
        }

        Assert.Equal(unchecked((int)0xFFFFFFFF), BitConverter.SingleToInt32Bits(data.Links[0].ObjectMatrix.Column1.X));
    }

    // Elements put back by undo get their editors back, the one put back at an index replaced the editor of the element that had moved
    // to it
    [AvaloniaFact]
    public void ListElementsPutBackByUndoGetTheirEditorsBack()
    {
        var links = _project.Add(new ChunkLinks { Chunk = "levels/test" }, "Links");
        var data = new ChunkLinksData(links) { Links = [new ChunkLink(), new ChunkLink()] };
        links.SetData(data);
        var document = Open(links);
        var (list, window) = Show<DocumentCollectionViewModel>(document, "Root.AssetData.Links");
        list.IsExpanded = true;
        Pump();
        Assert.Equal(2, list.Nodes.Count);

        list.Nodes[0].Close();
        Pump();
        Assert.Single(list.Nodes);
        document.Undo();
        Pump();
        Assert.Equal(2, data.Links.Count);
        Assert.Equal(document.PropertyGraph.Find("Root.AssetData.Links")!.Children, list.Nodes.Select(node => node.Property));
        window.Close();
    }

    // Combo boxes and check boxes show the values undo and redo put back
    [AvaloniaFact]
    public void CombosAndChecksShowUndoneValues()
    {
        var particles = _project.Add(new Particles(), "Particles");
        var particleData = new ParticleData(particles) { ParticleSystems = [new TT_Lab.AssetData.Instance.Particle.ParticleSystem { Name = "Fire" }], ParticleInstances = [] };
        particles.SetData(particleData);
        var document = Open(particles);
        var (sort, window) = Show<EnumFieldViewModel>(document, "Root.AssetData.ParticleSystems[0].GenSort");
        var original = particleData.ParticleSystems[0].GenSort;
        var other = Enum.GetValues<Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem.GenSortType>().First(value => value != original);

        sort.SelectedValue = other;
        Assert.Equal(other, particleData.ParticleSystems[0].GenSort);
        document.Undo();
        Assert.Equal(original, particleData.ParticleSystems[0].GenSort);
        Assert.Equal(original, sort.SelectedValue);
        document.Redo();
        Assert.Equal(other, sort.SelectedValue);
        window.Close();

        var instance = _project.Add(new ObjectInstance { Chunk = "levels/test", LayoutID = 0 }, "Instance");
        var instanceData = new ObjectInstanceData(instance) { StateFlags = Enums.InstanceState.Visible };
        instance.SetData(instanceData);
        var instanceDocument = Open(instance);
        var (flags, flagWindow) = Show<FlagsFieldViewModel>(instanceDocument, "Root.AssetData.StateFlags");
        flags.IsExpanded = true;
        Pump();
        var visible = Assert.IsType<BoolFieldViewModel>(flags.Nodes.Single(node => node.Property.Name == nameof(Enums.InstanceState.Visible)));
        var snap = Assert.IsType<BoolFieldViewModel>(flags.Nodes.Single(node => node.Property.Name == nameof(Enums.InstanceState.SnapToGround)));
        Assert.True(visible.IsChecked);
        visible.IsChecked = false;
        Assert.Equal(default, instanceData.StateFlags & Enums.InstanceState.Visible);
        // One click is one step
        Assert.Same(instanceDocument.History.Root, instanceDocument.History.Current.Parent);
        instanceDocument.History.CloseStep();
        snap.IsChecked = true;
        Assert.Equal(Enums.InstanceState.SnapToGround, instanceData.StateFlags);

        instanceDocument.Undo();
        Assert.False(snap.IsChecked);
        Assert.False(visible.IsChecked);
        instanceDocument.Undo();
        Assert.Equal(Enums.InstanceState.Visible, instanceData.StateFlags);
        Assert.True(visible.IsChecked);
        Assert.False(instanceDocument.CanUndo);
        instanceDocument.Redo();
        Assert.False(visible.IsChecked);
        instanceDocument.Redo();
        Assert.True(snap.IsChecked);
        flagWindow.Close();
    }
}
