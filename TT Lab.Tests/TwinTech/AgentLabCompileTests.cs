using Twinsanity.AgentLab;
using Twinsanity.AgentLab.AgentLabObjectDescs.PS2;
using Twinsanity.AgentLab.Resolvers.Compiler;
using Twinsanity.AgentLab.Resolvers.Decompiler;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using CompilerGraphResolver = Twinsanity.AgentLab.Resolvers.Compiler.DefaultGraphResolver;
using DecompilerGraphResolver = Twinsanity.AgentLab.Resolvers.Decompiler.DefaultGraphResolver;
using PacketData = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourControlPacket.ControlPacketData;
using TokenType = Twinsanity.AgentLab.AgentLabToken.TokenType;

namespace TT_Lab.Tests.TwinTech;

// What scripts compile to, and what the compiler refuses where the check passed: consts in any order, operations, a control packet's
// data, a condition's parameter, and the words and numbers the lexer reads
public class AgentLabCompileTests
{
    private const string Definitions = "ActionDefinitionsPs2.lab";

    private static AgentLabCompiler.CompilerOptions Options()
    {
        var graphs = new CompilerGraphResolver();
        graphs.AddNewGraphRef("TEST", 12);
        return new AgentLabCompiler.CompilerOptions
        {
            Command = new PS2CommandDesc(), CommandPack = new PS2CommandPackDesc(), State = new PS2StateDesc(), StateBody = new PS2StateBodyDesc(), Graph = new PS2GraphDesc(),
            ActionDefinitionsFile = Definitions, Resolver = new DefaultCompilerResolver(graphs, new DefaultGlobalObjectIdResolver())
        };
    }

    // The behaviour's line 1, its consts and packet after it, then the state and on the next line its body's condition (at column 10)
    // with the commands below it (at column 10 too)
    private static string Behaviour(string commands, string consts = "", string condition = "Random(0)", string packet = "") =>
        $"behaviour TEST {{\n{consts}{packet}{(packet == "" ? "" : "   [ControlPacket(P)]\n")}   state S() {{\n      if {condition} > 0.5 {{\n         {commands}\n      }}\n   }}\n}}\n";

    private static ITwinBehaviourGraph Graph(string script)
    {
        var result = AgentLabCompiler.Compile(script, Options());
        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message + "\n" + script);
        return result.Get<ITwinBehaviourGraph>();
    }

    private static List<ITwinBehaviourCommand> Commands(string commands)
    {
        var result = AgentLabCompiler.CompileCommands(commands, Options());
        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        return result.Get<ITwinBehaviourCommandPack>().Commands;
    }

    private static uint FirstArgument(ITwinBehaviourGraph graph) => graph.ScriptStates[0].Bodies[0].Commands[0].Arguments[0];

    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    private static byte[] Bytes(ITwinItem item)
    {
        item.Compile();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        return stream.ToArray();
    }

    // A check or compile that never ends fails its test instead of hanging the run
    private static Task<T> WithDeadline<T>(Func<T> work) => Task.Run(work).WaitAsync(TimeSpan.FromSeconds(20));

    // The consts are worked out in the order their values name each other, whatever order they're written in. One, two or three consts
    // never ended (List.Sort calls no comparer for one element, and the comparer only ever marked the first of the two it was given),
    // which hung the editor's live check and the build for good
    [Theory]
    [InlineData("   const A = 7;\n", "A", 7)]
    [InlineData("   const A = 7;\n   const B = A;\n", "B", 7)]
    [InlineData("   const B = A;\n   const A = 7;\n", "B", 7)]
    [InlineData("   const A = 7;\n   const B = A;\n   const C = B;\n", "C", 7)]
    [InlineData("   const C = B;\n   const B = A;\n   const A = 7;\n", "C", 7)]
    [InlineData("   const A = 1;\n   const B = A + 1;\n   const C = B + 1;\n   const D = C + 1;\n   const E = D + 1;\n", "E", 5)]
    [InlineData("   const E = D + 1;\n   const D = C + 1;\n   const C = B + 1;\n   const B = A + 1;\n   const A = 1;\n", "E", 5)]
    [InlineData("   const A = B;\n   const B = 0;\n   const C = A;\n   const D = F + G;\n   const E = B;\n   const F = 5;\n   const G = A;\n", "D", 5)]
    public async Task ConstsInAnyOrderAreCheckedAndCompiled(string consts, string used, int value)
    {
        var script = Behaviour($"AddLives({used});", consts);

        var status = await WithDeadline(() => AgentLabCompiler.Check(script, Definitions));
        var result = await WithDeadline(() => AgentLabCompiler.Compile(script, Options()));

        Assert.False(status.IsError, status.Message);
        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        Assert.Equal((uint)value, FirstArgument(result.Get<ITwinBehaviourGraph>()));
    }

    // The order is worked out without recursion, a long chain of consts written backwards and a long cycle among them
    [Fact]
    public async Task LongChainsOfConstsAreOrdered()
    {
        var chain = string.Concat(Enumerable.Range(1, 2000).Reverse().Select(i => $"   const C{i} = C{i - 1} + 1;\n")) + "   const C0 = 0;\n";
        var cycle = "   const C0 = C1999;\n" + string.Concat(Enumerable.Range(1, 1999).Select(i => $"   const C{i} = C{i - 1};\n"));

        var compiled = await WithDeadline(() => AgentLabCompiler.Compile(Behaviour("AddLives(C2000);", chain), Options()));
        var refused = await WithDeadline(() => AgentLabCompiler.Check(Behaviour("AddLives(C5);", cycle), Definitions));

        Assert.False(compiled.CompilerStatus.IsError, compiled.CompilerStatus.Message);
        Assert.Equal(2000u, FirstArgument(compiled.Get<ITwinBehaviourGraph>()));
        Assert.Equal("(2:10) Const C0's value depends on itself", refused.Message);
    }

    // A const whose value goes back to itself is an error at the first of the consts on the cycle, also where a const written before
    // names one of them (its value has no type to check it by)
    [Theory]
    [InlineData("   const A = A;\n", "A", 2)]
    [InlineData("   const A = B;\n   const B = A;\n", "A", 2)]
    [InlineData("   const C = -B;\n   const B = A * 2;\n   const A = B;\n", "B", 3)]
    [InlineData("   const D = 1;\n   const E = F;\n   const F = E + D;\n", "E", 3)]
    public async Task ConstsGoingBackToThemselvesAreErrors(string consts, string name, int line)
    {
        var script = Behaviour("AddLives(1);", consts);

        var status = await WithDeadline(() => AgentLabCompiler.Check(script, Definitions));
        var compiled = (await WithDeadline(() => AgentLabCompiler.Compile(script, Options()))).CompilerStatus;

        Assert.True(status.IsError);
        Assert.Equal($"({line}:10) Const {name}'s value depends on itself", status.Message);
        Assert.Equal((line, 10), (status.Line, status.Column));
        Assert.Equal((status.Line, status.Column, status.Message), (compiled.Line, compiled.Column, compiled.Message));
    }

    // Operations had no value in the compiler: (1 + 2) as an argument failed the compile and a const of one too. They group from the
    // left, every operation took the first operand as its left: 10 - 3 - 2 gave 8 and 2 * 3 * 4 gave 8
    [Theory]
    [InlineData("1 + 2", 3)]
    [InlineData("10 - 3 - 2", 5)]
    [InlineData("2 * 3 * 4", 24)]
    [InlineData("100 / 10 / 5", 2)]
    [InlineData("2 + 3 * 4 - 6 / 2", 11)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("7 / 2", 3)]
    [InlineData("-(2 + 3)", -5)]
    [InlineData("1 - -2", 3)]
    public void OperationsCompileToTheirValues(string operation, int value)
    {
        var argument = Commands($"AddLives(({operation}));")[0].Arguments[0];
        var viaConst = FirstArgument(Graph(Behaviour("AddLives(X);", $"   const X = {operation};\n")));

        Assert.Equal(unchecked((uint)value), argument);
        Assert.Equal(unchecked((uint)value), viaConst);
    }

    [Fact]
    public void OperationsCompileWhereverValuesGo()
    {
        var commands = Commands("DoAnimation({slotCount = 1 + 1}, (0.5 + 0.25), 1, 1, ((1.5 + 2) * 2), {slot1 = 2 * 4});");

        Assert.Equal(2u, commands[0].Arguments[0] & 0xF);
        Assert.Equal(TaggedValue.FromFloat(0.75f), commands[0].Arguments[1]);
        Assert.Equal(Bits(7.0f), commands[0].Arguments[4]);
        Assert.Equal(8u, commands[0].Arguments[5] & 0xFF);
        var body = Graph(Behaviour("AddLives(1);", "   const T = 0.25;\n", "Random((3 - 1)) > (T * 2) {\n         weight = (4 / 2);\n      }\n      if Random(0)")).ScriptStates[0].Bodies[0];
        Assert.Equal((2, 0.5f, 2.0f), (body.Condition.Parameter, body.Condition.Threshold, body.Condition.Weight));
    }

    // A packet's data of an operation got its byte and no float: the packet read the next data's float, or past its floats
    [Fact]
    public void ControlPacketDataTakesOperations()
    {
        var packet = "   packet P {\n      data {\n         MoveSpeed = (1.0 + 2.0);\n         KeyIndex = 2 + 1;\n         TurnSpeed = HALF * 3;\n      }\n   }\n";

        var data = Graph(Behaviour("AddLives(1);", "   const HALF = 1 / 2.0;\n", packet: packet)).ScriptStates[0].ControlPacket;

        Assert.Equal([Bits(3.0f), 3u, Bits(1.5f)], data.Floats);
        Assert.Equal((0, 1, 2), (data.Bytes[(int)PacketData.MoveSpeed], data.Bytes[(int)PacketData.KeyIndex], data.Bytes[(int)PacketData.TurnSpeed]));
    }

    // What has no value is an error of the compile at the operation (the check passes it), and a packet's data that isn't a number too
    [Theory]
    [InlineData("AddLives((1 / 0));", "", "(4:22) Division by zero")]
    [InlineData("AddLives(ZERO);", "   const ZERO = 3 / (1 - 1);\n", "(2:19) Division by zero")]
    [InlineData("AddLives((2147483647 + 1));", "", "(4:31) 2147483647+1 is past what a 32 bit integer holds")]
    [InlineData("AddLives((-2147483647 - 2));", "", "(4:32) -2147483647-2 is past what a 32 bit integer holds")]
    [InlineData("AddLives((BIG * 2));", "   const BIG = 2000000000;\n", "(5:24) BIG*2 is past what a 32 bit integer holds")]
    [InlineData("AddLives((Prop(2) + 1));", "", "(4:28) Binary operation + of nothing and Int32 isn't supported")]
    public void OperationsWithoutAValueAreErrorsAtTheirOperator(string commands, string consts, string error)
    {
        var compiled = AgentLabCompiler.Compile(Behaviour(commands, consts), Options()).CompilerStatus;

        Assert.True(compiled.IsError);
        Assert.Equal(error, compiled.Message);
    }

    // An instance's float is only a packet's data on its own, through a const it has no value in the compiler
    [Fact]
    public void ControlPacketDataThatIsntANumberIsAnError()
    {
        var packet = "   packet P {\n      data {\n         MoveSpeed = X;\n      }\n   }\n";

        var compiled = AgentLabCompiler.Compile(Behaviour("AddLives(1);", "   const X = InstanceFloat[1];\n", packet: packet), Options()).CompilerStatus;

        Assert.True(compiled.IsError);
        Assert.Equal("(5:22) MoveSpeed takes a number, not X", compiled.Message);
    }

    // A chain of operations is as deep a tree as it's long now, which the checks and the compiler walk a level at a time: past a
    // nesting no script has the parse stops, deep enough nesting ran the stack out and ended the program (parentheses already did)
    [Fact]
    public async Task NestingDeeperThanAnyScriptIsAnError()
    {
        const int most = 256;
        string Nested(int depth, string open, string close) => "AddLives(" + string.Concat(Enumerable.Repeat(open, depth)) + "7" + string.Concat(Enumerable.Repeat(close, depth)) + ");";

        Assert.Equal(7u, Commands(Nested(200, "(", ")"))[0].Arguments[0]);
        Assert.Equal(7u, Commands(Nested(80, "-(-", ")"))[0].Arguments[0]);
        Assert.Equal((uint)most, Commands("AddLives((1" + string.Concat(Enumerable.Repeat(" + 1", most - 1)) + "));")[0].Arguments[0]);
        foreach (var (script, column) in new[]
                 {
                     (Nested(most + 1, "(", ")"), "AddLives(".Length + most + 1),
                     (Nested(100000, "(", ")"), "AddLives(".Length + most + 1),
                     (Nested(100000, "-", ""), "AddLives(".Length + most + 1),
                     ("AddLives((1" + string.Concat(Enumerable.Repeat(" + 1", 100000)) + "));", "AddLives((1".Length + (most - 1) * 4 + 2),
                     ("AddLives((1" + string.Concat(Enumerable.Repeat(" * 1", 300)) + "));", "AddLives((1".Length + (most - 1) * 4 + 2)
                 })
        {
            var status = await WithDeadline(() => AgentLabCompiler.CheckCommands(script, Definitions));
            var compiled = await WithDeadline(() => AgentLabCompiler.CompileCommands(script, Options()));

            Assert.Equal($"(1:{column}) Values can't be nested more than {most} deep", status.Message);
            Assert.Equal(status.Message, compiled.CompilerStatus.Message);
        }
    }

    // A condition's parameter is an integer of 15 bits, the high bits of its word: one written without a parameter threw a
    // NullReferenceException in the compile, a float an invalid cast, and one past 15 bits was cut to them
    [Fact]
    public void ConditionParametersCompileToTheirValue()
    {
        Assert.Equal(0, Graph(Behaviour("AddLives(1);", condition: "Random()")).ScriptStates[0].Bodies[0].Condition.Parameter);
        Assert.Equal(32767, Graph(Behaviour("AddLives(1);", condition: "Random(32767)")).ScriptStates[0].Bodies[0].Condition.Parameter);
        Assert.Equal(4, Graph(Behaviour("AddLives(1);", "   const X = 5;\n", "Random((X - 1))")).ScriptStates[0].Bodies[0].Condition.Parameter);
    }

    [Theory]
    [InlineData("Random(1.5)", "", "1.5", 17)]
    [InlineData("Random(40000)", "", "40000", 17)]
    [InlineData("Random(32768)", "", "32768", 17)]
    [InlineData("Random(-1)", "", "-1", 17)]
    [InlineData("Random((0x7FFF + 1))", "", "32768", 18)]
    [InlineData("Random(HALF)", "   const HALF = 0.5;\n", "0.5", 17)]
    [InlineData("Random(X)", "   const X = InstanceFloat[1];\n", "X", 17)]
    public void ConditionParametersPastFifteenBitsAreErrors(string condition, string consts, string value, int column)
    {
        var line = consts == "" ? 3 : 4;

        var compiled = AgentLabCompiler.Compile(Behaviour("AddLives(1);", consts, condition), Options()).CompilerStatus;

        Assert.True(compiled.IsError);
        Assert.Equal($"({line}:{column}) Parameter param of Random takes an integer of 0 to 32767, not {value}", compiled.Message);
    }

    // The data is TumbleData, SpinData and TwistData in the compiler and the decompiler: the check had Tumble, Spin and Twist, which
    // failed the compile, and refused the decompiler's names
    [Fact]
    public void ControlPacketsTakeTheDecompilersDataNames()
    {
        var packet = "   packet P {\n      data {\n         TumbleData = 0.5;\n         SpinData = 1.5;\n         TwistData = 2.5;\n      }\n   }\n";

        var data = Graph(Behaviour("AddLives(1);", packet: packet)).ScriptStates[0].ControlPacket;

        Assert.Equal([Bits(0.5f), Bits(1.5f), Bits(2.5f)], data.Floats);
        Assert.Equal((0, 1, 2), (data.Bytes[(int)PacketData.TumbleData], data.Bytes[(int)PacketData.SpinData], data.Bytes[(int)PacketData.TwistData]));

        // Decompiled from a graph's bytes, the packet compiles back to them
        var graph = new PS2BehaviourGraph { Name = "TEST", StartState = 0 };
        graph.SetID(12);
        graph.ScriptStates = [new PS2BehaviourState { ControlPacket = data, Bodies = [] }];
        var bytes = Bytes(graph);
        var read = new PS2BehaviourGraph();
        read.Read(new BinaryReader(new MemoryStream(bytes)), bytes.Length);
        var script = AgentLabDecompiler.Decompile(read, new DecompilerGraphResolver(new DefaultStarterResolver(null, null), new DefaultStateResolversList(new DefaultStateResolver(null))));
        Assert.Contains("TumbleData = 0.5;", script);
        Assert.Contains("SpinData = 1.5;", script);
        Assert.Contains("TwistData = 2.5;", script);
        var status = AgentLabCompiler.Check(script, Definitions);
        Assert.False(status.IsError, status.Message + "\n" + script);
        var again = Graph(script);
        again.SetID(12);
        Assert.Equal(bytes, Bytes(again));
    }

    // Only the data the compiler has is a packet's data, the other names the check took failed the compile
    [Theory]
    [InlineData("Tumble = 1.0;", "Undefined control packet data Tumble, the data is TumbleData")]
    [InlineData("Spin = 1.0;", "Undefined control packet data Spin, the data is SpinData")]
    [InlineData("Twist = 1.0;", "Undefined control packet data Twist, the data is TwistData")]
    [InlineData("Translates = true;", "Undefined control packet data Translates")]
    [InlineData("RefListIndex = 1;", "Undefined control packet data RefListIndex")]
    [InlineData("InstanceFloat[3] = 1.0;", "Undefined control packet data InstanceFloat")]
    public void ControlPacketDataIsTheCompilersData(string data, string error)
    {
        var script = Behaviour("AddLives(1);", packet: $"   packet P {{\n      data {{\n         {data}\n      }}\n   }}\n");

        var status = AgentLabCompiler.Check(script, Definitions);
        var compiled = AgentLabCompiler.Compile(script, Options()).CompilerStatus;

        Assert.Equal($"(4:10) {error}", status.Message);
        Assert.Equal(status.Message, compiled.Message);
    }

    // eof in any case was the end of the script: the parser stopped at it and left out what came after it without an error
    [Fact]
    public void NoWordEndsTheScript()
    {
        var status = AgentLabCompiler.CheckCommands("SetShadow({}, 1.0, 10.0, 0); EOF garbage((((", Definitions);

        Assert.True(status.IsError);
        Assert.Equal((1, 34), (status.Line, status.Column));
        using var lexer = new AgentLabLexer("eof Eof EOF");
        Assert.Equal([TokenType.Identifier, TokenType.Identifier, TokenType.Identifier, TokenType.Eof],
            Enumerable.Range(0, 4).Select(_ => lexer.GetNextToken().Type));
        var script = "[StartFrom(Eof)]\nbehaviour TEST {\n   state Eof() {\n      if Random(0) > 0.5 {\n         execute Eof;\n      }\n   }\n}\n";
        var check = AgentLabCompiler.Check(script, Definitions);
        Assert.False(check.IsError, check.Message);
        Assert.Equal(0, Graph(script).ScriptStates[0].Bodies[0].JumpToState);
        // Names spelled like it are still written as strings where a name can be either
        Assert.True(AgentLabLexer.IsReservedKeyword("eof"));
    }

    // A number that doesn't read was dropped and what came after it took its place: AddLives(99999999999) said it got no parameters,
    // and a condition's parameter that didn't read passed the check as none
    [Theory]
    [InlineData("99999999999")]
    [InlineData("1.2.3")]
    [InlineData("0xFFFFFFFFF")]
    [InlineData("0x")]
    [InlineData("5-3")]
    [InlineData("12abc")]
    public void NumbersThatDontReadAreErrorsAtTheNumber(string number)
    {
        var command = AgentLabCompiler.CheckCommands($"AddLives({number});", Definitions);
        var condition = AgentLabCompiler.Check(Behaviour("AddLives(1);", condition: $"Random({number})"), Definitions);
        var compiled = AgentLabCompiler.Compile(Behaviour("AddLives(1);", condition: $"Random({number})"), Options()).CompilerStatus;

        Assert.Equal($"(1:10) Invalid number {number}", command.Message);
        Assert.Equal($"(3:17) Invalid number {number}", condition.Message);
        Assert.Equal(condition.Message, compiled.Message);
        using var lexer = new AgentLabLexer($"AddLives({number});");
        var tokens = Enumerable.Range(0, 6).Select(_ => lexer.GetNextToken()).ToList();
        Assert.Equal([TokenType.Identifier, TokenType.LeftParen, TokenType.Undefined, TokenType.RightParen, TokenType.Semicolon, TokenType.Eof], tokens.Select(token => token.Type));
        Assert.Equal(number, tokens[2].ToString());
    }

    // The word undefined is a token of that type too, which is no number
    [Fact]
    public void UndefinedIsNoNumber()
    {
        var status = AgentLabCompiler.CheckCommands("AddLives(undefined);", Definitions);

        Assert.True(status.IsError);
        Assert.DoesNotContain("Invalid number", status.Message);
    }
}
