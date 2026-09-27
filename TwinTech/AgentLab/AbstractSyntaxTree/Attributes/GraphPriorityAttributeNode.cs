namespace Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

internal class GraphPriorityAttributeNode : IAttributeNode
{
    public NumberNode Priority { get; }

    public GraphPriorityAttributeNode(NumberNode numberNode)
    {
        Priority = numberNode;
    }
}
