namespace Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

public interface ICompilerResolver : IResolver
{
    IGraphResolver GetGraphResolver();
    IStateGraphResolver GetStateGraphResolver();
    IGlobalObjectIdResolver GetObjectIdResolver();
}