namespace Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

public interface ICompilerResolver : IResolver
{
    IGraphResolver GetGraphResolver();
    IGlobalObjectIdResolver GetObjectIdResolver();
}