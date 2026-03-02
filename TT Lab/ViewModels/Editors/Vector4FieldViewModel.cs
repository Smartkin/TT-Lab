using System;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Vector4FieldViewModel(DocumentViewModel document, Vector4 data)
    : DocumentDataViewModel<Vector4>(document, data)
{
    public TextFieldViewModel X { get; } = new(document, data.X) { Caption = "X" };
    public TextFieldViewModel Y { get; } = new(document, data.Y) { Caption = "Y" };
    public TextFieldViewModel Z { get; } = new(document, data.Z) { Caption = "Z" };
    public TextFieldViewModel W { get; } = new(document, data.W) { Caption = "W" };

    public override void Save()
    {
        Data.X = (float)X.GetFinalData()!;
        Data.Y = (float)Y.GetFinalData()!;
        Data.Z = (float)Z.GetFinalData()!;
        Data.W = (float)W.GetFinalData()!;
        
        base.Save();
    }
}