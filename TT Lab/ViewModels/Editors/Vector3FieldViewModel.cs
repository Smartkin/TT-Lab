using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Vector3FieldViewModel(DocumentViewModel document, Vector3 data)
    : DocumentDataViewModel<Vector3>(document, data)
{
    public TextFieldViewModel X { get; } = new(document, data.X) { Caption = "X" };
    public TextFieldViewModel Y { get; } = new(document, data.Y) { Caption = "Y" };
    public TextFieldViewModel Z { get; } = new(document, data.Z) { Caption = "Z" };

    public override void Save()
    {
        Data.X = (float)X.GetFinalData()!;
        Data.Y = (float)Y.GetFinalData()!;
        Data.Z = (float)Z.GetFinalData()!;
        
        base.Save();
    }
}