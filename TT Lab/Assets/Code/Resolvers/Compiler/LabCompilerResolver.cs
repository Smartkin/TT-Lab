using Twinsanity.AgentLab.Resolvers.Compiler;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace TT_Lab.Assets.Code.Resolvers.Compiler;

public class LabCompilerResolver : ICompilerResolver
{
    private IGraphResolver _graphResolver;
    private IGlobalObjectIdResolver _globalObjectIdResolver = new LabGlobalObjectIdResolver();
    private IStateGraphResolver _stateGraphResolver = new LabStateGraphResolver();

    public LabCompilerResolver(IGlobalObjectIdResolver globalObjectIdResolver, IGraphResolver graphResolver)
    {
        _globalObjectIdResolver = globalObjectIdResolver;
        _graphResolver = graphResolver;
    }

    public LabCompilerResolver(int graphId = -1)
    {
        _graphResolver = new LabGraphResolver(graphId);
    }

    public IGraphResolver GetGraphResolver()
    {
        return _graphResolver;
    }

    public IStateGraphResolver GetStateGraphResolver()
    {
        return _stateGraphResolver;
    }

    public IGlobalObjectIdResolver GetObjectIdResolver()
    {
        return _globalObjectIdResolver;
    }
}