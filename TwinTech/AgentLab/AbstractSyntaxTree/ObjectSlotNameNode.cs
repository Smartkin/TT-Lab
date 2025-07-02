namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ObjectSlotNameNode : IAgentLabTreeNode
{
    public string SlotName { get; }
    public AgentLabToken.TokenType Token { get; }
    
    public ObjectSlotNameNode(AgentLabToken token)
    {
        SlotName = token.GetValue<string>();
        Token = token.Type;
    }
}