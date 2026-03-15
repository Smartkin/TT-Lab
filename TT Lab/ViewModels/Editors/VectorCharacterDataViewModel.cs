using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class VectorCharacterDataViewModel : DocumentDataViewModel<VectorCharacterData>
{
    public Vector2FieldViewModel Uv { get; }
    public Vector2FieldViewModel Size { get; }
    public TextFieldViewModel PageNum { get; }

    public VectorCharacterDataViewModel(DocumentViewModel document, PropertyNode data,
        params DocumentNodeViewModel[] dependencies)
        : base(document, data, dependencies)
    {
        Uv = new Vector2FieldViewModel(document, Property.Find("PageUv")!, this) { Caption = "Page UV" };
        Size = new Vector2FieldViewModel(document, Property.Find("Size")!, this) { Caption = "Size" };
        PageNum = new TextFieldViewModel(document, Property.Find("FontPageSpecifier")!, this) { Caption = "Font Page Number",
            EditorParameters =
            {
                { TextFieldViewModel.TextFieldNumberRange, new uint[] { 0, 3 } }
            }
        };
    }
}