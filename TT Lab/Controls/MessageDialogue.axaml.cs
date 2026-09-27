using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TT_Lab.Controls;

public partial class MessageDialogue : Window
{
    public MessageDialogue()
    {
        InitializeComponent();
    }

    public MessageDialogue(string title, string message) : this()
    {
        Title = title;
        DataContext = message;
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
