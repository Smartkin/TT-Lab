using System;
using System.Collections.Generic;
using System.Linq;

namespace TT_Lab.Project.Repair;

/// <summary>
/// A graph's script as the decompiler writes it, its starter apart: the starter's priority (<c>[Priority(n)]</c>, the graph's own without a
/// starter), the graph's own priority next to a starter's (<c>[GraphPriority(n)]</c>), the starter's block after an empty line under the
/// behaviour's line, and the rest with the line ends of no system
/// </summary>
internal sealed record BehaviourScript(string? Priority, string? GraphPriority, string? Starter, string Rest)
{
    private const string StarterLine = "starter {";

    public static BehaviourScript? Parse(string script)
    {
        var lines = script.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var behaviour = lines.FindIndex(line => line.TrimStart().StartsWith("behaviour ", StringComparison.Ordinal));
        if (behaviour < 0)
        {
            return null;
        }

        string? priority = null;
        string? graphPriority = null;
        var rest = new List<string>();
        foreach (var line in lines[..behaviour])
        {
            var attribute = line.Trim();
            if (attribute.StartsWith("[Priority(", StringComparison.Ordinal))
            {
                priority = attribute;
            }
            else if (attribute.StartsWith("[GraphPriority(", StringComparison.Ordinal))
            {
                graphPriority = attribute;
            }
            else
            {
                rest.Add(line);
            }
        }

        rest.Add(lines[behaviour]);
        var next = behaviour + 1;
        string? starter = null;
        if (next + 1 < lines.Count && lines[next].Length == 0 && lines[next + 1].TrimStart() == StarterLine)
        {
            var indent = lines[next + 1][..^StarterLine.Length];
            var end = lines.FindIndex(next + 2, line => line == indent + "}");
            if (end < 0)
            {
                return null;
            }

            starter = string.Join('\n', lines[(next + 1)..(end + 1)]);
            next = end + 1;
        }

        rest.AddRange(lines[next..]);
        return new BehaviourScript(priority, graphPriority, starter, string.Join('\n', rest));
    }

    public bool SameStarterAs(BehaviourScript other) => Starter == other.Starter && Priority == other.Priority && GraphPriority == other.GraphPriority;

    // The graph's own priority the way the decompiler writes it next to a starter: none when it's 0
    public string? OwnPriorityLine()
    {
        if (Starter != null)
        {
            return GraphPriority;
        }

        var own = Priority?["[Priority(".Length..^")]".Length];
        return own == null || own == "0" ? null : $"[GraphPriority({own})]";
    }
}
