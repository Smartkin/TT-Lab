using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace TT_Lab.Tests.Editor;

// The tagged values of instances, templates and objects keep the game's word and are typed the way scripts write tagged arguments
[Collection(ProjectCollection.Name)]
public sealed class TaggedPropertyEditorTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [Fact]
    public void ValuesAreWrittenWithTheirType()
    {
        Assert.Equal("5", TaggedValue.Describe(TaggedValue.FromInt(5)));
        Assert.Equal("Float(1.5)", TaggedValue.Describe(TaggedValue.FromFloat(1.5f)));
        Assert.Equal("Angle(90.0)", TaggedValue.Describe(TaggedValue.FromAngle(90.0f)));
        Assert.Equal("Prop(2)", TaggedValue.Describe(TaggedValue.FromProperty(2, TaggedValue.TypeFloat)));
        // The tools left words of no valid type, like an ID
        Assert.Equal("Raw(0x0000FFFF)", TaggedValue.Describe(0xFFFF));
    }

    [Fact]
    public void PlainValuesKeepTheType()
    {
        var current = TaggedValue.FromFloat(1.5f);

        Assert.Equal(TaggedValue.FromFloat(7.0f), TaggedValue.ParseKeepingType("7", current));
        Assert.Equal(TaggedValue.FromInt(3), TaggedValue.ParseKeepingType("Int(3)", current));
        Assert.Equal(TaggedValue.FromAngle(45.0f), TaggedValue.ParseKeepingType("Angle(45)", current));
        Assert.Equal(TaggedValue.FromProperty(4, TaggedValue.TypeFloat), TaggedValue.ParseKeepingType("Prop(4)", current));
        Assert.Equal(TaggedValue.FromInt(12), TaggedValue.ParseKeepingType("12", 0xFFFF));
        foreach (var bits in new[] { TaggedValue.FromInt(-20), TaggedValue.FromFloat(-0.25f), TaggedValue.FromAngle(-30.0f), TaggedValue.FromProperty(1, TaggedValue.TypeAngle), 0xFFFFu })
        {
            Assert.Equal(bits, TaggedValue.ParseKeepingType(TaggedValue.Describe(bits), bits));
        }
    }

    [AvaloniaFact]
    public void InstancesTypeTheirTaggedValues()
    {
        var instance = _project.Add(new ObjectInstance { Chunk = "levels/test", LayoutID = 0 }, "Instance");
        var data = new ObjectInstanceData(instance) { TaggedProperties = [new(TaggedValue.FromInt(5)), new(TaggedValue.FromFloat(1.5f))] };
        instance.SetData(data);
        var document = new DocumentViewModel(instance);
        document.Initialize();
        var node = document.PropertyGraph.Find("Root.AssetData.TaggedProperties[1]")!;

        var field = Assert.IsType<TaggedPropertyFieldViewModel>(EditorDescRegistry.GetDesc(document, node).Construct());
        field.Activator.Activate();
        Assert.Equal("Float(1.5)", field.Text);

        field.Text = "2.5";
        Assert.Equal(TaggedValue.FromFloat(2.5f), data.TaggedProperties[1].Bits);
        field.Text = "Int(";
        Assert.Equal(TaggedValue.FromFloat(2.5f), data.TaggedProperties[1].Bits);
        field.Text = "Int(4)";
        Assert.Equal(TaggedValue.FromInt(4), data.TaggedProperties[1].Bits);

        document.Undo();
        Assert.Equal(TaggedValue.FromFloat(1.5f), data.TaggedProperties[1].Bits);
        Assert.Equal("Float(1.5)", field.Text);
    }
}
