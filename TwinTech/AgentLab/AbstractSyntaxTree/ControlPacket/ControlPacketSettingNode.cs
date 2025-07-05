namespace Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

internal class ControlPacketSettingNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public AssignNode Assign { get; }
    public string Name { get; }
    
    public ControlPacketSettingNode(AgentLabToken token, AssignNode assign)
    {
        Token = token;
        Assign = assign;
        Name = token.GetValue<string>();
    }
}