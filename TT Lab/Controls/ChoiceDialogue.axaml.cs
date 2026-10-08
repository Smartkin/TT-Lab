using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using TT_Lab.Util;

namespace TT_Lab.Controls;

/// <summary>
/// Asks something with a few answers and a way out: the index of the answer picked, none when it's cancelled or closed. Without answers
/// it's a message to close
/// </summary>
public partial class ChoiceDialogue : Window
{
    private int? _answer;

    public ChoiceDialogue()
    {
        InitializeComponent();
    }

    public ChoiceDialogue(string title, string message, IReadOnlyList<string> answers) : this()
    {
        Title = title;
        Message.Text = message;
        for (var index = 0; index < answers.Count; index++)
        {
            var answer = index;
            AddButton(answers[index], index == 0, false).Click += (_, _) =>
            {
                _answer = answer;
                Close();
            };
        }

        AddButton(answers.Count == 0 ? "OK" : "Cancel", answers.Count == 0, true).Click += (_, _) => Close();
    }

    public static async Task<int?> Ask(string title, string message, IReadOnlyList<string> answers)
    {
        var dialogue = new ChoiceDialogue(title, message, answers);
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
        return dialogue._answer;
    }

    private Button AddButton(string text, bool isDefault, bool isCancel)
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
        Answers.Children.Add(button);
        return button;
    }
}
