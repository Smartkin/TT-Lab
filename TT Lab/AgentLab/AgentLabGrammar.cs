using System;
using System.Collections.Generic;
using TextMateSharp.Internal.Types;

namespace TT_Lab.AgentLab;

public class AgentLabGrammar : IRawGrammar
{
    private static string[] _fileTypes = [".lab"];
    
    public IRawGrammar Clone()
    {
        throw new NotImplementedException();
    }

    public IRawRepository GetRepository()
    {
        throw new NotImplementedException();
    }

    public String GetScopeName()
    {
        throw new NotImplementedException();
    }

    public ICollection<IRawRule> GetPatterns()
    {
        throw new NotImplementedException();
    }

    public Dictionary<String, IRawRule> GetInjections()
    {
        throw new NotImplementedException();
    }

    public String GetInjectionSelector()
    {
        throw new NotImplementedException();
    }

    public ICollection<String> GetFileTypes()
    {
        return _fileTypes;
    }

    public String GetName()
    {
        return "Agent Lab";
    }

    public String GetFirstLineMatch()
    {
        throw new NotImplementedException();
    }
}