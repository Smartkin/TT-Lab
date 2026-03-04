using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Vector2FieldViewModel(DocumentViewModel document, Vector2 data)
    : DocumentDataViewModel<Vector2>(document, data)
{
    public TextFieldViewModel X { get; } = new(document, data.X) { Caption = "X" };
    public TextFieldViewModel Y { get; } = new(document, data.Y) { Caption = "Y" };

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);
        
        this.WhenAnyValue(x => x.X.Data)
            .Skip(1)
            .Subscribe(x =>
                {
                    Data.X = (float)x;
                    this.RaisePropertyChanged(nameof(Data));
                }
            ).DisposeWith(disposables);
        
        this.WhenAnyValue(x => x.Y.Data)
            .Skip(1)
            .Subscribe(x =>
                {
                    Data.Y = (float)x;
                    this.RaisePropertyChanged(nameof(Data));
                }
            ).DisposeWith(disposables);
    }

    public override void Save()
    {
        Data.X = (float)X.GetFinalData()!;
        Data.Y = (float)Y.GetFinalData()!;
        
        base.Save();
    }
}