using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using TT_Lab.Project;
using TT_Lab.Util;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Controls;

/// <summary>
/// Asks for the folders of the game's files a project without the game's packages is unpacked from: the versions its packages need, at
/// least one when they need none. The folders by version, none when cancelled
/// </summary>
public partial class RetailDiscsDialogue : Window
{
    /// <param name="Check">What the project lacks and needs</param>
    /// <param name="Found">The folders the preferences have the game's files in</param>
    internal sealed record Request(RetailAssets.Check Check, IReadOnlyDictionary<GamePlatform, string> Found);

    private readonly Request? _request;
    private readonly Dictionary<GamePlatform, (TextBox Folder, TextBlock Status)> _rows = new();
    private Button? _unpack;
    private IReadOnlyDictionary<GamePlatform, string>? _answer;

    public RetailDiscsDialogue()
    {
        InitializeComponent();
    }

    internal RetailDiscsDialogue(Request request) : this()
    {
        _request = request;
        var missing = request.Check.Versions.Where(version => version.Missing).ToList();
        var noneRequired = missing.All(version => !version.Required);
        Message.Text = $"{request.Check.ProjectName} has its own packages but not the game's, which a shared project can't have. TT Lab unpacks them from the game's files "
                       + "the way creating a project does, copying the files into the project's disc folder. "
                       + (noneRequired
                           ? "Its packages don't use the game's assets, unpack at least one version of the game."
                           : $"Its packages use the {string.Join(" and ", missing.Where(version => version.Required).Select(version => RetailAssets.Describe(version.Platform)))} version's assets.");
        foreach (var version in missing)
        {
            AddRow(version, request.Found.GetValueOrDefault(version.Platform));
        }

        _unpack = AddButton("Unpack", true, false, Answer);
        AddButton("Cancel", false, true, Close);
        Check();
    }

    internal static async Task<IReadOnlyDictionary<GamePlatform, string>?> Ask(Request request)
    {
        var dialogue = new RetailDiscsDialogue(request);
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
        return dialogue._answer;
    }

    private void AddRow(RetailAssets.Version version, string? found)
    {
        var name = RetailAssets.Describe(version.Platform);
        var title = new TextBlock
        {
            Text = $"{name} game files ({(version.Required ? "needed" : "optional")}): the folder with the {(version.Platform == GamePlatform.Xbox ? "disc's default.xbe" : "disc's SYSTEM.CNF")}",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        var folder = new TextBox { Text = found ?? string.Empty, MinWidth = 420 };
        var browse = new Button { Content = "Browse...", Margin = new Avalonia.Thickness(6, 0, 0, 0), Padding = new Avalonia.Thickness(8, 3) };
        var status = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brushes.Gray };
        folder.TextChanged += (_, _) => Check();
        browse.Click += async (_, _) =>
        {
            var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false, Title = $"{name} game files" });
            if (picked.Count > 0)
            {
                folder.Text = picked[0].Path.LocalPath;
            }
        };
        var line = new DockPanel();
        DockPanel.SetDock(browse, Avalonia.Controls.Dock.Right);
        line.Children.Add(browse);
        line.Children.Add(folder);
        var row = new StackPanel { Spacing = 3 };
        row.Children.Add(title);
        row.Children.Add(line);
        row.Children.Add(status);
        Versions.Children.Add(row);
        _rows[version.Platform] = (folder, status);
    }

    // The folders given that have the game's files, and whether they do: every needed version, at least one when none is needed
    private Dictionary<GamePlatform, string> Folders()
    {
        return _rows.Where(row => RetailAssets.IsDisc(row.Key, row.Value.Folder.Text)).ToDictionary(row => row.Key, row => row.Value.Folder.Text!);
    }

    private void Check()
    {
        foreach (var (platform, (folder, status)) in _rows)
        {
            status.Text = string.IsNullOrWhiteSpace(folder.Text) ? "Not unpacked."
                : RetailAssets.IsDisc(platform, folder.Text) ? "The game's files are there."
                : $"The folder doesn't have the {(platform == GamePlatform.Xbox ? "Xbox disc's default.xbe" : "PS2 disc's SYSTEM.CNF")}.";
        }

        var folders = Folders();
        var suffice = _request != null && RetailAssets.Suffice(_request.Check, folders)
                                       && _rows.All(row => string.IsNullOrWhiteSpace(row.Value.Folder.Text) || folders.ContainsKey(row.Key));
        Problem.Text = suffice ? string.Empty : "Give the folders of the game's files needed, the ones given have to have the game's files.";
        if (_unpack != null)
        {
            _unpack.IsEnabled = suffice;
        }
    }

    private void Answer()
    {
        var folders = Folders();
        if (_request == null || !RetailAssets.Suffice(_request.Check, folders))
        {
            return;
        }

        _answer = folders;
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
