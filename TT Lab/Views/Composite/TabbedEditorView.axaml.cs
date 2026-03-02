using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.Views.Composite;

public partial class TabbedEditorView : ReactiveUserControl<TabbedEditorViewModel>
{
    public TabbedEditorView()
    {
        InitializeComponent();
    }
}