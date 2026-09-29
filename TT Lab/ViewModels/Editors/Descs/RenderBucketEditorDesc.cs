namespace TT_Lab.ViewModels.Editors.Descs;

public record RenderBucketEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new RenderBucketFieldViewModel(Document, Node);
}
