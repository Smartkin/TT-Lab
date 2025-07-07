using System.Collections.Generic;
using Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StateNode : IAgentLabTreeNode
{
    public AttributeListNode Attributes { get; }
    public AgentLabToken Token { get; }
    public IAgentLabTreeNode BehaviourIdToken { get; } // Can be null
    public StateBodyListNode Bodies { get; } // Can be null
    public string Name { get; }
    
    public StateNode(AgentLabToken token, IAgentLabTreeNode behaviourId = null, StateBodyListNode bodies = null, AttributeListNode attributes = null)
    {
        Name = token.GetValue<string>();
        Token = token;
        BehaviourIdToken = behaviourId;
        Attributes = attributes;
        Bodies = bodies;
    }
}