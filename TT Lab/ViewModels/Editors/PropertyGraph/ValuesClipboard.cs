using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Splat;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

/// <summary>
/// Copied values are the system clipboard's text, the JSON of the values: copying text anywhere else replaces them. Without a window
/// (the tests) the text stays in TT Lab
/// </summary>
public static class ValuesClipboard
{
    private static string? _text;

    /// <summary>
    /// Values got copied, what shows whether they can be pasted looks again
    /// </summary>
    public static event Action? Copied;

    public static async Task<string?> ReadTextAsync()
    {
        return Clipboard() is { } clipboard ? await clipboard.TryGetTextAsync() : _text;
    }

    public static async Task WriteTextAsync(string text)
    {
        if (Clipboard() is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
        else
        {
            _text = text;
        }

        Copied?.Invoke();
    }

    private static IClipboard? Clipboard()
    {
        return Locator.Current.GetService<ILabManager>() is ShellViewModel shell && shell.GetView() is Window window ? TopLevel.GetTopLevel(window)?.Clipboard : null;
    }
}
