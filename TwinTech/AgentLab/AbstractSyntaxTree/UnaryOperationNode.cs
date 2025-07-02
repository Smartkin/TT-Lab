namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class UnaryOperationNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public IAgentLabTreeNode Expression { get; }
    
    public UnaryOperationNode(AgentLabToken token, IAgentLabTreeNode expr)
    {
        Token = token;
        Expression = expr;
    }

    public override string ToString() => Token.GetValue<object>() + Expression.ToString();
}