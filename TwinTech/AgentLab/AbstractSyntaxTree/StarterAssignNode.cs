namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StarterAssignNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public AssignNode Assign { get; }
    
    public StarterAssignNode(AgentLabToken token, AssignNode assign)
    {
        Token = token;
        Assign = assign;
    }
}