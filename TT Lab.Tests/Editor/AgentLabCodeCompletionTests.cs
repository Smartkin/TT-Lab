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
using AvaloniaEdit.Rendering;
using TT_Lab.AgentLab;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.AgentLab;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Tests.Editor;

public class AgentLabCodeCompletionTests
{
    private const string Script =
        "behaviour TEST {\n" +
        "   state State_0() {\n" +
        "      if Else(0) > 0.5 {\n" +
        "         window = 0;\n" +
        "         restart = false;\n" +
        "         |\n" +
        "      }\n" +
        "   }\n" +
        "   state State_1() {\n" +
        "   }\n" +
        "}\n";

    private sealed class Fixture : IDisposable
    {
        public Fixture(string script = Script, Func<IEnumerable<AgentLabCompletionItem>>? behaviours = null)
        {
            Editor = new TextEditor { Document = new TextDocument(script.Replace("|", string.Empty)) };
            Window = new Window { Content = Editor, Width = 800, Height = 600 };
            Window.Show();
            Completion = new AgentLabCodeCompletion(Editor, "ActionDefinitionsPs2.lab", false, behaviours);
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

    // Typing filters the list again and again, Escape and more typing make a new window over the same suggestions: a suggestion kept
    // one control for its row, filtering made a new row for it while its old row still held it, and the control put in both threw in
    // the middle of a layout pass, which broke the list's panel and failed every layout pass after it (the whole UI stopped drawing)
    [AvaloniaFact]
    public void FilteringAndNewWindowsGiveEveryRowItsOwnControls()
    {
        using var fixture = new Fixture();
        var errors = new List<string>();
        void Caught(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            errors.Add(e.Exception.Message);
            e.Handled = true;
        }

        void Press(string keys)
        {
            foreach (var key in keys)
            {
                if (key == '<')
                {
                    fixture.Window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
                }
                else if (key == '!')
                {
                    fixture.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                }
                else
                {
                    fixture.Type(key.ToString());
                }

                // The list selects its best match in posted work, which lays the window out again
                for (var i = 0; i < 3; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                }

                Dispatcher.UIThread.RunJobs();
            }
        }

        Dispatcher.UIThread.UnhandledException += Caught;
        try
        {
            Press("Set<<<Do<e<<SetV<<<<!Set<D");
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Caught;
        }

        Assert.Empty(errors);
        var data = fixture.Completion.CompletionWindow!.CompletionList.CompletionData[0];
        Assert.NotSame(data.Content, data.Content);
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

    // The fields of a packed argument are suggested inside its braces, and their rows render (the field kind had no badge)
    [AvaloniaFact]
    public void FieldsOfPackedArgumentsAreSuggested()
    {
        using var fixture = new Fixture(Script.Replace("|", "DoAnimation({|"));

        fixture.Type("s");

        Assert.NotNull(fixture.Completion.CompletionWindow);
        Assert.Contains("slotCount", fixture.Suggestions);
        Assert.All(fixture.Completion.CompletionWindow!.CompletionList.CompletionData, data => Assert.NotNull(data.Content));
    }

    [AvaloniaFact]
    public void LiteralHelpersAreSuggestedInsideCalls()
    {
        using var fixture = new Fixture(Script.Replace("|", "DoAnimation({slotCount = 1}, |"));

        fixture.Type("P");

        Assert.NotNull(fixture.Completion.CompletionWindow);
        Assert.Contains("Prop", fixture.Suggestions);
        Assert.All(fixture.Completion.CompletionWindow!.CompletionList.CompletionData, data => Assert.NotNull(data.Content));
    }

    // Moving the caret into a call that's already written shows which parameter it is at
    [AvaloniaFact]
    public void SignatureOpensWhenTheCaretMovesIntoACall()
    {
        using var fixture = new Fixture(Script.Replace("|", "DoAnimation(1, 2, 3, 4, 5, 6);|"));
        Assert.Null(fixture.Completion.SignatureWindow);

        var call = fixture.Editor.Text.IndexOf("DoAnimation(1, 2, 3", StringComparison.Ordinal);
        fixture.Editor.CaretOffset = call + "DoAnimation(1, 2, ".Length;

        Assert.NotNull(fixture.Completion.SignatureWindow);
        Assert.Equal("Parameter 3 of 6", fixture.Completion.SignatureWindow!.Provider.CurrentContent);

        fixture.Editor.CaretOffset = call;
        Assert.Null(fixture.Completion.SignatureWindow);
    }

    // A state's opening parenthesis lists the behaviours it can run
    [AvaloniaFact]
    public void StateParenthesesSuggestBehaviours()
    {
        var behaviours = new[]
        {
            new AgentLabCompletionItem("COM_A", AgentLabCompletionKind.Behaviour, "behaviour COM_A"),
            new AgentLabCompletionItem("COM_B", AgentLabCompletionKind.Behaviour, "behaviour COM_B")
        };
        using var fixture = new Fixture("behaviour TEST {\n   state State_0|\n}\n", () => behaviours);
        fixture.Type("(");
        Assert.NotNull(fixture.Completion.CompletionWindow);
        Assert.Equal(["COM_A", "COM_B"], fixture.Suggestions);
        fixture.Type("COM_B");
        Assert.Equal("COM_B", fixture.Completion.CompletionWindow!.CompletionList.SelectedItem!.Text);
    }

    // Ctrl+click and F12 on the behaviour a state names open it
    [AvaloniaFact]
    public void CtrlClickOnAStatesBehaviourOpensIt()
    {
        var editor = new TextEditor { Document = new TextDocument("behaviour A {\n   state S(COM_X) {\n   }\n}\n") };
        var window = new Window { Content = editor, Width = 800, Height = 600 };
        window.Show();
        var target = new BehaviourGraph();
        var opened = new List<IAsset>();
        using var navigation = new AgentLabNavigation(editor, reference => reference == "COM_X" ? target : null, opened.Add);
        Dispatcher.UIThread.RunJobs();
        var offset = editor.Text.IndexOf("COM_X", StringComparison.Ordinal) + 2;
        var textView = editor.TextArea.TextView;
        var point = textView.GetVisualPosition(new TextViewPosition(editor.Document.GetLocation(offset)), VisualYPosition.LineMiddle) - textView.ScrollOffset;
        var inWindow = textView.TranslatePoint(point, window)!.Value;
        window.MouseDown(inWindow, MouseButton.Left, RawInputModifiers.Control);
        window.MouseUp(inWindow, MouseButton.Left, RawInputModifiers.Control);
        Assert.Equal([target], opened);

        // A plain click doesn't, F12 at the caret does
        window.MouseDown(inWindow, MouseButton.Left);
        window.MouseUp(inWindow, MouseButton.Left);
        Assert.Single(opened);
        editor.TextArea.Focus();
        editor.CaretOffset = offset;
        window.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.None);
        Assert.Equal(2, opened.Count);
        Assert.False(navigation.GoToDefinition(editor.Text.IndexOf("state", StringComparison.Ordinal)));
        window.Close();
    }

    // Ctrl+click and F12 on a state or control packet the script names select its declaration, the hint says so
    [AvaloniaFact]
    public void CtrlClickOnANamedStateOrPacketGoesToItsDeclaration()
    {
        const string script = "behaviour A {\n   packet P {\n   }\n   [ControlPacket(P)]\n   state S() {\n      if Else(0) > 0.5 {\n         execute T;\n      }\n   }\n" +
                              "   state T() {\n   }\n}\n";
        var editor = new TextEditor { Document = new TextDocument(script) };
        var window = new Window { Content = editor, Width = 800, Height = 600 };
        window.Show();
        using var navigation = new AgentLabNavigation(editor, _ => null, _ => { });
        using var hints = new AgentLabHoverHints(editor, "ActionDefinitionsPs2.lab");
        Dispatcher.UIThread.RunJobs();
        var executed = script.IndexOf("execute T", StringComparison.Ordinal) + "execute ".Length;
        var textView = editor.TextArea.TextView;
        var point = textView.GetVisualPosition(new TextViewPosition(editor.Document.GetLocation(executed)), VisualYPosition.LineMiddle) - textView.ScrollOffset;
        var inWindow = textView.TranslatePoint(point, window)!.Value;
        Assert.EndsWith("Ctrl+click or F12 goes to it", hints.GetHover(executed)!.Description);

        window.MouseDown(inWindow, MouseButton.Left, RawInputModifiers.Control);
        window.MouseUp(inWindow, MouseButton.Left, RawInputModifiers.Control);
        Assert.Equal((script.IndexOf("state T()", StringComparison.Ordinal) + "state ".Length, "T"), (editor.SelectionStart, editor.SelectedText));

        editor.TextArea.Focus();
        editor.CaretOffset = script.IndexOf("(P)", StringComparison.Ordinal) + 1;
        window.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.None);
        Assert.Equal((script.IndexOf("packet P", StringComparison.Ordinal) + "packet ".Length, "P"), (editor.SelectionStart, editor.SelectedText));

        // A plain click stays where it's clicked (right after the click before it, it's a double click selecting the word there)
        window.MouseDown(inWindow, MouseButton.Left);
        window.MouseUp(inWindow, MouseButton.Left);
        Assert.Equal(executed, editor.SelectionStart);

        // A declaration out of sight gets scrolled to
        editor.Document.Insert(script.IndexOf("   state T()", StringComparison.Ordinal), string.Concat(Enumerable.Repeat("   // filler\n", 200)));
        Dispatcher.UIThread.RunJobs();
        Assert.True(navigation.GoToDefinition(executed));
        Dispatcher.UIThread.RunJobs();
        textView.EnsureVisualLines();
        var declarationLine = editor.Document.GetLineByOffset(editor.SelectionStart).LineNumber;
        Assert.Equal("T", editor.SelectedText);
        Assert.Contains(textView.VisualLines, line => line.FirstDocumentLine.LineNumber == declarationLine);
        window.Close();
    }

    // Hovering a word shows what it is
    [AvaloniaFact]
    public async Task HoveringAnActionShowsItsSignature()
    {
        var editor = new TextEditor { Document = new TextDocument("behaviour A {\n   state S() {\n      if Else(0) > 0.5 {\n         DoAnimation(1, 2, 3, 4, 5, 6);\n      }\n   }\n}\n") };
        var window = new Window { Content = editor, Width = 800, Height = 600 };
        window.Show();
        using var hints = new AgentLabHoverHints(editor, "ActionDefinitionsPs2.lab");
        Dispatcher.UIThread.RunJobs();
        var offset = editor.Text.IndexOf("DoAnimation", StringComparison.Ordinal) + 3;
        Assert.StartsWith("action DoAnimation(", hints.GetHover(offset)!.Title);

        var textView = editor.TextArea.TextView;
        var location = editor.Document.GetLocation(offset);
        var point = textView.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineMiddle) - textView.ScrollOffset;
        var inWindow = textView.TranslatePoint(point, window)!.Value;
        window.MouseMove(inWindow);
        for (var waited = 0; waited < 20 && !ToolTip.GetIsOpen(editor.TextArea); waited++)
        {
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(ToolTip.GetIsOpen(editor.TextArea));
        Assert.StartsWith("action DoAnimation(", ((TextBlock)ToolTip.GetTip(editor.TextArea)!).Inlines!.Text);
        window.Close();
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
