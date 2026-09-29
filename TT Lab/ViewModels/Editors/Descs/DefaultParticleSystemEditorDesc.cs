using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.ViewModels.Editors.Descs;

public record DefaultParticleSystemEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new DefaultParticleSystemFieldViewModel(Document, Node);
}
