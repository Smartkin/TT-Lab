namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ConditionNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public IAgentLabTreeNode Number { get; } // Can be null
    public string Name { get; }

    public ConditionNode(AgentLabToken token, IAgentLabTreeNode number = null)
    {
        Token = token;
        Number = number;
        Name = token.GetValue<string>();
    }
}