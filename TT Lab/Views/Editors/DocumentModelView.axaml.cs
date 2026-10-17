using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
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
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class DocumentModelView : DocumentBaseView<DocumentModelViewModel>
{
    // Two sheets, the front one over the back one
    private const string CopyIcon = "M5.5,4 V1.5 H14.5 V11.5 H12 M2.5,4.5 H11.5 V14.5 H2.5 Z";
    // A clipboard with a sheet's lines on it
    private const string PasteIcon = "M4.5,2.5 H2.5 V14.5 H13.5 V2.5 H11.5 M5.5,1.5 H10.5 V4.5 H5.5 Z M5,7.5 H11 M5,10 H11 M5,12.5 H9";
    // Two links of a chain
    private const string ReplaceLinksIcon = "M6,10 L10,6 M8,4.5 L9.5,3 A2.47,2.47 0 0 1 13,6.5 L11.5,8 M8,11.5 L6.5,13 A2.47,2.47 0 0 1 3,9.5 L4.5,8";
    private const string ReplaceLinksHint = "Replace links: tick the asset's links to replace and pick what replaces each kind of them";
    // An orbit around a dot, Blender's
    private const string BlenderIcon = "M14,8.5 A5,5 0 1 1 4,8.5 A5,5 0 1 1 14,8.5 Z M10.5,8.5 A1.5,1.5 0 1 1 7.5,8.5 A1.5,1.5 0 1 1 10.5,8.5 Z M1,3.5 L5.5,6.5";
    internal const string BlenderHint = "Edit in Blender: the model's blend file opens in Blender (a new one or an existing one the first time), linked to TT Lab while it has the project open";
    // A chevron under the Blender button's other ways of editing
    private const string BlenderMenuIcon = "M2,6.5 L5,9.5 L8,6.5";
    internal const string BlenderMenuHint = "More of Blender: use an existing blend file, whose model replaces this one";

    // Only an asset's top has them, a document has a model view for every struct of a list
    private StackPanel? _assetValues;
    private Button? _pasteAssetValues;
    private Button? _editInBlender;
    private Button? _blenderMenu;

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
            var replaceLinks = IconButton(ReplaceLinksIcon);
            ToolTip.SetTip(replaceLinks, ReplaceLinksHint);
            replaceLinks.Click += async (_, _) =>
            {
                if (ViewModel != null)
                {
                    await ViewModel.ReplaceLinksAsync();
                }
            };
            _editInBlender = IconButton(BlenderIcon);
            ToolTip.SetTip(_editInBlender, BlenderHint);
            _editInBlender.Click += async (_, _) => await EditInBlender();
            _editInBlender.ContextFlyout = BlenderMenu();
            _blenderMenu = IconButton(BlenderMenuIcon);
            ((Path)_blenderMenu.Content!).Width = 10;
            _blenderMenu.Margin = new Thickness(0);
            ToolTip.SetTip(_blenderMenu, BlenderMenuHint);
            _blenderMenu.Flyout = BlenderMenu();
            _assetValues = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Thickness(6, 0),
                Children = { copy, _pasteAssetValues, replaceLinks, _editInBlender, _blenderMenu }
            };
            DockPanel.SetDock(_assetValues, Avalonia.Controls.Dock.Right);
            // Before the caption, which takes what's left: after it a long alias pushed the buttons out of the inspector
            Header.Children.Insert(0, _assetValues);
        }

        var paste = _pasteAssetValues!;
        // The view shows the next inspected asset as well, a model or not
        _editInBlender!.IsVisible = _blenderMenu!.IsVisible = ViewModel?.Property.Target is IAsset shown && Tools.Blender.BlenderEditing.CanEdit(shown);
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

    private async Task EditInBlender()
    {
        if (ViewModel?.Property.Target is IAsset asset)
        {
            await Tools.Blender.BlenderEditing.EditAsync(asset);
        }
    }

    // Edit in Blender and Use an Existing Blend File, the dropdown's and the Blender button's right click's
    private MenuFlyout BlenderMenu()
    {
        var edit = new MenuItem { Header = "Edit in Blender" };
        ToolTip.SetTip(edit, BlenderHint);
        edit.Click += async (_, _) => await EditInBlender();
        var useExisting = new MenuItem { Header = Tools.Blender.BlenderEditing.UseExistingHeader };
        ToolTip.SetTip(useExisting, Tools.Blender.BlenderEditing.UseExistingHint);
        useExisting.Click += async (_, _) =>
        {
            if (ViewModel?.Property.Target is IAsset asset)
            {
                await Tools.Blender.BlenderEditing.UseBlendFileAsync(asset);
            }
        };
        return new MenuFlyout { Items = { edit, useExisting } };
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
