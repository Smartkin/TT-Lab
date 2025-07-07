using System;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabBehaviourLibrarySymbol : AgentLabSymbol
{
    public AgentLabSymbolTable BehaviourLibrarySymbolTable { get; }
    
    public AgentLabBehaviourLibrarySymbol(string name, AgentLabSymbolTable inherit) : base(name)
    {
        BehaviourLibrarySymbolTable = new AgentLabSymbolTable
        {
            Parent = inherit
        };
    }

    public override String ToString()
    {
        return $"<{Name}:BehaviourLibrary\n {{ {BehaviourLibrarySymbolTable} }}>";
    }
}