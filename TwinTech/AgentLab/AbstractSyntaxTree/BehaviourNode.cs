using Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class BehaviourNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public BehaviourBodyNode Body { get; }
    public PriorityAttributeNode Priority { get; } // Can be null
    
    public BehaviourNode(AgentLabToken token, BehaviourBodyNode body, PriorityAttributeNode priorityAttributeNode = null)
    {
        Token = token;
        Body = body;
        Priority = priorityAttributeNode;
    }
}