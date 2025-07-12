using Twinsanity.AgentLab.AbstractSyntaxTree;

namespace Twinsanity.AgentLab.Analyzers;

internal interface IAnalyzer
{
    void Analyze(IAgentLabTreeNode tree);
}