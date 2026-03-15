namespace TT_Lab.ViewModels.Editors.Descs;

public record UriLinkEditorDesc : EditorDesc
{
    public bool OpenInInspector { get; set; } = false;
    
    protected override DocumentNodeViewModel ConstructInternal() => new UriLinkViewModel(Document, Node)
    {
        EditorParameters =
        {
            { UriLinkViewModel.OpenInInspector, OpenInInspector }
        }
    };
}