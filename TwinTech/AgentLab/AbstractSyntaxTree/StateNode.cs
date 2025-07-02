using System.Collections.Generic;
using Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StateNode : IAgentLabTreeNode
{
    public List<IAttributeNode> Attributes { get; }
    public AgentLabToken Token { get; }
    public AgentLabToken? BehaviourIdToken { get; }
    public StateBodyListNode Bodies { get; } // Can be null
    public string Name { get; }
    
    public StateNode(AgentLabToken token, AgentLabToken? behaviourId = null, StateBodyListNode bodies = null, params IAttributeNode[] attributes)
    {
        Name = token.GetValue<string>();
        Token = token;
        BehaviourIdToken = behaviourId;
        Attributes = new List<IAttributeNode>(attributes);
        Bodies = bodies;
    }
}