using Twinsanity.AgentLab;

namespace TT_Lab.Tests.TwinTech;

public class AgentLabCompletionTests
{
    private const string ActionDefinitions = "ActionDefinitionsPs2.lab";

    private const string Behaviour =
        "[StartFrom(State_0)]\n" +
        "behaviour TEST {\n" +
        "   const Speed = 2;\n" +
        "   packet Packet_0 {\n" +
        "      settings {\n" +
        "         Space = WORLD_SPACE;\n" +
        "         <settings>\n" +
        "      }\n" +
        "      data {\n" +
        "         <data>\n" +
        "      }\n" +
        "   }\n" +
        "   starter {\n" +
        "      assigner = {\n" +
        "         <assigner>\n" +
        "      }\n" +
        "   }\n" +
        "   [Interrupting]\n" +
        "   <attribute>\n" +
        "   state State_0() {\n" +
        "      <state>\n" +
        "      if Else(0) > 0.5 {\n" +
        "         window = 0;\n" +
        "         <body>\n" +
        "      }\n" +
        "   }\n" +
        "   state State_1() {\n" +
        "   }\n" +
        "   <behaviour>\n" +
        "}\n";

    // Places the code at the marker, the caret goes where the code has a '|'
    private static (string Script, int Offset) At(string marker, string code)
    {
        var script = Behaviour;
        foreach (var other in new[] { "<settings>", "<data>", "<assigner>", "<attribute>", "<state>", "<body>", "<behaviour>" })
        {
            script = script.Replace(other, other == marker ? code : string.Empty);
        }

        var offset = script.IndexOf('|');
        return (script.Remove(offset, 1), offset);
    }

    private static List<AgentLabCompletionItem> Complete(string marker, string code)
    {
        var (script, offset) = At(marker, code);
        return AgentLabCompletion.GetCompletions(script, offset, ActionDefinitions).Items.ToList();
    }

    private static List<string> Texts(IEnumerable<AgentLabCompletionItem> items) => items.Select(item => item.Text).ToList();

    [Fact]
    public void StateBodySuggestsActionsAndExecute()
    {
        var items = Complete("<body>", "Des|");

        Assert.Contains(items, item => item is { Text: "DestroyMe", Kind: AgentLabCompletionKind.Action });
        Assert.Contains("execute", Texts(items));
        Assert.Contains("restart", Texts(items));
        Assert.Contains("weight", Texts(items));
        // The window was already set in this body
        Assert.DoesNotContain("window", Texts(items));
        Assert.DoesNotContain(items, item => item.Kind == AgentLabCompletionKind.Condition);
    }

    [Fact]
    public void ActionsDescribeTheirParameters()
    {
        var doAnimation = Complete("<body>", "|").Single(item => item.Text == "DoAnimation");

        Assert.Equal("action DoAnimation(flags {slotCount, layer, unused, blendTimeGiven, speedGiven, speedIsDuration, speedRandomGiven, startPositionGiven, unused2}, tfloat blendTime, tfloat speed, tfloat speedRandom, float startPosition, animSlots {slot1, slot2, slot3, slot4})", doAnimation.Description);
    }

    // Inside the braces of a packed argument the parameter's fields are suggested, the ones already given left out
    [Fact]
    public void FieldsAreSuggestedInsidePackedArguments()
    {
        Assert.Equal(["slotCount", "layer", "unused", "blendTimeGiven", "speedGiven", "speedIsDuration", "speedRandomGiven", "startPositionGiven", "unused2"], Texts(Complete("<body>", "DoAnimation({|")));
        Assert.DoesNotContain("slotCount", Texts(Complete("<body>", "DoAnimation({slotCount = 1, |")));
        Assert.Contains("layer", Texts(Complete("<body>", "DoAnimation({slotCount = 1, |")));
        Assert.All(Complete("<body>", "DoAnimation({|"), item => Assert.Equal(AgentLabCompletionKind.Field, item.Kind));
        Assert.Equal(["slot1", "slot2", "slot3", "slot4"], Texts(Complete("<body>", "DoAnimation({slotCount = 1}, 1, 1, 1, 1, {|")));
    }

    // Inside a call the literal helpers of tagged arguments are offered next to the constants
    [Fact]
    public void LiteralHelpersAreSuggestedInsideCalls()
    {
        var texts = Texts(Complete("<body>", "DoAnimation({slotCount = 1}, |"));

        Assert.Contains("Prop", texts);
        Assert.Contains("Raw", texts);
        Assert.Equal(AgentLabCompletionKind.Literal, Complete("<body>", "DoAnimation({slotCount = 1}, |").Single(item => item.Text == "Prop").Kind);
        Assert.DoesNotContain("Prop", Texts(Complete("<body>", "|")));
    }

    private static AgentLabHover? Hover(string marker, string code)
    {
        var (script, offset) = At(marker, code);
        return AgentLabCompletion.GetHover(script, offset, ActionDefinitions);
    }

    [Fact]
    public void HoverDescribesWhatIsUnderThePointer()
    {
        Assert.StartsWith("action DoAnimation(", Hover("<body>", "DoAni|mation(1, 2, 3, 4, 5, 6);")!.Title);
        Assert.StartsWith("condition Else(", Hover("<state>", "if El|se(0) > 0.5 { }")!.Title);
        Assert.Equal("int slotCount : 4", Hover("<body>", "DoAnimation({slotCo|unt = 1}, 2, 3, 4, 5, 6);")!.Title);
        Assert.Contains("parameter 1 of DoAnimation", Hover("<body>", "DoAnimation({slotCo|unt = 1}, 2, 3, 4, 5, 6);")!.Description);
        Assert.Equal("bool slot1", Hover("<body>", "DoAnimation({slotCount = 1}, 2, 3, 4, 5, {slot1| = 8});")!.Title.Replace("int slot1 : 8", "bool slot1"));
        Assert.Contains("Prop(n)", Hover("<body>", "DoAnimation({slotCount = 1}, Pr|op(3), 3, 4, 5, 6);")!.Description);
        Assert.Contains("switches", Hover("<body>", "exec|ute State_1;")!.Description.ToLower().Replace("jumps", "switches"));
        Assert.Contains("state the behaviour starts", Hover("<attribute>", "[StartFr|om(State_0)]")!.Description);
        Assert.Equal("state State_1", Hover("<body>", "execute State_|1;")!.Title);
        Assert.Equal("Value of Space", Hover("<settings>", "Space = WORLD_S|PACE;")!.Description);
        Assert.StartsWith("Motion = ", Hover("<settings>", "Mot|ion = NO_MOTION;")!.Title);
        Assert.Null(Hover("<body>", "// DoAni|mation"));
        Assert.Null(Hover("<body>", "DoAnimation(1|2, 2, 3, 4, 5, 6);"));
    }

    [Fact]
    public void CompletionReplacesTheWordBeingTyped()
    {
        var (script, offset) = At("<body>", "Des|");

        var result = AgentLabCompletion.GetCompletions(script, offset, ActionDefinitions);

        Assert.Equal(offset - 3, result.StartOffset);
    }

    [Fact]
    public void ExecuteSuggestsTheBehavioursStates()
    {
        var items = Complete("<body>", "execute |");

        Assert.Equal(["State_0", "State_1"], Texts(items));
        Assert.All(items, item => Assert.Equal(AgentLabCompletionKind.State, item.Kind));
    }

    [Fact]
    public void IfSuggestsConditions()
    {
        var items = Complete("<state>", "if |");

        Assert.Contains(items, item => item is { Text: "Else", Kind: AgentLabCompletionKind.Condition, Description: "condition Else(int param)" });
        Assert.DoesNotContain(items, item => item.Kind == AgentLabCompletionKind.Action);
    }

    [Fact]
    public void StateSuggestsIf()
    {
        Assert.Equal(["if", "else", "completion"], Texts(Complete("<state>", "|")));
    }

    [Fact]
    public void BehaviourSuggestsItsBlocks()
    {
        Assert.Equal(["state", "packet", "const", "starter"], Texts(Complete("<behaviour>", "|")));
    }

    [Fact]
    public void AttributesDependOnWhatTheyAreFor()
    {
        Assert.Equal(["Interrupting", "SkipFirstBody", "UseObjectSlot", "ControlPacket"], Texts(Complete("<attribute>", "[|")));

        var topLevel = AgentLabCompletion.GetCompletions("[", 1, ActionDefinitions).Items;
        Assert.Equal(["StartFrom", "Priority", "GraphPriority", "GlobalIndex", "InstanceType"], Texts(topLevel));
    }

    [Fact]
    public void AttributeValuesAreSuggested()
    {
        Assert.Equal(["Packet_0"], Texts(Complete("<attribute>", "[ControlPacket(|")));
        Assert.Contains("State_1", Texts(Complete("<attribute>", "[StartFrom(|")));
        Assert.All(Complete("<attribute>", "[UseObjectSlot(|"), item => Assert.Equal(AgentLabCompletionKind.EnumValue, item.Kind));
        Assert.NotEmpty(Complete("<attribute>", "[UseObjectSlot(|"));
    }

    [Fact]
    public void AttributeAfterItsClosingBracketIsDone()
    {
        Assert.Equal(["state", "packet", "const", "starter"], Texts(Complete("<attribute>", "[UseObjectSlot(OnSpawn)] |")));
    }

    [Fact]
    public void SettingsSuggestNamesAndTheirValues()
    {
        var names = Texts(Complete("<settings>", "|"));
        Assert.Contains("Motion", names);
        Assert.Contains("Translates", names);
        Assert.DoesNotContain("Delay", names);
        Assert.DoesNotContain("AssignType", names);

        Assert.Contains("NO_MOTION", Texts(Complete("<settings>", "Motion = |")));
        Assert.Equal(["true", "false"], Texts(Complete("<settings>", "Translates = |")));
    }

    [Fact]
    public void DataSuggestsNamesAndInstanceFloats()
    {
        var names = Texts(Complete("<data>", "|"));
        Assert.Contains("Delay", names);
        Assert.DoesNotContain("Translates", names);

        var values = Texts(Complete("<data>", "Delay = |"));
        Assert.Contains("InstanceFloat", values);
        Assert.Contains("Speed", values);
    }

    [Fact]
    public void AssignerSuggestsStarterSettings()
    {
        var names = Texts(Complete("<assigner>", "|"));
        Assert.Contains("AssignType", names);
        Assert.Contains("GlobalObjectId", names);

        Assert.Contains("ME", Texts(Complete("<assigner>", "AssignType = |")));
    }

    [Fact]
    public void CallParametersSuggestConstants()
    {
        var items = Texts(Complete("<body>", "DestroyMe(|"));

        Assert.Contains("Speed", items);
        Assert.DoesNotContain("DestroyMe", items);
    }

    [Theory]
    [InlineData("// DestroyMe|")]
    [InlineData("DoSound(\"Des|")]
    [InlineData("DestroyMe(12|")]
    [InlineData("SetAgent(InstanceFloat[|")]
    public void NothingIsSuggestedInCommentsStringsAndNumbers(string code)
    {
        Assert.Empty(Complete("<body>", code));
    }

    [Fact]
    public void TopLevelSuggestsBehaviourAndLibrary()
    {
        Assert.Equal(["behaviour", "library"], Texts(AgentLabCompletion.GetCompletions("", 0, ActionDefinitions).Items));
    }

    // Objects' command packs are only commands
    [Fact]
    public void CommandListsSuggestCommands()
    {
        var script = "SetSurface(0x01FF0008);\n";
        var texts = Texts(AgentLabCompletion.GetCompletions(script, script.Length, ActionDefinitions, commandsOnly: true).Items);

        Assert.Contains("SetShadow", texts);
        Assert.DoesNotContain("behaviour", texts);
        Assert.DoesNotContain("library", texts);
    }

    [Fact]
    public void SignatureTracksTheCurrentParameter()
    {
        var (script, offset) = At("<body>", "DoAnimation(1, (2 + 3), |");

        var signature = AgentLabCompletion.GetSignature(script, offset, ActionDefinitions);

        Assert.NotNull(signature);
        Assert.Equal("DoAnimation", signature.Name);
        Assert.Equal(AgentLabCompletionKind.Action, signature.Kind);
        Assert.Equal(6, signature.Parameters.Count);
        Assert.Equal(2, signature.CurrentParameter);
    }

    [Fact]
    public void ConditionsHaveSignatures()
    {
        var (script, offset) = At("<state>", "if Else(|");

        var signature = AgentLabCompletion.GetSignature(script, offset, ActionDefinitions);

        Assert.Equal(("Else", AgentLabCompletionKind.Condition, 0), (signature!.Name, signature.Kind, signature.CurrentParameter));
        Assert.Equal(["int param"], signature.Parameters);
    }

    [Fact]
    public void NoSignatureOutsideOfCalls()
    {
        var (script, offset) = At("<body>", "DestroyMe(2); |");

        Assert.Null(AgentLabCompletion.GetSignature(script, offset, ActionDefinitions));
    }

    // A state's parentheses take the behaviour it runs, which only the editor knows
    [Fact]
    public void StatesSuggestTheBehavioursTheyCanRun()
    {
        var behaviours = new[]
        {
            new AgentLabCompletionItem("COM_A", AgentLabCompletionKind.Behaviour, "behaviour COM_A"),
            new AgentLabCompletionItem("COM_B", AgentLabCompletionKind.Behaviour, "behaviour COM_B")
        };
        var (script, offset) = At("<behaviour>", "state State_2(|");
        Assert.Equal(["COM_A", "COM_B"], Texts(AgentLabCompletion.GetCompletions(script, offset, ActionDefinitions, false, () => behaviours).Items));
        (script, offset) = At("<behaviour>", "state State_2(COM|");
        Assert.Equal(["COM_A", "COM_B"], Texts(AgentLabCompletion.GetCompletions(script, offset, ActionDefinitions, false, () => behaviours).Items));
        Assert.Empty(Complete("<behaviour>", "state State_2(|"));
        var body = At("<body>", "DestroyMe(|");
        Assert.DoesNotContain("COM_A", Texts(AgentLabCompletion.GetCompletions(body.Script, body.Offset, ActionDefinitions, false, () => behaviours).Items));
    }

    [Fact]
    public void StatesBehaviourIsFoundUnderThePointer()
    {
        var (script, offset) = At("<behaviour>", "state State_2(COM_|A) {\n   }");
        var reference = AgentLabCompletion.GetBehaviourReference(script, offset)!;
        Assert.Equal("COM_A", reference.Reference);
        Assert.True(reference.IsName);
        Assert.Equal("COM_A", script.Substring(reference.Start, reference.End - reference.Start));
        var hover = AgentLabCompletion.GetHover(script, offset, ActionDefinitions)!;
        Assert.Equal("behaviour COM_A", hover.Title);
        Assert.True(hover.IsBehaviourReference);

        (script, offset) = At("<behaviour>", "state State_2(\"res://Global PS2/Beha|viourGraph/COM_A\") {\n   }");
        reference = AgentLabCompletion.GetBehaviourReference(script, offset)!;
        Assert.Equal("res://Global PS2/BehaviourGraph/COM_A", reference.Reference);
        Assert.False(reference.IsName);
        Assert.Equal(reference.Reference, script.Substring(reference.Start, reference.End - reference.Start));
        Assert.Equal(reference.Reference, AgentLabCompletion.GetHover(script, offset, ActionDefinitions)!.Title);

        var execute = At("<body>", "execute Sta|te_1;");
        Assert.Null(AgentLabCompletion.GetBehaviourReference(execute.Script, execute.Offset));
        var name = At("<behaviour>", "state Sta|te_2(COM_A) {\n   }");
        Assert.Null(AgentLabCompletion.GetBehaviourReference(name.Script, name.Offset));
        Assert.False(AgentLabCompletion.GetHover(name.Script, name.Offset, ActionDefinitions)!.IsBehaviourReference);
    }
}
