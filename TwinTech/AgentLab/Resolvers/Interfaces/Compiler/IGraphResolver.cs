namespace Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

public interface IGraphResolver : IResolver
{
    short ResolveGraphReference(string graphRef);
}