using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using TT_Lab.AgentLab;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Tests.Editor;

public class AgentLabCodeCompletionTests
{
    private const string Script =
        "behaviour TEST {\n" +
        "   state State_0() {\n" +
        "      if Else(0) >= 0.5 {\n" +
        "         interval = 0;\n" +
        "         unknown = false;\n" +
        "         |\n" +
        "      }\n" +
        "   }\n" +
        "   state State_1() {\n" +
        "   }\n" +
        "}\n";

    private sealed class Fixture : IDisposable
    {
        public Fixture(string script = Script)
        {
            Editor = new TextEditor { Document = new TextDocument(script.Replace("|", string.Empty)) };
            Window = new Window { Content = Editor, Width = 800, Height = 600 };
            Window.Show();
            Completion = new AgentLabCodeCompletion(Editor, "ActionDefinitionsPs2.lab");
            Editor.TextArea.Focus();
            Editor.CaretOffset = script.IndexOf('|');
        }

        public Window Window { get; }
        public TextEditor Editor { get; }
        public AgentLabCodeCompletion Completion { get; }

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Window.KeyTextInput(character.ToString());
            }
        }

        public List<string> Suggestions => Completion.CompletionWindow!.CompletionList.CompletionData.Select(data => data.Text).ToList();

        public void Dispose()
        {
            Completion.Dispose();
            Window.Close();
        }
    }

    [AvaloniaFact]
    public void TypingAWordShowsMatchingSuggestions()
    {
        using var fixture = new Fixture();

        fixture.Type("Des");

        Assert.NotNull(fixture.Completion.CompletionWindow);
        Assert.Contains("DestroyMe", fixture.Suggestions);
        Assert.StartsWith("Des", fixture.Completion.CompletionWindow!.CompletionList.SelectedItem!.Text);
    }

    [AvaloniaFact]
    public void EnterInsertsTheSelectedSuggestion()
    {
        using var fixture = new Fixture();

        fixture.Type("DestroyM");
        fixture.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.Contains("         DestroyMe\n", fixture.Editor.Text);
        Assert.Null(fixture.Completion.CompletionWindow);
    }

    [AvaloniaFact]
    public void OpeningParenthesisCompletesAndShowsTheParameters()
    {
        using var fixture = new Fixture();

        fixture.Type("DoAnim(");

        Assert.Contains("DoAnimation(", fixture.Editor.Text);
        Assert.NotNull(fixture.Completion.SignatureWindow);

        fixture.Type("1, ");
        Assert.NotNull(fixture.Completion.SignatureWindow);

        fixture.Type("2, 3, 4, 5, 6);");
        Assert.Null(fixture.Completion.SignatureWindow);
    }

    [AvaloniaFact]
    public void ExecuteSuggestsStates()
    {
        using var fixture = new Fixture();

        fixture.Type("execute ");

        Assert.NotNull(fixture.Completion.CompletionWindow);
        Assert.Equal(["State_0", "State_1"], fixture.Suggestions);
    }

    [AvaloniaFact]
    public void CommentsGetNoSuggestions()
    {
        using var fixture = new Fixture();

        fixture.Type("// Des");

        Assert.Null(fixture.Completion.CompletionWindow);
    }

    [AvaloniaFact]
    public void ControlSpaceShowsSuggestions()
    {
        using var fixture = new Fixture();

        fixture.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);

        Assert.NotNull(fixture.Completion.CompletionWindow);
        Assert.Contains("execute", fixture.Suggestions);
        // The shortcut doesn't type a space
        Assert.Equal("         ", fixture.Editor.Document.GetText(fixture.Editor.Document.GetLineByOffset(fixture.Editor.CaretOffset)));
    }

    // Grabbing the scroll bar to drag it used to insert the selected suggestion
    [AvaloniaFact]
    public void ScrollBarDoesntInsertSuggestions()
    {
        using var fixture = new Fixture();
        fixture.Type("D");
        var list = fixture.Completion.CompletionWindow!.CompletionList;
        Dispatcher.UIThread.RunJobs();
        var scrollBar = list.GetVisualDescendants().OfType<ScrollBar>().First(bar => bar.Orientation == Avalonia.Layout.Orientation.Vertical);
        var textBefore = fixture.Editor.Text;

        Click(scrollBar);

        Assert.Equal(textBefore, fixture.Editor.Text);
        Assert.NotNull(fixture.Completion.CompletionWindow);
    }

    [AvaloniaFact]
    public void ClickingASuggestionInsertsIt()
    {
        using var fixture = new Fixture();
        fixture.Type("Des");
        var list = fixture.Completion.CompletionWindow!.CompletionList;
        Dispatcher.UIThread.RunJobs();
        var item = list.ListBox!.GetVisualDescendants().OfType<ListBoxItem>().First(candidate => ((ICompletionData)candidate.DataContext!).Text == "DestroyMe");

        Click(item);

        Assert.Contains("         DestroyMe\n", fixture.Editor.Text);
        Assert.Null(fixture.Completion.CompletionWindow);
    }

    private static void Click(Control target)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var root = (Visual)target.GetVisualRoot()!;
        var position = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root) ?? default;
        target.RaiseEvent(new PointerPressedEventArgs(target, pointer, root, position, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        target.RaiseEvent(new PointerReleasedEventArgs(target, pointer, root, position, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
    }

    // Lists opened before anything was typed kept the arrow keys from moving past their first suggestion
    [AvaloniaFact]
    public void ArrowKeysWorkBeforeTyping()
    {
        using var fixture = new Fixture(Script.Replace("|", string.Empty).Replace("   state State_1()", "   |\n   state State_1()"));
        fixture.Type("[");
        Dispatcher.UIThread.RunJobs();
        var list = fixture.Completion.CompletionWindow!.CompletionList;
        var suggestions = fixture.Suggestions;
        Assert.Equal(suggestions[0], list.SelectedItem?.Text);

        fixture.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        fixture.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal(suggestions[2], list.SelectedItem?.Text);

        fixture.Window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal(suggestions[1], list.SelectedItem?.Text);

        fixture.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Contains($"[{suggestions[1]}", fixture.Editor.Text);
    }
}
