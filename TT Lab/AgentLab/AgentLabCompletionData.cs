using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Twinsanity.AgentLab;

namespace TT_Lab.AgentLab;

/// <summary>
/// Suggestion shown in the behaviour editor's completion list
/// </summary>
public sealed class AgentLabCompletionData(AgentLabCompletionItem item) : ICompletionData
{
    // Colors match the ones the Dark+ theme highlights the same kind of code with
    private static readonly Dictionary<AgentLabCompletionKind, (string Badge, IBrush Brush)> Badges = new()
    {
        [AgentLabCompletionKind.Keyword] = ("K", new SolidColorBrush(Color.Parse("#569CD6"))),
        [AgentLabCompletionKind.Action] = ("A", new SolidColorBrush(Color.Parse("#DCDCAA"))),
        [AgentLabCompletionKind.Condition] = ("C", new SolidColorBrush(Color.Parse("#4EC9B0"))),
        [AgentLabCompletionKind.State] = ("S", new SolidColorBrush(Color.Parse("#C586C0"))),
        [AgentLabCompletionKind.ControlPacket] = ("P", new SolidColorBrush(Color.Parse("#9CDCFE"))),
        [AgentLabCompletionKind.Attribute] = ("@", new SolidColorBrush(Color.Parse("#D7BA7D"))),
        [AgentLabCompletionKind.Constant] = ("V", new SolidColorBrush(Color.Parse("#4FC1FF"))),
        [AgentLabCompletionKind.EnumValue] = ("E", new SolidColorBrush(Color.Parse("#B5CEA8"))),
        [AgentLabCompletionKind.Field] = ("F", new SolidColorBrush(Color.Parse("#9CDCFE"))),
        [AgentLabCompletionKind.Literal] = ("L", new SolidColorBrush(Color.Parse("#CE9178"))),
        [AgentLabCompletionKind.Behaviour] = ("B", new SolidColorBrush(Color.Parse("#4EC9B0")))
    };
    private static readonly (string Badge, IBrush Brush) DefaultBadge = ("?", Brushes.Gray);

    public AgentLabCompletionItem Item => item;

    public IImage? Image => null;

    public string Text => item.Text;

    // Made for every row that shows the suggestion, only once a row does (lists can hold every action): filtering makes a new row for
    // a suggestion while its old row still holds its control, and one control kept for both threw in the middle of a layout pass,
    // which left the list's panel broken and every layout pass after it failing
    public object Content => CreateContent();

    public object Description => item.Description;

    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, item.Text);
    }

    private Control CreateContent()
    {
        var (badge, brush) = Badges.GetValueOrDefault(item.Kind, DefaultBadge);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new Border
                {
                    Width = 16,
                    Height = 16,
                    CornerRadius = new Avalonia.CornerRadius(3),
                    Background = brush,
                    Child = new TextBlock
                    {
                        Text = badge,
                        FontSize = 10,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.Black,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                },
                new TextBlock { Text = item.Text, VerticalAlignment = VerticalAlignment.Center }
            }
        };
    }
}
