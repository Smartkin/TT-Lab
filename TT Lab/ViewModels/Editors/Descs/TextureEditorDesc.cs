using TT_Lab.ViewModels.Editors.Graphics;

namespace TT_Lab.ViewModels.Editors.Descs;

public record TextureEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new TextureViewModel(Document, Node);
}