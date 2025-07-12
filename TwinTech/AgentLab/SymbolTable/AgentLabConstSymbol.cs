namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabConstSymbol : AgentLabSymbol
{
    public int Id { get; }

    public AgentLabConstSymbol(string name, AgentLabSymbol type, int id) : base(name, type)
    {
        Id = id;
    }
}