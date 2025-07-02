using System;
using Twinsanity.AgentLab.AbstractSyntaxTree;

namespace Twinsanity.AgentLab;

internal class AgentLabInterpreterNodeVisitor : NodeVisitor
{
    public AgentLabInterpreterNodeVisitor()
    {
        Visitors.Add(typeof(BinaryOperationNode), VisitBinaryOperation);
        Visitors.Add(typeof(NumberNode), VisitNumber);
        Visitors.Add(typeof(UnaryOperationNode), VisitUnaryOperation);
        Visitors.Add(typeof(NoOpNode), VisitNoOp);
        Visitors.Add(typeof(ConstNode), VisitConst);
    }

    private Object VisitConst(IAgentLabTreeNode node)
    {
        var constNode = (ConstNode)node;
        throw new NotImplementedException();
    }

    private Object VisitNoOp(IAgentLabTreeNode node)
    {
        return null;
    }

    private Object VisitUnaryOperation(IAgentLabTreeNode node)
    {
        var unOp = (UnaryOperationNode)node;
        var result = Visit(unOp.Expression);
        return unOp.Token.Type switch
        {
            AgentLabToken.TokenType.AddOperator => +(int)result,
            AgentLabToken.TokenType.SubtractOperator => -(int)result,
            _ => null
        };
    }

    private Object VisitNumber(IAgentLabTreeNode node)
    {
        var number = (NumberNode)node;
        return number.Value;
    }

    private Object VisitBinaryOperation(IAgentLabTreeNode node)
    {
        var binOp = (BinaryOperationNode)node;
        var left = Visit(binOp.Left);
        var right = Visit(binOp.Right);
        return binOp.Token.Type switch
        {
            AgentLabToken.TokenType.AddOperator => (int)left + (int)right,
            AgentLabToken.TokenType.SubtractOperator => (int)left - (int)right,
            AgentLabToken.TokenType.MultiplyOperator => (int)left * (int)right,
            AgentLabToken.TokenType.DivideOperator => (int)left / (int)right,
            _ => null
        };
    }
}