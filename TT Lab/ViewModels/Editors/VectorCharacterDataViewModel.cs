using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class VectorCharacterDataViewModel(DocumentViewModel document, VectorCharacterData data)
    : DocumentDataViewModel<VectorCharacterData>(document, data)
{
    public Vector2FieldViewModel Uv { get; } = new(document, data.PageUv) { Caption = "Page UV" };
    public Vector2FieldViewModel Size { get; } = new(document, data.Size) { Caption = "Size" };
    public TextFieldViewModel PageNum { get;  } = new(document, data.FontPageSpecifier) { Caption = "Font Page Number",
        EditorParameters =
        {
            { TextFieldViewModel.TextFieldNumberRange, new uint[] { 0, 3 } }
        }
    };

    public override void Save()
    {
        Data.PageUv = (Vector2)Uv.GetFinalData()!;
        Data.Size = (Vector2)Size.GetFinalData()!;
        Data.FontPageSpecifier = (byte)PageNum.GetFinalData()!;
        
        base.Save();
    }
}