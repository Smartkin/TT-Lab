using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.ViewModels.Editors.Descs;

public record ParticleSystemNameEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ParticleSystemNameFieldViewModel(Document, Node);
}
