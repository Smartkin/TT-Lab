using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StarterAssignerNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }

    public StarterAssignerNode(params StarterAssignNode[] assigns)
    {
        Children = new List<IAgentLabTreeNode>(assigns);
    }
}