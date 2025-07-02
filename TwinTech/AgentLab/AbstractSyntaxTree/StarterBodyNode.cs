using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StarterBodyNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }
    
    public StarterBodyNode(params StarterAssignNode[] assigns)
    {
        Children = new List<IAgentLabTreeNode>(assigns);
    }
}