using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Object;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Tests.Editor;

// An object's behaviour slots are captioned by what the game starts them with for the object's type (the decomp's callers of
// RunAgentEvent), the slots only its scripts start keep the list's caption
[Collection(ProjectCollection.Name)]
public sealed class BehaviourSlotTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private DocumentViewModel Open(ITwinObject.ObjectType type, int slots)
    {
        var gameObject = _project.Add(new GameObject(), "Thing", 0x3);
        gameObject.SetData(new GameObjectData(gameObject) { Type = type, BehaviourSlots = Enumerable.Repeat(LabURI.Empty, slots).ToList() });
        var document = new DocumentViewModel(gameObject);
        document.Initialize();
        return document;
    }

    private static PropertyNode Slots(DocumentViewModel document) => document.PropertyGraph.Find("Root.AssetData.BehaviourSlots")!;

    private static string? Caption(DocumentViewModel document, int slot) => Slots(document).Children[slot].Presentation?.Caption;

    private static string Hint(DocumentViewModel document, int slot) => Slots(document).Children[slot].Presentation!.Hint!;

    private static void SetType(DocumentViewModel document, ITwinObject.ObjectType type) => document.PropertyGraph.Find("Root.AssetData.Type")!.SetValue(type);

    [AvaloniaFact]
    public void TheSlotsTheGameStartsAreCaptionedByWhatStartsThemForTheType()
    {
        var document = Open(ITwinObject.ObjectType.GenericObject, 18);

        Assert.Equal("Behaviour Slot 0 · Spawned", Caption(document, 0));
        Assert.Contains("OpenAllLinkedFurniture", Hint(document, 1));
        Assert.Equal("Behaviour Slot 3 · Touched", Caption(document, 3));
        Assert.Contains("any physics hit while walking into it reaches it", Hint(document, 3));
        Assert.Equal("Behaviour Slot 10 · Hit by a Thrown Character", Caption(document, 10));
        // Nothing starts slot 9, a generic object's slots past 10 are its scripts'
        Assert.Null(Caption(document, 9));
        Assert.Null(Caption(document, 11));

        // The inspector shows them, the slots the scripts decide on with the list's caption and hint
        document.Root.Activator.Activate();
        document.Root.IsExpanded = true;
        var list = (DocumentCollectionViewModel)document.Root.Nodes.Single(node => node.Property.Name == nameof(GameObjectData.BehaviourSlots));
        list.Activator.Activate();
        list.IsExpanded = true;
        Assert.Equal("Behaviour Slot 3 · Touched", list.Nodes[3].Caption);
        Assert.Equal(Hint(document, 3), list.Nodes[3].Hint);
        Assert.Equal("Behaviour Slot 11", list.Nodes[11].Caption);
        Assert.Contains("only run when a script starts them", list.Nodes[11].Hint);

        // They follow the type
        SetType(document, ITwinObject.ObjectType.Crate);
        Assert.Equal("Behaviour Slot 11 · Falling", list.Nodes[11].Caption);
        Assert.Contains("impulse up to 5", Hint(document, 3));
        Assert.Equal("Behaviour Slot 15 · Checkpoint Released", Caption(document, 15));
        Assert.Null(Caption(document, 14));

        // Projectiles drop contact messages, nothing attacks a graple
        SetType(document, ITwinObject.ObjectType.Projectile);
        Assert.Null(Caption(document, 2));
        Assert.Equal("Behaviour Slot 3 · Touched", Caption(document, 3));
        SetType(document, ITwinObject.ObjectType.Graple);
        Assert.Equal("Behaviour Slot 2 · Damaged", Caption(document, 2));
        Assert.Null(Caption(document, 3));
        Assert.Equal("Behaviour Slot 17 · Swipe Done", Caption(document, 17));

        // Undone (the type's changes in a row are one step), the captions are the type's again
        document.Undo();
        var type = document.PropertyGraph.Find("Root.AssetData.Type")!.GetValue<ITwinObject.ObjectType>();
        Assert.NotEqual(ITwinObject.ObjectType.Graple, type);
        for (var slot = 0; slot < 18; slot++)
        {
            Assert.Equal(BehaviourSlotEvents.Of(type, slot)?.Name, Caption(document, slot)?.Split(" · ")[1]);
        }
    }

    // The playable characters' code runs most of their slots past 10 itself
    [AvaloniaFact]
    public void ThePlayableCharactersSlotsAreTheirMovesEvents()
    {
        var document = Open(ITwinObject.ObjectType.Character, 111);

        Assert.Contains("contact messages take the character's hit points", Hint(document, 2));
        Assert.Equal("Behaviour Slot 12 · Idle", Caption(document, 12));
        Assert.Equal("Behaviour Slot 67 · Died", Caption(document, 67));
        Assert.Equal("Behaviour Slot 110 · Skate Crash", Caption(document, 110));
        // What Crash's scripts start themselves (his spin punch)
        Assert.Null(Caption(document, 20));
    }

    // A slot put in or taken out moves the ones after it to other slots
    [AvaloniaFact]
    public void TheCaptionsStayWithTheSlotsAsTheListChanges()
    {
        var document = Open(ITwinObject.ObjectType.Crate, 13);
        var slots = Slots(document);

        slots.RemoveElement(slots.Children[0]);
        Assert.Equal("Behaviour Slot 0 · Spawned", Caption(document, 0));
        Assert.Equal("Behaviour Slot 11 · Falling", Caption(document, 11));

        document.Undo();
        Assert.Equal("Behaviour Slot 12 · Landed", Caption(document, 12));

        slots.AddElement();
        Assert.Equal("Behaviour Slot 13 · Nitro Triggered", Caption(document, 13));
    }
}
