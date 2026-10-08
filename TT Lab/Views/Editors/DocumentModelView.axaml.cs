using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class DocumentModelView : DocumentBaseView<DocumentModelViewModel>
{
    // Two sheets, the front one over the back one
    private const string CopyIcon = "M5.5,4 V1.5 H14.5 V11.5 H12 M2.5,4.5 H11.5 V14.5 H2.5 Z";
    // A clipboard with a sheet's lines on it
    private const string PasteIcon = "M4.5,2.5 H2.5 V14.5 H13.5 V2.5 H11.5 M5.5,1.5 H10.5 V4.5 H5.5 Z M5,7.5 H11 M5,10 H11 M5,12.5 H9";

    // Only an asset's top has them, a document has a model view for every struct of a list
    private StackPanel? _assetValues;
    private Button? _pasteAssetValues;

    public DocumentModelView()
    {
        InitializeComponent();
        Header.PointerEntered += Header_OnPointerEntered;
    }

    // The constructors' flyout and the scroll requests are bound from the view model
    protected override bool RebindsOnRecycle => true;

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.Nodes,
            view => view.EditorsContainer.ItemsSource).DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.ScrollRequest)
            .WhereNotNull()
            .Subscribe(_ => BringNodeIntoView(EditorsContainer, ViewModel!.Nodes, ViewModel.TakeScrollRequest()))
            .DisposeWith(disposables);

        this.OneWayBind(ViewModel, viewModel => viewModel.Constructors,
            view => view.ConstructibleTypesContainer.ItemsSource).DisposeWith(disposables);

        FollowAssetRoot(disposables);
    }

    // A whole asset's values are copied and pasted from its header. The Inspector panel's view of the last inspected node takes the next
    // one over, which may or may not be an asset
    private void FollowAssetRoot(CompositeDisposable disposables)
    {
        var shown = new SerialDisposable().DisposeWith(disposables);
        this.WhenAnyValue(view => view.ViewModel!.IsAssetRoot)
            .Subscribe(isAssetRoot =>
            {
                shown.Disposable = isAssetRoot ? ShowAssetValues() : Disposable.Empty;
                if (_assetValues != null)
                {
                    _assetValues.IsVisible = isAssetRoot;
                }
            })
            .DisposeWith(disposables);
    }

    private CompositeDisposable ShowAssetValues()
    {
        if (_assetValues == null)
        {
            var copy = IconButton(CopyIcon);
            ToolTip.SetTip(copy, DocumentNodeViewModel.CopyAssetValuesHint);
            copy.Click += async (_, _) =>
            {
                if (ViewModel != null)
                {
                    await ViewModel.CopyValuesAsync();
                }
            };
            _pasteAssetValues = IconButton(PasteIcon);
            ToolTip.SetShowOnDisabled(_pasteAssetValues, true);
            _pasteAssetValues.Click += async (_, _) =>
            {
                if (ViewModel != null)
                {
                    await ViewModel.PasteValuesAsync();
                }
            };
            _assetValues = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Thickness(6, 0), Children = { copy, _pasteAssetValues } };
            DockPanel.SetDock(_assetValues, Avalonia.Controls.Dock.Right);
            Header.Children.Insert(1, _assetValues);
        }

        var paste = _pasteAssetValues!;
        var bindings = new CompositeDisposable();
        this.WhenAnyValue(view => view.ViewModel!.CanPasteValues).Subscribe(canPaste => paste.IsEnabled = canPaste).DisposeWith(bindings);
        this.WhenAnyValue(view => view.ViewModel!.PasteValuesHint).Subscribe(hint => ToolTip.SetTip(paste, hint)).DisposeWith(bindings);
        return bindings;
    }

    // A disabled button gets no pointer, the header does: what's on the clipboard is looked at again when the pointer comes
    private async void Header_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (ViewModel is { IsAssetRoot: true } viewModel)
        {
            await viewModel.RefreshPasteStateAsync();
        }
    }

    private static Button IconButton(string icon)
    {
        var button = new Button { Padding = new Thickness(2), Margin = new Thickness(1, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var path = new Path { Data = Geometry.Parse(icon), StrokeThickness = 1.2, Width = 16, Height = 16 };
        path.Bind(Shape.StrokeProperty, button.GetObservable(TemplatedControl.ForegroundProperty));
        path.Bind(OpacityProperty, button.GetObservable(IsEffectivelyEnabledProperty).Select(enabled => enabled ? 1.0 : 0.35));
        button.Content = path;
        return button;
    }

    private void ConstructorClicked(object? sender, RoutedEventArgs e)
    {
        ConstructNewInstance.Flyout?.Hide();
    }

    private void Caption_OnContextRequested(object? sender, Avalonia.Controls.ContextRequestedEventArgs e) => PropertyMenu.Show(sender, e);
}
