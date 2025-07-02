namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ConditionDefinitionNode : IAgentLabTreeNode
{
    public string Name { get; }
    public IAgentLabTreeNode Parameter { get; } // Can be null
    public IAgentLabTreeNode Index { get; } // Can be null
    public IAgentLabTreeNode Aliases { get; } // Can be null
    
    public ConditionDefinitionNode(AgentLabToken token, IAgentLabTreeNode parameter = null, IAgentLabTreeNode conditionIndex = null, IAgentLabTreeNode aliases = null)
    {
        Name = token.GetValue<string>();
        Parameter = parameter;
        Index = conditionIndex;
        Aliases = aliases;
    }
}