using Twinsanity.AgentLab.AbstractSyntaxTree.Attributes;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class BehaviourLibraryNode : IAgentLabTreeNode
{
    public string Name { get; }
    public LinearBehaviourListNode LinearBehaviours { get; }
    public ActionNode CreationAction { get; }
    public IAttributeNode GlobalIndex { get; } // Can be null
    public IAttributeNode InstanceType { get; } // Can be null
    
    public BehaviourLibraryNode(AgentLabToken token, LinearBehaviourListNode linearBehaviours, ActionNode creationAction, IAttributeNode globalIndex = null, IAttributeNode instanceType = null)
    {
        Name = token.GetValue<string>();
        LinearBehaviours = linearBehaviours;
        CreationAction = creationAction;
        GlobalIndex = globalIndex;
        InstanceType = instanceType;
    }
}