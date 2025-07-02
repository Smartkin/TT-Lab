namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabBuiltInSymbol : AgentLabSymbol
{
    public AgentLabBuiltInSymbol(string name) : base(name) { }
    
    public override string ToString() => $"<{Name}>";
}