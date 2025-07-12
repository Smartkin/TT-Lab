using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.AgentLab.AbstractSyntaxTree;
using Twinsanity.AgentLab.SymbolTable;

namespace Twinsanity.AgentLab.Analyzers;

/// <summary>
/// The analyzer goes through a const declaration list and reorders it in a way where the types can be determined top to bottom of the list
/// </summary>
internal class ConstDeclarationOrderAnalyzer : NodeVisitor, IAnalyzer
{
    private readonly Dictionary<string, bool> _constTypeCanBeDetermined = new();
    private AgentLabSymbolTable _symbolTable;
    
    public ConstDeclarationOrderAnalyzer(AgentLabSymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
        
        Visitors.Add(typeof(ConstDeclarationListNode), VisitConstDeclarationListNode);
        Visitors.Add(typeof(ConstNode), VisitConstNode);
        Visitors.Add(typeof(NumberNode), VisitNumberNode);
        Visitors.Add(typeof(BinaryOperationNode), VisitBinaryOperationNode);
        Visitors.Add(typeof(UnaryOperationNode), VisitUnaryOperation);
    }

    private Object VisitNumberNode(IAgentLabTreeNode node)
    {
        return true;
    }

    private Object VisitConstNode(IAgentLabTreeNode node)
    {
        var constNode = (ConstNode)node;
        return _constTypeCanBeDetermined[constNode.Name];
    }

    private Object VisitUnaryOperation(IAgentLabTreeNode node)
    {
        var unOp = (UnaryOperationNode)node;
        return (bool)Visit(unOp.Expression);
    }

    private Object VisitBinaryOperationNode(IAgentLabTreeNode node)
    {
        var binOp = (BinaryOperationNode)node;
        var leftHasConst = (bool)Visit(binOp.Left);
        var rightHasConst = (bool)Visit(binOp.Right);
        return leftHasConst || rightHasConst;
    }

    private Object VisitConstDeclarationListNode(IAgentLabTreeNode node)
    {
        /*
         * Input:
         * const myConst1 = myConst2;
         * const myConst2 = 0;
         * const myConst3 = myConst1;
         * const myConst4 = myConst6 + myConst7;
         * const myConst5 = myConst2;
         * const myConst6 = 5;
         * const myConst7 = myConst1;
         * Expected:
         * const myConst2 = 0;
         * const myConst6 = 5;
         * const myConst1 = myConst2;
         * const myConst7 = myConst1;
         * const myConst3 = myConst1;
         * const myConst4 = myConst6 + myConst7;
         * const myConst5 = myConst2;
         */
        
        var constDecls = (ConstDeclarationListNode)node;
        var sortedInOrderOfUsingConsts = constDecls.Children.Cast<ConstDeclarationNode>().ToList();
        foreach (var constNode in sortedInOrderOfUsingConsts.Select(constDecl => (ConstNode)constDecl.Assign.Left))
        {
            _constTypeCanBeDetermined.Add(constNode.Name, false);
        }

        while (!_constTypeCanBeDetermined.Values.All(b => b))
        {
            sortedInOrderOfUsingConsts.Sort((elem1, elem2) =>
            {
                var const1 = (ConstNode)elem1.Assign.Left;
                var const2 = (ConstNode)elem2.Assign.Left;

                _constTypeCanBeDetermined[const1.Name] = (bool)Visit(elem1.Assign.Right);
                var elem1TypeCanBeDetermined = _constTypeCanBeDetermined[const1.Name];

                _constTypeCanBeDetermined[const1.Name] = (bool)Visit(elem2.Assign.Right);
                var elem2TypeCanBeDetermined = _constTypeCanBeDetermined[const2.Name];

                switch (elem1TypeCanBeDetermined)
                {
                    case true when elem2TypeCanBeDetermined:
                        break;
                    case true:
                        return -1;
                    default:
                    {
                        if (elem2TypeCanBeDetermined)
                        {
                            return 1;
                        }

                        break;
                    }
                }

                return 0;
            });
        }

        constDecls.Children = sortedInOrderOfUsingConsts.Cast<IAgentLabTreeNode>().ToList();
        return null;
    }

    public void Analyze(IAgentLabTreeNode tree)
    {
        Visit(tree);
    }
}