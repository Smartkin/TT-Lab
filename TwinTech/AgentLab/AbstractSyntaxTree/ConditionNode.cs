namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ConditionNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public IAgentLabTreeNode Number { get; } // Can be null
    public string Value { get; }

    public ConditionNode(AgentLabToken token, IAgentLabTreeNode number = null)
    {
        Token = token;
        Number = number;
        Value = token.GetValue<string>();
    }
}