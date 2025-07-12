using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace Twinsanity.AgentLab.Resolvers.Decompiler;

public class DefaultStarterResolver : IStarterResolver
{
    private readonly TwinBehaviourStarter _starter;
    private readonly IStarterAssignerGlobalObjectIdResolversList _globalObjectIdResolvers;
    
    public DefaultStarterResolver(TwinBehaviourStarter starter, IStarterAssignerGlobalObjectIdResolversList globalObjectIdResolvers)
    {
        _starter = starter;
        _globalObjectIdResolvers = globalObjectIdResolvers;
    }
    
    public TwinBehaviourStarter ResolveStarter()
    {
        return _starter;
    }

    public IStarterAssignerGlobalObjectIdResolversList GetAssignerResolvers()
    {
        return _globalObjectIdResolvers;
    }
}