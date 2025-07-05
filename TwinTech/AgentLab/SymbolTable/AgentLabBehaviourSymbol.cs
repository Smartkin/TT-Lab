using System;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabBehaviourSymbol : AgentLabSymbol
{
    public AgentLabSymbolTable BehaviourSymbolTable { get; }
    
    public AgentLabBehaviourSymbol(string name, AgentLabSymbolTable inherit) : base(name)
    {
        BehaviourSymbolTable = new AgentLabSymbolTable();
        BehaviourSymbolTable.InitBuiltInTypes();
        foreach (var actionSymbol in inherit.GetSymbols<AgentLabActionSymbol>())
        {
            BehaviourSymbolTable.Define(actionSymbol);
        }

        foreach (var conditionSymbol in inherit.GetSymbols<AgentLabConditionSymbol>())
        {
            BehaviourSymbolTable.Define(conditionSymbol);
        }
    }

    public override String ToString()
    {
        return $"<{Name}:Behaviour\n {{ {BehaviourSymbolTable} }}>";
    }
}