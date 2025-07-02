namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ParamNode : IAgentLabTreeNode
{
    public IAgentLabTreeNode Value { get; }
    
    public ParamNode(IAgentLabTreeNode node)
    {
        Value = node;
    }
}