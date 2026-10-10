namespace TT_Lab.ViewModels.Editors.Descs;

public record FlagsEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new FlagsFieldViewModel(Document, Node)
    {
        IsExpanded = true
    };

    // The bits are rows of their own, their property's caption goes above them
    protected override void Finish(DocumentNodeViewModel editor)
    {
        editor.Orientation = Avalonia.Controls.Dock.Top;
    }
}