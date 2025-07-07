namespace Twinsanity.AgentLab.Resolvers.Interfaces;

public interface IStarterAssignerGlobalObjectIdResolversList : IResolver
{
    IStarterAssignerGlobalObjectIdResolver ResolverAssigner(int index);
}