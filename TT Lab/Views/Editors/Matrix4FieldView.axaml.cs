using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class Matrix4FieldView : DocumentBaseView<Matrix4FieldViewModel>
{
    public Matrix4FieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }
}