using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class ResourcesEditorsView : ReactiveUserControl<ResourcesEditorsViewModel>
{
    public ResourcesEditorsView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, viewModel => viewModel.Tabs, view => view.ResourcesEditorsDock.ItemsSource).DisposeWith(disposables);
        });
    }
}