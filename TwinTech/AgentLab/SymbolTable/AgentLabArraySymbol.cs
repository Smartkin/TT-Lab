using System;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabArraySymbol : AgentLabSymbol
{
    public int Size { get; }
    public AgentLabSymbol StorageType { get; }

    public AgentLabArraySymbol(string name, int size, AgentLabSymbol storageType, AgentLabSymbol type) : base(name, type)
    {
        Size = size;
        StorageType = storageType;
    }

    public override String ToString()
    {
        return $"<{Name}[{Size}:{StorageType}]:{Type}>";
    }
}