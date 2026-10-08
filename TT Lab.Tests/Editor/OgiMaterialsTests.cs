using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;

namespace TT_Lab.Tests.Editor;

// An OGI's inspector lists the materials its meshes draw with, a slot for each with the parts drawing with it: another material picked for a
// slot draws every one of its parts with that one, a step of the document's history, and the model's file keeps it once it's saved
[Collection(ProjectCollection.Name)]
public sealed class OgiMaterialsTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public OgiMaterialsTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    private static IEnumerable<LabURI> PartMaterials(OGIData ogi)
    {
        var assets = AssetManager.Get();
        return ogi.RigidModelIds.SelectMany(model => assets.GetAsset(model).GetData<RigidModelData>().Materials)
            .Concat(assets.GetAsset(ogi.Skin).GetData<SkinData>().SubSkins.Select(part => part.Material))
            .Concat(assets.GetAsset(ogi.BlendSkin).GetData<BlendSkinData>().Blends.Select(part => part.Material));
    }

    [AvaloniaFact]
    public void AnotherMaterialPickedForASlotDrawsEveryPartOfItWithIt()
    {
        var ogi = _assets.AddOgi();
        var gold = _assets.AddSkinMaterial("Gold");
        var document = new DocumentViewModel(ogi);
        document.Initialize();
        var data = ((IAsset)ogi).GetData<OGIData>();
        var slot = Assert.Single(data.MaterialSlots);
        var fur = slot.Material;
        Assert.Contains("rigid model 0's parts 0 and 1", slot.Parts);
        Assert.Contains("the skin's part", slot.Parts);
        Assert.Contains("the blend skin's part", slot.Parts);

        document.PropertyGraph.Find("Root.AssetData.MaterialSlots[0].Material")!.SetValue(gold.URI);

        Assert.All(PartMaterials(data), material => Assert.Equal(gold.URI, material));
        Assert.True(document.IsDirty);
        document.Undo();
        Assert.All(PartMaterials(data), material => Assert.Equal(fur, material));
        document.Redo();
        Assert.All(PartMaterials(data), material => Assert.Equal(gold.URI, material));

        var reloaded = _assets.Reload<OGIData>(ogi);
        Assert.Equal(gold.URI, Assert.Single(reloaded.MaterialSlots).Material);
        Assert.All(PartMaterials(reloaded), material => Assert.Equal(gold.URI, material));
    }

    // The slots are a list nothing gets added to or taken from, each captioned with its material, following a pick and its undo
    [AvaloniaFact]
    public void EachSlotIsCaptionedWithItsMaterial()
    {
        var ogi = _assets.AddOgi();
        var gold = _assets.AddSkinMaterial("Gold");
        var document = new DocumentViewModel(ogi);
        document.Initialize();
        var fur = _assets.Get(((IAsset)ogi).GetData<OGIData>().MaterialSlots[0].Material).Alias;
        var slots = Assert.IsType<DocumentCollectionViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.MaterialSlots")!).Construct());
        using var shown = slots.Activator.Activate();
        slots.IsExpanded = true;

        var slot = Assert.Single(slots.Nodes);
        Assert.Equal($"Slot 0 · {fur}", slot.Caption);
        Assert.False(slots.CanChangeCollection);

        document.PropertyGraph.Find("Root.AssetData.MaterialSlots[0].Material")!.SetValue(gold.URI);
        Assert.Equal($"Slot 0 · {gold.Alias}", slot.Caption);
        document.Undo();
        Assert.Equal($"Slot 0 · {fur}", slot.Caption);
    }

    // The slots aren't stored, they're made of the models' parts: finding the OGI's links (which importing does while a project is created,
    // where only the OGI's own data may be read) and fixing them for a deleted asset leave them alone and don't read the models
    [Fact]
    public void TheOgisLinksAreFoundWithoutReadingItsModels()
    {
        var skin = _project.Add(new Skin(), "Body", 0x1);
        var ogi = _project.Add(new OGI(), "Skeleton", 0x1);
        ogi.SetData(new OGIData(ogi) { Skin = skin.URI });

        ogi.Serialize(SerializationFlags.FixReferences);

        Assert.Contains(skin.URI, ogi.References);
    }
}
