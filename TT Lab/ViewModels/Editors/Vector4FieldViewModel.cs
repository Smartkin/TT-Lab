using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Vector4FieldViewModel(DocumentViewModel document, Vector4 data)
    : DocumentDataViewModel<Vector4>(document, data)
{
    public TextFieldViewModel X { get; } = new(document, data.X) { Caption = "X" };
    public TextFieldViewModel Y { get; } = new(document, data.Y) { Caption = "Y" };
    public TextFieldViewModel Z { get; } = new(document, data.Z) { Caption = "Z" };
    public TextFieldViewModel W { get; } = new(document, data.W) { Caption = "W" };

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);
        
        this.WhenAnyValue(x => x.Data)
            .Skip(1)
            .Subscribe(x =>
            {
                X.Text = x.X.ToString(CultureInfo.InvariantCulture);
                Y.Text = x.Y.ToString(CultureInfo.InvariantCulture);
                Z.Text = x.Z.ToString(CultureInfo.InvariantCulture);
                W.Text = x.W.ToString(CultureInfo.InvariantCulture);
            }).DisposeWith(disposables);

        this.WhenAnyValue(x => x.X.Data)
            .Skip(1)
            .Subscribe(x =>
                {
                    Data.X = (float)x;
                    DataVersion++;
                }
            ).DisposeWith(disposables);
        
        this.WhenAnyValue(x => x.Y.Data)
            .Skip(1)
            .Subscribe(x =>
                {
                    Data.Y = (float)x;
                    DataVersion++;
                }
            ).DisposeWith(disposables);
        
        this.WhenAnyValue(x => x.Z.Data)
            .Skip(1)
            .Subscribe(x =>
                {
                    Data.Z = (float)x;
                    DataVersion++;
                }
            ).DisposeWith(disposables);
        
        this.WhenAnyValue(x => x.W.Data)
            .Skip(1)
            .Subscribe(x =>
                {
                    Data.W = (float)x;
                    DataVersion++;
                }
            ).DisposeWith(disposables);
    }

    public override void Save()
    {
        Data.X = (float)X.GetFinalData()!;
        Data.Y = (float)Y.GetFinalData()!;
        Data.Z = (float)Z.GetFinalData()!;
        Data.W = (float)W.GetFinalData()!;
        
        base.Save();
    }
}