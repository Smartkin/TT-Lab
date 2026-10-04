using System;
using System.IO;
using System.Reactive.Disposables;
using Avalonia.Interactivity;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.Global;

namespace TT_Lab.Views.Editors.Global;

public partial class FontEditorView : DocumentBaseView<FontEditorViewModel>
{
    // Keeps state of its own about what it shows
    public override bool CanBeRecycled => false;

    public FontEditorView()
    {
        InitializeComponent();
        PageEditor.DragStarted += index => ViewModel?.BeginDrag(index);
        PageEditor.BoxDragged += box => ViewModel?.DragBox(box);
        PageEditor.DragEnded += () => ViewModel?.EndDrag();
        // The page scrolls in its own viewer, letting its bring into view requests through makes the document's scroll viewers jump
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }

    private async void ReplacePageClicked(object? sender, RoutedEventArgs e)
    {
        var file = await MiscUtils.GetFileFromDialogueAsync("Replace the page's image", "Image files", ["*.png"]);
        if (string.IsNullOrEmpty(file))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read);
            ViewModel?.ReplacePage(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.WriteLine($"Couldn't open {file}: {ex.Message}", Log.LogType.Error);
        }
    }
}
