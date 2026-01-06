using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class CreateAssetView : BurnBridgeWindow<CreateAssetViewModel>
{
    public CreateAssetView()
    {
        InitializeComponent();
    }
    
    private void AssetName_OnLoaded(object sender, RoutedEventArgs e)
    {
        AssetName.Focus();
        ResetTextSelection();
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ResetTextSelection();
    }

    private void ResetTextSelection()
    {
        AssetName.CaretIndex = AssetName.Text.Length;
        AssetName.SelectAll();
    }
}