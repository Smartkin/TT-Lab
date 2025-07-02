namespace Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

internal class ControlPacketAttributeNode : IAttributeNode
{
    public AgentLabToken Token { get; }
    
    public ControlPacketAttributeNode(AgentLabToken token)
    {
        Token = token;
    }
}