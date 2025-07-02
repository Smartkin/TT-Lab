namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class LinearBehaviourNode : IAgentLabTreeNode
{
    public ActionListNode Actions { get; } // Can be null
    public string Name { get; }
    
    public LinearBehaviourNode(AgentLabToken token, ActionListNode actions = null)
    {
        Name = token.GetValue<string>();
        Actions = actions;
    }
}