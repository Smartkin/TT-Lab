using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TT_Lab.Project;
using TT_Lab.Project.Repair;
using TT_Lab.Util;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Controls;

/// <summary>
/// Asks for the folder of a version's game files the repair reads its packages from again: the folder picked, none to skip the version
/// </summary>
public partial class RepairDiscDialogue : Window
{
    // The suggestion is a folder of the version's files the project or the preferences have, whose archive isn't one TT Lab knows, the problem why
    // the folder picked before wasn't taken
    internal sealed record Request(string ProjectName, GamePlatform Platform, string? Suggestion, string? Problem);

    private readonly Request? _request;
    private string? _answer;

    internal string? Answer => _answer;

    public RepairDiscDialogue()
    {
        InitializeComponent();
    }

    internal RepairDiscDialogue(Request request) : this()
    {
        _request = request;
        var name = RetailAssets.Describe(request.Platform);
        Message.Text = $"The repair of {request.ProjectName}'s {name} game packages reads the game's {name} files again: the folder with " +
                       (request.Platform == GamePlatform.Xbox ? "the disc's default.xbe and its Startup folder." : "the disc's SYSTEM.CNF and its Crash6 archive. TT Lab 1.1.0's builds wrote " +
                           "the built archive over the project's disc folder, the original disc's files are needed.");
        SkipButton.Content = $"Skip the {name} Version";
        Folder.Text = request.Suggestion ?? string.Empty;
        Problem.Text = request.Problem ?? string.Empty;
        Check();
    }

    internal static async Task<string?> Ask(Request request)
    {
        var dialogue = new RepairDiscDialogue(request);
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
        return dialogue._answer;
    }

    // What the folder looks like as it's typed, its archive's hashes are checked once it's picked
    private void Check()
    {
        if (_request == null)
        {
            return;
        }

        var check = RepairDiscs.Glance(_request.Platform, Folder.Text);
        Status.Text = string.IsNullOrWhiteSpace(Folder.Text) ? string.Empty
            : check.Verdict == RepairDiscs.Verdict.Missing ? check.Text
            : _request.Platform == GamePlatform.Xbox ? check.Text : "The folder has the PS2 disc's archive, which is checked once the folder is used.";
        UseButton.IsEnabled = check.Verdict != RepairDiscs.Verdict.Missing;
    }

    private void Folder_OnTextChanged(object? sender, TextChangedEventArgs e) => Check();

    private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            { AllowMultiple = false, Title = $"{RetailAssets.Describe(_request?.Platform ?? GamePlatform.PS2)} game files" });
        if (picked.Count > 0)
        {
            Folder.Text = picked[0].Path.LocalPath;
        }
    }

    private void UseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_request == null || RepairDiscs.Glance(_request.Platform, Folder.Text).Verdict == RepairDiscs.Verdict.Missing)
        {
            return;
        }

        _answer = Folder.Text;
        Close();
    }

    private void SkipButton_OnClick(object? sender, RoutedEventArgs e) => Close();
}
