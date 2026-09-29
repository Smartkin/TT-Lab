using TT_Lab.ViewModels.Editors.Global;

namespace TT_Lab.ViewModels.Editors.Descs;

public record TextFileEditorDesc : EditorDesc
{
    public override bool ShowsInSidePane => true;

    protected override DocumentNodeViewModel ConstructInternal() => new TextFileEditorViewModel(Document, Node);
}
