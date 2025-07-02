namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class ConstDeclarationNode : IAgentLabTreeNode
{
    public AssignNode Assign { get; set; }
    
    public ConstDeclarationNode(AssignNode assign)
    {
        Assign = assign;
    }
}