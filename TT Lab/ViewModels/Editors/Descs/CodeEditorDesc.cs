namespace TT_Lab.ViewModels.Editors.Descs;

public record CodeEditorDesc : EditorDesc
{
    public bool ValidateCode { get; set; } = true;

    public override bool ShowsInSidePane => true;

    protected override DocumentNodeViewModel ConstructInternal() => new CodeEditorViewModel(Document, Node)
    {
        EditorParameters =
        {
            { CodeEditorViewModel.ValidateAgentLabCode, ValidateCode }
        }
    };
}