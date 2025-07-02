using System;

namespace Twinsanity.AgentLab.SymbolTable;

internal abstract class AgentLabSymbol
{
    public string Name { get; }
    public AgentLabSymbol Type { get; internal set; } // Can be null

    protected AgentLabSymbol(string name, AgentLabSymbol type = null)
    {
        Name = name;
        Type = type;
    }

    public override String ToString() => $"<{Name}:{Type}>";
}