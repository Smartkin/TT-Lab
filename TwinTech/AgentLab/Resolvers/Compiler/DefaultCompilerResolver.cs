using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace Twinsanity.AgentLab.Resolvers.Compiler;

public class DefaultCompilerResolver : ICompilerResolver
{

    private IGraphResolver _graphResolver;
    private IGlobalObjectIdResolver _globalObjectIdResolver;
    private IStateGraphResolver _stateGraphResolver;
    
    public DefaultCompilerResolver(IGraphResolver graphResolver, IGlobalObjectIdResolver objectIdResolver)
    {
        _graphResolver = graphResolver;
        _globalObjectIdResolver = objectIdResolver;
        _stateGraphResolver = new DefaultStateGraphResolver();
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