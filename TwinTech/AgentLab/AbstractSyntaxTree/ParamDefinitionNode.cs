using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ParamDefinitionNode : IAgentLabTreeNode
{
    public string Name { get; }
    public TypeNode Type { get; }
    /// <summary>
    /// The fields of a packed parameter, null for a plain one
    /// </summary>
    public List<FieldDefinitionNode> Fields { get; }
    
    public ParamDefinitionNode(AgentLabToken token, TypeNode type)
    {
        Name = token.GetValue<string>();
        Type = type;
    }

    public ParamDefinitionNode(AgentLabToken token, List<FieldDefinitionNode> fields)
    {
        Name = token.GetValue<string>();
        Type = new TypeNode(new AgentLabToken(AgentLabToken.TokenType.PackedType, "packed"));
        Fields = fields;
    }
}
