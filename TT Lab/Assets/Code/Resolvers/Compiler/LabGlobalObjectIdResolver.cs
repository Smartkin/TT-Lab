using System;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace TT_Lab.Assets.Code.Resolvers.Compiler;

public class LabGlobalObjectIdResolver : IGlobalObjectIdResolver
{
    public ushort ResolveGlobalObjectId(string globalObjectId)
    {
        return (ushort)AssetManager.Get().GetAsset((LabURI)globalObjectId).ID;
    }
}