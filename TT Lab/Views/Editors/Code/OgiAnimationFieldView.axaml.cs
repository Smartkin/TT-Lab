using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReactiveUI;
using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.Views.Editors.Code;

public partial class OgiAnimationFieldView : DocumentBaseView<OgiAnimationFieldViewModel>
{
    // A slot in a narrow inspector still gets a search its animations' names fit in
    private const double MinSearchWidth = 240;
    private const int PageStep = 8;

    public OgiAnimationFieldView()
    {
        InitializeComponent();
        // The combo box shows the slot's animation and never opens its own list: a click, F4, Alt+Up/Down, Enter or Space opens the
        // search. A flyout's popup has no visual parent, so nothing typed or scrolled in it gets to the combo box or the inspector
        AnimationChoices.AddHandler(PointerPressedEvent, OnChoicesPointerPressed, RoutingStrategies.Tunnel);
        AnimationChoices.AddHandler(KeyDownEvent, OnChoicesKeyDown, RoutingStrategies.Tunnel);
        AnimationChoices.DropDownOpened += (_, _) =>
        {
            AnimationChoices.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            OpenSearch();
        };
        SearchFlyout.Opened += (_, _) => SearchBox.Focus();
        SearchPanel.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        SearchResults.Tapped += OnResultTapped;
    }

    internal FlyoutBase SearchFlyout => FlyoutBase.GetAttachedFlyout(AnimationChoices)!;

    // The combo box drops its selection when its items change
    protected override bool RebindsOnRecycle => true;

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.Animations, view => view.AnimationChoices.ItemsSource).DisposeWith(disposables);
        this.OneWayBind(ViewModel, viewModel => viewModel.CanChooseAnimation, view => view.AnimationChoices.IsEnabled).DisposeWith(disposables);
        this.Bind(ViewModel, viewModel => viewModel.SelectedAnimation, view => view.AnimationChoices.SelectedItem).DisposeWith(disposables);
        this.Bind(ViewModel, viewModel => viewModel.Search, view => view.SearchBox.Text).DisposeWith(disposables);
        this.OneWayBind(ViewModel, viewModel => viewModel.ShownAnimations, view => view.SearchResults.ItemsSource).DisposeWith(disposables);
        this.Bind(ViewModel, viewModel => viewModel.Highlighted, view => view.SearchResults.SelectedItem).DisposeWith(disposables);
    }

    internal void OpenSearch()
    {
        if (ViewModel is not { CanChooseAnimation: true } viewModel || SearchFlyout.IsOpen)
        {
            return;
        }

        viewModel.StartSearch();
        SearchPanel.Width = Math.Max(MinSearchWidth, AnimationChoices.Bounds.Width);
        SearchFlyout.ShowAt(AnimationChoices);
    }

    // Hiding a flyout leaves the focus nowhere
    private void CloseSearch()
    {
        SearchFlyout.Hide();
        AnimationChoices.Focus();
    }

    private void OnChoicesPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(AnimationChoices).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        OpenSearch();
    }

    private void OnChoicesKeyDown(object? sender, KeyEventArgs e)
    {
        var opens = (e.Key == Key.F4 && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                    || (e.Key is Key.Up or Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                    || e.Key is Key.Enter or Key.Space;
        if (!opens)
        {
            return;
        }

        e.Handled = true;
        OpenSearch();
    }

    // The search box keeps the keyboard: the arrows move through what it shows, Enter picks, Escape leaves the slot as it was
    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                ViewModel?.MoveHighlight(1);
                break;
            case Key.Up:
                ViewModel?.MoveHighlight(-1);
                break;
            case Key.PageDown:
                ViewModel?.MoveHighlight(PageStep);
                break;
            case Key.PageUp:
                ViewModel?.MoveHighlight(-PageStep);
                break;
            case Key.Enter:
                if (ViewModel?.PickHighlighted() == true)
                {
                    CloseSearch();
                }

                break;
            case Key.Escape:
                CloseSearch();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { DataContext: OgiAnimationChoice animation })
        {
            return;
        }

        ViewModel?.Pick(animation);
        CloseSearch();
    }
}
