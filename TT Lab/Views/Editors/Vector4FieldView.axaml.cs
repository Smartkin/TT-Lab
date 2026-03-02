using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class Vector4FieldView : DocumentBaseView<Vector4FieldViewModel>
{
    public Vector4FieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }
}