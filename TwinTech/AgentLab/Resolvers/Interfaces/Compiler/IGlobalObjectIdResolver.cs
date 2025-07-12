namespace Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

public interface IGlobalObjectIdResolver : IResolver
{
    ushort ResolveGlobalObjectId(string globalObjectId);
}