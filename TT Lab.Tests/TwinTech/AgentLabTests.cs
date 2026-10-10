using Twinsanity.AgentLab;
using Twinsanity.AgentLab.AgentLabObjectDescs.PS2;
using Twinsanity.AgentLab.Resolvers.Compiler;
using Twinsanity.AgentLab.Resolvers.Decompiler;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;
using CompilerGraphResolver = Twinsanity.AgentLab.Resolvers.Compiler.DefaultGraphResolver;
using DecompilerGraphResolver = Twinsanity.AgentLab.Resolvers.Decompiler.DefaultGraphResolver;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using TokenType = Twinsanity.AgentLab.AgentLabToken.TokenType;

namespace TT_Lab.Tests.TwinTech;

public class AgentLabTests
{
    private const string ActionDefinitions = "ActionDefinitionsPs2.lab";

    private static string Behaviour(string actions = "         DestroyMe(0x00000002);\n         execute State_1;\n") =>
        "[StartFrom(State_0)]\n" +
        "[Priority(1)]\n" +
        "behaviour TEST_BEHAVIOUR {\n" +
        "   [Interrupting]\n" +
        "   state State_0() {\n" +
        "      if Else(0) > 0.5 {\n" +
        "         window = 0;\n" +
        "         restart = false;\n" +
        actions +
        "      }\n" +
        "   }\n" +
        "\n" +
        "   state State_1() {\n" +
        "   }\n" +
        "}\n";

    [Fact]
    public void LexerTracksLinesAndColumns()
    {
        using var lexer = new AgentLabLexer("behaviour A {\n  state S() {\n\tif Else(0) >= 0.5 { }\n}");

        var tokens = new List<(TokenType, int, int)>();
        for (var token = lexer.GetNextToken(); token.Type != TokenType.Eof; token = lexer.GetNextToken())
        {
            tokens.Add((token.Type, token.Line, token.Column));
        }

        Assert.Equal(
        [
            (TokenType.Behaviour, 1, 1), (TokenType.Identifier, 1, 11), (TokenType.OpenBracket, 1, 13),
            (TokenType.State, 2, 3), (TokenType.Identifier, 2, 9), (TokenType.LeftParen, 2, 10), (TokenType.RightParen, 2, 11), (TokenType.OpenBracket, 2, 13),
            (TokenType.If, 3, 2), (TokenType.Identifier, 3, 5), (TokenType.LeftParen, 3, 9), (TokenType.Integer, 3, 10), (TokenType.RightParen, 3, 11),
            (TokenType.GreaterEqual, 3, 13), (TokenType.FloatingPoint, 3, 16), (TokenType.OpenBracket, 3, 20), (TokenType.CloseBracket, 3, 22),
            (TokenType.CloseBracket, 4, 1)
        ], tokens);
    }

    // Big floats print with the exponent's sign, 1E+30 used to come back as infinity
    [Theory]
    [InlineData("1E+30", 1e30f)]
    [InlineData("1E-05", 1e-5f)]
    [InlineData("2.5E+38", 2.5e38f)]
    public void FloatsWithExponentsAreRead(string text, float value)
    {
        using var lexer = new AgentLabLexer(text);

        var token = lexer.GetNextToken();

        Assert.Equal(TokenType.FloatingPoint, token.Type);
        Assert.Equal(value, token.GetValue<float>());
        Assert.Equal(TokenType.Eof, lexer.GetNextToken().Type);
    }

    [Fact]
    public void ParserThrowsSyntaxErrorWithPosition()
    {
        using var parser = new AgentLabParser(new AgentLabLexer("behaviour {"));

        var exception = Assert.Throws<AgentLabSyntaxException>(() => parser.Parse());

        Assert.Equal(1, exception.Line);
        Assert.Equal(11, exception.Column);
        Assert.StartsWith("(1:11) ", exception.ToString());
    }

    [Fact]
    public void ValidBehaviourPassesTheCheck()
    {
        var status = AgentLabCompiler.Check(Behaviour(), ActionDefinitions);

        Assert.False(status.IsError, status.Message);
        Assert.Equal(0, status.Line);
    }

    [Fact]
    public void BehaviourWithoutActionDefinitionsStillParses()
    {
        var status = AgentLabCompiler.Check(Behaviour("         execute State_1;\n"), "");

        Assert.False(status.IsError, status.Message);
    }

    [Theory]
    [InlineData("         DestroyMe(0x00000002)\n         execute State_1;\n", 10, 10, "expected Semicolon")]
    [InlineData("         NoSuchAction();\n         execute State_1;\n", 9, 10, "Undefined action call NoSuchAction")]
    [InlineData("         DestroyMe(0x00000002, 5);\n         execute State_1;\n", 9, 10, "Expected 1 parameters for DestroyMe but got 2")]
    [InlineData("         DestroyMe(0x00000002);\n         execute Missing;\n", 10, 18, "Undefined state Missing")]
    public void ErrorsPointAtTheirPlaceInTheScript(string actions, int line, int column, string message)
    {
        var status = AgentLabCompiler.Check(Behaviour(actions), ActionDefinitions);

        Assert.True(status.IsError);
        Assert.Equal(line, status.Line);
        Assert.Equal(column, status.Column);
        Assert.Contains(message, status.Message);
    }

    [Fact]
    public void UndefinedConditionIsAnError()
    {
        var status = AgentLabCompiler.Check(Behaviour().Replace("if Else(0)", "if NoSuchCondition(0)"), ActionDefinitions);

        Assert.True(status.IsError);
        Assert.Equal((6, 10), (status.Line, status.Column));
        Assert.Contains("Undefined condition call NoSuchCondition", status.Message);
    }

    [Fact]
    public void UndefinedStartingStateIsAnError()
    {
        var status = AgentLabCompiler.Check(Behaviour().Replace("[StartFrom(State_0)]", "[StartFrom(Nowhere)]"), ActionDefinitions);

        Assert.True(status.IsError);
        Assert.True(status.Line > 0);
        Assert.Contains("Undefined state Nowhere", status.Message);
    }

    // Checks run on a background thread while the editor stays usable, so they must not share state
    // Objects' command packs can only be a list of commands, most objects have none
    [Theory]
    [InlineData("")]
    [InlineData("  \n\n")]
    [InlineData("// Nothing yet\n")]
    [InlineData("SetSurface(0x01FF0008);\nSetShadow(0x00000000, 0x41F00004, 0x41200004, 0x41200004);\n")]
    public void CommandListsPassTheCheck(string script)
    {
        var status = AgentLabCompiler.CheckCommands(script, ActionDefinitions);

        Assert.False(status.IsError, status.Message);
    }

    [Fact]
    public void OnlyCommandsCanBeInACommandList()
    {
        var status = AgentLabCompiler.CheckCommands("SetSurface(0x01FF0008);\n" + Behaviour(), ActionDefinitions);

        Assert.True(status.IsError);
        Assert.Contains("Only commands can be written here", status.Message);
        Assert.Equal((2, 1), (status.Line, status.Column));
        Assert.True(AgentLabCompiler.CheckCommands(Behaviour(), ActionDefinitions).IsError);
        Assert.True(AgentLabCompiler.CheckCommands("NotACommand(0);", ActionDefinitions).IsError);
    }

    [Fact]
    public void CommandListsCompileToAPack()
    {
        var options = new AgentLabCompiler.CompilerOptions { Command = new PS2CommandDesc(), CommandPack = new PS2CommandPackDesc(), ActionDefinitionsFile = ActionDefinitions };

        var empty = AgentLabCompiler.CompileCommands("// Nothing yet\n", options);
        var pack = AgentLabCompiler.CompileCommands("SetSurface(0x01FF0008);\nSetShadow(0x00000000, 0x41F00004, 0x41200004, 0x41200004);\n", options);

        Assert.Empty(empty.Get<ITwinBehaviourCommandPack>().Commands);
        Assert.Equal(2, pack.Get<ITwinBehaviourCommandPack>().Commands.Count);
    }

    [Fact]
    public void ChecksCanRunInParallel()
    {
        var scripts = Enumerable.Range(0, 32).Select(i => i % 2 == 0 ? Behaviour() : Behaviour("         NoSuchAction();\n")).ToList();

        var statuses = scripts.AsParallel().Select(script => AgentLabCompiler.Check(script, ActionDefinitions)).ToList();

        for (var i = 0; i < statuses.Count; i++)
        {
            Assert.Equal(i % 2 != 0, statuses[i].IsError);
        }
    }

    private static AgentLabCompiler.CompilerOptions Ps2Options(string graphName = null, short graphId = 0)
    {
        var graphResolver = new CompilerGraphResolver();
        if (graphName != null)
        {
            graphResolver.AddNewGraphRef(graphName, graphId);
        }

        return new AgentLabCompiler.CompilerOptions
        {
            Command = new PS2CommandDesc(), CommandPack = new PS2CommandPackDesc(), State = new PS2StateDesc(), StateBody = new PS2StateBodyDesc(), Graph = new PS2GraphDesc(),
            ActionDefinitionsFile = ActionDefinitions, Resolver = new DefaultCompilerResolver(graphResolver, new DefaultGlobalObjectIdResolver())
        };
    }

    // A packed argument's fields go to their bits, the ones left out are 0, and a whole value is still taken for the dword
    [Fact]
    public void PackedArgumentsCompileToTheirBits()
    {
        var result = AgentLabCompiler.CompileCommands("DoAnimation({slotCount = 2, layer = 3, speedGiven = true}, 1, 1, 1, 1, {slot1 = 8, slot4 = 255});\nDoAnimation(0x4032, 1, 1, 1, 1, 7);\nDoSound({slotCount = 1}, {slotA = -1, slotB = 5}, {}, {}, {}, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0);", Ps2Options());

        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        var commands = result.Get<ITwinBehaviourCommandPack>().Commands;
        Assert.Equal(2u | 3u << 4 | 1u << 14, commands[0].Arguments[0]);
        Assert.Equal(8u | 255u << 24, commands[0].Arguments[5]);
        Assert.Equal(0x4032u, commands[1].Arguments[0]);
        Assert.Equal(0x0005FFFFu, commands[2].Arguments[1]);
        Assert.Equal(0u, commands[2].Arguments[2]);
        var script = AgentLabDecompiler.Decompile(result.Get<ITwinBehaviourCommandPack>());
        Assert.Contains("DoAnimation({slotCount = 2, layer = 3, speedGiven = true}, 1.0, 1.0, 1.0, 1.0, {slot1 = 8, slot4 = 255});", script);
        Assert.Contains("DoSound({slotCount = 1}, {slotA = -1, slotB = 5}, {}, {}, {}, ", script);
        Assert.False(AgentLabCompiler.CheckCommands(script, ActionDefinitions).IsError);
    }

    // Older scripts had the state's bits the game never reads, they're dropped
    [Fact]
    public void LegacyUnknownAttributeIsDropped()
    {
        var result = AgentLabCompiler.Compile(Behaviour().Replace("   [Interrupting]\n", "   [Unknown(0x40)]\n   [Interrupting]\n"), Ps2Options());

        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        var bytes = Bytes(result.Get<ITwinBehaviourGraph>());
        var read = new PS2BehaviourGraph();
        read.Read(new BinaryReader(new MemoryStream(bytes)), bytes.Length);
        Assert.Equal(0, read.ScriptStates[0].Bitfield & 0x3E0);
        Assert.True(read.ScriptStates[0].Interrupting);
    }

    private static byte[] Bytes(ITwinItem item)
    {
        item.Compile();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        return stream.ToArray();
    }

    private static byte[] PackBytes(ITwinBehaviourCommandPack pack)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        pack.Write(writer);
        return stream.ToArray();
    }

    // The names the values had before their meaning was known, and the comparisons that were written as >= and <=
    [Fact]
    public void OldSyntaxStillCompiles()
    {
        var script = Behaviour().Replace("[Interrupting]", "[NonBlocking]").Replace("if Else(0) > 0.5", "if Else(0) >= 0.5").Replace("window = 0;", "interval = 0;").Replace("restart = false;", "unknown = false;");

        var result = AgentLabCompiler.Compile(script, Ps2Options());

        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        var state = result.Get<ITwinBehaviourGraph>().ScriptStates[0];
        Assert.True(state.Interrupting);
        Assert.False(state.Bodies[0].RestartsState);
        Assert.Equal(0.5f, state.Bodies[0].Condition.Threshold);
    }

    // Slots 9 and 10 were OnPhysicsCollision and OnUnknownCollision until what starts them was known (the decomp's AgentBehaviourSlot):
    // nothing starts 9, a character thrown at the object starts 10. Old scripts' names still compile, scripts get written with the new ones
    [Fact]
    public void TheObjectSlotsOldNamesStillCompile()
    {
        var script = Behaviour().Replace("   [Interrupting]\n", "   [Interrupting]\n   [UseObjectSlot(OnPhysicsCollision)]\n")
            .Replace("   state State_1() {\n", "   [UseObjectSlot(OnUnknownCollision)]\n   state State_1() {\n");

        var result = AgentLabCompiler.Compile(script, Ps2Options());

        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        var graph = result.Get<ITwinBehaviourGraph>();
        Assert.Equal([(true, (short)9), (true, (short)10)], graph.ScriptStates.Select(state => (state.UsesObjectSlot, state.BehaviourIndexOrSlot)));
        // Decompiled as the game's file has it
        var bytes = Bytes(graph);
        var read = new PS2BehaviourGraph();
        read.Read(new BinaryReader(new MemoryStream(bytes)), bytes.Length);
        var starter = new TwinBehaviourStarter { Priority = 1 };
        starter.Assigners.Add(new TwinBehaviourAssigner { AssignType = TwinBehaviourAssigner.AssignTypeID.ORIGINATOR });
        var decompiled = AgentLabDecompiler.Decompile(read, new DecompilerGraphResolver(new DefaultStarterResolver(starter, null),
            new DefaultStateResolversList(new DefaultStateResolver(null), new DefaultStateResolver(null))));
        Assert.Contains("[UseObjectSlot(Slot_9)]", decompiled);
        Assert.Contains("[UseObjectSlot(OnGettingThrownAttacked)]", decompiled);
        var again = AgentLabCompiler.Compile(decompiled, Ps2Options("TEST_BEHAVIOUR", 12));
        Assert.False(again.CompilerStatus.IsError, again.CompilerStatus.Message + "\n" + decompiled);
        Assert.Equal([9, 10], again.Get<ITwinBehaviourGraph>().ScriptStates.Select(state => (int)state.BehaviourIndexOrSlot));
    }

    [Fact]
    public void ElseAndCompletionBlocksCompileToTheGamesConditions()
    {
        var script = "[StartFrom(State_0)]\nbehaviour B {\n   state State_0() {\n      completion {\n         restart = true;\n         execute State_0;\n      }\n" +
                     "      if IsCollidable(0) < 0.25 {\n         window = 0.2;\n         weight = 8;\n         execute State_1;\n      }\n      else {\n         threshold = 1;\n         execute State_1;\n      }\n   }\n   state State_1() {\n   }\n}\n";

        var result = AgentLabCompiler.Compile(script, Ps2Options());

        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        var state = result.Get<ITwinBehaviourGraph>().ScriptStates[0];
        Assert.True(state.HasCompletionBody);
        var completion = state.Bodies[0];
        Assert.Equal((TwinBehaviourCondition.NextConditionIndex, 0.5f, 2.0f, 0.0f, false), (completion.Condition.ConditionIndex, completion.Condition.Threshold, completion.Condition.Weight, completion.Condition.TimeWindow, completion.Condition.NotGate));
        Assert.True(completion.RestartsState);
        Assert.Equal(0, completion.JumpToState);
        var inverted = state.Bodies[1].Condition;
        Assert.Equal((0.25f, 0.2f, 8.0f, true), (inverted.Threshold, inverted.TimeWindow, inverted.Weight, inverted.NotGate));
        var fallback = state.Bodies[2].Condition;
        Assert.Equal((TwinBehaviourCondition.ElseConditionIndex, 1.0f, 1.0f), (fallback.ConditionIndex, fallback.Threshold, fallback.Weight));
    }

    [Fact]
    public void CompletionBlockHasToBeTheFirstBody()
    {
        var script = "[StartFrom(State_0)]\nbehaviour B {\n   state State_0() {\n      else {\n      }\n      completion {\n      }\n   }\n}\n";

        var status = AgentLabCompiler.Check(script, ActionDefinitions);

        Assert.True(status.IsError);
        Assert.Equal((6, 7), (status.Line, status.Column));
        Assert.Contains("first body", status.Message);
    }

    // DoAnimation is (int, tfloat, tfloat, tfloat, float, int): tagged values are a literal of their type or Prop(index) of the instance's properties
    [Fact]
    public void ArgumentsAreEncodedByTheParametersType()
    {
        var options = Ps2Options();

        var pack = AgentLabCompiler.CompileCommands("DoAnimation(1, 1.5, Prop(3), Raw(0x12345678), 2, 0x7);\nDoAnimation(-1, 2, Int(-3), Angle(90), 0x3F800000, 1);\n", options);

        Assert.False(pack.CompilerStatus.IsError, pack.CompilerStatus.Message);
        var commands = pack.Get<ITwinBehaviourCommandPack>().Commands;
        Assert.Equal([1u, 0x3FC00004u, 0x1Du, 0x12345678u, 0x40000000u, 7u], commands[0].Arguments);
        Assert.Equal([0xFFFFFFFFu, 0x40000004u, unchecked((uint)(-3 << 3)), TaggedValue.FromAngle(90.0f), 0x3F800000u, 1u], commands[1].Arguments);
        Assert.Equal(TaggedValue.TypeAngle, TaggedValue.TypeOf(commands[1].Arguments[3]));
    }

    // The game's scripts hold floats no literal writes: negative zero, NaN, denormal leftovers, and int.MinValue, whose magnitude the lexer can't read
    [Fact]
    public void AwkwardArgumentBitsRoundTrip()
    {
        var pack = new PS2BehaviourCommandPack();
        // DoSound is (int, int, int, int, int, tfloat, float, float, float, int, ...): -0.0 and NaN floats, a denormal tagged float, int.MinValue
        pack.Commands.Add(new PS2BehaviourCommand { CommandIndex = 11, Arguments = [1u, 0u, 0u, 0u, 0u, 0x00000014u, 0x80000000u, 0x7FC00000u, 0x3E4CCCCDu, 0x80000000u, 0u, 0x0046A190u, 0u, 0x00000010u, 0x7F800000u] });
        var bytes = PackBytes(pack);

        var script = AgentLabDecompiler.Decompile(pack);
        var again = AgentLabCompiler.CompileCommands(script, Ps2Options());

        Assert.False(again.CompilerStatus.IsError, again.CompilerStatus.Message + "\n" + script);
        Assert.Contains("Raw(0x00000014)", script);
        Assert.Contains("0x80000000", script);
        Assert.Equal(bytes, PackBytes(again.Get<ITwinBehaviourCommandPack>()));
    }

    // Property references used to print as the denormal float their bits make, 0x1D is Prop(3) of a tfloat
    [Fact]
    public void OldScriptsKeepTheirPropertyReferences()
    {
        var pack = AgentLabCompiler.CompileCommands("DoAnimation(1, 4.06E-44, 1.5000005, 1, 0, 0);", Ps2Options());

        Assert.False(pack.CompilerStatus.IsError, pack.CompilerStatus.Message);
        Assert.Equal([1u, 0x1Du, 0x3FC00004u, 0x3F800004u, 0u, 0u], pack.Get<ITwinBehaviourCommandPack>().Commands[0].Arguments);
    }

    [Theory]
    [InlineData("DoAnimation(1.5, 1, 1, 1, 1, 1);", "Expected PackedType but got FloatType for parameter flags")]
    [InlineData("DoAnimation(1, Prop(1.5), 1, 1, 1, 1);", "Prop() takes an integer")]
    [InlineData("DoAnimation(1, 1, 1, 1, 1, Prop(1));", "Expected PackedType but got TaggedLiteral")]
    [InlineData("DoAnimation({slotCount = 16}, 1, 1, 1, 1, 1);", "Field slotCount takes 0 to 15, not 16")]
    [InlineData("DoAnimation({loop = true}, 1, 1, 1, 1, 1);", "has no field loop")]
    [InlineData("DoAnimation({slotCount = 1, slotCount = 2}, 1, 1, 1, 1, 1);", "Field slotCount is given twice")]
    [InlineData("DoAnimation({speedGiven = 1.5}, 1, 1, 1, 1, 1);", "Field speedGiven takes a bool")]
    [InlineData("DoAnimation(1, {slotCount = 1}, 1, 1, 1, 1);", "Parameter blendTime of DoAnimation is a value, not fields")]
    public void ArgumentTypeMismatchesAreErrors(string command, string message)
    {
        var status = AgentLabCompiler.CheckCommands(command, ActionDefinitions);

        Assert.True(status.IsError);
        Assert.Contains(message, status.Message);
        Assert.Equal(1, status.Line);
    }

    // What the game's bytes decompile to has to compile back to the same bytes
    [Fact]
    public void ScriptsRoundTripThroughTheDecompiler()
    {
        var graph = new PS2BehaviourGraph { Name = "TEST", StartState = 1, Priority = 3 };
        graph.SetID(12);
        var completion = new PS2BehaviourStateBody { Condition = new TwinBehaviourCondition { ConditionIndex = 0, Threshold = 0.5f, Weight = 2.0f }, RestartsState = true, HasStateJump = true, JumpToState = 0 };
        var collidable = new PS2BehaviourStateBody { Condition = new TwinBehaviourCondition { ConditionIndex = 1, Threshold = 0.5f, Weight = 2.0f } };
        collidable.Commands.Add(new PS2BehaviourCommand { CommandIndex = 9, Arguments = [1u, 0x3FC00004u, 0x1Du, 0x12345678u, 0x40000000u, 7u] });
        collidable.Commands.Add(new PS2BehaviourCommand { CommandIndex = 4, Arguments = [0x00000003u] });
        collidable.Commands.Add(new PS2BehaviourCommand { CommandIndex = 5, Arguments = [] });
        var fallback = new PS2BehaviourStateBody { Condition = new TwinBehaviourCondition { ConditionIndex = 2, Threshold = 0.5f, Weight = 2.0f, TimeWindow = 0.2f }, HasStateJump = true, JumpToState = 1 };
        var state0 = new PS2BehaviourState { HasCompletionBody = true, Interrupting = true, Bodies = [completion, collidable, fallback] };
        var random = new PS2BehaviourStateBody { Condition = new TwinBehaviourCondition { ConditionIndex = 3, Parameter = 5, Threshold = 0.25f, Weight = 1e30f, NotGate = true }, HasStateJump = true, JumpToState = 0 };
        var state1 = new PS2BehaviourState { UsesObjectSlot = true, BehaviourIndexOrSlot = 3, Bodies = [random] };
        graph.ScriptStates = [state0, state1];
        var starter = new TwinBehaviourStarter { Priority = 1 };
        starter.SetID(11);
        starter.Assigners.Add(new TwinBehaviourAssigner { AssignType = TwinBehaviourAssigner.AssignTypeID.GLOBAL_AGENT, RefListIndex = 19 });
        starter.Assigners.Add(new TwinBehaviourAssigner { AssignType = TwinBehaviourAssigner.AssignTypeID.ORIGINATOR });
        var graphBytes = Bytes(graph);
        var starterBytes = Bytes(starter);
        var read = new PS2BehaviourGraph();
        read.Read(new BinaryReader(new MemoryStream(graphBytes)), graphBytes.Length);

        var script = AgentLabDecompiler.Decompile(read, new DecompilerGraphResolver(new DefaultStarterResolver(starter, null), new DefaultStateResolversList(new DefaultStateResolver(null), new DefaultStateResolver(null))));
        var result = AgentLabCompiler.Compile(script, Ps2Options("TEST", 12));

        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message + "\n" + script);
        Assert.Contains("completion {", script);
        Assert.Contains("restart = true;", script);
        Assert.Contains("if IsCollidable(0) > 0.5 {", script);
        Assert.Contains("DoAnimation({slotCount = 1}, 1.5, Prop(3), Raw(0x12345678), 2.0, {slot1 = 7});", script);
        Assert.Contains("else {", script);
        Assert.Contains("window = 0.2;", script);
        Assert.Contains("if Random(5) < 0.25 {", script);
        Assert.Contains("weight = 1E+30;", script);
        Assert.Contains("[UseObjectSlot(OnTouch)]", script);
        Assert.Contains("RefListIndex = 19;", script);
        Assert.DoesNotContain("AssignLocality", script);
        var compiled = result.Get<ITwinBehaviourGraph>();
        compiled.SetID(12);
        Assert.True(graphBytes.SequenceEqual(Bytes(compiled)), "The graph changed:\n" + script);
        var compiledStarter = result.Get<TwinBehaviourStarter>();
        compiledStarter.SetID(11);
        Assert.True(starterBytes.SequenceEqual(Bytes(compiledStarter)), "The starter changed:\n" + script);
    }

    // Knows the states' behaviours by name, strings of digits are indexes like the default resolver takes
    private sealed class NamedStateResolver(AgentLabCompiler.CompilerOptions options, Dictionary<string, short> graphs) : ICompilerResolver, IStateGraphResolver
    {
        public IGraphResolver GetGraphResolver() => options.Resolver.GetGraphResolver();

        public IStateGraphResolver GetStateGraphResolver() => this;

        public IGlobalObjectIdResolver GetObjectIdResolver() => options.Resolver.GetObjectIdResolver();

        public short ResolveGraphReference(string graphRef) => graphs.TryGetValue(graphRef, out var id) ? id : short.Parse(graphRef);
    }

    // A state names the behaviour it runs, a string is for what only the resolver understands (a URI, an index)
    [Fact]
    public void StatesNameTheirChildBehaviour()
    {
        var options = Ps2Options();
        options.Resolver = new NamedStateResolver(options, new Dictionary<string, short> { ["COM_CHILD"] = 7 });
        var result = AgentLabCompiler.Compile("behaviour A {\n   state S(COM_CHILD) {\n   }\n   state T(\"12\") {\n   }\n}\n", options);
        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        var graph = result.Get<ITwinBehaviourGraph>();
        Assert.Equal(7, graph.ScriptStates[0].BehaviourIndexOrSlot);
        Assert.Equal(12, graph.ScriptStates[1].BehaviourIndexOrSlot);

        var states = new DefaultStateResolversList(new DefaultStateResolver("COM_CHILD"), new DefaultStateResolver("res://Global PS2/BehaviourGraph/COM_OTHER"));
        var script = AgentLabDecompiler.Decompile(graph, new DecompilerGraphResolver(new DefaultStarterResolver(null, null), states));
        // States only get their index written, both are State_0 here
        Assert.True(script.Contains("state State_0(COM_CHILD) {"), script);
        Assert.True(script.Contains("(\"res://Global PS2/BehaviourGraph/COM_OTHER\") {"), script);

        var unknown = AgentLabCompiler.Compile("behaviour A {\n   state S(COM_UNKNOWN) {\n   }\n}\n", options);
        Assert.True(unknown.CompilerStatus.IsError);
        Assert.Equal(2, unknown.CompilerStatus.Line);
        Assert.Contains("COM_UNKNOWN", unknown.CompilerStatus.Message);
    }

    // The editor's check knows which behaviours exist, without that the references pass
    [Fact]
    public void CheckReportsBehavioursThatDontExist()
    {
        const string script = "behaviour A {\n   state S(COM_KNOWN) {\n   }\n   state T(COM_OTHER) {\n   }\n}\n";
        Assert.False(AgentLabCompiler.Check(script, ActionDefinitions).IsError);
        Assert.False(AgentLabCompiler.Check(script, ActionDefinitions, _ => true).IsError);
        var status = AgentLabCompiler.Check(script, ActionDefinitions, reference => reference == "COM_KNOWN");
        Assert.True(status.IsError);
        Assert.Equal(4, status.Line);
        Assert.Contains("COM_OTHER", status.Message);
    }

    // Names the lexer keeps for itself can't be written bare, the decompiler writes those as strings
    [Fact]
    public void ReservedWordsArentIdentifiers()
    {
        Assert.True(AgentLabLexer.IsReservedKeyword("state"));
        Assert.True(AgentLabLexer.IsReservedKeyword("Priority"));
        Assert.True(AgentLabLexer.IsReservedKeyword("action"));
        Assert.False(AgentLabLexer.IsReservedKeyword("COM_NINA_LIFE_CRATE_BREAK"));
        Assert.True(AgentLabLexer.IsIdentifier("_a1"));
        Assert.False(AgentLabLexer.IsIdentifier("1a"));
        Assert.False(AgentLabLexer.IsIdentifier("res://a/b"));
    }
}
