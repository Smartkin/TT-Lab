using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TT_Lab.AgentLab;

namespace TT_Lab.Tests.Editor;

public class AgentLabGrammarTests
{
    private static readonly IGrammar Grammar = new Registry(new AgentLabRegistryOptions(ThemeName.DarkPlus)).LoadGrammar(AgentLabRegistryOptions.ScopeName);

    private static List<(string Text, string Scope)> Tokenize(params string[] lines)
    {
        var result = new List<(string, string)>();
        IStateStack? state = null;
        foreach (var line in lines)
        {
            var tokens = Grammar.TokenizeLine(line, state, TimeSpan.MaxValue);
            state = tokens.RuleStack;
            foreach (var token in tokens.Tokens)
            {
                var text = line[token.StartIndex..Math.Min(token.EndIndex, line.Length)];
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add((text, token.Scopes[^1]));
                }
            }
        }

        return result;
    }

    [Fact]
    public void GrammarIsEmbedded()
    {
        Assert.NotNull(Grammar);
        Assert.Equal(AgentLabRegistryOptions.ScopeName, Grammar.GetScopeName());
    }

    [Theory]
    [InlineData("behaviour", "storage.type.agentlab")]
    [InlineData("COM_TNT_CRATE", "entity.name.type.agentlab")]
    [InlineData("// comment", "comment.line.double-slash.agentlab")]
    public void BehaviourHeaderIsHighlighted(string text, string scope)
    {
        var tokens = Tokenize("behaviour COM_TNT_CRATE { // comment");

        Assert.Contains((text, scope), tokens);
    }

    [Fact]
    public void StateBodyIsHighlighted()
    {
        var tokens = Tokenize(
            "   state State_0() {",
            "      if Else(0) > 0.5 {",
            "         AUnknown_122(0x03FBF6FC, -1.5, \"text\");",
            "         execute State_1;");

        Assert.Contains(("if", "keyword.control.agentlab"), tokens);
        Assert.Contains(("execute", "keyword.control.agentlab"), tokens);
        Assert.Contains((">", "keyword.operator.agentlab"), tokens);
        Assert.Contains(("0.5", "constant.numeric.agentlab"), tokens);
        Assert.Contains(("-1.5", "constant.numeric.agentlab"), tokens);
        Assert.Contains(tokens, token => token.Text == "0x03FBF6FC" && token.Scope.StartsWith("constant.numeric"));
        Assert.Contains(tokens, token => token.Text == "AUnknown_122" && token.Scope.StartsWith("entity.name.function"));
        Assert.Contains(tokens, token => token.Text == "text" && token.Scope.StartsWith("string.quoted.double"));
    }

    [Fact]
    public void AttributesAreHighlighted()
    {
        var tokens = Tokenize("[StartFrom(State_16)]");

        Assert.Contains(("[", "punctuation.definition.attribute.agentlab"), tokens);
        Assert.Contains(tokens, token => token.Text == "StartFrom" && token.Scope.StartsWith("entity.other.attribute-name"));
    }
}
