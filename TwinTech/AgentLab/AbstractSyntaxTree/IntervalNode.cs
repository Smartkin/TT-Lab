namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class IntervalNode : IAgentLabTreeNode
{
    public IAgentLabTreeNode Value { get; }
    
    public IntervalNode(IAgentLabTreeNode value)
    {
        Value = value;
    }
}