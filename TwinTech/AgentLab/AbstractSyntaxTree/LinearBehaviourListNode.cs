using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class LinearBehaviourListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }
    
    public LinearBehaviourListNode(params LinearBehaviourNode[] customActions)
    {
        Children = new List<IAgentLabTreeNode>(customActions);
    }
}