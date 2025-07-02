using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class AliasListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }

    public AliasListNode(params AliasNode[] aliases)
    {
        Children = new List<IAgentLabTreeNode>(aliases);
    }
}