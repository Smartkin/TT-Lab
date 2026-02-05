using System;
using System.IO;
using System.Reflection;
using Twinsanity.AgentLab.AbstractSyntaxTree;

namespace Twinsanity.AgentLab.SymbolTable;

public class AgentLabSymbolTableBuilder
{
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
        string path = AppContext.BaseDirectory;
        using FileStream fs = new(Path.Combine(Path.GetDirectoryName(path), $"AgentLab{Path.DirectorySeparatorChar}ConditionDefinitions.lab"), FileMode.Open, FileAccess.Read);
        using StreamReader sr = new StreamReader(fs);
        using StringReader reader = new StringReader(sr.ReadToEnd());
        var parser = new AgentLabParser(new AgentLabLexer(reader));
        _visitor.Visit(parser.Parse());
        
        return this;
    }

    public AgentLabSymbolTableBuilder BuildActions(string actionDefinitionFile = "")
    {
        if (actionDefinitionFile != "")
        {
            string path = AppContext.BaseDirectory;
            using FileStream fs = new(Path.Combine(Path.GetDirectoryName(path), $"AgentLab{Path.DirectorySeparatorChar}{actionDefinitionFile}"), FileMode.Open, FileAccess.Read);
            using StreamReader sr = new StreamReader(fs);
            using StringReader reader = new StringReader(sr.ReadToEnd());
            var parser = new AgentLabParser(new AgentLabLexer(reader));
            _visitor.Visit(parser.Parse());
        }

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