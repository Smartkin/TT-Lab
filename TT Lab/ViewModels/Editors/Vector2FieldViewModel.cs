using System.Reactive.Disposables;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Vector2FieldViewModel(DocumentViewModel document, Vector2 data)
    : DocumentDataViewModel<Vector2>(document, data)
{
    public TextFieldViewModel X { get; } = new(document, data.X) { Caption = "X" };
    public TextFieldViewModel Y { get; } = new(document, data.Y) { Caption = "Y" };

    public override void Save()
    {
        Data.X = (float)X.GetFinalData()!;
        Data.Y = (float)Y.GetFinalData()!;
        
        base.Save();
    }
}