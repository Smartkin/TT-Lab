using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Project;
using TT_Lab.Util;

namespace TT_Lab.Controls;

/// <summary>
/// A folder of the project tree a <see cref="FolderPickerDialogue"/> shows, and why what's moved can't go into it
/// </summary>
public sealed class FolderChoice
{
    public required Folder Folder { get; init; }
    public required string Name { get; init; }
    public required Bitmap Icon { get; init; }
    public string? Problem { get; init; }
    public bool IsExpanded { get; set; }
    public List<FolderChoice> Children { get; } = [];

    public bool CanTake => Problem == null;
    public double Opacity => CanTake ? 1.0 : 0.5;
}

/// <summary>
/// Asks which folder of the project tree something moves into: the tree's folders, the ones it can't go into grayed with the reason
/// shown when picked. None when cancelled
/// </summary>
public partial class FolderPickerDialogue : Window
{
    private readonly Button? _ok;
    private Folder? _answer;

    public FolderPickerDialogue()
    {
        InitializeComponent();
    }

    public FolderPickerDialogue(IAsset item, IReadOnlyList<FolderChoice> roots) : this()
    {
        Title = $"Move {item.Alias} to";
        Message.Text = $"The folder {item.Alias} moves into:";
        Folders.ItemsSource = roots;
        Folders.SelectionChanged += (_, _) => Check();
        Folders.AddHandler(DoubleTappedEvent, (_, _) => Answer(), RoutingStrategies.Bubble);
        _ok = AddButton("Move", true, false, Answer);
        AddButton("Cancel", false, true, Close);
        Check();
    }

    public static async Task<Folder?> Ask(IAsset item)
    {
        var dialogue = new FolderPickerDialogue(item, Choices(item));
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
        return dialogue._answer;
    }

    /// <summary>
    /// The tree's folders but chunks' and the disc's, each with why the item can't go into it. The folders down to where the item is are
    /// expanded
    /// </summary>
    internal static List<FolderChoice> Choices(IAsset item)
    {
        var manager = Locator.Current.GetService<ProjectManager>()!;
        var destinations = new AssetRelocation.Destinations(item);
        var assets = AssetManager.Get();
        var current = item is Folder folder && assets.DoesAssetExist(folder.Parent) ? assets.GetAsset<Folder>(folder.Parent) : AssetRelocation.FolderListing(item);
        var expanded = new HashSet<Folder>();
        for (var parent = current; parent != null; parent = parent.Parent != LabURI.Empty && assets.DoesAssetExist(parent.Parent) ? assets.GetAsset<Folder>(parent.Parent) : null)
        {
            expanded.Add(parent);
        }

        return manager.FullProjectTree.Select(row => row.Asset).OfType<Folder>().Where(Shown).Select(Choice).ToList();

        FolderChoice Choice(Folder shown)
        {
            var choice = new FolderChoice
            {
                Folder = shown,
                Name = shown.Alias,
                Icon = MiscUtils.GetLabIcon(System.IO.Path.GetFileNameWithoutExtension(shown.IconPath)),
                Problem = destinations.WhyNot(shown),
                IsExpanded = expanded.Contains(shown) || shown.Parent == LabURI.Empty
            };
            choice.Children.AddRange(shown.Children.Where(assets.DoesAssetExist).Select(assets.GetAsset).OfType<Folder>().Where(Shown)
                .OrderBy(child => child.Alias, StringComparer.OrdinalIgnoreCase).Select(Choice));
            return choice;
        }

        static bool Shown(Folder shown) => !shown.Mark.HasFlag(FolderMark.IsChunk) && !shown.Mark.HasFlag(FolderMark.Disc);
    }

    private void Check()
    {
        var choice = Folders.SelectedItem as FolderChoice;
        Problem.Text = choice?.Problem is { } problem ? $"It can't go into {choice.Name}: {problem}." : string.Empty;
        if (_ok != null)
        {
            _ok.IsEnabled = choice is { CanTake: true };
        }
    }

    private void Answer()
    {
        if (Folders.SelectedItem is not FolderChoice { CanTake: true } choice)
        {
            return;
        }

        _answer = choice.Folder;
        Close();
    }

    private Button AddButton(string text, bool isDefault, bool isCancel, Action click)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 90,
            Margin = new Avalonia.Thickness(5),
            Padding = new Avalonia.Thickness(8, 3),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsDefault = isDefault,
            IsCancel = isCancel,
        };
        button.Click += (_, _) => click();
        Answers.Children.Add(button);
        return button;
    }
}
