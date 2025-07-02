namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StringNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public string Value { get; }
    
    public StringNode(AgentLabToken token)
    {
        Token = token;
        Value = token.GetValue<string>();
    }
}