using System.IO;
using System.Reactive.Disposables;
using Avalonia.Interactivity;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.Global;

namespace TT_Lab.Views.Editors.Global;

public partial class PsmEditorView : DocumentBaseView<PsmEditorViewModel>
{
    // Keeps state of its own about what it shows
    public override bool CanBeRecycled => false;

    public PsmEditorView()
    {
        InitializeComponent();
        // The picture scrolls in its own viewer, letting its bring into view requests through makes the document's scroll viewers jump
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }

    private async void ReplacePictureClicked(object? sender, RoutedEventArgs e)
    {
        var file = await MiscUtils.GetFileFromDialogueAsync("Replace the picture", "Image files", ["*.png", "*.jpg", "*.jpeg", "*.bmp"]);
        if (string.IsNullOrEmpty(file))
        {
            return;
        }

        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read);
        ViewModel?.ReplacePicture(stream);
    }

    private async void ReplacePartClicked(object? sender, RoutedEventArgs e)
    {
        var file = await MiscUtils.GetFileFromDialogueAsync("Replace the picked part", "Image files", ["*.png", "*.jpg", "*.jpeg", "*.bmp"]);
        if (string.IsNullOrEmpty(file))
        {
            return;
        }

        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read);
        ViewModel?.ReplacePart(stream);
    }

    private async void SavePictureClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var file = await MiscUtils.GetSaveFileFromDialogueAsync("Save the picture", "PNG image", ["*.png"], $"{ViewModel.Name}.png", "png");
        if (!string.IsNullOrEmpty(file))
        {
            await File.WriteAllBytesAsync(file, ViewModel.GetPicturePng());
        }
    }

    private async void SavePartClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedPart is not { } part)
        {
            return;
        }

        var file = await MiscUtils.GetSaveFileFromDialogueAsync("Save the part", "PNG image", ["*.png"], $"{part.Name}.png", "png");
        if (!string.IsNullOrEmpty(file) && ViewModel.GetPartPng() is { } png)
        {
            await File.WriteAllBytesAsync(file, png);
        }
    }
}
