using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StateBodyListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }

    public StateBodyListNode(params StateBodyListNode[] children)
    {
        Children = new List<IAgentLabTreeNode>(children);
    }
}