using System;
using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;

namespace Twinsanity.AgentLab.Resolvers.Decompiler;

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