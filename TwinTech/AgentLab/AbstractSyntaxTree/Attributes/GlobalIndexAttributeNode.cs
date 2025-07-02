namespace Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

internal class GlobalIndexAttributeNode : IAttributeNode
{
    public NumberNode Index { get; }
    
    public GlobalIndexAttributeNode(NumberNode indexNode)
    {
        Index = indexNode;
    }
}