using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;

namespace TT_Lab.Tests.Assets;

public class DeletedReferenceFixerTests
{
    private static readonly LabURI Deleted = new("res://Test/Texture/Deleted");
    private static readonly LabURI Kept = new("res://Test/Texture/Kept");
    private static readonly LabURI Placeholder = new("res://Test/Texture/Placeholder");

    [ReferencesAssets]
    private class Item
    {
        public LabURI Asset { get; set; } = LabURI.Empty;
    }

    [ReferencesAssets]
    private class ModelSlots
    {
        public List<ModelSlot> Slots { get; set; } = [];
    }

    [ReferencesAssets]
    private class Data
    {
        public LabURI Default { get; set; } = LabURI.Empty;

        [OnReferenceDeleted(DeletedReferenceAction.Clear)]
        public LabURI Cleared { get; set; } = LabURI.Empty;

        // Single references can't be removed so they get cleared
        [OnReferenceDeleted(DeletedReferenceAction.Remove)]
        public LabURI Removed { get; set; } = LabURI.Empty;

        public List<LabURI> ReplacedList { get; set; } = [];

        [OnReferenceDeleted(DeletedReferenceAction.Remove)]
        public List<LabURI> RemovedList { get; set; } = [];

        [OnReferenceDeleted(DeletedReferenceAction.Remove)]
        public List<Item> RemovedItems { get; set; } = [];

        public List<Item> FixedItems { get; set; } = [];

        public LabURI[] FixedSizeList { get; set; } = [];

        public Item Nested { get; set; } = new();

        public Data? Self { get; set; }
    }

    private static DeletedReferenceFixer CreateFixer() => new(new HashSet<LabURI> { Deleted }, new Dictionary<LabURI, LabURI> { { Deleted, Placeholder } });

    private static DeletedReferenceFixer CreateDryRun() => new(new HashSet<LabURI> { Deleted });

    [Fact]
    public void ReferencesAreReplacedWithPlaceholdersByDefault()
    {
        var data = new Data { Default = Deleted, ReplacedList = [Kept, Deleted, Kept] };

        CreateFixer().FixObject(data);

        Assert.Equal(Placeholder, data.Default);
        Assert.Equal([Kept, Placeholder, Kept], data.ReplacedList);
    }

    [Fact]
    public void ClearedAndRemovedSingleReferencesBecomeEmpty()
    {
        var data = new Data { Cleared = Deleted, Removed = Deleted };

        CreateFixer().FixObject(data);

        Assert.Equal(LabURI.Empty, data.Cleared);
        Assert.Equal(LabURI.Empty, data.Removed);
    }

    [Fact]
    public void RemovedReferencesAreTakenOutOfLists()
    {
        var data = new Data { RemovedList = [Deleted, Kept, Deleted] };

        CreateFixer().FixObject(data);

        Assert.Equal([Kept], data.RemovedList);
    }

    [Fact]
    public void ObjectsReferencingDeletedAssetsAreRemovedFromLists()
    {
        var data = new Data { RemovedItems = [new Item { Asset = Deleted }, new Item { Asset = Kept }] };

        CreateFixer().FixObject(data);

        Assert.Equal([Kept], data.RemovedItems.Select(item => item.Asset));
    }

    [Fact]
    public void ObjectsInOtherListsAndNestedObjectsAreFixed()
    {
        var data = new Data { FixedItems = [new Item { Asset = Deleted }], Nested = new Item { Asset = Deleted } };

        CreateFixer().FixObject(data);

        Assert.Equal(Placeholder, data.FixedItems.Single().Asset);
        Assert.Equal(Placeholder, data.Nested.Asset);
    }

    [Fact]
    public void FixedSizeListsGetPlaceholdersInsteadOfRemovals()
    {
        var data = new Data { FixedSizeList = [Kept, Deleted] };

        CreateFixer().FixObject(data);

        Assert.Equal([Kept, Placeholder], data.FixedSizeList);
    }

    [Fact]
    public void DryRunOnlyCollectsWhatNeedsPlaceholders()
    {
        var data = new Data { Default = Deleted, Cleared = Deleted, RemovedList = [Deleted], RemovedItems = [new Item { Asset = Deleted }] };
        var fixer = CreateDryRun();

        fixer.FixObject(data);

        Assert.True(fixer.FoundReferences);
        Assert.Equal([Deleted], fixer.RequiredPlaceholders);
        Assert.Equal(Deleted, data.Default);
        Assert.Equal(Deleted, data.Cleared);
        Assert.Single(data.RemovedList);
        Assert.Single(data.RemovedItems);
    }

    [Fact]
    public void RemovalsAndClearsNeedNoPlaceholders()
    {
        var data = new Data { Cleared = Deleted, RemovedList = [Deleted], RemovedItems = [new Item { Asset = Deleted }] };
        var fixer = CreateDryRun();

        fixer.FixObject(data);

        Assert.True(fixer.FoundReferences);
        Assert.Empty(fixer.RequiredPlaceholders);
    }

    [Fact]
    public void UnrelatedDataIsLeftAlone()
    {
        var data = new Data { Default = Kept, ReplacedList = [Kept] };
        var fixer = CreateDryRun();

        fixer.FixObject(data);

        Assert.False(fixer.FoundReferences);
        Assert.Equal(Kept, data.Default);
    }

    [Fact]
    public void CyclesAreVisitedOnce()
    {
        var data = new Data { Default = Deleted };
        data.Self = data;

        CreateFixer().FixObject(data);

        Assert.Equal(Placeholder, data.Default);
    }

    [Fact]
    public void ObjectsWithoutReferencesAttributeAreSkipped()
    {
        var fixer = CreateDryRun();

        fixer.FixObject(new { Asset = Deleted });

        Assert.False(fixer.FoundReferences);
    }

    // The animation belongs to the OGI, a slot without one can't play it
    [Fact]
    public void ModelSlotsLoseTheirDeletedOgi()
    {
        var slots = new ModelSlots { Slots = [new ModelSlot { Ogi = Kept, Animation = 1 }, new ModelSlot { Ogi = Deleted, Animation = 2 }] };

        CreateFixer().FixObject(slots);

        Assert.Equal([Kept, LabURI.Empty], slots.Slots.Select(slot => slot.Ogi));
    }
}
