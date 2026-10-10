using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using TT_Lab.Assets;
using TT_Lab.Util;

namespace TT_Lab.Controls;

// A row of Open Asset's results
public sealed class QuickOpenResult
{
    public required IAsset Asset { get; init; }
    public required string Name { get; init; }
    public required string Where { get; init; }
    public required Bitmap Icon { get; init; }
}

/// <summary>
/// Open Asset (Ctrl+Shift+O): what's typed finds the project's assets by a fuzzy search of their names (<see cref="AssetSearch"/>), Up and
/// Down pick one of the best matches, Enter or a double click opens it, Escape gives up. The assets are read and searched off the UI thread,
/// a search starts again on every change of what's typed and drops the one before it
/// </summary>
public partial class QuickOpenDialogue : Window
{
    // As many as the list shows, the rest only matches worse
    private const int MostShown = 100;
    private static readonly TimeSpan TypingPause = TimeSpan.FromMilliseconds(40);

    private readonly Task<AssetSearch> _search;
    private CancellationTokenSource? _searching;
    private List<QuickOpenResult> _shown = [];

    public QuickOpenDialogue() : this(Task.FromResult(AssetSearch.Of([])))
    {
    }

    public QuickOpenDialogue(Task<AssetSearch> search)
    {
        InitializeComponent();
        _search = search;
        Status.Text = "Reading the project's assets...";
        Query.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                SearchAgain();
            }
        };
        Results.DoubleTapped += (_, _) => Choose();
        // Ahead of the text box, which would take Up, Down, Enter and Escape for its caret
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => Query.Focus();
        _ = ShowCount();
    }

    /// <summary>
    /// The asset picked, none when the dialogue was closed without one
    /// </summary>
    public IAsset? Chosen { get; private set; }

    /// <summary>
    /// The search the last change of what's typed started, done once its results are shown
    /// </summary>
    internal Task Searched { get; private set; } = Task.CompletedTask;

    public static async Task<IAsset?> Ask()
    {
        var dialogue = new QuickOpenDialogue(Task.Run(() => AssetSearch.Of(AssetManager.Get().GetAssets())));
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
        return dialogue.Chosen;
    }

    private async Task ShowCount()
    {
        var search = await _search;
        if (string.IsNullOrWhiteSpace(Query.Text))
        {
            Status.Text = $"{search.Count} assets, the resources of level chunks are in their scenes";
        }
    }

    private void SearchAgain()
    {
        _searching?.Cancel();
        var searching = _searching = new CancellationTokenSource();
        Searched = Search(Query.Text ?? string.Empty, searching.Token);
    }

    private async Task Search(string query, CancellationToken token)
    {
        try
        {
            await Task.Delay(TypingPause, token);
            var search = await _search;
            var found = await Task.Run(() => search.Search(query, MostShown, token), token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            Show(found, search.Count, query);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Show(IReadOnlyList<AssetSearch.Found> found, int count, string query)
    {
        _shown = found.Select(match => new QuickOpenResult
        {
            Asset = match.Entry.Asset,
            Name = match.Entry.Name,
            Where = match.Entry.Where,
            Icon = MiscUtils.GetLabIcon(System.IO.Path.GetFileNameWithoutExtension(match.Entry.Asset.IconPath)),
        }).ToList();
        Results.ItemsSource = _shown;
        Results.SelectedIndex = _shown.Count > 0 ? 0 : -1;
        Status.Text = string.IsNullOrWhiteSpace(query) ? $"{count} assets, the resources of level chunks are in their scenes"
            : _shown.Count == 0 ? "Nothing matches, every character typed has to be in the name or its package and folders in that order"
            : _shown.Count == MostShown ? $"The best {MostShown} matches" : $"{_shown.Count} matches";
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Move(1);
                break;
            case Key.Up:
                Move(-1);
                break;
            case Key.PageDown:
                Move(10);
                break;
            case Key.PageUp:
                Move(-10);
                break;
            case Key.Enter:
                Choose();
                break;
            case Key.Escape:
                Close();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void Move(int rows)
    {
        if (_shown.Count == 0)
        {
            return;
        }

        Results.SelectedIndex = Math.Clamp(Results.SelectedIndex + rows, 0, _shown.Count - 1);
        Results.ScrollIntoView(Results.SelectedIndex);
    }

    private void Choose()
    {
        if (Results.SelectedItem is not QuickOpenResult result)
        {
            return;
        }

        Chosen = result.Asset;
        Close();
    }
}
