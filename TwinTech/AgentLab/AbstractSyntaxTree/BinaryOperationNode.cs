using System;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class BinaryOperationNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public IAgentLabTreeNode Left { get; }
    public IAgentLabTreeNode Right { get; }
    
    public BinaryOperationNode(IAgentLabTreeNode left, AgentLabToken token, IAgentLabTreeNode right)
    {
        Left = left;
        Token = token;
        Right = right;
    }

    public override String ToString() => Left + Token.ToString() + Right;
}