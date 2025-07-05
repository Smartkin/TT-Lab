using System;
using System.Collections.Generic;
using System.Linq;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabActionSymbol : AgentLabSymbol
{
    public AgentLabSymbolTable Parameters { get; internal init; }
    
    public AgentLabActionSymbol(string name, AgentLabSymbol type, params AgentLabSymbol[] parameters) : base(name, type)
    {
        Parameters = new AgentLabSymbolTable();
        if (parameters == null)
        {
            return;
        }
        
        foreach (var parameter in parameters)
        {
            Parameters.Define(parameter);
        }
    }

    public override String ToString()
    {
        var resultString = $"<action {Name}(";
        var index = 0;
        if (Parameters != null)
        {
            var paramsAmount = Parameters.GetAllSymbols().Count();
            foreach (var parameter in Parameters.GetAllSymbols())
            {
                if (index != paramsAmount - 1)
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