using TT_Lab.ViewModels.Editors.Graphics;

namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// A shader's animation, edited on a timeline of its frames' keys
/// </summary>
public record ShaderAnimationEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ShaderAnimationEditorViewModel(Document, Node);
}
