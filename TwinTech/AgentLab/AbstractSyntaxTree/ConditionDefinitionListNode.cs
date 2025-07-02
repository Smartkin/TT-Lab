using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ConditionDefinitionListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }
    
    public ConditionDefinitionListNode(params ConditionDefinitionNode[] children)
    {
        Children = new List<IAgentLabTreeNode>(children);
    }
}