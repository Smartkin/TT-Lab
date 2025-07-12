namespace Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

internal class ControlPacketSettingNode : IAgentLabTreeNode
{
    public string ControlPacketOwner { get; }
    public AgentLabToken Token { get; }
    public AssignNode Assign { get; }
    public string Name { get; }
    
    public ControlPacketSettingNode(AgentLabToken token, AssignNode assign, string controlPacketOwner)
    {
        Token = token;
        Assign = assign;
        Name = token.GetValue<string>();
        ControlPacketOwner = controlPacketOwner;
    }
}