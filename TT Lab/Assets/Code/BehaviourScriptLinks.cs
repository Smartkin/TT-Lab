using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Twinsanity.AgentLab;

namespace TT_Lab.Assets.Code;

/// <summary>
/// Where a script names other behaviours: the child behaviour of each state (<c>state S(COM_X)</c> or <c>state S("res://...")</c>) and any
/// other string that's a URI (the old <c>GlobalObjectId = "res://..."</c>), found by the lexer so comments and the rest of the text stay out
/// </summary>
internal static class BehaviourScriptLinks
{
    /// <param name="Start">Offset of the name or of the string's opening quote</param>
    /// <param name="End">Offset past the name or the string's closing quote</param>
    /// <param name="Text">The name or the string's text</param>
    /// <param name="IsName">A name rather than a string</param>
    /// <param name="IsChild">A state's child behaviour rather than another string</param>
    public readonly record struct Reference(int Start, int End, string Text, bool IsName, bool IsChild);

    public static List<Reference> Find(string script)
    {
        var tokens = Lex(script);
        var references = new List<Reference>();
        var children = new HashSet<int>();
        for (var i = 0; i + 4 < tokens.Count; i++)
        {
            if (tokens[i].Token.Type != AgentLabToken.TokenType.State || tokens[i + 1].Token.Type != AgentLabToken.TokenType.Identifier
                || tokens[i + 2].Token.Type != AgentLabToken.TokenType.LeftParen || tokens[i + 4].Token.Type != AgentLabToken.TokenType.RightParen)
            {
                continue;
            }

            var child = tokens[i + 3];
            if (child.Token.Type is not (AgentLabToken.TokenType.Identifier or AgentLabToken.TokenType.String))
            {
                continue;
            }

            children.Add(i + 3);
            references.Add(new Reference(child.Start, child.End, child.Token.ToString() ?? string.Empty, child.Token.Type == AgentLabToken.TokenType.Identifier, true));
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var (token, start, end) = tokens[i];
            if (token.Type == AgentLabToken.TokenType.String && !children.Contains(i) && BehaviourReferences.IsUri(token.ToString() ?? string.Empty))
            {
                references.Add(new Reference(start, end, token.ToString()!, false, false));
            }
        }

        return references.OrderBy(reference => reference.Start).ToList();
    }

    /// <summary>
    /// The script with the references' text replaced
    /// </summary>
    public static string Replace(string script, IEnumerable<(Reference Reference, string Text)> replacements)
    {
        var builder = new StringBuilder(script);
        foreach (var (reference, text) in replacements.OrderByDescending(replacement => replacement.Reference.Start))
        {
            builder.Remove(reference.Start, reference.End - reference.Start).Insert(reference.Start, text);
        }

        return builder.ToString();
    }

    /// <summary>
    /// How the requester's script names the graph, its URI in quotes when asked or when its name doesn't find it from there
    /// </summary>
    public static string Write(IAsset requester, BehaviourGraph graph, bool asUri)
    {
        var reference = asUri ? graph.URI.ToString() : BehaviourReferences.ReferenceTo(requester, graph);
        return BehaviourReferences.IsUri(reference) ? Quote(reference) : reference;
    }

    public static string Quote(string uri) => uri.Contains('"') ? $"'{uri}'" : $"\"{uri}\"";

    // Every token with where it is in the text: the lexer counts lines from 1 and columns from 1, a line's \r counted in its columns
    private static List<(AgentLabToken Token, int Start, int End)> Lex(string script)
    {
        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < script.Length; i++)
        {
            if (script[i] == '\n')
            {
                lineStarts.Add(i + 1);
            }
        }

        var tokens = new List<(AgentLabToken, int, int)>();
        using var lexer = new AgentLabLexer(script);
        var last = -1;
        while (true)
        {
            var token = lexer.GetNextToken();
            if (token.Type == AgentLabToken.TokenType.Eof || token.Line < 1 || token.Line > lineStarts.Count)
            {
                break;
            }

            var start = Math.Clamp(lineStarts[token.Line - 1] + token.Column - 1, 0, script.Length);
            // A lexer stuck on a character it doesn't know gives it again
            if (start <= last && token.Type == AgentLabToken.TokenType.Identifier && string.IsNullOrEmpty(token.ToString()))
            {
                break;
            }

            last = start;
            var text = token.ToString() ?? string.Empty;
            var end = start + text.Length;
            if (token.Type == AgentLabToken.TokenType.String && start < script.Length)
            {
                end = start + 1 + text.Length;
                if (end < script.Length && script[end] == script[start])
                {
                    end++;
                }
            }

            tokens.Add((token, start, Math.Min(end, script.Length)));
        }

        return tokens;
    }
}
