using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace Twinsanity.AgentLab.Resolvers.Compiler;

public class DefaultCompilerResolver : ICompilerResolver
{

    private IGraphResolver _graphResolver;
    private IGlobalObjectIdResolver _globalObjectIdResolver;
    
    public DefaultCompilerResolver(IGraphResolver graphResolver, IGlobalObjectIdResolver objectIdResolver)
    {
        _graphResolver = graphResolver;
        _globalObjectIdResolver = objectIdResolver;
    }
    
    public IGraphResolver GetGraphResolver()
    {
        return _graphResolver;
    }

    public IGlobalObjectIdResolver GetObjectIdResolver()
    {
        return _globalObjectIdResolver;
    }
}