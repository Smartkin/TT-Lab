namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class IntervalNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public IAgentLabTreeNode Value { get; }
    
    public IntervalNode(AgentLabToken token, IAgentLabTreeNode value)
    {
        Token = token;
        Value = value;
    }
}