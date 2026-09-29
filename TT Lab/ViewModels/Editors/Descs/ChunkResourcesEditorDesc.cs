namespace TT_Lab.ViewModels.Editors.Descs;

public record ChunkResourcesEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ChunkResourcesTreeViewModel(Document, Node);
}
