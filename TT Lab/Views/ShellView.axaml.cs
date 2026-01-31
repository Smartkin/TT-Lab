using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
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
        Log.SetLogBox(LogText);

        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var textMateInstallation = LogText.InstallTextMate(registryOptions);
        LogText.Options = new TextEditorOptions
        {
            AllowScrollBelowDocument = false,
            EnableHyperlinks = true,
        };
        textMateInstallation.SetGrammar(registryOptions.GetScopeByExtension(".log"));
        LogText.TextChanged += LogTextOnTextChanged;
        
        HotKeyManager.SetHotKey(OpenProjectItem, new KeyGesture(Key.O, KeyModifiers.Control));
        HotKeyManager.SetHotKey(SaveProjectItem, new KeyGesture(Key.S, KeyModifiers.Control | KeyModifiers.Shift));
    }

    private void LogTextOnTextChanged(object? sender, EventArgs e)
    {
        LogText.ScrollToEnd();
    }

    private void UIElement_OnIsVisibleChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox is { IsVisible: true })
        {
            textBox.Focus();
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