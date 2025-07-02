using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ParamListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }
    
    public ParamListNode(params ParamNode[] parameters)
    {
        Children = new List<IAgentLabTreeNode>(parameters);
    }
}