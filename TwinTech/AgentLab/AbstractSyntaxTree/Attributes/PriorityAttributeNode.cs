namespace Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

internal class PriorityAttributeNode : IAttributeNode
{
    public NumberNode Priority { get; }
    
    public PriorityAttributeNode(NumberNode numberNode)
    {
        Priority = numberNode;
    }
}