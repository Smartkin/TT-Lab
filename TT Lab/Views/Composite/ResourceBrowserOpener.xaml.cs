using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Caliburn.Micro;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.Views.Composite;

public partial class ResourceBrowserOpener : UserControl
{
    public static readonly DependencyProperty BrowseTypeProperty = DependencyProperty.Register(
        nameof(BrowseType), typeof(Type), typeof(ResourceBrowserOpener), new PropertyMetadata(typeof(IAsset)));

    [Description("Type of asset to browse"), Category("Common Properties")]
    public Type BrowseType
    {
        get => (Type)GetValue(BrowseTypeProperty);
        set => SetValue(BrowseTypeProperty, value);
    }
    
    // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty BrowserNameProperty =
        DependencyProperty.Register(nameof(BrowserName), typeof(object), typeof(ResourceBrowserOpener),
            new FrameworkPropertyMetadata("Browser", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    
    [Description("Input text."), Category("Common Properties")]
    public object BrowserName
    {
        get => GetValue(BrowserNameProperty);
        set => SetValue(BrowserNameProperty, value);
    }

    public static readonly DependencyProperty LinkedResourceProperty = DependencyProperty.Register(
        nameof(LinkedResource), typeof(LabURI), typeof(ResourceBrowserOpener), new FrameworkPropertyMetadata(LabURI.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public LabURI LinkedResource
    {
        get => (LabURI)GetValue(LinkedResourceProperty);
        set => SetValue(LinkedResourceProperty, value);
    }

    public static readonly DependencyProperty ResourcesToBrowseProperty = DependencyProperty.Register(
        nameof(ResourcesToBrowse), typeof(ObservableCollection<LabURI>), typeof(ResourceBrowserOpener), new PropertyMetadata(default(ObservableCollection<LabURI>?)));

    public ObservableCollection<LabURI>? ResourcesToBrowse
    {
        get => (ObservableCollection<LabURI>?)GetValue(ResourcesToBrowseProperty);
        set => SetValue(ResourcesToBrowseProperty, value);
    }

    public static readonly DependencyProperty IncludeEmptyResourceInBrowseProperty = DependencyProperty.Register(
        nameof(IncludeEmptyResourceInBrowse), typeof(bool), typeof(ResourceBrowserOpener), new PropertyMetadata(false));

    public bool IncludeEmptyResourceInBrowse
    {
        get => (bool)GetValue(IncludeEmptyResourceInBrowseProperty);
        set => SetValue(IncludeEmptyResourceInBrowseProperty, value);
    }

    public static readonly DependencyProperty FilterCommandProperty = DependencyProperty.Register(
        nameof(FilterCommand), typeof(ICommand), typeof(ResourceBrowserOpener), new PropertyMetadata(default(ICommand)));

    public ICommand? FilterCommand
    {
        get => (ICommand?)GetValue(FilterCommandProperty);
        set => SetValue(FilterCommandProperty, value);
    }
    
    public ResourceBrowserOpener()
    {
        InitializeComponent();
    }

    private void OnOpenBrowser(object sender, RoutedEventArgs e)
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
        
        var windowManager = IoC.Get<IWindowManager>();
        var linkBrowserWindow = windowManager.ShowDialogAsync(linkBrowser);
        linkBrowserWindow.Wait();
        var result = linkBrowserWindow.Result;
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
        
        IoC.Get<ILabManager>().OpenEditor(AssetManager.Get().GetAsset(LinkedResource));
    }
}