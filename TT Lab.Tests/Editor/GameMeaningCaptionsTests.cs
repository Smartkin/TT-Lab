using Avalonia.Headless.XUnit;
using Newtonsoft.Json;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.Tests.Editor;

// Values whose names say something else than what the game does with them (the decomp's) are captioned by what it does, the names
// stay until a version of TT Lab renames them
[Collection(ProjectCollection.Name)]
public sealed class GameMeaningCaptionsTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static DocumentNodeViewModel Editor(IAsset asset, string path)
    {
        var document = new DocumentViewModel(asset);
        document.Initialize();
        return EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct();
    }

    [Fact]
    public void EnumMembersShowWhatTheGameDoes()
    {
        Assert.Equal("NeedsFlying", EnumCaptions.Of(typeof(Enums.AiPathFlags), nameof(Enums.AiPathFlags.Flag5)));
        Assert.Equal("Unused0", EnumCaptions.Of(typeof(Enums.AiPathFlags), nameof(Enums.AiPathFlags.Flag0)));
        Assert.Equal("NeedsJump", EnumCaptions.Of(typeof(Enums.AiPathFlags), nameof(Enums.AiPathFlags.NeedsJump)));
        Assert.Equal("Soft", EnumCaptions.Of(typeof(Enums.SurfaceCollisionFlags), nameof(Enums.SurfaceCollisionFlags.LeavesFootprints)));
        // Only scripts read these, with SoftFlagSet(n)
        Assert.Equal("SoftFlag20", EnumCaptions.Of(typeof(Enums.InstanceState), Enum.GetName((Enums.InstanceState)(1U << 20))!));
        Assert.Equal("SoftFlag4", EnumCaptions.Of(typeof(Enums.InstanceState), nameof(Enums.InstanceState.PlayableCharacterCanMoveAlong)));
    }

    // Bits the game never reads get no check box, they keep what they have when the others are ticked
    [AvaloniaFact]
    public void FlagsTheGameNeverReadsGetNoCheckBox()
    {
        var surface = _project.Add(new CollisionSurface { Chunk = "default", LayoutID = ChunkLayouts.CollisionSurfaces }, "Ground");
        var data = new CollisionSurfaceData(surface) { CollisionMask = Enums.SurfaceCollisionFlags.Default12 | Enums.SurfaceCollisionFlags.SolidToPlayer };
        surface.SetData(data);
        var mask = Assert.IsType<FlagsFieldViewModel>(Editor(surface, "Root.AssetData.CollisionMask"));
        mask.Activator.Activate();
        mask.IsExpanded = true;

        var boxes = mask.Nodes.Select(node => node.Property.Name).ToList();
        Assert.Contains(nameof(Enums.SurfaceCollisionFlags.SolidToPlayer), boxes);
        Assert.DoesNotContain(nameof(Enums.SurfaceCollisionFlags.Default12), boxes);
        Assert.DoesNotContain(nameof(Enums.SurfaceCollisionFlags.Default19), boxes);
        Assert.True(EnumCaptions.IsNeverRead(typeof(Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout.ITwinCamera.CameraFlags), "Unused14"));

        mask.Nodes.Single(node => node.Property.Name == nameof(Enums.SurfaceCollisionFlags.Sticky)).Property.SetValue(true);
        Assert.Equal(Enums.SurfaceCollisionFlags.Default12 | Enums.SurfaceCollisionFlags.SolidToPlayer | Enums.SurfaceCollisionFlags.Sticky, data.CollisionMask);
    }

    // A contact's kinds are named after what sends them in retail, their tooltips say what tests them; the ones nothing sends or tests
    // get no check box
    [AvaloniaFact]
    public void ContactKindsAreNamedAfterWhatSendsThem()
    {
        // The check boxes go by the members' order
        Assert.Equal(Enumerable.Range(0, 32).Select(bit => 1UL << bit), Enum.GetValues<Enums.ContactKinds>().Select(kind => (UInt64)kind));
        Assert.Equal(0x800008u, (UInt32)(Enums.ContactKinds.Burning | Enums.ContactKinds.Sinking));
        Assert.Equal(0x2800000u, (UInt32)(Enums.ContactKinds.Water | Enums.ContactKinds.Sinking));
        Assert.Equal(0x400u, (UInt32)Enums.ContactKinds.GenericHit);

        var surface = _project.Add(new CollisionSurface { Chunk = "default", LayoutID = ChunkLayouts.CollisionSurfaces }, "Lava");
        var data = new CollisionSurfaceData(surface);
        data.ContactMessage.Kinds = Enums.ContactKinds.Burning | Enums.ContactKinds.Sinking | Enums.ContactKinds.Unused0;
        surface.SetData(data);
        var kinds = Assert.IsType<FlagsFieldViewModel>(Editor(surface, "Root.AssetData.ContactMessage.Kinds"));
        kinds.Activator.Activate();
        kinds.IsExpanded = true;

        var boxes = kinds.Nodes.ToDictionary(node => node.Property.Name);
        Assert.DoesNotContain(nameof(Enums.ContactKinds.Unused0), boxes.Keys);
        Assert.Contains("ObjectContextFlags3or22", boxes[nameof(Enums.ContactKinds.Burning)].Hint);
        Assert.Contains("Nothing tests it", boxes[nameof(Enums.ContactKinds.Sinking)].Hint);
        boxes[nameof(Enums.ContactKinds.Water)].Property.SetValue(true);
        Assert.Equal(Enums.ContactKinds.Burning | Enums.ContactKinds.Sinking | Enums.ContactKinds.Unused0 | Enums.ContactKinds.Water, data.ContactMessage.Kinds);
        // Stored as the number it always was
        var stored = JsonConvert.SerializeObject(data.ContactMessage);
        Assert.Contains($"\"Kinds\":{(UInt32)data.ContactMessage.Kinds}", stored);
        Assert.Equal(Enums.ContactKinds.FallingThrough,
            JsonConvert.DeserializeObject<ContactMessage>(stored.Replace($"\"Kinds\":{(UInt32)data.ContactMessage.Kinds}", "\"Kinds\":4"))!.Kinds);
    }

    // The character offers the cameras it's in by their trigger's low byte (OfferCamera), a plain trigger's is the tools' kind
    [AvaloniaFact]
    public void ACamerasKindIsItsPriority()
    {
        var camera = _project.Add(new Camera { Chunk = "default", LayoutID = 4 }, "Camera");
        camera.SetData(new CameraData(camera) { MainCamera1 = new CameraSpline() });
        var trigger = _project.Add(new Trigger { Chunk = "default", LayoutID = 0 }, "Trigger");
        trigger.SetData(new TriggerData(trigger));

        var priority = Editor(camera, "Root.AssetData.Trigger.Kind");
        var kind = Editor(trigger, "Root.AssetData.Kind");

        Assert.Equal("Priority", priority.Caption);
        Assert.Contains("highest priority", priority.Hint);
        Assert.Equal("Kind", kind.Caption);
        Assert.Contains("reverb", kind.Hint);
        Assert.Contains("frames", Editor(camera, "Root.AssetData.Trigger.Instances").Hint);
        Assert.Contains("messages", Editor(trigger, "Root.AssetData.Instances").Hint);
        // Lists of TwinTech's types get their hints too
        Assert.Contains("word", Editor(camera, "Root.AssetData.MainCamera1.PathPoints").Hint);
    }

    // The first subtype moves the follow camera's target and the second its place (FollowCameraTarget::TakeCamera,
    // FollowCameraPositioner::TakeCamera): a boss camera made the first turned the camera behind the player to look past them
    [AvaloniaFact]
    public void ACamerasSubtypesAreWhereItLooksAndWhereItStands()
    {
        var camera = _project.Add(new Camera { Chunk = "default", LayoutID = 4 }, "Camera");
        camera.SetData(new CameraData(camera) { MainCamera1 = new CameraLine2(), MainCamera2 = new BossCamera() });

        var lookAt = Editor(camera, "Root.AssetData.MainCamera1");
        var positioning = Editor(camera, "Root.AssetData.MainCamera2");

        Assert.Equal("Camera Look At", lookAt.Caption);
        Assert.Contains("looks at", lookAt.Hint);
        Assert.Equal("Camera Positioning", positioning.Caption);
        Assert.Contains("stands", positioning.Hint);
    }
}
