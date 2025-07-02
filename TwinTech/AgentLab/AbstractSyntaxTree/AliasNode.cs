namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class AliasNode : IAgentLabTreeNode
{
    public string Name { get; }
    
    public AliasNode(AgentLabToken token)
    {
        Name = token.GetValue<string>();
    }
}