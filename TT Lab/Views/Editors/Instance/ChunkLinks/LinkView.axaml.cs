using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels.Editors.Instance.ChunkLinks;

namespace TT_Lab.Views.Editors.Instance.ChunkLinks;

public partial class LinkView : BurnBridgeControl<LinkViewModel>
{
    public LinkView()
    {
        InitializeComponent();
    }
}