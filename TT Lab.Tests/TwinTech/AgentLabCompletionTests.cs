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
        "   [NonBlocking]\n" +
        "   <attribute>\n" +
        "   state State_0() {\n" +
        "      <state>\n" +
        "      if Else(0) >= 0.5 {\n" +
        "         interval = 0;\n" +
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
        Assert.Contains("unknown", Texts(items));
        // Interval was already set in this body
        Assert.DoesNotContain("interval", Texts(items));
        Assert.DoesNotContain(items, item => item.Kind == AgentLabCompletionKind.Condition);
    }

    [Fact]
    public void ActionsDescribeTheirParameters()
    {
        var doAnimation = Complete("<body>", "|").Single(item => item.Text == "DoAnimation");

        Assert.Equal("action DoAnimation(int param1, float param2, int param3, int param4, int param5, int param6)", doAnimation.Description);
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
        Assert.Equal(["if"], Texts(Complete("<state>", "|")));
    }

    [Fact]
    public void BehaviourSuggestsItsBlocks()
    {
        Assert.Equal(["state", "packet", "const", "starter"], Texts(Complete("<behaviour>", "|")));
    }

    [Fact]
    public void AttributesDependOnWhatTheyAreFor()
    {
        Assert.Equal(["NonBlocking", "SkipFirstBody", "UseObjectSlot", "ControlPacket", "Unknown"], Texts(Complete("<attribute>", "[|")));

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
        Assert.Equal(["state", "packet", "const", "starter"], Texts(Complete("<attribute>", "[Unknown(0x40)] |")));
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
}
