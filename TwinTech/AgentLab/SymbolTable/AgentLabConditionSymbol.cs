using System;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabConditionSymbol : AgentLabSymbol
{
    public AgentLabSymbol ReturnType { get; }
    public AgentLabSymbol ParameterType { get; }
    public int Id { get; }
    
    public AgentLabConditionSymbol(string name, int id, AgentLabSymbol type, AgentLabSymbol returnType, AgentLabSymbol parameterType = null) : base(name, type)
    {
        ParameterType = parameterType;
        ReturnType = returnType;
        Id = id;
    }

    public override String ToString() => $"<condition {Name}:{Type}({ParameterType})>";
}