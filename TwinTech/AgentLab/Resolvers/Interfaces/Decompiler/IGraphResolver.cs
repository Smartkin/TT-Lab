namespace Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;

public interface IGraphResolver : IResolver
{
    IStarterResolver GetStarterResolver();
    IStateResolversList GetStateResolvers();
}