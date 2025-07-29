using System;
using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;

namespace TT_Lab.Assets.Code.Resolvers.Decompiler;

public class LabStarterAssignerGlobalObjectIdResolver : IStarterAssignerGlobalObjectIdResolver
{
    private readonly string resolvedObjectUri;
    
    public LabStarterAssignerGlobalObjectIdResolver(LabURI package, string? variant, uint objectId)
    {
        resolvedObjectUri = AssetManager.Get().GetUri(package, nameof(GameObject), variant, objectId);
    }
    
    public String ResolveGlobalObjectId()
    {
        return resolvedObjectUri;
    }
}