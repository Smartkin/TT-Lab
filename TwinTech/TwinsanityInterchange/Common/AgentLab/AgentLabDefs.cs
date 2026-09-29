using System;
using System.Collections.Generic;

namespace Twinsanity.TwinsanityInterchange.Common.AgentLab
{
    public class AgentLabDefs
    {
        public Dictionary<String, String> ConditionMap { get; set; }
        public List<String> CommandSizes { get; set; }
        public List<Int32> DeletedCommands { get; set; }
        public Dictionary<String, AgentLabCommandDef> CommandMap { get; set; }
    }
    public class AgentLabCommandDef
    {
        public String Name { get; set; }
        /// <summary>
        /// Type of each argument dword: single, int32, hex, or the tagged values tfloat, tint and tangle
        /// </summary>
        public List<String> Arguments { get; set; }
        /// <summary>
        /// Names of the arguments in the same order, missing ones get param1, param2...
        /// </summary>
        public List<String> ArgumentNames { get; set; }
        /// <summary>
        /// Names the command went by before, which scripts written with them still compile under
        /// </summary>
        public List<String> Aliases { get; set; }
        /// <summary>
        /// The fields of the arguments the game packs into one dword (flags, bytes, halfwords), by argument index. The
        /// fields of an argument cover all its 32 bits, so scripts can hold every value the game's do
        /// </summary>
        public Dictionary<String, List<AgentLabArgumentField>> ArgumentFields { get; set; }

        public List<AgentLabArgumentField> GetFields(Int32 argument)
        {
            if (ArgumentFields == null || !ArgumentFields.TryGetValue(argument.ToString(), out var fields))
            {
                return null;
            }

            return fields;
        }
    }
    /// <summary>
    /// One field of a packed argument: <see cref="Width"/> bits from <see cref="Bit"/> up, a bool (1 bit), an unsigned int or a signed int (sint)
    /// </summary>
    public class AgentLabArgumentField
    {
        public const String BoolType = "bool";
        public const String IntType = "int";
        public const String SignedIntType = "sint";

        public String Name { get; set; }
        public Int32 Bit { get; set; }
        public Int32 Width { get; set; }
        public String Type { get; set; }

        public Boolean IsBool => Type == BoolType;
        public Boolean IsSigned => Type == SignedIntType;
        public UInt32 Mask => Width >= 32 ? 0xFFFFFFFF : ((1u << Width) - 1) << Bit;

        /// <summary>
        /// The field's value in a dword, sign extended when signed
        /// </summary>
        public Int64 Get(UInt32 value)
        {
            var raw = (value & Mask) >> Bit;
            if (IsSigned && Width < 32 && (raw & (1u << (Width - 1))) != 0)
            {
                return (Int64)raw - (1L << Width);
            }

            return raw;
        }

        /// <summary>
        /// Whether a value fits the field: 0 or 1 for bools, the unsigned or two's complement range otherwise
        /// </summary>
        public Boolean Fits(Int64 value)
        {
            if (IsBool)
            {
                return value == 0 || value == 1;
            }

            if (Width >= 32)
            {
                return IsSigned ? value >= Int32.MinValue && value <= Int32.MaxValue : value >= 0 && value <= UInt32.MaxValue;
            }

            return IsSigned ? value >= -(1L << (Width - 1)) && value < 1L << (Width - 1) : value >= 0 && value < 1L << Width;
        }

        public UInt32 Set(UInt32 dword, Int64 value)
        {
            return (dword & ~Mask) | (((UInt32)value << Bit) & Mask);
        }
    }
    /// <summary>
    /// The script form of a packed argument: <c>{name = value, ...}</c> with the fields that aren't 0 in the order they're declared
    /// </summary>
    public static class PackedArgument
    {
        public static String Format(UInt32 value, IReadOnlyList<AgentLabArgumentField> fields)
        {
            var parts = new List<String>();
            foreach (var field in fields)
            {
                var fieldValue = field.Get(value);
                if (fieldValue == 0)
                {
                    continue;
                }

                if (field.IsBool)
                {
                    parts.Add($"{field.Name} = true");
                }
                else if (field.Name.StartsWith("unused"))
                {
                    // leftover tool memory, kept for byte exact scripts, is easier to tell apart in hex
                    parts.Add($"{field.Name} = 0x{(UInt32)fieldValue & (field.Mask >> field.Bit):X}");
                }
                else
                {
                    parts.Add($"{field.Name} = {fieldValue}");
                }
            }

            return "{" + String.Join(", ", parts) + "}";
        }
    }
}
