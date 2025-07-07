using Twinsanity.AgentLab.Resolvers.Interfaces;

public interface IStarterAssignerGlobalObjectIdResolver : IResolver
{
    string ResolveGlobalObjectId();
}