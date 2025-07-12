namespace Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;

public interface IStateResolversList : IResolver
{
    IStateResolver GetStateResolver(int index);
}