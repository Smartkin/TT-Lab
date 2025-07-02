using System;
using System.IO;
using System.Reflection;
using Twinsanity.AgentLab.AbstractSyntaxTree;

namespace Twinsanity.AgentLab.SymbolTable;

public class AgentLabSymbolTableBuilder
{
    private readonly AgentLabSymbolTableNodeVisitor _visitor = new();

    public AgentLabSymbolTableBuilder(IAgentLabTreeNode tree, string actionDefinitionFile = "")
    {
        {
            string codeBase = Assembly.GetExecutingAssembly().Location;
            UriBuilder uri = new(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            using FileStream fs = new(Path.Combine(Path.GetDirectoryName(path), @"AgentLab\ConditionDefinitions.lab"), FileMode.Open, FileAccess.Read);
            using StreamReader sr = new StreamReader(fs);
            using StringReader reader = new StringReader(sr.ReadToEnd());
            var parser = new AgentLabParser(new AgentLabLexer(reader));
            _visitor.Visit(parser.Parse());
        }

        if (actionDefinitionFile != "")
        {
            string codeBase = Assembly.GetExecutingAssembly().Location;
            UriBuilder uri = new(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            using FileStream fs = new(Path.Combine(Path.GetDirectoryName(path), @$"AgentLab\{actionDefinitionFile}"), FileMode.Open, FileAccess.Read);
            using StreamReader sr = new StreamReader(fs);
            using StringReader reader = new StringReader(sr.ReadToEnd());
            var parser = new AgentLabParser(new AgentLabLexer(reader));
            _visitor.Visit(parser.Parse());
        }
        
        _visitor.Visit(tree);
    }
}