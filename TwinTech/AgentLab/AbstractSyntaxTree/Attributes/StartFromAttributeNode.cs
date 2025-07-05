namespace Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

internal class StartFromAttributeNode : IAttributeNode
{
    public string StateName { get; }
    
    public StartFromAttributeNode(AgentLabToken token)
    {
        StateName = token.GetValue<string>();
    }
}