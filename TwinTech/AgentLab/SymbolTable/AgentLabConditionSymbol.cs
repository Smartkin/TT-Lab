using System;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabConditionSymbol : AgentLabSymbol
{
    public AgentLabSymbol ReturnType { get; }
    public AgentLabSymbol ParameterType { get; }
    
    public AgentLabConditionSymbol(string name, AgentLabSymbol type, AgentLabSymbol returnType, AgentLabSymbol parameterType = null) : base(name, type)
    {
        ParameterType = parameterType;
        ReturnType = returnType;
    }

    public override String ToString() => $"<condition {Name}:{Type}({ParameterType})>";
}