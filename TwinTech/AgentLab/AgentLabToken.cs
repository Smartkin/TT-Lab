using System;
using System.Diagnostics;
using System.Globalization;

namespace Twinsanity.AgentLab;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public struct AgentLabToken
{
    private readonly object _value;
    
    public enum TokenType
    {
        Eof,
        LeftParen, // '('
        RightParen, // ')'
        OpenBracket, // '{'
        CloseBracket, // '}'
        AttributeOpen, // '['
        AttributeClose, // ']'
        Semicolon, // ';'
        Colon, // ':'
        Comma, // ','
        Not, // '!'
        Equality, // "=="
        GreaterEqual, // ">="
        LessEqual, // "<="
        Interval, // "interval"
        Assign, // '='
        Identifier, // (any identifier including reserved keywords)
        Priority, // "Priority"
        NonBlocking, // "NonBlocking"
        SkipFirstBody, // "SkipFirstBody"
        UseObjectSlot, // "UseObjectSlot"
        ControlPacket, // "ControlPacket"
        GlobalIndex, // "GlobalIndex"
        InstanceType, // "InstanceType"
        Const, // "const"
        Data, // "data"
        Settings, // "settings"
        Starter, // "starter"
        If, // "if"
        Comment, // "//"
        Execute, // "execute"
        Behaviour, // "behaviour"
        Library, // "library"
        State, // "state"
        True, // "true"
        False, // "false"
        Boolean, // true or false
        String, // any string in single or double quotes
        Integer, // any integer number
        FloatingPoint, // any floating point number
        FloatType, // "float"
        EnumType, // "enum"
        StringType, // "string"
        IntegerType, // "int"
        BooleanType, // "bool"
        Action, // "action"
        Condition, // "condition"
        BehaviourLibrary, // "behaviour_library"
        AddOperator, // "+"
        SubtractOperator, // "-"
        MultiplyOperator, // "*"
        DivideOperator, // "/"
    }

    public AgentLabToken(TokenType tokenType, object value)
    {
        if (value != null && tokenType == TokenType.Identifier && Enum.TryParse(value.ToString(), true, out TokenType actualTokenType))
        {
            tokenType = actualTokenType;
        }
        
        Type = tokenType;
        _value = value;
    }
    
    public TokenType Type { get; }

    public T GetValue<T>()
    {
        return (T)_value;
    }

    public override String ToString()
    {
        return Type == TokenType.FloatingPoint ? GetValue<float>().ToString(CultureInfo.InvariantCulture) : _value?.ToString();
    }
    
    public string ToDebugString() => DebuggerDisplay;
    
    private string DebuggerDisplay => $"Token {Type.ToString()} {this}";
}