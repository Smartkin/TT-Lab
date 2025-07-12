using System;
using System.Collections.Generic;
using Twinsanity.AgentLab.AbstractSyntaxTree;
using Twinsanity.AgentLab.AgentLabObjectDescs;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.AgentLab.Resolvers.Interfaces.Compiler;
using Twinsanity.AgentLab.SymbolTable;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.AgentLab;

/// <summary>
/// Compiles AgentLab string script into a bytecode AgentLab object
/// </summary>
public static class AgentLabCompiler
{
    /// <summary>
    /// Compiler result status
    /// </summary>
    public class CompilerStatus
    {
        /// <summary>
        /// Any errors occured during compilation
        /// </summary>
        public bool IsError { get; internal set; }
        /// <summary>
        /// Error message
        /// </summary>
        public string Message { get; internal set; }
    }

    /// <summary>
    /// Compilation results
    /// </summary>
    public class CompilerResult
    {
        private Dictionary<Type, ITwinAgentLab> AgentLabObjects { get; } = new();
        
        /// <summary>
        /// Status of the compiler at the end of compilation
        /// </summary>
        public CompilerStatus CompilerStatus { get; internal set; } = new();

        public bool Contains<T>()
        {
            return AgentLabObjects.ContainsKey(typeof(T));
        }

        public T Get<T>() where T : ITwinAgentLab
        {
            return (T)AgentLabObjects[typeof(T)];
        }
        
        internal void Add<T>(T agentLabObject) where T : ITwinAgentLab
        {
            AgentLabObjects.Add(typeof(T), agentLabObject);
        }
    }

    /// <summary>
    /// Different compiler switches
    /// </summary>
    public class CompilerOptions
    {
        /// <summary>
        /// How resource references should get resolved
        /// </summary>
        public ICompilerResolver Resolver { get; set; }
        /// <summary>
        /// How a command should be created
        /// </summary>
        public CommandDesc Command { get; set; }
        /// <summary>
        /// How a command pack should be created
        /// </summary>
        public CommandPackDesc CommandPack { get; set; }
        /// <summary>
        /// How a commands sequence should be created
        /// </summary>
        public CommandsSequenceDesc CommandsSequence { get; set; }
        /// <summary>
        /// How the graph/behaviour should be created
        /// </summary>
        public GraphDesc Graph { get; set; }
        /// <summary>
        /// How behaviour's state should be created
        /// </summary>
        public StateDesc State { get; set; }
        /// <summary>
        /// How state's body should be created
        /// </summary>
        public StateBodyDesc StateBody { get; set; }
        /// <summary>
        /// Name of the AgentLab file that contains action definitions
        /// </summary>
        public string ActionDefinitionsFile { get; set; }
    }
    
    /// <summary>
    /// <inheritdoc cref="AgentLabCompiler"/>
    /// </summary>
    /// <param name="script">AgentLab code</param>
    /// <param name="resolver">Object reference resolvers to game IDs depending on the script type that is being compiled</param>
    /// <param name="options">Compiler settings</param>
    /// <returns>AgentLab object in bytecode</returns>
    public static CompilerResult Compile(string script, CompilerOptions options)
    {
        var result = new CompilerResult();
        // try
        // {
            var lexer = new AgentLabLexer(script);
            var parser = new AgentLabParser(lexer);
            // Lexical analysis
            var tree = parser.Parse();
            var symbolTable = new AgentLabSymbolTableBuilder();
            // Semantical analysis
            symbolTable.BuildBuiltInTypes().BuildActions(options.ActionDefinitionsFile).BuildConditions().BuildFromAst(tree);
            var visitor = new AgentLabCompilerNodeVisitor(result, options, symbolTable.GetSymbolTable());
            visitor.Visit(tree);
        // }
        // catch (Exception ex)
        // {
        //     result.CompilerStatus.IsError = true;
        //     result.CompilerStatus.Message = ex.Message;
        //     if (ex.StackTrace != null)
        //     {
        //         result.CompilerStatus.Message += "\n" + ex.StackTrace;
        //     }
        // }

        return result;
    }
}