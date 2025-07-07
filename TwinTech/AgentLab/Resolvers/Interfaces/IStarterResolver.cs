using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

public interface IStarterResolver : IResolver
{
    TwinBehaviourStarter ResolveStarter();
    IStarterAssignerGlobalObjectIdResolversList GetAssignerResolvers();
}