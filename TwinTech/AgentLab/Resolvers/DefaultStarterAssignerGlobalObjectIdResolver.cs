using System;

namespace Twinsanity.AgentLab.Resolvers;

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