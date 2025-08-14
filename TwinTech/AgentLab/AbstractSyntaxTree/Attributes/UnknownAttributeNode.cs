namespace Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

internal class UnknownAttributeNode : IAttributeNode
{
    public NumberNode Number { get; }
    
    public UnknownAttributeNode(NumberNode number)
    {
        Number = number;
    }
}