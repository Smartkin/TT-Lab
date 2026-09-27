using System.Collections.Generic;
using System.IO;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace TT_Lab.AgentLab;

/// <summary>
/// Registry with the built-in grammars and themes plus the AgentLab grammar
/// </summary>
public class AgentLabRegistryOptions(ThemeName theme) : IRegistryOptions
{
    public const string ScopeName = "source.agentlab";
    private const string GrammarResource = "TT_Lab.AgentLab.agentlab.tmLanguage.json";

    private static IRawGrammar? _grammar;
    private readonly RegistryOptions _builtInOptions = new(theme);

    public IRawTheme GetTheme(string scopeName) => _builtInOptions.GetTheme(scopeName);

    public IRawGrammar GetGrammar(string scopeName)
    {
        return scopeName == ScopeName ? LoadGrammar() : _builtInOptions.GetGrammar(scopeName);
    }

    public ICollection<string> GetInjections(string scopeName) => _builtInOptions.GetInjections(scopeName);

    public IRawTheme GetDefaultTheme() => _builtInOptions.GetDefaultTheme();

    private static IRawGrammar LoadGrammar()
    {
        if (_grammar != null)
        {
            return _grammar;
        }

        using var stream = typeof(AgentLabRegistryOptions).Assembly.GetManifestResourceStream(GrammarResource)!;
        using var reader = new StreamReader(stream);
        _grammar = GrammarReader.ReadGrammarSync(reader);
        return _grammar;
    }
}
