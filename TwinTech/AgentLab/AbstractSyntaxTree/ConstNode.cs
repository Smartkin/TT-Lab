namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ConstNode : IAgentLabTreeNode
{
    public string Value { get; }
    public AgentLabToken.TokenType Type { get; } = AgentLabToken.TokenType.IntegerType;
    
    public ConstNode(AgentLabToken token)
    {
        Value = token.GetValue<string>();
    }
}