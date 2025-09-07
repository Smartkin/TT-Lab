using System;
using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;

namespace TT_Lab.Assets.Code.Resolvers.Decompiler;

public class LabStarterAssignerGlobalObjectIdResolver : IStarterAssignerGlobalObjectIdResolver
{
    private readonly string resolvedObjectUri;
    
    public LabStarterAssignerGlobalObjectIdResolver(IAsset requester, uint objectId)
    {
        resolvedObjectUri = AssetManager.Get().GetUriByTwinId<GameObject>(requester, objectId);
    }
    
    public String ResolveGlobalObjectId()
    {
        return resolvedObjectUri;
    }
}