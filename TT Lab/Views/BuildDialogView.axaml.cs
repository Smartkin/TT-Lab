using System;
using Avalonia.Controls;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class BuildDialogView : Window
{
    private Action? _close;

    public BuildDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is BuildDialogViewModel viewModel)
            {
                _close ??= Close;
                viewModel.CloseRequested += _close;
            }
        };
    }
}
