using System;
using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class NodeVisitor
{
    protected readonly Dictionary<Type, Func<IAgentLabTreeNode, object>> Visitors = new();
    private readonly List<IAgentLabTreeNode> deferredVisits = new();
    
    public object Visit(IAgentLabTreeNode node)
    {
        try
        {
            return VisitNode(node);
        }
        catch (Exception e) when (e is not AgentLabSyntaxException && TryGetPosition(node, out var line, out var column))
        {
            // Errors get the position of the innermost node that knows where it is in the script
            throw new AgentLabSyntaxException(e.Message, line, column, e);
        }
    }

    private static bool TryGetPosition(IAgentLabTreeNode node, out int line, out int column)
    {
        line = 0;
        column = 0;
        if (node?.GetType().GetProperty("Token")?.GetValue(node) is not AgentLabToken { Line: > 0 } token)
        {
            return false;
        }

        line = token.Line;
        column = token.Column;
        return true;
    }

    private object VisitNode(IAgentLabTreeNode node)
    {
        if (node == null)
        {
            // Skip optional nodes
            return null;
        }
        
        if (!Visitors.ContainsKey(node.GetType()) && node is not IAgentLabListNode)
        {
            return null;
        }

        // Special fallback case for lists of nodes
        if (!Visitors.ContainsKey(node.GetType()) && Visitors.ContainsKey(typeof(IAgentLabListNode)) && node is IAgentLabListNode)
        {
            return Visitors[typeof(IAgentLabListNode)](node);
        }
        
        return Visitors[node.GetType()](node);
    }

    protected void DeferredVisit(IAgentLabTreeNode node)
    {
        deferredVisits.Add(node);
    }

    protected void DoDeferredVisits()
    {
        foreach (var deferred in deferredVisits)
        {
            Visit(deferred);
        }
        
        deferredVisits.Clear();
    }
}