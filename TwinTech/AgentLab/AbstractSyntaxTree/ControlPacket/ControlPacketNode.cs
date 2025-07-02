namespace Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

internal class ControlPacketNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public ControlPacketBodyNode Body { get; }
    
    public ControlPacketNode(AgentLabToken token, ControlPacketBodyNode body)
    {
        Token = token;
        Body = body;
    }
}