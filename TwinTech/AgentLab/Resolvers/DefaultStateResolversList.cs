using System.Collections.Generic;
using Twinsanity.AgentLab.Resolvers.Interfaces;

namespace Twinsanity.AgentLab.Resolvers;

public class DefaultStateResolversList : IStateResolversList
{
    private List<IStateResolver> _stateResolvers;

    public DefaultStateResolversList(params IStateResolver[] stateResolvers)
    {
        _stateResolvers = new List<IStateResolver>(stateResolvers);
    }
    
    public IStateResolver GetStateResolver(int index)
    {
        return _stateResolvers[index];
    }
}