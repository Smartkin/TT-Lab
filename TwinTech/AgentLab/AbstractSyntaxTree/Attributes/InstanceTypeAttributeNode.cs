namespace Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

internal class InstanceTypeAttributeNode : IAttributeNode
{
    public ConstNode Key { get; }
    
    public InstanceTypeAttributeNode(ConstNode key)
    {
        Key = key;
    }
}