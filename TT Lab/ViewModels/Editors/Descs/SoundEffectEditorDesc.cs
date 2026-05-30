using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.ViewModels.Editors.Descs;

public record SoundEffectEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new SoundEffectViewModel(Document, Node);
}