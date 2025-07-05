using Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class BehaviourNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public string Name { get; }
    public BehaviourBodyNode Body { get; }
    public PriorityAttributeNode Priority { get; } // Can be null
    public StartFromAttributeNode StartFrom { get; } // Can be null
    
    public BehaviourNode(AgentLabToken token, BehaviourBodyNode body, PriorityAttributeNode priorityAttributeNode = null, StartFromAttributeNode startFrom = null)
    {
        Token = token;
        Body = body;
        Name = token.GetValue<string>();
        Priority = priorityAttributeNode;
        StartFrom = startFrom;
    }
}