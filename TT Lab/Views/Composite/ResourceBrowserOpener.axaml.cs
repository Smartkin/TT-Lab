using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Caliburn.Micro;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.Views.Composite;

public partial class ResourceBrowserOpener : UserControl
{
    public static readonly StyledProperty<Type> BrowseTypeProperty = AvaloniaProperty.Register<ResourceBrowserOpener, Type>(nameof(BrowseType), typeof(IAsset));
    // DependencyProperty.Register(
    // nameof(BrowseType), typeof(Type), typeof(ResourceBrowserOpener), new PropertyMetadata(typeof(IAsset)));

    [Description("Type of asset to browse"), Category("Common Properties")]
    public Type BrowseType
    {
        get => GetValue(BrowseTypeProperty);
        set => SetValue(BrowseTypeProperty, value);
    }
    
    // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<object> BrowserNameProperty = AvaloniaProperty.Register<ResourceBrowserOpener, object>(nameof(BrowserName), "Browser", false, BindingMode.TwoWay);
    // DependencyProperty.Register(nameof(BrowserName), typeof(object), typeof(ResourceBrowserOpener),
    //     new FrameworkPropertyMetadata("Browser", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    
    [Description("Input text."), Category("Common Properties")]
    public object BrowserName
    {
        get => GetValue(BrowserNameProperty);
        set => SetValue(BrowserNameProperty, value);
    }

    public static readonly StyledProperty<LabURI> LinkedResourceProperty = AvaloniaProperty.Register<ResourceBrowserOpener, LabURI>(nameof(LinkedResource), LabURI.Empty, false, BindingMode.TwoWay);
    // DependencyProperty.Register(
    // nameof(LinkedResource), typeof(LabURI), typeof(ResourceBrowserOpener), new FrameworkPropertyMetadata(LabURI.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public LabURI LinkedResource
    {
        get => GetValue(LinkedResourceProperty);
        set => SetValue(LinkedResourceProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<LabURI>?> ResourcesToBrowseProperty =
        AvaloniaProperty.Register<ResourceBrowserOpener, ObservableCollection<LabURI>?>(nameof(ResourcesToBrowse));
    // DependencyProperty.Register(
    // nameof(ResourcesToBrowse), typeof(ObservableCollection<LabURI>), typeof(ResourceBrowserOpener), new PropertyMetadata(default(ObservableCollection<LabURI>?)));

    public ObservableCollection<LabURI>? ResourcesToBrowse
    {
        get => GetValue(ResourcesToBrowseProperty);
        set => SetValue(ResourcesToBrowseProperty, value);
    }

    public static readonly StyledProperty<bool> IncludeEmptyResourceInBrowseProperty = AvaloniaProperty.Register<ResourceBrowserOpener, bool>(nameof(IncludeEmptyResourceInBrowse));
    // DependencyProperty.Register(
    // nameof(IncludeEmptyResourceInBrowse), typeof(bool), typeof(ResourceBrowserOpener), new PropertyMetadata(false));

    public bool IncludeEmptyResourceInBrowse
    {
        get => GetValue(IncludeEmptyResourceInBrowseProperty);
        set => SetValue(IncludeEmptyResourceInBrowseProperty, value);
    }

    public static readonly StyledProperty<ICommand?> FilterCommandProperty = AvaloniaProperty.Register<ResourceBrowserOpener, ICommand?>(nameof(FilterCommand));
    // DependencyProperty.Register(
    // nameof(FilterCommand), typeof(ICommand), typeof(ResourceBrowserOpener), new PropertyMetadata(default(ICommand)));

    public ICommand? FilterCommand
    {
        get => GetValue(FilterCommandProperty);
        set => SetValue(FilterCommandProperty, value);
    }
    
    public ResourceBrowserOpener()
    {
        InitializeComponent();
    }

    private async void OnOpenBrowser(object sender, RoutedEventArgs e)
    {
        if (ResourcesToBrowse != null && IncludeEmptyResourceInBrowse)
        {
            ResourcesToBrowse.Add(LabURI.Empty);
        }
        
        ResourceBrowserViewModel linkBrowser;
        if (ResourcesToBrowse != null && BrowseType != typeof(IAsset))
        {
            linkBrowser = new ResourceBrowserViewModel(BrowseType, ResourcesToBrowse, LinkedResource);
        }
        else if (ResourcesToBrowse != null)
        {
            linkBrowser = new ResourceBrowserViewModel(ResourcesToBrowse, LinkedResource);
        }
        else
        {
            linkBrowser = new ResourceBrowserViewModel(BrowseType, LinkedResource);
        }

        if (FilterCommand != null)
        {
            linkBrowser.Filter(FilterCommand);
        }
        
        // var windowManager = Locator.Current.GetService<IWindowManager>()!;
        // var linkBrowserWindow = await windowManager.ShowDialogAsync(linkBrowser);
        var linkBrowserDialogue = new ResourceBrowserView
        {
            DataContext = linkBrowser
        };
        var result = await linkBrowserDialogue.ShowDialog<bool?>((Window)((ShellViewModel)Locator.Current.GetService<ILabManager>()!).GetView());
        if (result.HasValue && result.Value)
        {
            LinkedResource = linkBrowser.SelectedLink;
        }
    }

    private void OnOpenLink(object sender, RoutedEventArgs e)
    {
        if (LinkedResource == LabURI.Empty)
        {
            return;
        }
        
        Locator.Current.GetService<ILabManager>()!.OpenEditor(AssetManager.Get().GetAsset(LinkedResource));
    }
}