using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Twinsanity.AgentLab.AbstractSyntaxTree;

namespace Twinsanity.AgentLab;

/// <summary>
/// AgentLab's behaviour interpreter
/// </summary>
public class AgentLabInterpreter : IDisposable
{
    private readonly AgentLabParser _parser;
    private readonly AgentLabInterpreterNodeVisitor _nodeVisitor = new();

    /// <summary>
    /// Construct from pure string
    /// </summary>
    /// <param name="text">Behaviour text to interpret</param>
    public AgentLabInterpreter(string text)
    {
        _parser = new AgentLabParser(new AgentLabLexer(text));
    }

    /// <summary>
    /// Construct from text stream
    /// </summary>
    /// <param name="reader">Stream of characters to interpret</param>
    public AgentLabInterpreter(StringReader reader)
    {
        _parser = new AgentLabParser(new AgentLabLexer(reader));
    }

    /// <summary>
    /// Interpret the given code and get the result
    /// </summary>
    /// <returns>Result of the interpretation</returns>
    public object Interpret()
    {
        var tree = _parser.Parse();
        var result = _nodeVisitor.Visit(tree);
        Console.WriteLine(result);
        return result;
    }

    /// <summary>
    /// Dispose of unmanaged objects
    /// </summary>
    public void Dispose()
    {
        _parser?.Dispose();
    }
}