using System;
using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class NodeVisitor
{
    protected readonly Dictionary<Type, Func<IAgentLabTreeNode, object>> Visitors = new();
    private List<IAgentLabTreeNode> deferredVisits = new();
    
    public object Visit(IAgentLabTreeNode node)
    {
        if (node == null)
        {
            // Skip optional nodes
            return null;
        }
        
        if (!Visitors.ContainsKey(node.GetType()))
        {
            // TODO: raise visitor error
            return null;
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