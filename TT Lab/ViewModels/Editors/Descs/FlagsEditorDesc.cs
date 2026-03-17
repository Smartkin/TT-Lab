namespace TT_Lab.ViewModels.Editors.Descs;

public record FlagsEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new FlagsFieldViewModel(Document, Node)
    {
        IsExpanded = true
    };
}