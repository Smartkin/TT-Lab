using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class ChunkEditorView : BurnBridgeControl<ChunkEditorViewModel>
{
    public ChunkEditorView()
    {
        InitializeComponent();
    }
    
    private void UIElement_OnIsVisibleChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox is { IsVisible: true })
        {
            textBox.Focus();
        }
    }
}