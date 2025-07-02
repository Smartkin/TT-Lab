namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class TypeNode : IAgentLabTreeNode
{
    public AgentLabToken.TokenType Type { get; }
    
    public TypeNode(AgentLabToken token)
    {
        Type = token.Type;
    }
}