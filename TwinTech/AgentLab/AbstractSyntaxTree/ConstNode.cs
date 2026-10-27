namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ConstNode : IAgentLabTreeNode
{
    public string Name { get; }
    public AgentLabToken.TokenType Type { get; protected set; } = AgentLabToken.TokenType.Undefined;
    // Not called Token: NodeVisitor places the errors of what it visits by the Token of the innermost node that has one, and the errors
    // inside a const's visit have always been placed by what holds it
    public AgentLabToken NameToken { get; }
    
    public ConstNode(AgentLabToken token)
    {
        Name = token.GetValue<string>();
        NameToken = token;
    }

    public override string ToString() => Name;
}
