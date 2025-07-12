using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;

namespace Twinsanity.AgentLab.Resolvers.Decompiler;

public class DefaultStarterAssignerGlobalObjectIdResolver : IStarterAssignerGlobalObjectIdResolver
{
    private int _id;
    public DefaultStarterAssignerGlobalObjectIdResolver(int id)
    {
        _id = id;
    }
    
    public string ResolveGlobalObjectId()
    {
        return _id.ToString();
    }
}