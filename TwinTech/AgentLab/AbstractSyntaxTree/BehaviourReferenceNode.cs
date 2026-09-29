namespace Twinsanity.AgentLab.AbstractSyntaxTree;

/// <summary>
/// The behaviour a state runs as its child, by its name (an identifier) or by a string the resolver understands (a URI, an index)
/// </summary>
internal class BehaviourReferenceNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public string Reference { get; }
    public bool IsName { get; }

    public BehaviourReferenceNode(AgentLabToken token, bool isName)
    {
        Token = token;
        Reference = token.GetValue<string>();
        IsName = isName;
    }
}
