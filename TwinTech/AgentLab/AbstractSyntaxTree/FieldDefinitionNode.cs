namespace Twinsanity.AgentLab.AbstractSyntaxTree;

/// <summary>
/// One field of a packed parameter in a definition: <c>int name : width</c>, <c>sint name : width</c> or <c>bool name</c>
/// </summary>
internal class FieldDefinitionNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public string Name { get; }
    public AgentLabToken.TokenType Type { get; }
    public int Width { get; }

    public FieldDefinitionNode(AgentLabToken name, AgentLabToken type, int width)
    {
        Token = name;
        Name = name.GetValue<string>();
        Type = type.Type;
        Width = width;
    }
}
