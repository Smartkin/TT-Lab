using System.Text;
using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Tests.Support;
using Twinsanity.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.Tests.Assets;

[Collection(ProjectCollection.Name)]
public sealed class BehaviourReferenceTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly BehaviourGraph _global;
    private readonly BehaviourGraph _level;
    private readonly BehaviourGraph _xbox;

    public BehaviourReferenceTests()
    {
        _global = _project.Add(new BehaviourGraph(), "COM_GLOBAL", 0x10);
        _level = _project.Add(new BehaviourGraph(), "COM_LEVEL", 0x11, _project.Project.Ps2Package);
        _xbox = _project.Add(new BehaviourGraph(), "COM_GLOBAL", 0x12, _project.Project.GlobalPackageXbox);
    }

    public void Dispose() => _project.Dispose();

    private static AgentLabCompiler.CompilerResult Compile(IAsset requester, string script)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(0x20);
            writer.Write(Encoding.UTF8.GetBytes(script));
        }

        stream.Position = 0;
        return new PS2ItemFactory().GenerateBehaviourGraph(stream, requester);
    }

    private static string Script(string reference) => $"behaviour TEST {{\n   state S({reference}) {{\n   }}\n}}\n";

    // A script names the behaviours of its package and the packages it depends on. The global packages depend on nothing, so their
    // scripts only name their own, and the PS2 and Xbox versions never see each other's
    [AvaloniaFact]
    public void NamesResolveWithinThePackageAndItsDependencies()
    {
        Assert.Same(_global, BehaviourReferences.Find(_level, "COM_GLOBAL"));
        Assert.Same(_level, BehaviourReferences.Find(_level, "COM_LEVEL"));
        Assert.Same(_global, BehaviourReferences.Find(_global, "COM_GLOBAL"));
        Assert.Null(BehaviourReferences.Find(_global, "COM_LEVEL"));
        Assert.Same(_xbox, BehaviourReferences.Find(_xbox, "COM_GLOBAL"));
        Assert.Same(_global, BehaviourReferences.Find(_level, _global.URI));
        Assert.Equal(["COM_GLOBAL", "COM_LEVEL"], BehaviourReferences.GetCompletionItems(_level).Select(item => item.Text));
        Assert.Equal(["COM_GLOBAL"], BehaviourReferences.GetCompletionItems(_global).Select(item => item.Text));
        Assert.Equal("COM_GLOBAL", BehaviourReferences.ReferenceTo(_level, _global));

        // A level's own behaviour of a global name wins, the global one is then referred to by its URI
        var own = _project.Add(new BehaviourGraph(), "COM_GLOBAL", 0x13, _project.Project.Ps2Package);
        Assert.Same(own, BehaviourReferences.Find(_level, "COM_GLOBAL"));
        Assert.Equal(_global.URI, BehaviourReferences.ReferenceTo(_level, _global));
        Assert.Equal("COM_GLOBAL", BehaviourReferences.ReferenceTo(_global, _global));
    }

    [AvaloniaFact]
    public void ScriptsCompileAndDecompileWithNames()
    {
        var result = Compile(_level, Script("COM_GLOBAL"));
        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        var graph = result.Get<ITwinBehaviourGraph>();
        Assert.Equal(0x10, graph.ScriptStates[0].BehaviourIndexOrSlot);

        var data = new BehaviourGraphData(_level, graph);
        data.Import(_level.Package, null, null);
        Assert.Contains("state State_0(COM_GLOBAL) {", data.Graph);

        var byUri = Compile(_level, Script($"\"{_global.URI}\""));
        Assert.False(byUri.CompilerStatus.IsError, byUri.CompilerStatus.Message);
        Assert.Equal(0x10, byUri.Get<ITwinBehaviourGraph>().ScriptStates[0].BehaviourIndexOrSlot);

        var outOfReach = Compile(_global, Script("COM_LEVEL"));
        Assert.True(outOfReach.CompilerStatus.IsError);
        Assert.Equal(2, outOfReach.CompilerStatus.Line);
        Assert.Contains("COM_LEVEL", outOfReach.CompilerStatus.Message);

        // The editor's check tells as well
        Assert.True(AgentLabCompiler.Check(Script("COM_LEVEL"), "ActionDefinitionsPs2.lab", reference => BehaviourReferences.Find(_global, reference) != null).IsError);
        Assert.False(AgentLabCompiler.Check(Script("COM_LEVEL"), "ActionDefinitionsPs2.lab", reference => BehaviourReferences.Find(_level, reference) != null).IsError);
    }
}
