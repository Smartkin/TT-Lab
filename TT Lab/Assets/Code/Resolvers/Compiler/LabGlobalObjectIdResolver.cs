using System;
using System.Collections.Generic;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace TT_Lab.Assets.Code.Resolvers.Compiler;

public class LabGlobalObjectIdResolver : IGlobalObjectIdResolver
{
    private readonly List<LabURI> _resolvedObjects = [];
    
    public ushort ResolveGlobalObjectId(string globalObjectId)
    {
        _resolvedObjects.Add((LabURI)globalObjectId);
        return (ushort)AssetManager.Get().GetAsset((LabURI)globalObjectId).ID;
    }
    
    public IReadOnlyList<LabURI> ResolvedObjects => _resolvedObjects;
}