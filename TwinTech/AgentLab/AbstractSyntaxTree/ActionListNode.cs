using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ActionListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }
    
    public ActionListNode(params ActionNode[] actions)
    {
        Children = new List<IAgentLabTreeNode>(actions);
    }
}