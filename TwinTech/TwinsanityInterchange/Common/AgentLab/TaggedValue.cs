using System;
using System.Globalization;

namespace Twinsanity.TwinsanityInterchange.Common.AgentLab
{
    /// <summary>
    /// A command argument that is either a literal or a reference to one of the instance's properties. Bits 1-2 hold the
    /// type (int, angle or float), bit 0 whether bits 3-31 are a property index. A literal float keeps its bits with the
    /// low three cleared, a literal int is shifted up by three and an angle is a float in radians
    /// </summary>
    public static class TaggedValue
    {
        public const UInt32 PropertyBit = 0x1;
        public const UInt32 TypeMask = 0x6;
        public const UInt32 TypeInt = 0x0;
        public const UInt32 TypeAngle = 0x2;
        public const UInt32 TypeFloat = 0x4;

        public static Boolean IsProperty(UInt32 value)
        {
            return (value & PropertyBit) != 0;
        }

        public static UInt32 TypeOf(UInt32 value)
        {
            return value & TypeMask;
        }

        public static Int32 PropertyIndex(UInt32 value)
        {
            return (Int32)(value >> 3);
        }

        public static Single FloatValue(UInt32 value)
        {
            return BitConverter.UInt32BitsToSingle(value & ~7u);
        }

        public static Int32 IntValue(UInt32 value)
        {
            return (Int32)value >> 3;
        }

        public static Single AngleDegrees(UInt32 value)
        {
            return FloatValue(value) * (180.0f / MathF.PI);
        }

        public static UInt32 FromFloat(Single value)
        {
            return (BitConverter.SingleToUInt32Bits(value) & ~7u) | TypeFloat;
        }

        public static UInt32 FromInt(Int32 value)
        {
            return ((UInt32)value << 3) | TypeInt;
        }

        public static UInt32 FromAngle(Single degrees)
        {
            return (BitConverter.SingleToUInt32Bits(degrees * (MathF.PI / 180.0f)) & ~7u) | TypeAngle;
        }

        public static UInt32 FromProperty(Int32 index, UInt32 type)
        {
            return ((UInt32)index << 3) | PropertyBit | (type & TypeMask);
        }

        /// <summary>
        /// A float the way a script writes it, always lexing as a float (so -0 keeps its sign) and only when the text gives
        /// exactly these bits back: NaN, infinities and denormals (leftover memory) have no literal and return null
        /// </summary>
        public static String FloatLiteral(Single value)
        {
            if (!float.IsFinite(value) || value != 0.0f && MathF.Abs(value) < 1e-30f)
            {
                return null;
            }

            var text = value.ToString("R", CultureInfo.InvariantCulture);
            if (!text.Contains('.') && !text.Contains('E') && !text.Contains('e'))
            {
                text += ".0";
            }

            return BitConverter.SingleToUInt32Bits(Single.Parse(text, CultureInfo.InvariantCulture)) == BitConverter.SingleToUInt32Bits(value) ? text : null;
        }

        /// <summary>
        /// The type an AgentLab parameter type name stands for, null when it isn't a tagged type
        /// </summary>
        public static UInt32? TypeOfLabType(String labType)
        {
            switch (labType)
            {
                case "tfloat":
                    return TypeFloat;
                case "tint":
                    return TypeInt;
                case "tangle":
                    return TypeAngle;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The argument the way scripts write it: a literal of the parameter's type, Prop(index), or Raw(0x...) for bits
        /// that don't come back from the literal (another type than the parameter's, or an angle that doesn't round trip)
        /// </summary>
        public static String Format(UInt32 value, UInt32 expectedType)
        {
            if (TypeOf(value) == expectedType)
            {
                if (IsProperty(value))
                {
                    return $"Prop({PropertyIndex(value)})";
                }

                switch (expectedType)
                {
                    case TypeFloat:
                    {
                        // Denormal floats are leftovers rather than values and old scripts printed property references as them, so the bits stay explicit
                        var literal = FloatLiteral(FloatValue(value));
                        if (literal != null)
                        {
                            return literal;
                        }

                        break;
                    }
                    case TypeInt:
                        return IntValue(value).ToString(CultureInfo.InvariantCulture);
                    case TypeAngle:
                    {
                        var degrees = AngleDegrees(value);
                        var literal = FloatLiteral(degrees);
                        if (literal != null && FromAngle(degrees) == value)
                        {
                            return literal;
                        }

                        break;
                    }
                }
            }

            return $"Raw(0x{value:X8})";
        }

        /// <summary>
        /// A value of its own type the way <see cref="ParseKeepingType"/> reads it back: an int as it is, Float(x) and Angle(x) (degrees),
        /// Prop(index), and Raw(0x...) for bits no literal gives back or the type none of the three (words the tools left in instances'
        /// properties)
        /// </summary>
        public static String Describe(UInt32 value)
        {
            var type = TypeOf(value);
            if (type == TypeMask)
            {
                return $"Raw(0x{value:X8})";
            }

            if (type == TypeAngle && !IsProperty(value))
            {
                var degrees = ShortestDegrees(value);
                return degrees != null ? "Angle(" + degrees + ")" : $"Raw(0x{value:X8})";
            }

            var text = Format(value, type);
            if (IsProperty(value) || type == TypeInt || text.StartsWith("Raw("))
            {
                return text;
            }

            return "Float(" + text + ")";
        }

        // The fewest decimals of degrees that give the angle's bits back: the tag clears the radians' low bits, so the degrees worked
        // out from them rarely come back to the same bits themselves
        private static String ShortestDegrees(UInt32 value)
        {
            var degrees = FloatValue(value) * (180.0 / Math.PI);
            for (var decimals = 0; decimals <= 9; ++decimals)
            {
                var literal = FloatLiteral((Single)Math.Round(degrees, decimals));
                if (literal != null && FromAngle(Single.Parse(literal, CultureInfo.InvariantCulture)) == value)
                {
                    return literal;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads what <see cref="Describe"/> writes: a plain literal and Prop(index) keep the current value's type
        /// </summary>
        public static UInt32 ParseKeepingType(String text, UInt32 current)
        {
            var type = TypeOf(current);
            return Parse(text, type == TypeMask ? TypeInt : type);
        }

        /// <summary>
        /// Reads what <see cref="Format"/> writes, plus Float(x), Int(x) and Angle(x) for a literal of another type than the parameter's
        /// </summary>
        public static UInt32 Parse(String text, UInt32 expectedType)
        {
            text = text.Trim();
            var open = text.IndexOf('(');
            if (open > 0 && text.EndsWith(")"))
            {
                var call = text.Substring(0, open).Trim();
                var inner = text.Substring(open + 1, text.Length - open - 2).Trim();
                switch (call)
                {
                    case "Prop":
                        return FromProperty(Int32.Parse(inner, CultureInfo.InvariantCulture), expectedType);
                    case "Raw":
                        return inner.StartsWith("0x") ? Convert.ToUInt32(inner.Substring(2), 16) : UInt32.Parse(inner, CultureInfo.InvariantCulture);
                    case "Float":
                        return FromFloat(Single.Parse(inner, CultureInfo.InvariantCulture));
                    case "Int":
                        return FromInt(Int32.Parse(inner, CultureInfo.InvariantCulture));
                    case "Angle":
                        return FromAngle(Single.Parse(inner, CultureInfo.InvariantCulture));
                }
            }

            switch (expectedType)
            {
                case TypeFloat:
                    return FromFloat(Single.Parse(text, CultureInfo.InvariantCulture));
                case TypeAngle:
                    return FromAngle(Single.Parse(text, CultureInfo.InvariantCulture));
                default:
                    return FromInt(Int32.Parse(text, CultureInfo.InvariantCulture));
            }
        }
    }
}
