namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class StateExecuteNode : IAgentLabTreeNode
{
    public string StateName { get; }
    public AgentLabToken Token { get; }
    
    public StateExecuteNode(AgentLabToken token)
    {
        Token = token;
        StateName = token.GetValue<string>();
    }
}