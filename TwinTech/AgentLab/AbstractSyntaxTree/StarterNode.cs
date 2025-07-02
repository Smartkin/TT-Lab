namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StarterNode : IAgentLabTreeNode
{
    public StarterBodyNode Body { get; }
    
    public StarterNode(StarterBodyNode body)
    {
        Body = body;
    }
}