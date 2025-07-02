using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ConstListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; }

    public ConstListNode(params ConstDeclarationNode[] constants)
    {
        Children = new List<IAgentLabTreeNode>(constants);
    }
}