using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ParamDefinitionListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }

    public ParamDefinitionListNode(params ParamDefinitionNode[] children)
    {
        Children = new List<IAgentLabTreeNode>(children);
    }
}