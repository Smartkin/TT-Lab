using System.Reactive.Disposables;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Matrix4FieldViewModel(DocumentViewModel document, Matrix4? data)
    : DocumentDataViewModel<Matrix4?>(document, data)
{
    public Vector4FieldViewModel V1 { get; } = new(document, data?.Column1 ?? new Vector4());
    public Vector4FieldViewModel V2 { get; } = new(document, data?.Column2 ?? new Vector4());
    public Vector4FieldViewModel V3 { get; } = new(document, data?.Column3 ?? new Vector4());
    public Vector4FieldViewModel V4 { get; } = new(document, data?.Column4 ?? new Vector4());

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        if (Data == null)
        {
            IsReadOnly = true;
        }
        
        base.OnInitialized(disposables);
    }

    public override void Save()
    {
        if (Data == null)
        {
            return;
        }
        
        Data.Column1 = (Vector4)V1.GetFinalData()!;
        Data.Column2 = (Vector4)V2.GetFinalData()!;
        Data.Column3 = (Vector4)V3.GetFinalData()!;
        Data.Column4 = (Vector4)V4.GetFinalData()!;
        
        base.Save();
    }
}