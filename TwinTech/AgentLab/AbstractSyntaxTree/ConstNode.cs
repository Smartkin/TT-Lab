namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ConstNode : IAgentLabTreeNode
{
    public string Name { get; }
    public AgentLabToken.TokenType Type { get; protected set; } = AgentLabToken.TokenType.IntegerType;
    
    public ConstNode(AgentLabToken token)
    {
        Name = token.GetValue<string>();
    }
}