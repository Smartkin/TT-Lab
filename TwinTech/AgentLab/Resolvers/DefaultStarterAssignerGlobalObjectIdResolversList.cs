using System.Collections.Generic;
using Twinsanity.AgentLab.Resolvers.Interfaces;

namespace Twinsanity.AgentLab.Resolvers;

public class DefaultStarterAssignerGlobalObjectIdResolversList : IStarterAssignerGlobalObjectIdResolversList
{
    private List<IStarterAssignerGlobalObjectIdResolver> _resolvers;
    
    public DefaultStarterAssignerGlobalObjectIdResolversList(params IStarterAssignerGlobalObjectIdResolver[] resolvers)
    {
        _resolvers = new List<IStarterAssignerGlobalObjectIdResolver>(resolvers);
    }
    
    public IStarterAssignerGlobalObjectIdResolver ResolverAssigner(int index)
    {
        return _resolvers[index];
    }
}