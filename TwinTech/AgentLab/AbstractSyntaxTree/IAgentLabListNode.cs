using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal interface IAgentLabListNode : IAgentLabTreeNode
{
    IList<IAgentLabTreeNode> Children { get; }
}