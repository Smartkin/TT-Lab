using System;
using Twinsanity.AgentLab.Resolvers.Interfaces;

namespace Twinsanity.AgentLab.Resolvers;

public class DefaultStateResolver : IStateResolver
{
    private string _behaviourName;
    
    public DefaultStateResolver(string behaviourName)
    {
        _behaviourName = behaviourName;
    }
    
    public String ResolveBehaviour()
    {
        return _behaviourName;
    }
}