namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class NumberNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public object Value { get; }
    
    public NumberNode(AgentLabToken token)
    {
        Token = token;
        Value = token.GetValue<object>();
    }

    public override string ToString() => Value.ToString();
}