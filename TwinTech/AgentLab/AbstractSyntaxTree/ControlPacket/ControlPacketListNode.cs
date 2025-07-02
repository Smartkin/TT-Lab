using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

internal class ControlPacketListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }

    public ControlPacketListNode(params IAgentLabTreeNode[] children)
    {
        Children = new List<IAgentLabTreeNode>(children);
    }
}