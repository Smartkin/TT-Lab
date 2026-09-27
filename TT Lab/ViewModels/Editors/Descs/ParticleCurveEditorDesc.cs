using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.ViewModels.Editors.Descs;

public record ParticleCurveEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ParticleCurveViewModel(Document, Node);
}

public record ParticleGradientEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ParticleGradientViewModel(Document, Node);
}
