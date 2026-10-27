using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.AgentLab.AbstractSyntaxTree;

namespace Twinsanity.AgentLab.Analyzers;

/// <summary>
/// Orders a behaviour's const declarations so that every const comes after the consts its value names, which lets their types and
/// values be worked out top to bottom. Consts whose values go back to themselves (<see cref="Cyclic"/>) and then the ones naming those
/// can't be worked out, they come last in the order they're written
/// </summary>
internal class ConstDeclarationOrderAnalyzer : IAnalyzer
{
    // The declarations whose value names itself, or a const whose value goes back to it
    public HashSet<ConstDeclarationNode> Cyclic { get; } = new();

    /*
     * Input:
     * const myConst1 = myConst2;
     * const myConst2 = 0;
     * const myConst3 = myConst1;
     * const myConst4 = myConst6 + myConst7;
     * const myConst5 = myConst2;
     * const myConst6 = 5;
     * const myConst7 = myConst1;
     * Output, every time the first written of the ones whose values can be worked out:
     * const myConst2 = 0;
     * const myConst1 = myConst2;
     * const myConst3 = myConst1;
     * const myConst5 = myConst2;
     * const myConst6 = 5;
     * const myConst7 = myConst1;
     * const myConst4 = myConst6 + myConst7;
     */
    public void Analyze(IAgentLabTreeNode tree)
    {
        if (tree is not ConstDeclarationListNode list || list.Children.Count == 0)
        {
            return;
        }

        var declarations = list.Children.Cast<ConstDeclarationNode>().ToList();
        var byName = new Dictionary<string, int>();
        for (var i = 0; i < declarations.Count; i++)
        {
            byName.TryAdd(NameOf(declarations[i]), i);
        }

        var dependencies = declarations.Select(declaration => NamesIn(declaration.Assign.Right).Where(byName.ContainsKey).Select(name => byName[name]).Distinct().ToList()).ToList();
        var dependents = declarations.Select(_ => new List<int>()).ToList();
        var waitingFor = new int[declarations.Count];
        var ready = new SortedSet<int>();
        for (var i = 0; i < declarations.Count; i++)
        {
            foreach (var dependency in dependencies[i])
            {
                dependents[dependency].Add(i);
            }

            waitingFor[i] = dependencies[i].Count;
            if (waitingFor[i] == 0)
            {
                ready.Add(i);
            }
        }

        var order = new List<IAgentLabTreeNode>(declarations.Count);
        var placed = new bool[declarations.Count];
        while (ready.Count > 0)
        {
            var next = ready.Min;
            ready.Remove(next);
            order.Add(declarations[next]);
            placed[next] = true;
            foreach (var dependent in dependents[next])
            {
                if (--waitingFor[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        if (order.Count < declarations.Count)
        {
            FindCycles(declarations, dependencies, placed);
            // The cyclic ones first: the symbol pass stops at the first of them, and a const naming one has no type to check its value by
            order.AddRange(declarations.Where((declaration, i) => !placed[i] && Cyclic.Contains(declaration)));
            order.AddRange(declarations.Where((declaration, i) => !placed[i] && !Cyclic.Contains(declaration)));
        }

        list.Children = order;
    }

    private static string NameOf(ConstDeclarationNode declaration) => ((ConstNode)declaration.Assign.Left).Name;

    // The names the value refers to: its consts, an array's index's (the array's own name isn't a const)
    private static IEnumerable<string> NamesIn(IAgentLabTreeNode value)
    {
        var nodes = new Stack<IAgentLabTreeNode>();
        nodes.Push(value);
        while (nodes.Count > 0)
        {
            switch (nodes.Pop())
            {
                case ArrayNode array:
                    nodes.Push(array.Index);
                    break;
                case ConstNode name:
                    yield return name.Name;
                    break;
                case UnaryOperationNode unary:
                    nodes.Push(unary.Expression);
                    break;
                case BinaryOperationNode binary:
                    nodes.Push(binary.Right);
                    nodes.Push(binary.Left);
                    break;
                case TaggedLiteralNode tagged:
                    nodes.Push(tagged.Value);
                    break;
            }
        }
    }

    // The consts left over either go back to themselves or name one that does: Tarjan's strongly connected components among them,
    // without recursion (a chain of thousands of consts would run out of stack), the ones of a component of several or naming itself
    // are the cyclic ones
    private void FindCycles(List<ConstDeclarationNode> declarations, List<List<int>> dependencies, bool[] placed)
    {
        var count = declarations.Count;
        var indexes = Enumerable.Repeat(-1, count).ToArray();
        var lowest = new int[count];
        var onStack = new bool[count];
        var stack = new Stack<int>();
        var next = 0;
        for (var root = 0; root < count; root++)
        {
            if (placed[root] || indexes[root] != -1)
            {
                continue;
            }

            var work = new Stack<(int Node, int Edge)>();
            Enter(root);
            work.Push((root, 0));
            while (work.Count > 0)
            {
                var (node, edge) = work.Pop();
                if (edge < dependencies[node].Count)
                {
                    work.Push((node, edge + 1));
                    var dependency = dependencies[node][edge];
                    if (placed[dependency])
                    {
                        continue;
                    }

                    if (indexes[dependency] == -1)
                    {
                        Enter(dependency);
                        work.Push((dependency, 0));
                    }
                    else if (onStack[dependency])
                    {
                        lowest[node] = Math.Min(lowest[node], indexes[dependency]);
                    }

                    continue;
                }

                if (work.Count > 0)
                {
                    var parent = work.Peek().Node;
                    lowest[parent] = Math.Min(lowest[parent], lowest[node]);
                }

                if (lowest[node] != indexes[node])
                {
                    continue;
                }

                var component = new List<int>();
                int member;
                do
                {
                    member = stack.Pop();
                    onStack[member] = false;
                    component.Add(member);
                }
                while (member != node);

                if (component.Count > 1 || dependencies[node].Contains(node))
                {
                    foreach (var cyclic in component)
                    {
                        Cyclic.Add(declarations[cyclic]);
                    }
                }
            }
        }

        void Enter(int node)
        {
            indexes[node] = next;
            lowest[node] = next;
            next++;
            stack.Push(node);
            onStack[node] = true;
        }
    }
}
