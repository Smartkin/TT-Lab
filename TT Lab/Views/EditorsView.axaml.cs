using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class EditorsView : ReactiveUserControl<EditorsViewModel>
{
    public EditorsView()
    {
        InitializeComponent();
    }
}