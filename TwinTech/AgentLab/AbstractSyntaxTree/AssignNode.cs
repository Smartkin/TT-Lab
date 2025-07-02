namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class AssignNode : IAgentLabTreeNode
{
    public IAgentLabTreeNode Left { get; }
    public AgentLabToken Token { get; }
    public IAgentLabTreeNode Right { get; }
    
    public AssignNode(IAgentLabTreeNode left, AgentLabToken token, IAgentLabTreeNode right)
    {
        Left = left;
        Token = token;
        Right = right;
    }
}