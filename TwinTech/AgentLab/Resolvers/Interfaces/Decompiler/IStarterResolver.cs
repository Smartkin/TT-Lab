using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;

public interface IStarterResolver : IResolver
{
    TwinBehaviourStarter ResolveStarter();
    IStarterAssignerGlobalObjectIdResolversList GetAssignerResolvers();
}