using System;
using System.Collections.Generic;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabActionSymbol : AgentLabSymbol
{
    public IList<AgentLabSymbol> Parameters { get; }
    
    public AgentLabActionSymbol(string name, AgentLabSymbol type, params AgentLabSymbol[] parameters) : base(name, type)
    {
        Parameters = parameters;
    }

    public override String ToString()
    {
        var resultString = $"<action {Name}(";
        var index = 0;
        if (Parameters != null)
        {
            foreach (var parameter in Parameters)
            {
                if (index != Parameters.Count - 1)
                {
                    resultString += parameter + ", ";
                }
                else
                {
                    resultString += parameter;
                }

                index++;
            }
        }

        resultString += ")>";
        return resultString;
    }
}