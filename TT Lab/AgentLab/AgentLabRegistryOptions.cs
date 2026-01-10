using System;
using System.Collections.Generic;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace TT_Lab.AgentLab;

public class AgentLabRegistryOptions : IRegistryOptions
{
    public IRawTheme GetTheme(string scopeName)
    {
        throw new NotImplementedException();
    }

    public IRawGrammar GetGrammar(string scopeName)
    {
        throw new NotImplementedException();
    }

    public ICollection<String> GetInjections(string scopeName)
    {
        throw new NotImplementedException();
    }

    public IRawTheme GetDefaultTheme()
    {
        throw new NotImplementedException();
    }
}