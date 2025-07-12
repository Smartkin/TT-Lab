using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ConstDeclarationListNode : IAgentLabListNode
{
    public IList<IAgentLabTreeNode> Children { get; internal set; }

    public ConstDeclarationListNode(params ConstDeclarationNode[] constants)
    {
        Children = new List<IAgentLabTreeNode>(constants);
    }
}