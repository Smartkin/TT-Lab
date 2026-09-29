namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// An angle the game keeps in 65536ths of a turn, shown and typed in degrees
/// </summary>
public record AngleEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new AngleFieldViewModel(Document, Node);
}
