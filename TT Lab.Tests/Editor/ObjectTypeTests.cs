using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Object;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Tests.Editor;

// Changing an object's type froze the game while the beach loaded (fuzzed on 2026-09-29): the game makes other nodes of each type
// and keeps another share of the properties (the decomp's instancefactory.cpp), so a type change gives the object what the new type
// needs, in the type's step, and its sub type is picked from what the game makes of the type
[Collection(ProjectCollection.Name)]
public sealed class ObjectTypeTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private (GameObject Object, GameObjectData Data, DocumentViewModel Document) Open(GameObjectData data, string name = "Thing", uint id = 0x3)
    {
        var gameObject = _project.Add(new GameObject(), name, id);
        data = CopyInto(gameObject, data);
        var document = new DocumentViewModel(gameObject);
        document.Initialize();
        return (gameObject, data, document);
    }

    private static GameObjectData CopyInto(GameObject gameObject, GameObjectData values)
    {
        var data = new GameObjectData(gameObject)
        {
            Type = values.Type,
            SubType = values.SubType,
            ExitPointAmount = values.ExitPointAmount,
            CameraReactJointAmount = values.CameraReactJointAmount,
            InstanceStateFlags = values.InstanceStateFlags,
            TaggedProperties = values.TaggedProperties,
            FloatProperties = values.FloatProperties,
            IntProperties = values.IntProperties,
            ModelSlots = values.ModelSlots,
        };
        gameObject.SetData(data);
        return data;
    }

    private static void SetType(DocumentViewModel document, ITwinObject.ObjectType type) => document.PropertyGraph.Find("Root.AssetData.Type")!.SetValue(type);

    [AvaloniaFact]
    public void AnObjectsTypeIsPickedWithWhatEachIsFor()
    {
        var (_, _, document) = Open(new GameObjectData(null!) { Type = ITwinObject.ObjectType.Crate });
        document.Root.Activator.Activate();
        document.Root.IsExpanded = true;

        var field = (EnumFieldViewModel)document.Root.Nodes.Single(node => node.Property.Name == nameof(GameObjectData.Type));
        Assert.Equal(ITwinObject.ObjectType.Crate, field.SelectedValue);
        Assert.True(field.CanWrite);
        Assert.Contains("Changing it gives the object what the new type needs", field.Hint);
        Assert.Contains("Nina's claw", EnumCaptions.HintOf(typeof(ITwinObject.ObjectType), nameof(ITwinObject.ObjectType.Grabbable)));
    }

    // Only pickups read the sub type: 16 and 17 make a custom pickup, any other object with them gets its collision pinned and a pickup's
    // timer written into its node (GameFactoryStandIn)
    [AvaloniaFact]
    public void SubTypesArePickedFromTheTypesOwnAndFollowIt()
    {
        var (_, data, document) = Open(new GameObjectData(null!) { Type = ITwinObject.ObjectType.Pickup, SubType = ObjectTypes.CustomPickupSubType, ModelSlots = [new ModelSlot()] });
        var field = Assert.IsType<ChoiceFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.SubType")!).Construct());
        field.Activator.Activate();

        Assert.Equal([1, 16, 17], field.Choices.Select(choice => choice.Value));
        Assert.Equal("Custom", field.SelectedChoice!.Name);

        SetType(document, ITwinObject.ObjectType.Crate);

        Assert.Equal(ObjectTypes.PlainSubType, data.SubType);
        Assert.Equal([1], field.Choices.Select(choice => choice.Value));
        Assert.Equal("None", field.SelectedChoice!.Name);

        // The tools gave projectiles 18, which nothing reads
        SetType(document, ITwinObject.ObjectType.Projectile);
        Assert.Equal(ObjectTypes.ProjectileSubType, data.SubType);
        Assert.Equal([1, 18], field.Choices.Select(choice => choice.Value));

        document.Undo();
        document.Undo();
        Assert.Equal(ITwinObject.ObjectType.Pickup, data.Type);
        Assert.Equal(ObjectTypes.CustomPickupSubType, data.SubType);
        Assert.Equal([1, 16, 17], field.Choices.Select(choice => choice.Value));
        Assert.Equal("Custom", field.SelectedChoice!.Name);
        // A value of no meaning for the type is still shown, so it can be put right
        Assert.Equal("Pickups only", ObjectTypes.FindSubType(ITwinObject.ObjectType.Crate, 17).Name);
        Assert.Equal("Not read", ObjectTypes.FindSubType(ITwinObject.ObjectType.Crate, 9).Name);
    }

    // A character is a playable character: its first integer picks which and the character code reads 11 exit points and up to 29 joint
    // IDs with no check, made of another object it's the character none, which no controls drive and nothing stands in for
    [AvaloniaFact]
    public void AnObjectMadeACharacterGetsWhatTheCharacterCodeReads()
    {
        var (_, data, document) = Open(new GameObjectData(null!)
        {
            Type = ITwinObject.ObjectType.GenericObject,
            FloatProperties = [2.0f],
            IntProperties = [0, 200],
            InstanceStateFlags = (Enums.InstanceState)0x7D36,
        });

        SetType(document, ITwinObject.ObjectType.Character);

        Assert.Equal(9, data.TaggedProperties.Count);
        Assert.Equal(56, data.FloatProperties.Count);
        // Its own values stay where the class reads its, the rest are Crash's
        Assert.Equal(2.0f, data.FloatProperties[0]);
        Assert.Equal(ObjectTypes.CrashFloats[1], data.FloatProperties[1]);
        Assert.Equal([ObjectTypes.CharacterNone, 200, 2], data.IntProperties);
        Assert.Equal((Byte)11, data.ExitPointAmount);
        Assert.Equal((Byte)29, data.CameraReactJointAmount);
        // It had values, its state stays; every instance reads its first model slot
        Assert.Equal((Enums.InstanceState)0x7D36, data.InstanceStateFlags);
        Assert.Single(data.ModelSlots);

        // One step
        document.Undo();
        Assert.Equal(ITwinObject.ObjectType.GenericObject, data.Type);
        Assert.Equal([2.0f], data.FloatProperties);
        Assert.Equal([0, 200], data.IntProperties);
        Assert.Empty(data.TaggedProperties);
        Assert.Equal((Byte)0, data.ExitPointAmount);
        Assert.Equal((Byte)0, data.CameraReactJointAmount);
        Assert.Empty(data.ModelSlots);
        document.Redo();
        Assert.Equal([ObjectTypes.CharacterNone, 200, 2], data.IntProperties);
    }

    // The class keeps its share and puts 7 more aside: a character's 68 values made a pickup's keep the 3 the pickup reads, an object
    // without values gets the type's and its state
    [AvaloniaFact]
    public void WhatTheNewTypeCantPutAsideIsDroppedAndAnObjectWithoutValuesGetsTheTypes()
    {
        var (_, character, characterDocument) = Open(new GameObjectData(null!)
        {
            Type = ITwinObject.ObjectType.Character,
            TaggedProperties = [..ObjectTypes.CrashTaggedValues.Select(bits => new TaggedProperty(bits))],
            FloatProperties = [..ObjectTypes.CrashFloats],
            IntProperties = [..ObjectTypes.CrashIntegers],
            ExitPointAmount = 11,
            CameraReactJointAmount = 29,
        }, "Crash", 0x0);
        SetType(characterDocument, ITwinObject.ObjectType.Pickup);
        Assert.Empty(character.TaggedProperties);
        Assert.Equal([ObjectTypes.CrashFloats[0]], character.FloatProperties);
        Assert.Equal([ObjectTypes.CharacterCrash, 255], character.IntProperties);

        var (_, bare, bareDocument) = Open(new GameObjectData(null!) { Type = ITwinObject.ObjectType.Creature }, "Bare", 0x4);
        SetType(bareDocument, ITwinObject.ObjectType.Crate);
        Assert.Equal([1.0f, 50.0f, 0.0f], bare.FloatProperties);
        Assert.Equal([0, 0], bare.IntProperties);
        Assert.Equal(ObjectTypes.Of(ITwinObject.ObjectType.Crate)!.State, bare.InstanceStateFlags);
        // A graple's rope moves joints 0 and 1
        SetType(bareDocument, ITwinObject.ObjectType.Graple);
        Assert.Equal((Byte)2, bare.CameraReactJointAmount);
        Assert.Equal(18, bare.FloatProperties.Count);
    }

    // An instance linked to another object takes what the object's type needs from the object: the values the class reads and the
    // object's state when it had none, no positions for a type that follows none
    [AvaloniaFact]
    public void AnInstanceLinkedToAnotherObjectIsFittedForIt()
    {
        var prop = _project.Add(new GameObject(), "Prop", 0x5);
        CopyInto(prop, new GameObjectData(null!) { Type = ITwinObject.ObjectType.GenericObject, ModelSlots = [new ModelSlot()] });
        var crate = _project.Add(new GameObject(), "Crate", 0x6);
        CopyInto(crate, new GameObjectData(null!)
        {
            Type = ITwinObject.ObjectType.Crate, FloatProperties = [1.0f, 50.0f, 10.0f], IntProperties = [0, 0], InstanceStateFlags = (Enums.InstanceState)0x811E,
            ModelSlots = [new ModelSlot()],
        });
        var position = _project.Add(new Position { Chunk = "levels/test", LayoutID = 0 }, "Position", 0);
        position.SetData(new PositionData(position));
        var instance = _project.Add(new ObjectInstance { Chunk = "levels/test", LayoutID = 0 }, "Instance", 0);
        var data = new ObjectInstanceData(instance) { ObjectId = prop.URI, FloatProperties = [3.0f], IntProperties = [1, 255], Positions = [position.URI] };
        instance.SetData(data);
        var document = new DocumentViewModel(instance);
        document.Initialize();

        document.PropertyGraph.Find("Root.AssetData.ObjectId")!.SetValue(crate.URI);

        Assert.Equal([3.0f, 50.0f, 10.0f], data.FloatProperties);
        Assert.Equal([1, 255], data.IntProperties);
        Assert.Empty(data.Positions);
        document.Undo();
        Assert.Equal(prop.URI, data.ObjectId);
        Assert.Equal([3.0f], data.FloatProperties);
        Assert.Equal([position.URI], data.Positions);

        // Without values of its own it takes the object's and its state
        var bare = _project.Add(new ObjectInstance { Chunk = "levels/test", LayoutID = 0 }, "Bare", 1);
        var bareData = new ObjectInstanceData(bare) { ObjectId = prop.URI };
        bare.SetData(bareData);
        var bareDocument = new DocumentViewModel(bare);
        bareDocument.Initialize();
        bareDocument.PropertyGraph.Find("Root.AssetData.ObjectId")!.SetValue(crate.URI);
        Assert.Equal([1.0f, 50.0f, 10.0f], bareData.FloatProperties);
        Assert.Equal((Enums.InstanceState)0x811E, bareData.StateFlags);
    }
}
