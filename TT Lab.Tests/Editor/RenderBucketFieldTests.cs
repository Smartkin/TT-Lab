using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Tests.Editor;

// A material's render bucket is picked from the game's 28 buckets
[Collection(ProjectCollection.Name)]
public sealed class RenderBucketFieldTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    // Composites make their nodes once activated and expanded
    private static DocumentNodeViewModel? FindNode(DocumentCompositeViewModel parent, string name)
    {
        parent.Activator.Activate();
        parent.IsExpanded = true;
        foreach (var node in parent.Nodes)
        {
            if (node.Property.Name == name)
            {
                return node;
            }

            if (node is DocumentCompositeViewModel composite && FindNode(composite, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<string> Describe(DocumentCompositeViewModel parent)
    {
        foreach (var node in parent.Nodes)
        {
            yield return $"{node.Property.Path} ({node.GetType().Name})";
            if (node is DocumentCompositeViewModel composite)
            {
                foreach (var inner in Describe(composite))
                {
                    yield return inner;
                }
            }
        }
    }

    [AvaloniaFact]
    public void BucketDropdownListsTheGamesBucketsAndSetsTheValue()
    {
        var material = new TestAssets(_project).AddMaterial("Bucketed");
        ((MaterialData)material.GetData()).DmaChainIndex = 2;
        var document = new DocumentViewModel(material);
        document.Initialize();
        var found = FindNode(document.Root, "DmaChainIndex");
        Assert.True(found != null, "nodes: " + string.Join(", ", Describe(document.Root)));
        var field = (RenderBucketFieldViewModel)found;
        field.Activator.Activate();

        Assert.Equal(28, field.Buckets.Count);
        Assert.Equal("Opaque", field.SelectedBucket!.Name);

        field.SelectedBucket = RenderBuckets.Find(24);

        Assert.Equal(24u, document.PropertyGraph.Find("Root.AssetData.DmaChainIndex")!.GetValue<uint>());
        Assert.Equal("UI", RenderBuckets.Find(24).Name);
        Assert.Equal("Not a bucket", RenderBuckets.Find(40).Name);
    }
}
