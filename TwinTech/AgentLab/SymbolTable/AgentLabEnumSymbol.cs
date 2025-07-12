using System;
using System.Linq;
using System.Text;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabEnumSymbol : AgentLabSymbol
{
    public AgentLabSymbolTable Enums { get; }
    
    public AgentLabEnumSymbol(string name, AgentLabSymbol type, params string[] enumNames) : base(name, type)
    {
        Enums = new AgentLabSymbolTable();

        var enumIndex = 0;
        foreach (var enumName in enumNames)
        {
            Enums.Define(new AgentLabConstSymbol(enumName, this, enumIndex++));
        }
    }

    public override String ToString()
    {
        var enumNames = Enums.GetSymbols<AgentLabConstSymbol>().Select(s => s.Name);
        var stringBuilder = new StringBuilder();
        foreach (var enumName in enumNames)
        {
            stringBuilder.Append(enumName);
            stringBuilder.Append('\n');
        }
        
        return $"<{Name}:enum\n {{ {stringBuilder} }}>";
    }
}