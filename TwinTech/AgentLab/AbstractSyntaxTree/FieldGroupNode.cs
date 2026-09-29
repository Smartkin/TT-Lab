using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

/// <summary>
/// An argument given as the fields of a packed parameter: <c>{name = value, ...}</c>
/// </summary>
internal class FieldGroupNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public List<(AgentLabToken Name, IAgentLabTreeNode Value)> Fields { get; }

    public FieldGroupNode(AgentLabToken token, List<(AgentLabToken Name, IAgentLabTreeNode Value)> fields)
    {
        Token = token;
        Fields = fields;
    }
}
