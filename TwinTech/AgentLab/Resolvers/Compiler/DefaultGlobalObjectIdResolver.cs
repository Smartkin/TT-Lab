using System;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace Twinsanity.AgentLab.Resolvers.Compiler;

public class DefaultGlobalObjectIdResolver : IGlobalObjectIdResolver
{
    public ushort ResolveGlobalObjectId(string globalObjectId)
    {
        return ushort.Parse(globalObjectId);
    }
}