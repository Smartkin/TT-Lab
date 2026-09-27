using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Dock.Model.Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Splat;
using TextMateSharp.Grammars;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class ShellView : BurnBridgeWindow<ShellViewModel>
{
    public ShellView()
    {
        InitializeComponent();

        HotKeyManager.SetHotKey(OpenProjectItem, new KeyGesture(Key.O, KeyModifiers.Control));
        HotKeyManager.SetHotKey(SaveProjectItem, new KeyGesture(Key.S, KeyModifiers.Control | KeyModifiers.Shift));
        HotKeyManager.SetHotKey(ReopenClosedEditorItem, new KeyGesture(Key.T, KeyModifiers.Control | KeyModifiers.Shift));
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // Floating dock windows are closed right after this, so the layout has to be captured while they still exist
        if (!e.Cancel)
        {
            ViewModel?.SaveLayoutOnExit();
        }
    }

    private void About_OnClick(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutView
        {
            DataContext = Locator.Current.GetService<AboutViewModel>(),
        };
        aboutWindow.ShowDialog(this);
    }

    private void Preferences_OnClick(object? sender, RoutedEventArgs e)
    {
        var preferencesWindow = new PreferencesView
        {
            DataContext = Locator.Current.GetService<PreferencesViewModel>()
        };
        preferencesWindow.ShowDialog(this);
    }

    private void CreateProject_OnClick(object? sender, RoutedEventArgs e)
    {
        var openProjectWindow = new ProjectCreationView
        {
            DataContext = Locator.Current.GetService<ProjectCreationViewModel>()
        };
        openProjectWindow.ShowDialog(this);
    }
}