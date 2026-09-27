using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.ViewModels.Editors.Descs;

public record ParticleSystemEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ParticleSystemFieldViewModel(Document, Node);
}
