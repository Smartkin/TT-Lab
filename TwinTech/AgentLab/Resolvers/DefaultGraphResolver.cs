using Twinsanity.AgentLab.Resolvers.Interfaces;

namespace Twinsanity.AgentLab.Resolvers;

public class DefaultGraphResolver : IGraphResolver
{
    private IStarterResolver _starterResolver;
    private IStateResolversList _stateResolvers;
    
    public DefaultGraphResolver(IStarterResolver starterResolver, IStateResolversList stateResolvers)
    {
        _starterResolver = starterResolver;
        _stateResolvers = stateResolvers;
    }

    public IStarterResolver GetStarterResolver()
    {
        return _starterResolver;
    }

    public IStateResolversList GetStateResolvers()
    {
        return _stateResolvers;
    }
}