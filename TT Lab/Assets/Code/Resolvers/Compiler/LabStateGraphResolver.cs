using System;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;

namespace TT_Lab.Assets.Code.Resolvers.Compiler;

public class LabStateGraphResolver : IStateGraphResolver
{
    public Int16 ResolveGraphReference(string graphRef)
    {
        return (short)AssetManager.Get().GetAsset((LabURI)graphRef).ID;
    }
}