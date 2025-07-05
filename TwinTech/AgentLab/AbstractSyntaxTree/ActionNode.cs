namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ActionNode : IAgentLabTreeNode
{
    public ParamListNode Parameters { get; } // Can be null
    public AgentLabToken Token { get; }
    public string Name { get; }
    
    public ActionNode(AgentLabToken token, ParamListNode parameters = null)
    {
        Token = token;
        Parameters = parameters;
        Name = token.GetValue<string>();
    }
}