namespace Twinsanity.AgentLab.Resolvers.Interfaces;

public interface IGraphResolver : IResolver
{
    IStarterResolver GetStarterResolver();
    IStateResolversList GetStateResolvers();
}