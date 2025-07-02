namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ActionDefinitionNode : IAgentLabTreeNode
{
    public string Name { get; }
    public IAgentLabTreeNode Parameters { get; } // Can be null
    public IAgentLabTreeNode Index { get; }
    public IAgentLabTreeNode Aliases { get; }
    
    public ActionDefinitionNode(AgentLabToken token, IAgentLabTreeNode parameters = null, IAgentLabTreeNode actionIndex = null, IAgentLabTreeNode aliases = null)
    {
        Name = token.GetValue<string>();
        Parameters = parameters;
        Index = actionIndex;
        Aliases = aliases;
    }
}