namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ParamDefinitionNode : IAgentLabTreeNode
{
    public string Name { get; }
    public TypeNode Type { get; }
    
    public ParamDefinitionNode(AgentLabToken token, TypeNode type)
    {
        Name = token.GetValue<string>();
        Type = type;
    }
}