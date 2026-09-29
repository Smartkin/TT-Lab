using TT_Lab.AssetData.Code.Behaviour;
using Twinsanity.AgentLab;
using Twinsanity.AgentLab.AgentLabObjectDescs.PS2;
using Twinsanity.AgentLab.Resolvers.Compiler;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.Tests.TwinTech;

public class BehaviourGraphTemplateTests
{
    // The script a new graph starts with has to compile with what the game has
    [Fact]
    public void TemplateCompiles()
    {
        var graphResolver = new DefaultGraphResolver();
        graphResolver.AddNewGraphRef("COM_RENAME_ME", 12);
        var options = new AgentLabCompiler.CompilerOptions
        {
            Command = new PS2CommandDesc(), CommandPack = new PS2CommandPackDesc(), State = new PS2StateDesc(), StateBody = new PS2StateBodyDesc(), Graph = new PS2GraphDesc(),
            ActionDefinitionsFile = "ActionDefinitionsPs2.lab", Resolver = new DefaultCompilerResolver(graphResolver, new DefaultGlobalObjectIdResolver())
        };

        var status = AgentLabCompiler.Check(BehaviourGraphData.Template, "ActionDefinitionsPs2.lab");
        var result = AgentLabCompiler.Compile(BehaviourGraphData.Template, options);

        Assert.False(status.IsError, status.Message);
        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        var graph = result.Get<ITwinBehaviourGraph>();
        Assert.Equal(2, graph.ScriptStates.Count);
        Assert.Equal(TwinBehaviourCondition.ElseConditionIndex, graph.ScriptStates[0].Bodies[1].Condition.ConditionIndex);
        Assert.Equal(0.2f, graph.ScriptStates[0].Bodies[0].Condition.TimeWindow);
        Assert.Equal(20, result.Get<TwinBehaviourStarter>().Priority);
    }
}
