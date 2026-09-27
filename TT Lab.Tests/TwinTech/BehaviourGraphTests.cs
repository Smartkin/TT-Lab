using System.Text;
using TT_Lab.Assets.Factory;
using Twinsanity.AgentLab;
using Twinsanity.AgentLab.Resolvers.Decompiler;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.Tests.TwinTech;

public class BehaviourGraphTests
{
    private static AgentLabCompiler.CompilerResult Compile(int graphId, string script)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(graphId);
            writer.Write(Encoding.UTF8.GetBytes(script));
        }

        stream.Position = 0;
        return new PS2ItemFactory().GenerateBehaviourGraph(stream);
    }

    private static string Script(string graphPriority) =>
        "[StartFrom(State_0)]\n" +
        "[Priority(50)]\n" +
        graphPriority +
        "behaviour TEST_BEHAVIOUR {\n" +
        "   starter {\n" +
        "      assigner = {\n" +
        "         AssignType = ME;\n" +
        "         AssignLocality = ANYWHERE;\n" +
        "         AssignStatus = ANYSTATE;\n" +
        "         AssignPreference = ANYHOW;\n" +
        "      }\n" +
        "   }\n" +
        "   [Unknown(0x0)]\n" +
        "   state State_0() {\n" +
        "   }\n" +
        "}\n";

    // With a starter the Priority attribute is the starter's, the graph's own priority (0, 1 or 3 in the game) used to be lost
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void GraphsKeepTheirPriorityNextToTheirStarter(int graphPriority)
    {
        var compiled = Compile(0x2B, Script(graphPriority == 0 ? "" : $"[GraphPriority({graphPriority})]\n"));
        var graph = compiled.Get<ITwinBehaviourGraph>();
        var starter = compiled.Get<TwinBehaviourStarter>();

        var code = AgentLabDecompiler.Decompile(graph, new DefaultGraphResolver(new DefaultStarterResolver(starter, null), null));
        var recompiled = Compile(0x2B, code);

        Assert.Equal(graphPriority, graph.Priority);
        Assert.Equal(50, starter.Priority);
        Assert.Equal(graphPriority != 0, code.Contains($"[GraphPriority({graphPriority})]"));
        Assert.Equal(graphPriority, recompiled.Get<ITwinBehaviourGraph>().Priority);
        Assert.Equal(50, recompiled.Get<TwinBehaviourStarter>().Priority);
    }

    [Fact]
    public void GraphPriorityIsCheckedAndCompleted()
    {
        Assert.False(AgentLabCompiler.Check(Script("[GraphPriority(1)]\n"), "ActionDefinitionsPs2.lab").IsError);
        Assert.True(AgentLabCompiler.Check(Script("[GraphPriority(State_0)]\n"), "ActionDefinitionsPs2.lab").IsError);
        Assert.Contains(AgentLabCompletion.GetCompletions("[", 1, "ActionDefinitionsPs2.lab").Items, item => item.Text == "GraphPriority");
    }
}
