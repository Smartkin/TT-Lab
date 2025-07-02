namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ThresholdNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public IAgentLabTreeNode Value { get; }
    
    public ThresholdNode(AgentLabToken token, IAgentLabTreeNode value)
    {
        Token = token;
        Value = value;
    }
}