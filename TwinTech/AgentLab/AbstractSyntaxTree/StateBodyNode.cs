namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal enum StateBodyKind
{
    /// <summary>
    /// if Condition(parameter) > threshold { ... }
    /// </summary>
    If,
    /// <summary>
    /// else { ... }, the body that runs when no other passes
    /// </summary>
    Else,
    /// <summary>
    /// completion { ... }, the first body of a state, run when its control packet or child behaviour finishes
    /// </summary>
    Completion
}

internal class StateBodyNode : IAgentLabTreeNode
{
    public StateBodyKind Kind { get; }
    public ConditionNode Condition { get; } // Null for else and completion blocks
    public IAgentLabTreeNode Window { get; } // Can be null
    public IAgentLabTreeNode Threshold { get; } // Can be null for else and completion blocks
    public IAgentLabTreeNode Weight { get; } // Can be null
    public IAgentLabTreeNode Restart { get; } // Can be null
    public bool IsNot { get; }
    public ActionListNode ActionList { get; } // Can be null
    public StateExecuteNode StateExecute { get; } // Can be null
    public AgentLabToken Token { get; }

    public StateBodyNode(AgentLabToken token, StateBodyKind kind, ConditionNode condition, IAgentLabTreeNode threshold, bool isNot, IAgentLabTreeNode window, IAgentLabTreeNode weight, IAgentLabTreeNode restart, ActionListNode actions, StateExecuteNode executeNode)
    {
        Token = token;
        Kind = kind;
        Condition = condition;
        Threshold = threshold;
        IsNot = isNot;
        Window = window;
        Weight = weight;
        Restart = restart;
        ActionList = actions;
        StateExecute = executeNode;
    }
}
