using System;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabBehaviourSymbol : AgentLabSymbol
{
    public AgentLabSymbolTable BehaviourSymbolTable { get; }
    
    public AgentLabBehaviourSymbol(string name, AgentLabSymbolTable inherit) : base(name)
    {
        BehaviourSymbolTable = new AgentLabSymbolTable
        {
            Parent = inherit
        };
    }

    public override String ToString()
    {
        return $"<{Name}:Behaviour\n {{ {BehaviourSymbolTable} }}>";
    }
}