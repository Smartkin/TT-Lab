using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using TT_Lab.Assets;
using TT_Lab.Util;

namespace TT_Lab.Controls;

/// <summary>
/// Asks what a copy is named, checking the name as it's typed: the name and whether the rest get theirs all at once, none when cancelled
/// </summary>
public partial class NameDialogue : Window
{
    private readonly AssetRelocation.NameRequest? _request;
    private readonly List<Button> _confirming = [];
    private AssetRelocation.NameAnswer? _answer;

    public NameDialogue()
    {
        InitializeComponent();
    }

    public NameDialogue(AssetRelocation.NameRequest request) : this()
    {
        _request = request;
        Title = request.Title;
        Message.Text = request.Message;
        NameBox.Text = request.Suggested;
        NameBox.TextChanged += (_, _) => Check();
        _confirming.Add(AddButton("OK", true, false, () => Answer(false)));
        if (request.OffersAllAtOnce)
        {
            _confirming.Add(AddButton("Name the rest all at once", false, false, () => Answer(true)));
        }

        AddButton("Cancel", false, true, Close);
        Opened += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
        Check();
    }

    public static async Task<AssetRelocation.NameAnswer?> Ask(AssetRelocation.NameRequest request)
    {
        var dialogue = new NameDialogue(request);
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
        return dialogue._answer;
    }

    private void Check()
    {
        var problem = _request!.Validate(NameBox.Text ?? string.Empty);
        Problem.Text = problem == null ? string.Empty : $"{char.ToUpperInvariant(problem[0])}{problem[1..]}.";
        Problem.IsVisible = problem != null;
        foreach (var button in _confirming)
        {
            button.IsEnabled = problem == null;
        }
    }

    private void Answer(bool allAtOnce)
    {
        var name = NameBox.Text ?? string.Empty;
        if (_request!.Validate(name) != null)
        {
            return;
        }

        _answer = new AssetRelocation.NameAnswer(name, allAtOnce);
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
