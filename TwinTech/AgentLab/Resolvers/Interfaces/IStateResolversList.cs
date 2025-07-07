namespace Twinsanity.AgentLab.Resolvers.Interfaces;

public interface IStateResolversList : IResolver
{
    IStateResolver GetStateResolver(int index);
}