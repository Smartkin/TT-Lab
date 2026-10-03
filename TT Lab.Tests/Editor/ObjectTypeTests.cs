using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets.Code;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Tests.Editor;

// Changing an object's type froze the game while the beach loaded: the type can be changed, its hint warns about it
[Collection(ProjectCollection.Name)]
public sealed class ObjectTypeTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [AvaloniaFact]
    public void AnObjectsTypeIsEditableWithAWarning()
    {
        var crate = _project.Add(new GameObject(), "Crate", 0x3, _project.Project.Ps2Package);
        crate.SetData(new GameObjectData(crate) { Type = ITwinObject.ObjectType.Crate });
        var document = new DocumentViewModel(crate);
        document.Initialize();
        document.Root.Activator.Activate();
        document.Root.IsExpanded = true;

        var field = (EnumFieldViewModel)document.Root.Nodes.Single(node => node.Property.Name == nameof(GameObjectData.Type));
        Assert.Equal(ITwinObject.ObjectType.Crate, field.SelectedValue);
        Assert.True(field.CanWrite);
        Assert.Contains("crash", field.Hint, StringComparison.OrdinalIgnoreCase);
    }
}
