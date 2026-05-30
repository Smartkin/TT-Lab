using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.Views.Editors.Instance;

public partial class AiPathView : BurnBridgeControl<AiPathViewModel>
{
    public AiPathView()
    {
        InitializeComponent();
    }
}