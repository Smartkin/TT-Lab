namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ArrayNode : ConstNode
{
    public IAgentLabTreeNode Index { get; }
    public AgentLabToken.TokenType StorageType => AgentLabToken.TokenType.IntegerType;
    
    public ArrayNode(AgentLabToken token, IAgentLabTreeNode index) : base(token)
    {
        Index = index;
        Type = AgentLabToken.TokenType.ArrayType;
    }
}