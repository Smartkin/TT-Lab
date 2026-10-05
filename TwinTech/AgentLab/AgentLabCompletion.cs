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
    /// Field of a packed argument
    /// </summary>
    Field,
    /// <summary>
    /// Literal helper of a tagged argument: Prop(n), Raw(bits), Float(x), Int(n) or Angle(degrees)
    /// </summary>
    Literal,
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
    EnumValue,
    /// <summary>
    /// Behaviour graph a state runs as its child
    /// </summary>
    Behaviour
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
/// What the word under the pointer is, for a hover hint
/// </summary>
public class AgentLabHover
{
    /// <summary>
    /// Creates the hint
    /// </summary>
    public AgentLabHover(string title, string description, bool isBehaviourReference = false)
    {
        Title = title;
        Description = description;
        IsBehaviourReference = isBehaviourReference;
    }

    /// <summary>
    /// Whether the word is the behaviour a state runs, which the editor can look up and open
    /// </summary>
    public bool IsBehaviourReference { get; }

    /// <summary>
    /// The word and what kind of thing it is, like an action's signature
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// What it does or holds
    /// </summary>
    public string Description { get; }
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
/// The behaviour a state's parentheses name, as a name or as a string (a URI, an index)
/// </summary>
public class AgentLabReference
{
    /// <summary>
    /// Creates the reference
    /// </summary>
    public AgentLabReference(string reference, int start, int end, bool isName)
    {
        Reference = reference;
        Start = start;
        End = end;
        IsName = isName;
    }

    /// <summary>
    /// The name or the string's content
    /// </summary>
    public string Reference { get; }

    /// <summary>
    /// Offset of the reference's first character, after the quote of a string
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Offset after the reference's last character, the closing quote of a string
    /// </summary>
    public int End { get; }

    /// <summary>
    /// Whether the behaviour is named, strings are for what only the resolver understands
    /// </summary>
    public bool IsName { get; }
}

/// <summary>
/// A state or control packet declared in a behaviour script, where its name is
/// </summary>
public class AgentLabDeclaration
{
    /// <summary>
    /// Creates the declaration
    /// </summary>
    public AgentLabDeclaration(string keyword, string name, int start)
    {
        Keyword = keyword;
        Name = name;
        Start = start;
    }

    /// <summary>
    /// What declares it, <c>state</c> or <c>packet</c>
    /// </summary>
    public string Keyword { get; }

    /// <summary>
    /// The declared name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Offset of the name's first character
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Offset after the name's last character
    /// </summary>
    public int End => Start + Name.Length;
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
    private static readonly string[] StateAttributes = { "Interrupting", "SkipFirstBody", "UseObjectSlot", "ControlPacket" };
    private static readonly HashSet<string> StarterNames = new() { "AssignType", "AssignLocality", "AssignStatus", "AssignPreference", "RefListIndex", "GlobalObjectId" };
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
        public Token(TokenKind kind, string text, int start)
        {
            Kind = kind;
            Text = text;
            Start = start;
        }

        public TokenKind Kind { get; }
        public string Text { get; }
        public int Start { get; }
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
        /// <summary>
        /// The field names of each packed parameter, null for plain ones
        /// </summary>
        public List<List<string>> Fields { get; init; } = new();
        /// <summary>
        /// Each packed parameter's fields as their declaration (int name : 4), null for plain ones
        /// </summary>
        public List<Dictionary<string, string>> FieldDescriptions { get; init; } = new();

        public string Signature => $"{(Kind == AgentLabCompletionKind.Action ? "action" : "condition")} {Name}({string.Join(", ", Parameters)})";
    }

    private sealed class Definitions
    {
        public Dictionary<string, Callable> Actions { get; } = new();
        public Dictionary<string, Callable> Conditions { get; } = new();
        public Dictionary<string, List<string>> Enums { get; } = new();
        public Dictionary<string, string> Constants { get; } = new();
        // The suggestion lists don't change, so every completion hands out the same items (the editor caches its rows by them)
        public List<AgentLabCompletionItem> ActionItems { get; set; } = new();
        public List<AgentLabCompletionItem> ConditionItems { get; set; } = new();
    }

    private static readonly Dictionary<string, string> KeywordDescriptions = new()
    {
        ["behaviour"] = "A behaviour graph made of states, or a behaviour of a library made of actions only",
        ["library"] = "Library of behaviours made of actions only",
        ["state"] = "State of the behaviour: its bodies' conditions are evaluated every update while the state runs",
        ["if"] = "Body that runs when its condition's result passes the threshold (> or <)",
        ["else"] = "Body that runs when no other body of the state passes",
        ["completion"] = "First body of a state, runs when the state's control packet or child behaviour finishes",
        ["execute"] = "Jumps to another state after the body's commands",
        ["window"] = "window = <seconds>; time window of the event conditions (touched, spun, got a message)",
        ["weight"] = "weight = <value>; how the body wins over other passing bodies, 1 / threshold by default",
        ["threshold"] = "threshold = <value>; of an else or completion body, 0.5 by default",
        ["restart"] = "restart = true; a jump to the body's own state enters it again, restarting its child behaviour and timer",
        ["starter"] = "How the behaviour starts on its own when the object gets an event, with the agents its commands address",
        ["assigner"] = "assigner = { AssignType = ...; } an agent the behaviour's commands address by its index",
        ["packet"] = "Control packet a state moves the object with",
        ["settings"] = "How the packet moves the agent",
        ["data"] = "Values the packet moves the agent with",
        ["const"] = "Constant of the behaviour",
        ["true"] = "bool",
        ["false"] = "bool",
        ["Prop"] = "Prop(n): a tagged argument taken from the instance's property n when the command runs",
        ["Raw"] = "Raw(bits): the argument's 32 bits as they are, for values no literal reproduces",
        ["Float"] = "Float(x): a tagged float literal",
        ["Int"] = "Int(n): a tagged int literal",
        ["Angle"] = "Angle(degrees): a tagged angle literal"
    };

    private static readonly Dictionary<string, string> AttributeDescriptions = new()
    {
        ["StartFrom"] = "[StartFrom(State)] the state the behaviour starts in, the first one without it",
        ["Priority"] = "[Priority(n)] the starter's priority: a behaviour started with a higher one takes over a running one",
        ["GraphPriority"] = "[GraphPriority(n)] the graph's own priority (0, 1 or 3)",
        ["Interrupting"] = "[Interrupting] the state's conditions keep being evaluated while its child behaviour runs, a passing body ends it",
        ["SkipFirstBody"] = "[SkipFirstBody] the first body is the completion body (the same as writing completion { })",
        ["UseObjectSlot"] = "[UseObjectSlot(Slot)] the child behaviour is the one the object has in that event slot",
        ["ControlPacket"] = "[ControlPacket(Name)] the state moves the object the way the packet says while it lasts",
        ["GlobalIndex"] = "[GlobalIndex(n)] index of the library in the game's global storage",
        ["InstanceType"] = "[InstanceType(Type)] the kind of instance the library's behaviours run on",
        ["NonBlocking"] = "[NonBlocking] the old name of [Interrupting]",
        ["Unknown"] = "[Unknown(n)] state bits the game never reads, dropped"
    };

    private static readonly AgentLabCompletionItem[] Literals =
    {
        new("Prop", AgentLabCompletionKind.Literal, KeywordDescriptions["Prop"]),
        new("Raw", AgentLabCompletionKind.Literal, KeywordDescriptions["Raw"]),
        new("Float", AgentLabCompletionKind.Literal, KeywordDescriptions["Float"]),
        new("Int", AgentLabCompletionKind.Literal, KeywordDescriptions["Int"]),
        new("Angle", AgentLabCompletionKind.Literal, KeywordDescriptions["Angle"])
    };

    /// <summary>
    /// Reads the definitions ahead of the first completion, which otherwise pays for parsing them
    /// </summary>
    public static void Warm(string actionDefinitionsFile)
    {
        GetDefinitions(actionDefinitionsFile);
    }

    /// <summary>
    /// What the word at an offset is: a keyword, an attribute, an action or condition with its parameters, a field of a
    /// packed argument, a literal helper, an enum value or setting, or something the script declares. Null over anything else
    /// </summary>
    public static AgentLabHover GetHover(string script, int offset, string actionDefinitionsFile)
    {
        offset = Math.Clamp(offset, 0, script.Length);
        if (GetBehaviourReference(script, offset) is { } reference)
        {
            return new AgentLabHover(reference.IsName ? $"behaviour {reference.Reference}" : reference.Reference, "Behaviour the state runs as its child while it lasts", true);
        }

        var start = offset;
        while (start > 0 && IsIdentifierChar(script[start - 1]))
        {
            start--;
        }

        var end = offset;
        while (end < script.Length && IsIdentifierChar(script[end]))
        {
            end++;
        }

        if (start == end || char.IsAsciiDigit(script[start]))
        {
            return null;
        }

        var word = script.Substring(start, end - start);
        var tokens = Scan(script, start, out var insideCommentOrString);
        if (insideCommentOrString)
        {
            return null;
        }

        var definitions = GetDefinitions(actionDefinitionsFile);
        var statement = GetContext(tokens).Statement;
        var call = FindOpenCall(statement);
        if (call > 0 && statement[call - 1].Kind == TokenKind.Identifier && definitions.Actions.TryGetValue(statement[call - 1].Text, out var callable))
        {
            var parameter = CurrentParameter(statement, call, out var insideFields);
            if (insideFields && parameter < callable.FieldDescriptions.Count && callable.FieldDescriptions[parameter] is { } fields && fields.TryGetValue(word, out var field))
            {
                return new AgentLabHover($"{field}", $"Field of {callable.Parameters[parameter]}, parameter {parameter + 1} of {callable.Name}");
            }
        }

        if (definitions.Actions.TryGetValue(word, out var action))
        {
            return new AgentLabHover(action.Signature, action.Fields.Any(fields => fields != null) ? "Parameters in braces are fields packed into one value, written as {name = value, ...}" : "Action");
        }

        if (definitions.Conditions.TryGetValue(word, out var condition))
        {
            return new AgentLabHover(condition.Signature, "Condition, compared with the body's threshold");
        }

        if (statement.Count > 0 && statement[0].Text == "[" && AttributeDescriptions.TryGetValue(word, out var attribute))
        {
            return new AgentLabHover(word, attribute);
        }

        if (KeywordDescriptions.TryGetValue(word, out var keyword))
        {
            return new AgentLabHover(word, keyword);
        }

        if (definitions.Enums.TryGetValue(word, out var values))
        {
            return new AgentLabHover($"{word} = {string.Join(" | ", values)}", StarterNames.Contains(word) ? "Assigner setting" : "Control packet setting");
        }

        var enumOwner = definitions.Enums.FirstOrDefault(e => e.Value.Contains(word)).Key;
        if (enumOwner != null)
        {
            return new AgentLabHover(word, $"Value of {enumOwner}");
        }

        if (definitions.Constants.TryGetValue(word, out var type))
        {
            return new AgentLabHover($"{type} {word}", StarterNames.Contains(word) ? "Assigner setting" : "Control packet value");
        }

        foreach (var (keywordName, kind) in new[] { ("state", "State"), ("packet", "Control packet"), ("const", "Constant") })
        {
            if (GetDeclared(script, keywordName).Contains(word))
            {
                return new AgentLabHover($"{keywordName} {word}", $"{kind} declared in this script");
            }
        }

        return null;
    }

    /// <summary>
    /// The behaviour reference of a state header the offset is in (<c>state Name(Behaviour)</c> or <c>state Name("res://...")</c>),
    /// null anywhere else
    /// </summary>
    public static AgentLabReference GetBehaviourReference(string script, int offset)
    {
        offset = Math.Clamp(offset, 0, script.Length);
        var start = offset;
        while (start > 0 && IsIdentifierChar(script[start - 1]))
        {
            start--;
        }

        var end = offset;
        while (end < script.Length && IsIdentifierChar(script[end]))
        {
            end++;
        }

        if (start < end)
        {
            if (char.IsAsciiDigit(script[start]))
            {
                return null;
            }

            // a word of a string is part of the string's reference
            var tokens = Scan(script, start, out var insideCommentOrString);
            if (!insideCommentOrString)
            {
                return IsStateHeader(GetContext(tokens).Statement) ? new AgentLabReference(script.Substring(start, end - start), start, end, true) : null;
            }
        }

        // Inside a string: its quotes are on the offset's line
        var lineStart = offset > 0 ? script.LastIndexOf('\n', offset - 1) + 1 : 0;
        var lineEnd = script.IndexOf('\n', offset);
        if (lineEnd < 0)
        {
            lineEnd = script.Length;
        }

        var open = offset > lineStart ? script.LastIndexOf('"', offset - 1, offset - lineStart) : -1;
        var close = offset < lineEnd ? script.IndexOf('"', offset, lineEnd - offset) : -1;
        if (open < 0 || close < 0)
        {
            return null;
        }

        var before = Scan(script, open, out var insideOther);
        return !insideOther && IsStateHeader(GetContext(before).Statement) ? new AgentLabReference(script.Substring(open + 1, close - open - 1), open + 1, close, false) : null;
    }

    /// <summary>
    /// The declaration of the state or control packet the name at the offset refers to: the state an <c>execute</c> or a
    /// <c>[StartFrom]</c> names, the packet a <c>[ControlPacket]</c> names. Null anywhere else and for names the script doesn't declare
    /// </summary>
    public static AgentLabDeclaration GetDeclaration(string script, int offset)
    {
        offset = Math.Clamp(offset, 0, script.Length);
        var start = offset;
        while (start > 0 && IsIdentifierChar(script[start - 1]))
        {
            start--;
        }

        var end = offset;
        while (end < script.Length && IsIdentifierChar(script[end]))
        {
            end++;
        }

        if (start == end || char.IsAsciiDigit(script[start]))
        {
            return null;
        }

        var tokens = Scan(script, start, out var insideCommentOrString);
        if (insideCommentOrString)
        {
            return null;
        }

        var context = GetContext(tokens);
        var keyword = context.Statement switch
        {
            [{ Text: "execute" }] when context.Block == BlockKind.StateBody => "state",
            [{ Text: "[" }, { Text: "StartFrom" }, { Text: "(" }] => "state",
            [{ Text: "[" }, { Text: "ControlPacket" }, { Text: "(" }] => "packet",
            _ => null
        };
        if (keyword == null)
        {
            return null;
        }

        var name = script.Substring(start, end - start);
        var declared = Scan(script, script.Length, out _);
        for (var i = 0; i < declared.Count - 1; i++)
        {
            if (declared[i].Kind == TokenKind.Identifier && declared[i].Text == keyword && declared[i + 1].Kind == TokenKind.Identifier && declared[i + 1].Text == name)
            {
                return new AgentLabDeclaration(keyword, name, declared[i + 1].Start);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the suggestions for the caret's place in the script
    /// </summary>
    /// <param name="script">AgentLab code</param>
    /// <param name="offset">Caret's offset in the script</param>
    /// <param name="actionDefinitionsFile">Name of the AgentLab file that contains action definitions</param>
    /// <param name="commandsOnly">Whether the script can only be a list of commands, like an object's command pack</param>
    /// <param name="behaviours">The behaviours a state can run as its child, asked for inside a state's parentheses only. Null suggests none</param>
    /// <returns>Suggestions, empty inside of comments, strings and numbers</returns>
    public static AgentLabCompletionResult GetCompletions(string script, int offset, string actionDefinitionsFile, bool commandsOnly = false, Func<IEnumerable<AgentLabCompletionItem>> behaviours = null)
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
        var items = GetItems(GetContext(tokens), definitions, script, commandsOnly, behaviours);
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

        var parameter = CurrentParameter(statement, call, out _);
        return new AgentLabSignature(callable.Name, callable.Kind, callable.Parameters, parameter);
    }

    // Which argument of the call opened at statement[call] the caret is in, and whether it's inside that argument's {...}
    private static int CurrentParameter(List<Token> statement, int call, out bool insideFields)
    {
        var parameter = 0;
        var depth = 0;
        var braces = 0;
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
                case "{":
                    braces++;
                    break;
                case "}":
                    braces--;
                    break;
                case "," when depth == 0 && braces == 0:
                    parameter++;
                    break;
            }
        }

        insideFields = braces > 0;
        return parameter;
    }

    private static List<AgentLabCompletionItem> GetItems(Context context, Definitions definitions, string script, bool commandsOnly, Func<IEnumerable<AgentLabCompletionItem>> behaviours)
    {
        var items = new List<AgentLabCompletionItem>();
        var statement = context.Statement;

        // A state's parentheses name the behaviour it runs as its child
        if (IsStateHeader(statement))
        {
            if (behaviours != null)
            {
                items.AddRange(behaviours());
            }

            return items;
        }

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

        var openCall = FindOpenCall(statement);
        if (openCall >= 0)
        {
            var parameter = CurrentParameter(statement, openCall, out var insideFields);
            if (insideFields && openCall > 0 && definitions.Actions.TryGetValue(statement[openCall - 1].Text, out var callable) && parameter < callable.Fields.Count && callable.Fields[parameter] != null)
            {
                // the fields of the packed argument, the ones already given left out
                var given = new HashSet<string>(statement.Skip(openCall + 1).Where((token, index) => index + openCall + 2 < statement.Count && statement[index + openCall + 2].Text == "=").Select(token => token.Text));
                items.AddRange(callable.Fields[parameter].Where(field => !given.Contains(field)).Select(field => new AgentLabCompletionItem(field, AgentLabCompletionKind.Field, $"Field of {callable.Parameters[parameter]}")));
                return items;
            }

            items.AddRange(GetValues(script, true));
            items.AddRange(Literals);
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
                items.Add(Keyword("if", "Body that runs when its condition passes"));
                items.Add(Keyword("else", "Body that runs when no other body passes"));
                items.Add(Keyword("completion", "First body, runs when the state's control packet or child behaviour finishes"));
                break;
            case BlockKind.State when statement.Count == 1 && first == "if":
                items.AddRange(GetConditions(definitions));
                break;
            case BlockKind.StateBody when statement.Count == 0:
                if (!context.BlockStatements.Contains("window"))
                {
                    items.Add(Keyword("window", "window = <seconds>; time window of the event conditions"));
                }

                if (!context.BlockStatements.Contains("weight"))
                {
                    items.Add(Keyword("weight", "weight = <value>; how the body wins over other passing bodies, 1 / threshold by default"));
                }

                if (!context.BlockStatements.Contains("threshold"))
                {
                    items.Add(Keyword("threshold", "threshold = <value>; of an else or completion body, 0.5 by default"));
                }

                if (!context.BlockStatements.Contains("restart"))
                {
                    items.Add(Keyword("restart", "restart = true; entering the state again restarts its child behaviour"));
                }

                items.Add(Keyword("execute", "Switches to another state"));
                items.AddRange(GetActions(definitions));
                break;
            case BlockKind.StateBody when statement.Count == 1 && first == "execute":
                items.AddRange(GetStates(script));
                break;
            case BlockKind.StateBody when first is "restart" or "unknown" && statement.Count == 2 && statement[1].Text == "=":
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

    // state Name( with the reference about to be written
    private static bool IsStateHeader(List<Token> statement)
    {
        return statement.Count == 3 && statement[0].Text == "state" && statement[1].Kind == TokenKind.Identifier && statement[2].Text == "(";
    }

    private static bool IsAfterAssign(List<Token> statement) => statement.Any(token => token.Text == "=");

    private static AgentLabCompletionItem Keyword(string keyword, string description) => new(keyword, AgentLabCompletionKind.Keyword, description);

    private static AgentLabCompletionItem Constant(string name, string description) => new(name, AgentLabCompletionKind.Constant, description);

    private static IEnumerable<AgentLabCompletionItem> GetBooleans()
    {
        yield return new AgentLabCompletionItem("true", AgentLabCompletionKind.Keyword, "bool");
        yield return new AgentLabCompletionItem("false", AgentLabCompletionKind.Keyword, "bool");
    }

    private static IEnumerable<AgentLabCompletionItem> GetActions(Definitions definitions) => definitions.ActionItems;

    private static IEnumerable<AgentLabCompletionItem> GetConditions(Definitions definitions) => definitions.ConditionItems;

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
                // braces inside a call hold the fields of a packed argument, not a block
                case "{" or "}" when FindOpenCall(statement) >= 0:
                    statement.Add(token);
                    break;
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
            "if" or "else" or "completion" => BlockKind.StateBody,
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
                tokens.Add(new Token(char.IsAsciiDigit(text[0]) || text[0] == '.' ? TokenKind.Number : TokenKind.Identifier, text, start));
                continue;
            }

            if (character is '>' or '<' or '=' && i + 1 < end && script[i + 1] == '=')
            {
                tokens.Add(new Token(TokenKind.Symbol, script.Substring(i, 2), i));
                i += 2;
                continue;
            }

            tokens.Add(new Token(TokenKind.Symbol, character.ToString(), i));
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
                    Parameters = action.Parameters.GetSymbols<AgentLabParamSymbol>().Select(ParameterText).ToList(),
                    Fields = action.Parameters.GetSymbols<AgentLabParamSymbol>().Select(parameter => parameter.Fields?.Select(field => field.Name).ToList()).ToList(),
                    FieldDescriptions = action.Parameters.GetSymbols<AgentLabParamSymbol>().Select(parameter => parameter.Fields?.ToDictionary(field => field.Name, field => field.ToString())).ToList()
                };
            }

            definitions.ActionItems = definitions.Actions.Values.OrderBy(action => action.Name, StringComparer.OrdinalIgnoreCase)
                .Select(action => new AgentLabCompletionItem(action.Name, AgentLabCompletionKind.Action, action.Signature)).ToList();

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

            definitions.ConditionItems = definitions.Conditions.Values.OrderBy(condition => condition.Name, StringComparer.OrdinalIgnoreCase)
                .Select(condition => new AgentLabCompletionItem(condition.Name, AgentLabCompletionKind.Condition, condition.Signature)).ToList();

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

    // A parameter as the signature shows it, packed ones with their fields
    private static string ParameterText(AgentLabParamSymbol parameter)
    {
        if (parameter.Fields == null)
        {
            return $"{GetTypeName(parameter.Type)} {parameter.Name}";
        }

        return $"{parameter.Name} {{{string.Join(", ", parameter.Fields.Select(field => field.Name))}}}";
    }

    private static string GetTypeName(AgentLabSymbol type)
    {
        return type?.Name switch
        {
            nameof(AgentLabToken.TokenType.IntegerType) => "int",
            nameof(AgentLabToken.TokenType.FloatType) => "float",
            nameof(AgentLabToken.TokenType.BooleanType) => "bool",
            nameof(AgentLabToken.TokenType.TaggedFloatType) => "tfloat",
            nameof(AgentLabToken.TokenType.TaggedIntType) => "tint",
            nameof(AgentLabToken.TokenType.TaggedAngleType) => "tangle",
            nameof(AgentLabToken.TokenType.StringType) => "string",
            nameof(AgentLabToken.TokenType.EnumType) => "enum",
            null => "value",
            _ => type.Name
        };
    }
}
