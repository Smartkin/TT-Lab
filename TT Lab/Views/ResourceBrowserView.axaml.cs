using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TT_Lab.Assets;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class ResourceBrowserView : BurnBridgeWindow<ResourceBrowserViewModel>
{
    public ResourceBrowserView()
    {
        InitializeComponent();
        // A double click on a row links it like picking it and pressing Link does, one on the list's empty space or scroll bar doesn't
        Links.AddHandler(DoubleTappedEvent, LinkDoubleTapped, RoutingStrategies.Bubble);
    }

    private void LinkDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<ListBoxItem>(true) is not { DataContext: LabURI link } || ViewModel == null)
        {
            return;
        }

        ViewModel.SelectedLink = link;
        ViewModel.Link();
        e.Handled = true;
    }
}
