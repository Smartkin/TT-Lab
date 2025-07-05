using System;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabBehaviourLibrarySymbol : AgentLabSymbol
{
    public AgentLabSymbolTable BehaviourLibrarySymbolTable { get; }
    
    public AgentLabBehaviourLibrarySymbol(string name, AgentLabSymbolTable inherit) : base(name)
    {
        BehaviourLibrarySymbolTable = new AgentLabSymbolTable();
        BehaviourLibrarySymbolTable.InitBuiltInTypes();
        foreach (var actionSymbol in inherit.GetSymbols<AgentLabActionSymbol>())
        {
            BehaviourLibrarySymbolTable.Define(actionSymbol);
        }
    }

    public override String ToString()
    {
        return $"<{Name}:BehaviourLibrary\n {{ {BehaviourLibrarySymbolTable} }}>";
    }
}