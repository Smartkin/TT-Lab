using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors.Graphics;

namespace TT_Lab.Views.Editors.Graphics;

public partial class TextureView : ReactiveUserControl<TextureViewModel>
{
    public TextureView()
    {
        InitializeComponent();
    }
}