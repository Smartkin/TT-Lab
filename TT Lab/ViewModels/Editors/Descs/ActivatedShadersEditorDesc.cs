namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// A material's activated shaders, ticked by the types of the shaders it has
/// </summary>
public record ActivatedShadersEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ActivatedShadersFieldViewModel(Document, Node);
}
