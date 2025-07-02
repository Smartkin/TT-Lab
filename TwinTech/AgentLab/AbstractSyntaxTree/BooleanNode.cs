using System;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class BooleanNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public Boolean Value { get; }
    
    public BooleanNode(AgentLabToken token)
    {
        Token = token;
        Value = bool.Parse(token.GetValue<String>());
    }
}