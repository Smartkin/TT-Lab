namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// A sound's pitch picked as the sample rate it stands for
/// </summary>
public record SampleRateEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new SampleRateFieldViewModel(Document, Node);
}
