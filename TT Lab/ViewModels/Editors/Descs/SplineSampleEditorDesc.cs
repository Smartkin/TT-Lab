namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// A camera spline's sample, its W shown as the values the game keeps in it
/// </summary>
public record SplineSampleEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new SplineSampleFieldViewModel(Document, Node);
}
