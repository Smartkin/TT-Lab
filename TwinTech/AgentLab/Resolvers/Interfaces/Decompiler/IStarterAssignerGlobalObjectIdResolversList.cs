namespace Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;

public interface IStarterAssignerGlobalObjectIdResolversList : IResolver
{
    IStarterAssignerGlobalObjectIdResolver ResolverAssigner(int index);
}