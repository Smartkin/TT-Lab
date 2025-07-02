using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ActionDefinitionListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }

    public ActionDefinitionListNode(params ActionDefinitionNode[] children)
    {
        Children = new List<IAgentLabTreeNode>(children);
    }
}