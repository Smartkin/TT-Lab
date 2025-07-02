namespace Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

internal class UseObjectSlotAttributeNode : IAttributeNode
{
    public ObjectSlotNameNode SlotName { get; }
    
    public UseObjectSlotAttributeNode(ObjectSlotNameNode node)
    {
        SlotName = node;
    }
}