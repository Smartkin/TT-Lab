namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// A scenery light's color picked as a color, its brightness left to the intensity
/// </summary>
public record LightColorEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new LightColorFieldViewModel(Document, Node);
}
