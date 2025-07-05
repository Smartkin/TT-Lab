using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

internal class AttributeListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }

    public AttributeListNode(params IAttributeNode[] attributes)
    {
        Children = new List<IAgentLabTreeNode>(attributes);
    }
}