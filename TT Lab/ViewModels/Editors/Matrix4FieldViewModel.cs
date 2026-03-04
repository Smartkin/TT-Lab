using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Matrix4FieldViewModel(DocumentViewModel document, Matrix4? data)
    : DocumentDataViewModel<Matrix4?>(document, data)
{
    public Vector4FieldViewModel V1 { get; private set; } = new(document, data?.Column1 ?? new Vector4());
    public Vector4FieldViewModel V2 { get; private set; } = new(document, data?.Column2 ?? new Vector4());
    public Vector4FieldViewModel V3 { get; private set; } = new(document, data?.Column3 ?? new Vector4());
    public Vector4FieldViewModel V4 { get; private set; } = new(document, data?.Column4 ?? new Vector4());

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);
        
        if (Data == null)
        {
            IsReadOnly = true;
        }

        this.WhenAnyValue(x => x.Data)
            .Skip(1)
            .Subscribe(x =>
            {
                V1.Data = x?.Column1 ?? new Vector4();
                V2.Data = x?.Column2 ?? new Vector4();
                V3.Data = x?.Column3 ?? new Vector4();
                V4.Data = x?.Column4 ?? new Vector4();
            }).DisposeWith(disposables);

        this.WhenAnyValue(x => x.V1.DataVersion, x => x.V1.Data)
            .Skip(1)
            .Subscribe(x =>
            {
                Data ??= new Matrix4();
                Data.Column1 = CloneUtils.Clone(x.Item2);
                DataVersion++;
            }).DisposeWith(disposables);
        
        this.WhenAnyValue(x => x.V2.DataVersion, x => x.V2.Data)
            .Skip(1)
            .Subscribe(x =>
            {
                Data ??= new Matrix4();
                Data.Column2 = CloneUtils.Clone(x.Item2);
                DataVersion++;
            }).DisposeWith(disposables);
        
        this.WhenAnyValue(x => x.V3.DataVersion, x => x.V3.Data)
            .Skip(1)
            .Subscribe(x =>
            {
                Data ??= new Matrix4();
                Data.Column3 = CloneUtils.Clone(x.Item2);
                DataVersion++;
            }).DisposeWith(disposables);
        
        this.WhenAnyValue(x => x.V4.DataVersion, x => x.V4.Data)
            .Skip(1)
            .Subscribe(x =>
            {
                Data ??= new Matrix4();
                Data.Column4 = CloneUtils.Clone(x.Item2);
                DataVersion++;
            }).DisposeWith(disposables);
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