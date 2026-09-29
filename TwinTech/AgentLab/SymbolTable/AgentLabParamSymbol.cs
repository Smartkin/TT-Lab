using System.Collections.Generic;

namespace Twinsanity.AgentLab.SymbolTable;

internal class AgentLabParamSymbol : AgentLabSymbol
{
    /// <summary>
    /// The fields of a packed parameter, in bit order, null for a plain one
    /// </summary>
    public IReadOnlyList<AgentLabFieldDefinition> Fields { get; }

    public AgentLabParamSymbol(string name, AgentLabSymbol type = null, IReadOnlyList<AgentLabFieldDefinition> fields = null) : base(name, type)
    {
        Fields = fields;
    }

    public AgentLabFieldDefinition FindField(string name)
    {
        if (Fields == null)
        {
            return null;
        }

        foreach (var field in Fields)
        {
            if (field.Name == name)
            {
                return field;
            }
        }

        return null;
    }
}

/// <summary>
/// One field of a packed parameter: <see cref="Width"/> bits from <see cref="Bit"/> up
/// </summary>
internal record AgentLabFieldDefinition(string Name, int Bit, int Width, bool IsBool, bool IsSigned)
{
    public uint Mask => Width >= 32 ? 0xFFFFFFFF : ((1u << Width) - 1) << Bit;

    public bool Fits(long value)
    {
        if (IsBool)
        {
            return value is 0 or 1;
        }

        if (Width >= 32)
        {
            return IsSigned ? value is >= int.MinValue and <= int.MaxValue : value is >= 0 and <= uint.MaxValue;
        }

        return IsSigned ? value >= -(1L << (Width - 1)) && value < 1L << (Width - 1) : value >= 0 && value < 1L << Width;
    }

    public uint Set(uint dword, long value) => (dword & ~Mask) | (((uint)value << Bit) & Mask);

    public override string ToString() => IsBool ? $"bool {Name}" : $"{(IsSigned ? "sint" : "int")} {Name} : {Width}";
}
