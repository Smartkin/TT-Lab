namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// A tagged value of an instance's properties, typed the way the scripts write tagged arguments
/// </summary>
public record TaggedPropertyEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new TaggedPropertyFieldViewModel(Document, Node);
}
