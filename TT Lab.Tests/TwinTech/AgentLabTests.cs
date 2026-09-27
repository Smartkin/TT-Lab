using Twinsanity.AgentLab;
using Twinsanity.AgentLab.AgentLabObjectDescs.PS2;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using TokenType = Twinsanity.AgentLab.AgentLabToken.TokenType;

namespace TT_Lab.Tests.TwinTech;

public class AgentLabTests
{
    private const string ActionDefinitions = "ActionDefinitionsPs2.lab";

    private static string Behaviour(string actions = "         DestroyMe(0x00000002);\n         execute State_1;\n") =>
        "[StartFrom(State_0)]\n" +
        "[Priority(1)]\n" +
        "behaviour TEST_BEHAVIOUR {\n" +
        "   [Unknown(0x40)]\n" +
        "   [NonBlocking]\n" +
        "   state State_0() {\n" +
        "      if Else(0) >= 0.5 {\n" +
        "         interval = 0;\n" +
        "         unknown = false;\n" +
        actions +
        "      }\n" +
        "   }\n" +
        "\n" +
        "   [Unknown(0x0)]\n" +
        "   state State_1() {\n" +
        "   }\n" +
        "}\n";

    [Fact]
    public void LexerTracksLinesAndColumns()
    {
        using var lexer = new AgentLabLexer("behaviour A {\n  state S() {\n\tif Else(0) >= 0.5 { }\n}");

        var tokens = new List<(TokenType, int, int)>();
        for (var token = lexer.GetNextToken(); token.Type != TokenType.Eof; token = lexer.GetNextToken())
        {
            tokens.Add((token.Type, token.Line, token.Column));
        }

        Assert.Equal(
        [
            (TokenType.Behaviour, 1, 1), (TokenType.Identifier, 1, 11), (TokenType.OpenBracket, 1, 13),
            (TokenType.State, 2, 3), (TokenType.Identifier, 2, 9), (TokenType.LeftParen, 2, 10), (TokenType.RightParen, 2, 11), (TokenType.OpenBracket, 2, 13),
            (TokenType.If, 3, 2), (TokenType.Identifier, 3, 5), (TokenType.LeftParen, 3, 9), (TokenType.Integer, 3, 10), (TokenType.RightParen, 3, 11),
            (TokenType.GreaterEqual, 3, 13), (TokenType.FloatingPoint, 3, 16), (TokenType.OpenBracket, 3, 20), (TokenType.CloseBracket, 3, 22),
            (TokenType.CloseBracket, 4, 1)
        ], tokens);
    }

    // Big floats print with the exponent's sign, 1E+30 used to come back as infinity
    [Theory]
    [InlineData("1E+30", 1e30f)]
    [InlineData("1E-05", 1e-5f)]
    [InlineData("2.5E+38", 2.5e38f)]
    public void FloatsWithExponentsAreRead(string text, float value)
    {
        using var lexer = new AgentLabLexer(text);

        var token = lexer.GetNextToken();

        Assert.Equal(TokenType.FloatingPoint, token.Type);
        Assert.Equal(value, token.GetValue<float>());
        Assert.Equal(TokenType.Eof, lexer.GetNextToken().Type);
    }

    [Fact]
    public void ParserThrowsSyntaxErrorWithPosition()
    {
        using var parser = new AgentLabParser(new AgentLabLexer("behaviour {"));

        var exception = Assert.Throws<AgentLabSyntaxException>(() => parser.Parse());

        Assert.Equal(1, exception.Line);
        Assert.Equal(11, exception.Column);
        Assert.StartsWith("(1:11) ", exception.ToString());
    }

    [Fact]
    public void ValidBehaviourPassesTheCheck()
    {
        var status = AgentLabCompiler.Check(Behaviour(), ActionDefinitions);

        Assert.False(status.IsError, status.Message);
        Assert.Equal(0, status.Line);
    }

    [Fact]
    public void BehaviourWithoutActionDefinitionsStillParses()
    {
        var status = AgentLabCompiler.Check(Behaviour("         execute State_1;\n"), "");

        Assert.False(status.IsError, status.Message);
    }

    [Theory]
    [InlineData("         DestroyMe(0x00000002)\n         execute State_1;\n", 11, 10, "expected Semicolon")]
    [InlineData("         NoSuchAction();\n         execute State_1;\n", 10, 10, "Undefined action call NoSuchAction")]
    [InlineData("         DestroyMe(0x00000002, 5);\n         execute State_1;\n", 10, 10, "Expected 1 parameters for DestroyMe but got 2")]
    [InlineData("         DestroyMe(0x00000002);\n         execute Missing;\n", 11, 18, "Undefined state Missing")]
    public void ErrorsPointAtTheirPlaceInTheScript(string actions, int line, int column, string message)
    {
        var status = AgentLabCompiler.Check(Behaviour(actions), ActionDefinitions);

        Assert.True(status.IsError);
        Assert.Equal(line, status.Line);
        Assert.Equal(column, status.Column);
        Assert.Contains(message, status.Message);
    }

    [Fact]
    public void UndefinedConditionIsAnError()
    {
        var status = AgentLabCompiler.Check(Behaviour().Replace("if Else(0)", "if NoSuchCondition(0)"), ActionDefinitions);

        Assert.True(status.IsError);
        Assert.Equal((7, 10), (status.Line, status.Column));
        Assert.Contains("Undefined condition call NoSuchCondition", status.Message);
    }

    [Fact]
    public void UndefinedStartingStateIsAnError()
    {
        var status = AgentLabCompiler.Check(Behaviour().Replace("[StartFrom(State_0)]", "[StartFrom(Nowhere)]"), ActionDefinitions);

        Assert.True(status.IsError);
        Assert.True(status.Line > 0);
        Assert.Contains("Undefined state Nowhere", status.Message);
    }

    // Checks run on a background thread while the editor stays usable, so they must not share state
    // Objects' command packs can only be a list of commands, most objects have none
    [Theory]
    [InlineData("")]
    [InlineData("  \n\n")]
    [InlineData("// Nothing yet\n")]
    [InlineData("SetSurface(0x01FF0008);\nSetShadow(0x00000000, 0x41F00004, 0x41200004, 0x41200004);\n")]
    public void CommandListsPassTheCheck(string script)
    {
        var status = AgentLabCompiler.CheckCommands(script, ActionDefinitions);

        Assert.False(status.IsError, status.Message);
    }

    [Fact]
    public void OnlyCommandsCanBeInACommandList()
    {
        var status = AgentLabCompiler.CheckCommands("SetSurface(0x01FF0008);\n" + Behaviour(), ActionDefinitions);

        Assert.True(status.IsError);
        Assert.Contains("Only commands can be written here", status.Message);
        Assert.Equal((2, 1), (status.Line, status.Column));
        Assert.True(AgentLabCompiler.CheckCommands(Behaviour(), ActionDefinitions).IsError);
        Assert.True(AgentLabCompiler.CheckCommands("NotACommand(0);", ActionDefinitions).IsError);
    }

    [Fact]
    public void CommandListsCompileToAPack()
    {
        var options = new AgentLabCompiler.CompilerOptions { Command = new PS2CommandDesc(), CommandPack = new PS2CommandPackDesc(), ActionDefinitionsFile = ActionDefinitions };

        var empty = AgentLabCompiler.CompileCommands("// Nothing yet\n", options);
        var pack = AgentLabCompiler.CompileCommands("SetSurface(0x01FF0008);\nSetShadow(0x00000000, 0x41F00004, 0x41200004, 0x41200004);\n", options);

        Assert.Empty(empty.Get<ITwinBehaviourCommandPack>().Commands);
        Assert.Equal(2, pack.Get<ITwinBehaviourCommandPack>().Commands.Count);
    }

    [Fact]
    public void ChecksCanRunInParallel()
    {
        var scripts = Enumerable.Range(0, 32).Select(i => i % 2 == 0 ? Behaviour() : Behaviour("         NoSuchAction();\n")).ToList();

        var statuses = scripts.AsParallel().Select(script => AgentLabCompiler.Check(script, ActionDefinitions)).ToList();

        for (var i = 0; i < statuses.Count; i++)
        {
            Assert.Equal(i % 2 != 0, statuses[i].IsError);
        }
    }
}
