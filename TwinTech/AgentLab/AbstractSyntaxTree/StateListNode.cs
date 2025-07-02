using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StateListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }
    
    public StateListNode(params StateNode[] states)
    {
        Children = new List<IAgentLabTreeNode>(states);
    }
}