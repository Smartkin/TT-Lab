namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StateBodyNode : IAgentLabTreeNode
{
    public ConditionNode Condition { get; }
    public IntervalNode Interval { get; }
    public IAgentLabTreeNode Unknown { get; }
    public IAgentLabTreeNode Threshold { get; }
    public bool IsNot { get; }
    public ActionListNode ActionList { get; } // Can be null
    public StateExecuteNode StateExecute { get; } // Can be null
    
    public StateBodyNode(ConditionNode condition, IntervalNode interval, IAgentLabTreeNode threshold, IAgentLabTreeNode unknown, ActionListNode actions, StateExecuteNode executeNode, bool isNot)
    {
        Condition = condition;
        Interval = interval;
        Unknown = unknown;
        ActionList = actions;
        Threshold = threshold;
        StateExecute = executeNode;
        IsNot = isNot;
    }
}