namespace Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

internal class ControlPacketDataNode : IAgentLabTreeNode
{
    public string ControlPacketOwner { get; }
    public AgentLabToken Token { get; }
    public string Name { get; }
    public AssignNode Assign { get; }
    
    public ControlPacketDataNode(AgentLabToken token, AssignNode assign, string controlPacketOwner)
    {
        Token = token;
        Assign = assign;
        Name = token.GetValue<string>();
        ControlPacketOwner = controlPacketOwner;
    }
}