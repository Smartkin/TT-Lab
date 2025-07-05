namespace Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

internal class ControlPacketDataNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public string Name { get; }
    public AssignNode Assign { get; }
    
    public ControlPacketDataNode(AgentLabToken token, AssignNode assign)
    {
        Token = token;
        Assign = assign;
        Name = token.GetValue<string>();
    }
}