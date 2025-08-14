using System;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace Twinsanity.AgentLab.Resolvers.Compiler;

public class DefaultStateGraphResolver : IStateGraphResolver
{
    public Int16 ResolveGraphReference(string graphRef)
    {
        return short.Parse(graphRef);
    }
}