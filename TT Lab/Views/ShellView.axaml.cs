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
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.Views;

public partial class ShellView : BurnBridgeWindow<ShellViewModel>
{
    public ShellView() : this(null)
    {
    }

    // The dock's theme binds to its layout once the window's XAML gets styled, which logged binding errors while there was no view model yet
    public ShellView(ILabManager? viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        HotKeyManager.SetHotKey(OpenProjectItem, new KeyGesture(Key.O, KeyModifiers.Control));
        HotKeyManager.SetHotKey(SaveProjectItem, new KeyGesture(Key.S, KeyModifiers.Control | KeyModifiers.Shift));
        HotKeyManager.SetHotKey(ReopenClosedEditorItem, new KeyGesture(Key.T, KeyModifiers.Control | KeyModifiers.Shift));
    }

    // Closing again once the unsaved changes got saved or discarded goes through
    private bool _unsavedChangesHandled;
    private bool _askingAboutUnsavedChanges;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel)
        {
            return;
        }

        // Editors with unsaved changes are asked about first, once for all of them, like closing an editor asks about its own
        if (!_unsavedChangesHandled && ViewModel?.EditorsViewModel.GetUnsavedEditors().Count > 0)
        {
            e.Cancel = true;
            AskAboutUnsavedChanges();
            return;
        }

        // Floating dock windows are closed right after this, so the layout has to be captured while they still exist
        ViewModel?.SaveLayoutOnExit();
    }

    private async void AskAboutUnsavedChanges()
    {
        if (_askingAboutUnsavedChanges || ViewModel == null)
        {
            return;
        }

        _askingAboutUnsavedChanges = true;
        try
        {
            if (!await ViewModel.EditorsViewModel.SaveOrDiscardUnsavedChanges())
            {
                return;
            }

            _unsavedChangesHandled = true;
            Close();
        }
        catch (Exception exception)
        {
            Log.WriteLine($"Couldn't ask about the unsaved changes: {exception}", Log.LogType.Error);
        }
        finally
        {
            _askingAboutUnsavedChanges = false;
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