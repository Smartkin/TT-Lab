using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Twinsanity.AgentLab;

public class AgentLabLexer : IDisposable
{
    private StringReader _reader;
    private AgentLabToken _currentToken;
    private List<AgentLabToken> _reservedKeywords = new();
    private char[] _reservedChar = { '/', '*', '!', '[', ']', '{', '}', '(', ')', '+', '-', '=', ';', ':', ',', '>', '<', '"', '\'' };
    private Char? _currentChar;
    
    public AgentLabLexer(string text)
    {
        InitKeywords();
        _reader = new StringReader(text);
        Advance();
    }

    public AgentLabLexer(StringReader reader)
    {
        InitKeywords();
        _reader = reader;
        Advance();
    }

    private void InitKeywords()
    {
        static AgentLabToken GenerateKeyword(string keyword)
        {
            return new AgentLabToken(AgentLabToken.TokenType.Identifier, keyword);
        }

        static AgentLabToken GenerateKeywordType(AgentLabToken.TokenType type, string keyword)
        {
            return new AgentLabToken(type, keyword);
        }
        
        _reservedKeywords.Clear();
        _reservedKeywords.Add(GenerateKeyword("library"));
        _reservedKeywords.Add(GenerateKeyword("behaviour"));
        _reservedKeywords.Add(GenerateKeyword("state"));
        _reservedKeywords.Add(GenerateKeyword("starter"));
        _reservedKeywords.Add(GenerateKeyword("interval"));
        _reservedKeywords.Add(GenerateKeyword("threshold"));
        _reservedKeywords.Add(GenerateKeyword("if"));
        _reservedKeywords.Add(GenerateKeyword("const"));
        _reservedKeywords.Add(GenerateKeyword("execute"));
        _reservedKeywords.Add(GenerateKeyword("true"));
        _reservedKeywords.Add(GenerateKeyword("false"));
        _reservedKeywords.Add(GenerateKeywordType(AgentLabToken.TokenType.BooleanType, "bool"));
        _reservedKeywords.Add(GenerateKeywordType(AgentLabToken.TokenType.IntegerType, "int"));
        _reservedKeywords.Add(GenerateKeywordType(AgentLabToken.TokenType.FloatType, "float"));
        _reservedKeywords.Add(GenerateKeywordType(AgentLabToken.TokenType.Action, "action"));
        _reservedKeywords.Add(GenerateKeywordType(AgentLabToken.TokenType.Condition, "condition"));
        _reservedKeywords.Add(GenerateKeyword("GlobalObjectId"));
        _reservedKeywords.Add(GenerateKeyword("Priority"));
        _reservedKeywords.Add(GenerateKeyword("NonBlocking"));
        _reservedKeywords.Add(GenerateKeyword("SkipFirstBody"));
        _reservedKeywords.Add(GenerateKeyword("UseObjectSlot"));
        _reservedKeywords.Add(GenerateKeyword("ControlPacket"));
        _reservedKeywords.Add(GenerateKeyword("GlobalIndex"));
        _reservedKeywords.Add(GenerateKeyword("InstanceType"));
        _reservedKeywords.Add(GenerateKeyword("settings"));
        _reservedKeywords.Add(GenerateKeyword("data"));
    }

    private void Advance()
    {
        var peek = _reader.Peek();
        if (peek != -1)
        {
            _currentChar = (char)_reader.Read();
        }
        else
        {
            _currentChar = null;
        }
    }

    private Char? Peek()
    {
        Char? resultChar = null;
        var peek = _reader.Peek();
        if (peek != -1)
        {
            resultChar = (Char)_reader.Peek();
        }

        return resultChar;
    }

    private string AdvanceLine(char stopCharacter = '\n')
    {
        var line = "";
        while (_currentChar.HasValue && _currentChar.Value != stopCharacter)
        {
            line += _currentChar.Value;
            Advance();
        }

        return line;
    }

    private string AdvanceLexeme()
    {
        var lexeme = string.Empty;
        while (_currentChar.HasValue && !char.IsWhiteSpace(_currentChar.Value) && !_reservedChar.Contains(_currentChar.Value))
        {
            lexeme += _currentChar.Value;
            Advance();
        }
        
        return lexeme;
    }

    private AgentLabToken GetIdentifier()
    {
        var result = AdvanceLexeme();
        var token = _reservedKeywords.Where(keyword => keyword.ToString() == result).FirstOrDefault(new AgentLabToken(AgentLabToken.TokenType.Identifier, result));
        return token;
    }

    public AgentLabToken GetNextToken()
    {
        while (_currentChar != null)
        {
            AgentLabToken token = new AgentLabToken(AgentLabToken.TokenType.Eof, null);

            if (char.IsWhiteSpace(_currentChar.Value))
            {
                Advance();
                continue;
            }
            
            if (char.IsAsciiDigit(_currentChar.Value))
            {
                var lexeme = AdvanceLexeme();
                if (_currentChar == '-')
                {
                    lexeme += _currentChar;
                    Advance();
                    lexeme += AdvanceLexeme();
                }
                
                if (lexeme.Contains('.') || (lexeme.ToLower().Contains('e') && !lexeme.StartsWith("0x")))
                {
                    if (float.TryParse(lexeme, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    {
                        token = new AgentLabToken(AgentLabToken.TokenType.FloatingPoint, value);
                    }
                    else
                    {
                        // TODO: Raise lexer error
                    }
                }
                else
                {
                    if (lexeme.StartsWith("0x"))
                    {
                        if (int.TryParse(lexeme[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parseResult))
                        {
                            token = new AgentLabToken(AgentLabToken.TokenType.Integer, parseResult);
                        }
                    }
                    else if (int.TryParse(lexeme, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    {
                        token = new AgentLabToken(AgentLabToken.TokenType.Integer, value);
                    }
                    else
                    {
                        // TODO: Raise lexer error
                    }
                }
            }

            if (token.Type == AgentLabToken.TokenType.Eof)
            {
                token = _currentChar switch
                {
                    '"' or '\'' => new AgentLabToken(AgentLabToken.TokenType.String, (new Func<string>(() =>
                    {
                        var skipChar = _currentChar.Value;
                        Advance();
                        return AdvanceLine(skipChar);
                    })).Invoke()),
                    '/' when Peek() == '/' => new AgentLabToken(AgentLabToken.TokenType.Comment, AdvanceLine()),
                    '=' when Peek() == '=' => new AgentLabToken(AgentLabToken.TokenType.Equality, "=="),
                    '>' when Peek() == '=' => new AgentLabToken(AgentLabToken.TokenType.GreaterEqual, ">="),
                    '<' when Peek() == '=' => new AgentLabToken(AgentLabToken.TokenType.LessEqual, "<="),
                    ',' => new AgentLabToken(AgentLabToken.TokenType.Comma, ','),
                    ':' => new AgentLabToken(AgentLabToken.TokenType.Colon, ':'),
                    ';' => new AgentLabToken(AgentLabToken.TokenType.Semicolon, ';'),
                    '!' => new AgentLabToken(AgentLabToken.TokenType.Not, '!'),
                    '=' => new AgentLabToken(AgentLabToken.TokenType.Assign, '='),
                    '[' => new AgentLabToken(AgentLabToken.TokenType.AttributeOpen, '['),
                    ']' => new AgentLabToken(AgentLabToken.TokenType.AttributeClose, ']'),
                    '+' => new AgentLabToken(AgentLabToken.TokenType.AddOperator, '+'),
                    '-' => new AgentLabToken(AgentLabToken.TokenType.SubtractOperator, '-'),
                    '*' => new AgentLabToken(AgentLabToken.TokenType.MultiplyOperator, '*'),
                    '/' => new AgentLabToken(AgentLabToken.TokenType.DivideOperator, '/'),
                    '(' => new AgentLabToken(AgentLabToken.TokenType.LeftParen, '('),
                    ')' => new AgentLabToken(AgentLabToken.TokenType.RightParen, ')'),
                    '{' => new AgentLabToken(AgentLabToken.TokenType.OpenBracket, '{'),
                    '}' => new AgentLabToken(AgentLabToken.TokenType.CloseBracket, '}'),
                    _ => token
                };
                
                if (token.Type != AgentLabToken.TokenType.Comment && token.Type != AgentLabToken.TokenType.Eof)
                {
                    Advance();
                }

                switch (token.Type)
                {
                    case AgentLabToken.TokenType.Equality or AgentLabToken.TokenType.GreaterEqual or AgentLabToken.TokenType.LessEqual:
                        Advance();
                        break;
                    case AgentLabToken.TokenType.Comment:
                        return GetNextToken();
                }
            }

            if (token.Type == AgentLabToken.TokenType.Eof)
            {
                _currentToken = GetIdentifier();
                return _currentToken;
            }
            
            _currentToken = token;
            return token;
        }
        
        return new AgentLabToken(AgentLabToken.TokenType.Eof, null);
    }
    
    public void Dispose()
    {
        _reader?.Dispose();
    }
}