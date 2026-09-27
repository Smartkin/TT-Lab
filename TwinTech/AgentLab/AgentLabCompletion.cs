using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.AgentLab.SymbolTable;

namespace Twinsanity.AgentLab;

/// <summary>
/// What a completion suggestion refers to
/// </summary>
public enum AgentLabCompletionKind
{
    /// <summary>
    /// Language keyword
    /// </summary>
    Keyword,
    /// <summary>
    /// Action from the action definitions
    /// </summary>
    Action,
    /// <summary>
    /// Condition from the condition definitions
    /// </summary>
    Condition,
    /// <summary>
    /// State of the behaviour being edited
    /// </summary>
    State,
    /// <summary>
    /// Control packet of the behaviour being edited
    /// </summary>
    ControlPacket,
    /// <summary>
    /// Attribute name
    /// </summary>
    Attribute,
    /// <summary>
    /// Constant, either built in or declared in the behaviour
    /// </summary>
    Constant,
    /// <summary>
    /// Value of an enumeration
    /// </summary>
    EnumValue
}

/// <summary>
/// Single completion suggestion
/// </summary>
public class AgentLabCompletionItem
{
    /// <summary>
    /// Creates a suggestion
    /// </summary>
    public AgentLabCompletionItem(string text, AgentLabCompletionKind kind, string description)
    {
        Text = text;
        Kind = kind;
        Description = description;
    }

    /// <summary>
    /// Text that gets inserted
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// What the suggestion refers to
    /// </summary>
    public AgentLabCompletionKind Kind { get; }

    /// <summary>
    /// Signature or explanation shown next to the suggestion
    /// </summary>
    public string Description { get; }
}

/// <summary>
/// Suggestions for a place in a script
/// </summary>
public class AgentLabCompletionResult
{
    /// <summary>
    /// Creates the result
    /// </summary>
    public AgentLabCompletionResult(int startOffset, IReadOnlyList<AgentLabCompletionItem> items)
    {
        StartOffset = startOffset;
        Items = items;
    }

    /// <summary>
    /// Start of the word being completed, a suggestion replaces the text from here up to the caret
    /// </summary>
    public int StartOffset { get; }

    /// <summary>
    /// Suggestions valid at the caret
    /// </summary>
    public IReadOnlyList<AgentLabCompletionItem> Items { get; }
}

/// <summary>
/// Parameters of the action or condition call the caret is in
/// </summary>
public class AgentLabSignature
{
    /// <summary>
    /// Creates the signature
    /// </summary>
    public AgentLabSignature(string name, AgentLabCompletionKind kind, IReadOnlyList<string> parameters, int currentParameter)
    {
        Name = name;
        Kind = kind;
        Parameters = parameters;
        CurrentParameter = currentParameter;
    }

    /// <summary>
    /// Name of the called action or condition
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether it's an action or a condition
    /// </summary>
    public AgentLabCompletionKind Kind { get; }

    /// <summary>
    /// Every parameter as its type and name
    /// </summary>
    public IReadOnlyList<string> Parameters { get; }

    /// <summary>
    /// Index of the parameter the caret is at
    /// </summary>
    public int CurrentParameter { get; }
}

/// <summary>
/// Suggests what can be written at a place in a behaviour script. Scripts being edited are rarely valid so they aren't parsed,
/// the text before the caret is scanned to find out which block and statement the caret is in instead
/// </summary>
public static class AgentLabCompletion
{
    private static readonly ConcurrentDictionary<string, Definitions> DefinitionsCache = new();

    private static readonly string[] BehaviourAttributes = { "StartFrom", "Priority", "GraphPriority" };
    private static readonly string[] LibraryAttributes = { "GlobalIndex", "InstanceType" };
    private static readonly string[] StateAttributes = { "NonBlocking", "SkipFirstBody", "UseObjectSlot", "ControlPacket", "Unknown" };
    private static readonly HashSet<string> StarterNames = new() { "AssignType", "AssignLocality", "AssignStatus", "AssignPreference", "GlobalObjectId" };
    private static readonly HashSet<string> AttributeEnums = new() { "ObjectBehaviourSlot", "InstanceType" };

    private enum BlockKind
    {
        TopLevel,
        Behaviour,
        Library,
        LinearBehaviour,
        State,
        StateBody,
        Packet,
        Settings,
        Data,
        Starter,
        Assigner,
        Unknown
    }

    private enum TokenKind
    {
        Identifier,
        Number,
        Symbol
    }

    private readonly struct Token
    {
        public Token(TokenKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public TokenKind Kind { get; }
        public string Text { get; }
    }

    private sealed class Block
    {
        public Block(BlockKind kind)
        {
            Kind = kind;
        }

        public BlockKind Kind { get; }
        public HashSet<string> Statements { get; } = new();
    }

    private sealed class Context
    {
        public BlockKind Block { get; init; }
        public HashSet<string> BlockStatements { get; init; }
        public List<Token> Statement { get; init; }
    }

    private sealed class Callable
    {
        public string Name { get; init; }
        public AgentLabCompletionKind Kind { get; init; }
        public List<string> Parameters { get; init; }

        public string Signature => $"{(Kind == AgentLabCompletionKind.Action ? "action" : "condition")} {Name}({string.Join(", ", Parameters)})";
    }

    private sealed class Definitions
    {
        public Dictionary<string, Callable> Actions { get; } = new();
        public Dictionary<string, Callable> Conditions { get; } = new();
        public Dictionary<string, List<string>> Enums { get; } = new();
        public Dictionary<string, string> Constants { get; } = new();
    }

    /// <summary>
    /// Gets the suggestions for the caret's place in the script
    /// </summary>
    /// <param name="script">AgentLab code</param>
    /// <param name="offset">Caret's offset in the script</param>
    /// <param name="actionDefinitionsFile">Name of the AgentLab file that contains action definitions</param>
    /// <param name="commandsOnly">Whether the script can only be a list of commands, like an object's command pack</param>
    /// <returns>Suggestions, empty inside of comments, strings and numbers</returns>
    public static AgentLabCompletionResult GetCompletions(string script, int offset, string actionDefinitionsFile, bool commandsOnly = false)
    {
        offset = Math.Clamp(offset, 0, script.Length);
        var wordStart = offset;
        while (wordStart > 0 && IsIdentifierChar(script[wordStart - 1]))
        {
            wordStart--;
        }

        var empty = new AgentLabCompletionResult(wordStart, Array.Empty<AgentLabCompletionItem>());
        if (wordStart < offset && char.IsAsciiDigit(script[wordStart]))
        {
            return empty;
        }

        var tokens = Scan(script, wordStart, out var insideCommentOrString);
        if (insideCommentOrString)
        {
            return empty;
        }

        var definitions = GetDefinitions(actionDefinitionsFile);
        var items = GetItems(GetContext(tokens), definitions, script, commandsOnly);
        return new AgentLabCompletionResult(wordStart, items);
    }

    /// <summary>
    /// Gets the parameters of the action or condition call the caret is in
    /// </summary>
    /// <param name="script">AgentLab code</param>
    /// <param name="offset">Caret's offset in the script</param>
    /// <param name="actionDefinitionsFile">Name of the AgentLab file that contains action definitions</param>
    /// <returns>The signature, null when the caret isn't within a call's parentheses</returns>
    public static AgentLabSignature GetSignature(string script, int offset, string actionDefinitionsFile)
    {
        offset = Math.Clamp(offset, 0, script.Length);
        var tokens = Scan(script, offset, out var insideCommentOrString);
        if (insideCommentOrString)
        {
            return null;
        }

        var statement = GetContext(tokens).Statement;
        var call = FindOpenCall(statement);
        if (call < 1 || statement[call - 1].Kind != TokenKind.Identifier)
        {
            return null;
        }

        var definitions = GetDefinitions(actionDefinitionsFile);
        var name = statement[call - 1].Text;
        if (!definitions.Actions.TryGetValue(name, out var callable) && !definitions.Conditions.TryGetValue(name, out callable))
        {
            return null;
        }

        var parameter = 0;
        var depth = 0;
        for (var i = call + 1; i < statement.Count; i++)
        {
            switch (statement[i].Text)
            {
                case "(":
                    depth++;
                    break;
                case ")":
                    depth--;
                    break;
                case "," when depth == 0:
                    parameter++;
                    break;
            }
        }

        return new AgentLabSignature(callable.Name, callable.Kind, callable.Parameters, parameter);
    }

    private static List<AgentLabCompletionItem> GetItems(Context context, Definitions definitions, string script, bool commandsOnly)
    {
        var items = new List<AgentLabCompletionItem>();
        var statement = context.Statement;

        // Attributes are only written in front of behaviours, libraries and states, anywhere else a bracket indexes an array
        if (statement.Count > 0 && statement[0].Text == "[")
        {
            if (statement.Count == 1)
            {
                var attributes = context.Block switch
                {
                    BlockKind.TopLevel => BehaviourAttributes.Concat(LibraryAttributes),
                    BlockKind.Behaviour => StateAttributes,
                    _ => Enumerable.Empty<string>()
                };
                items.AddRange(attributes.Select(attribute => new AgentLabCompletionItem(attribute, AgentLabCompletionKind.Attribute, $"[{attribute}] attribute")));
            }
            else if (statement.Count == 3 && statement[2].Text == "(")
            {
                switch (statement[1].Text)
                {
                    case "StartFrom":
                        items.AddRange(GetStates(script));
                        break;
                    case "ControlPacket":
                        items.AddRange(GetPackets(script));
                        break;
                    case "UseObjectSlot":
                        items.AddRange(GetEnumValues(definitions, "ObjectBehaviourSlot"));
                        break;
                    case "InstanceType":
                        items.AddRange(GetEnumValues(definitions, "InstanceType"));
                        break;
                }
            }

            return items;
        }

        if (statement.Count > 0 && statement[^1].Text == "[")
        {
            return items;
        }

        if (FindOpenCall(statement) >= 0)
        {
            items.AddRange(GetValues(script, true));
            return items;
        }

        var first = statement.Count > 0 ? statement[0].Text : null;
        switch (context.Block)
        {
            case BlockKind.TopLevel when statement.Count == 0 && commandsOnly:
                items.AddRange(GetActions(definitions));
                break;
            case BlockKind.TopLevel when statement.Count == 0:
                items.Add(Keyword("behaviour", "Behaviour made of states"));
                items.Add(Keyword("library", "Library of behaviours made of actions only"));
                break;
            case BlockKind.Library when statement.Count == 0:
                items.Add(Keyword("behaviour", "Behaviour made of actions only"));
                items.AddRange(GetActions(definitions));
                break;
            case BlockKind.LinearBehaviour when statement.Count == 0:
                items.AddRange(GetActions(definitions));
                break;
            case BlockKind.Behaviour when statement.Count == 0:
                items.Add(Keyword("state", "State of the behaviour"));
                items.Add(Keyword("packet", "Control packet that states can use"));
                items.Add(Keyword("const", "Constant of the behaviour"));
                items.Add(Keyword("starter", "Rules the behaviour gets started with"));
                break;
            case BlockKind.Behaviour when first == "const" && IsAfterAssign(statement):
                items.AddRange(GetValues(script, true));
                break;
            case BlockKind.State when statement.Count == 0:
                items.Add(Keyword("if", "Body that runs when its condition is met"));
                break;
            case BlockKind.State when statement.Count == 1 && first == "if":
                items.AddRange(GetConditions(definitions));
                break;
            case BlockKind.StateBody when statement.Count == 0:
                if (!context.BlockStatements.Contains("interval"))
                {
                    items.Add(Keyword("interval", "interval = <value>;"));
                }

                if (!context.BlockStatements.Contains("unknown"))
                {
                    items.Add(Keyword("unknown", "unknown = <true or false>;"));
                }

                items.Add(Keyword("execute", "Switches to another state"));
                items.AddRange(GetActions(definitions));
                break;
            case BlockKind.StateBody when statement.Count == 1 && first == "execute":
                items.AddRange(GetStates(script));
                break;
            case BlockKind.StateBody when first == "unknown" && statement.Count == 2 && statement[1].Text == "=":
                items.AddRange(GetBooleans());
                break;
            case BlockKind.Packet when statement.Count == 0:
                items.Add(Keyword("settings", "How the packet moves the agent"));
                items.Add(Keyword("data", "Values the packet moves the agent with"));
                break;
            case BlockKind.Settings when statement.Count == 0:
                items.AddRange(definitions.Enums.Keys.Where(IsSetting).Select(name => Constant(name, $"{name} = {string.Join(" | ", definitions.Enums[name])}")));
                items.AddRange(definitions.Constants.Where(constant => constant.Value == "bool").Select(constant => Constant(constant.Key, $"{constant.Key} = <true or false>")));
                break;
            case BlockKind.Settings when statement.Count == 2 && statement[1].Text == "=":
                items.AddRange(definitions.Enums.ContainsKey(first) ? GetEnumValues(definitions, first) : GetBooleans());
                break;
            case BlockKind.Data when statement.Count == 0:
                items.AddRange(definitions.Constants.Where(constant => constant.Value is "int" or "float").Select(constant => Constant(constant.Key, $"{constant.Value} {constant.Key}")));
                break;
            case BlockKind.Data when IsAfterAssign(statement):
                items.Add(Constant("InstanceFloat", "InstanceFloat[<index>], float parameter of the instance"));
                items.AddRange(GetValues(script, false));
                break;
            case BlockKind.Starter when statement.Count == 0:
                items.Add(Keyword("assigner", "assigner = { ... }"));
                break;
            case BlockKind.Assigner when statement.Count == 0:
                items.AddRange(StarterNames.Select(name => Constant(name, definitions.Enums.TryGetValue(name, out var values) ? $"{name} = {string.Join(" | ", values)}" : $"{name} = <value>")));
                break;
            case BlockKind.Assigner when statement.Count == 2 && statement[1].Text == "=" && definitions.Enums.ContainsKey(first):
                items.AddRange(GetEnumValues(definitions, first));
                break;
        }

        return items;
    }

    private static bool IsSetting(string enumName) => !StarterNames.Contains(enumName) && !AttributeEnums.Contains(enumName);

    private static bool IsAfterAssign(List<Token> statement) => statement.Any(token => token.Text == "=");

    private static AgentLabCompletionItem Keyword(string keyword, string description) => new(keyword, AgentLabCompletionKind.Keyword, description);

    private static AgentLabCompletionItem Constant(string name, string description) => new(name, AgentLabCompletionKind.Constant, description);

    private static IEnumerable<AgentLabCompletionItem> GetBooleans()
    {
        yield return new AgentLabCompletionItem("true", AgentLabCompletionKind.Keyword, "bool");
        yield return new AgentLabCompletionItem("false", AgentLabCompletionKind.Keyword, "bool");
    }

    private static IEnumerable<AgentLabCompletionItem> GetActions(Definitions definitions)
    {
        return definitions.Actions.Values.OrderBy(action => action.Name, StringComparer.OrdinalIgnoreCase)
            .Select(action => new AgentLabCompletionItem(action.Name, AgentLabCompletionKind.Action, action.Signature));
    }

    private static IEnumerable<AgentLabCompletionItem> GetConditions(Definitions definitions)
    {
        return definitions.Conditions.Values.OrderBy(condition => condition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(condition => new AgentLabCompletionItem(condition.Name, AgentLabCompletionKind.Condition, condition.Signature));
    }

    private static IEnumerable<AgentLabCompletionItem> GetEnumValues(Definitions definitions, string enumName)
    {
        return definitions.Enums.TryGetValue(enumName, out var values)
            ? values.Select(value => new AgentLabCompletionItem(value, AgentLabCompletionKind.EnumValue, $"{enumName} value"))
            : Enumerable.Empty<AgentLabCompletionItem>();
    }

    private static IEnumerable<AgentLabCompletionItem> GetStates(string script)
    {
        return GetDeclared(script, "state").Select(state => new AgentLabCompletionItem(state, AgentLabCompletionKind.State, $"state {state}"));
    }

    private static IEnumerable<AgentLabCompletionItem> GetPackets(string script)
    {
        return GetDeclared(script, "packet").Select(packet => new AgentLabCompletionItem(packet, AgentLabCompletionKind.ControlPacket, $"packet {packet}"));
    }

    private static IEnumerable<AgentLabCompletionItem> GetValues(string script, bool withBooleans)
    {
        var constants = GetDeclared(script, "const").Select(constant => new AgentLabCompletionItem(constant, AgentLabCompletionKind.Constant, $"const {constant}"));
        return withBooleans ? constants.Concat(GetBooleans()) : constants;
    }

    // Names declared by the keyword anywhere in the script, in the order they're declared
    private static IEnumerable<string> GetDeclared(string script, string keyword)
    {
        var tokens = Scan(script, script.Length, out _);
        var names = new List<string>();
        for (var i = 0; i < tokens.Count - 1; i++)
        {
            if (tokens[i].Kind == TokenKind.Identifier && tokens[i].Text == keyword && tokens[i + 1].Kind == TokenKind.Identifier && !names.Contains(tokens[i + 1].Text))
            {
                names.Add(tokens[i + 1].Text);
            }
        }

        return names;
    }

    // Index of the innermost parenthesis in the statement that isn't closed yet, -1 when there's none
    private static int FindOpenCall(List<Token> statement)
    {
        var open = new Stack<int>();
        for (var i = 0; i < statement.Count; i++)
        {
            if (statement[i].Text == "(")
            {
                open.Push(i);
            }
            else if (statement[i].Text == ")" && open.Count > 0)
            {
                open.Pop();
            }
        }

        return open.Count > 0 ? open.Peek() : -1;
    }

    private static Context GetContext(List<Token> tokens)
    {
        var blocks = new Stack<Block>();
        var statement = new List<Token>();
        var bracketDepth = 0;
        foreach (var token in tokens)
        {
            switch (token.Text)
            {
                case "{":
                    blocks.Push(new Block(GetBlockKind(statement, blocks.Count > 0 ? blocks.Peek().Kind : BlockKind.TopLevel)));
                    statement.Clear();
                    bracketDepth = 0;
                    break;
                case "}":
                    if (blocks.Count > 0)
                    {
                        blocks.Pop();
                    }

                    statement.Clear();
                    bracketDepth = 0;
                    break;
                case ";":
                    if (blocks.Count > 0 && statement.Count > 0)
                    {
                        blocks.Peek().Statements.Add(statement[0].Text);
                    }

                    statement.Clear();
                    bracketDepth = 0;
                    break;
                case "[":
                    bracketDepth++;
                    statement.Add(token);
                    break;
                case "]":
                    bracketDepth--;
                    // A finished attribute doesn't belong to the statement after it
                    if (bracketDepth == 0 && statement.Count > 0 && statement[0].Text == "[")
                    {
                        statement.Clear();
                    }
                    else
                    {
                        statement.Add(token);
                    }

                    break;
                default:
                    statement.Add(token);
                    break;
            }
        }

        var current = blocks.Count > 0 ? blocks.Peek() : null;
        return new Context
        {
            Block = current?.Kind ?? BlockKind.TopLevel,
            BlockStatements = current?.Statements ?? new HashSet<string>(),
            Statement = statement
        };
    }

    private static BlockKind GetBlockKind(List<Token> statement, BlockKind parent)
    {
        if (statement.Count == 0)
        {
            return BlockKind.Unknown;
        }

        return statement[0].Text switch
        {
            "behaviour" => parent == BlockKind.Library ? BlockKind.LinearBehaviour : BlockKind.Behaviour,
            "library" => BlockKind.Library,
            "state" => BlockKind.State,
            "if" => BlockKind.StateBody,
            "packet" => BlockKind.Packet,
            "settings" => BlockKind.Settings,
            "data" => BlockKind.Data,
            "starter" => BlockKind.Starter,
            "assigner" => BlockKind.Assigner,
            _ => BlockKind.Unknown
        };
    }

    private static bool IsIdentifierChar(char character) => char.IsLetterOrDigit(character) || character == '_';

    // Splits the script up to the end into tokens, skipping comments and strings
    private static List<Token> Scan(string script, int end, out bool insideCommentOrString)
    {
        var tokens = new List<Token>();
        insideCommentOrString = false;
        var i = 0;
        while (i < end)
        {
            var character = script[i];
            if (char.IsWhiteSpace(character))
            {
                i++;
                continue;
            }

            if (character == '/' && i + 1 < script.Length && script[i + 1] == '/')
            {
                var lineEnd = script.IndexOf('\n', i);
                if (lineEnd == -1 || lineEnd >= end)
                {
                    insideCommentOrString = true;
                    break;
                }

                i = lineEnd + 1;
                continue;
            }

            if (character is '"' or '\'')
            {
                var closing = i + 1;
                while (closing < script.Length && script[closing] != character && script[closing] != '\n')
                {
                    closing++;
                }

                if (closing >= end)
                {
                    insideCommentOrString = true;
                    break;
                }

                i = closing + 1;
                continue;
            }

            if (IsIdentifierChar(character) || character == '.' && i + 1 < end && char.IsAsciiDigit(script[i + 1]))
            {
                var start = i;
                while (i < end && (IsIdentifierChar(script[i]) || script[i] == '.'))
                {
                    i++;
                }

                var text = script[start..i];
                tokens.Add(new Token(char.IsAsciiDigit(text[0]) || text[0] == '.' ? TokenKind.Number : TokenKind.Identifier, text));
                continue;
            }

            if (character is '>' or '<' or '=' && i + 1 < end && script[i + 1] == '=')
            {
                tokens.Add(new Token(TokenKind.Symbol, script.Substring(i, 2)));
                i += 2;
                continue;
            }

            tokens.Add(new Token(TokenKind.Symbol, character.ToString()));
            i++;
        }

        return tokens;
    }

    private static Definitions GetDefinitions(string actionDefinitionsFile)
    {
        return DefinitionsCache.GetOrAdd(actionDefinitionsFile ?? string.Empty, file =>
        {
            var table = new AgentLabSymbolTableBuilder().BuildBuiltInTypes().BuildActions(file).BuildConditions().GetSymbolTable();
            var definitions = new Definitions();
            foreach (var action in table.GetSymbols<AgentLabActionSymbol>())
            {
                definitions.Actions[action.Name] = new Callable
                {
                    Name = action.Name,
                    Kind = AgentLabCompletionKind.Action,
                    Parameters = action.Parameters.GetSymbols<AgentLabParamSymbol>().Select(parameter => $"{GetTypeName(parameter.Type)} {parameter.Name}").ToList()
                };
            }

            foreach (var condition in table.GetSymbols<AgentLabConditionSymbol>())
            {
                definitions.Conditions[condition.Name] = new Callable
                {
                    Name = condition.Name,
                    Kind = AgentLabCompletionKind.Condition,
                    Parameters = condition.ParameterType switch
                    {
                        null => new List<string>(),
                        AgentLabParamSymbol parameter => new List<string> { $"{GetTypeName(parameter.Type)} {parameter.Name}" },
                        var type => new List<string> { $"{GetTypeName(type)} param" }
                    }
                };
            }

            foreach (var enumSymbol in table.GetSymbols<AgentLabEnumSymbol>())
            {
                definitions.Enums[enumSymbol.Name] = enumSymbol.Enums.GetSymbols<AgentLabConstSymbol>().Select(value => value.Name).ToList();
            }

            foreach (var constant in table.GetSymbols<AgentLabConstSymbol>())
            {
                definitions.Constants[constant.Name] = GetTypeName(constant.Type);
            }

            return definitions;
        });
    }

    private static string GetTypeName(AgentLabSymbol type)
    {
        return type?.Name switch
        {
            nameof(AgentLabToken.TokenType.IntegerType) => "int",
            nameof(AgentLabToken.TokenType.FloatType) => "float",
            nameof(AgentLabToken.TokenType.BooleanType) => "bool",
            nameof(AgentLabToken.TokenType.StringType) => "string",
            nameof(AgentLabToken.TokenType.EnumType) => "enum",
            null => "value",
            _ => type.Name
        };
    }
}
