using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using Twinsanity.AgentLab.AbstractSyntaxTree;

namespace Twinsanity.AgentLab.SymbolTable;

public class AgentLabSymbolTableBuilder
{
    // Definition files never change while running and parsing them took most of every compile, their trees are only read so they can be shared
    private static readonly ConcurrentDictionary<string, IAgentLabTreeNode> ParsedDefinitions = new();

    private readonly AgentLabSymbolTableNodeVisitor _visitor = new();

    public AgentLabSymbolTableBuilder()
    {
        
    }

    internal AgentLabSymbolTable GetSymbolTable()
    {
        return _visitor.SymbolTable;
    }

    public AgentLabSymbolTableBuilder BuildBuiltInTypes()
    {
        _visitor.SymbolTable.InitBuiltInTypes();
        
        return this;
    }

    public AgentLabSymbolTableBuilder BuildConditions()
    {
        _visitor.Visit(GetDefinitions("ConditionDefinitions.lab"));
        
        return this;
    }

    public AgentLabSymbolTableBuilder BuildActions(string actionDefinitionFile = "")
    {
        if (actionDefinitionFile != "")
        {
            _visitor.Visit(GetDefinitions(actionDefinitionFile));
        }

        return this;
    }

    private static IAgentLabTreeNode GetDefinitions(string definitionFile)
    {
        var path = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "AgentLab", definitionFile);
        return ParsedDefinitions.GetOrAdd(path, definitionsPath =>
        {
            using var reader = new StringReader(File.ReadAllText(definitionsPath));
            var parser = new AgentLabParser(new AgentLabLexer(reader));
            return parser.Parse();
        });
    }

    /// <summary>
    /// Makes the states' behaviour references get checked against what exists
    /// </summary>
    public AgentLabSymbolTableBuilder CheckBehaviours(Func<string, bool> behaviourExists)
    {
        _visitor.BehaviourExists = behaviourExists;
        return this;
    }

    public AgentLabSymbolTableBuilder BuildFromAst(IAgentLabTreeNode tree)
    {
        _visitor.Visit(tree);

        return this;
    }

    public override String ToString()
    {
        return _visitor.SymbolTable.ToString();
    }
}