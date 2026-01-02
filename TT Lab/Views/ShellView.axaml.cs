using Avalonia;
using Avalonia.Controls;

namespace TT_Lab.Views;

public partial class ShellView : Window
{
    public ShellView()
    {
        InitializeComponent();
        Log.SetLogBox(LogText);
    }

    // private void AdonisWindow_SizeChanged(System.Object sender, System.Windows.SizeChangedEventArgs e)
    // {
    //     IoC.Get<OgreWindowManager>().NotifyResizeAllWindows();
    // }
    //
    // private void AdonisWindow_LocationChanged(System.Object sender, System.EventArgs e)
    // {
    //     IoC.Get<OgreWindowManager>().NotifyResizeAllWindows();
    // }

    private void UIElement_OnIsVisibleChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox is { IsVisible: true })
        {
            textBox.Focus();
        }
    }
}