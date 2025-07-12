namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabStateSymbol : AgentLabSymbol
{
    public int Id { get; }
    
    public AgentLabStateSymbol(string name, int id) : base(name)
    {
        Id = id;
    }
}