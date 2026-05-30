using Twinsanity.AgentLab.AbstractSyntaxTree;

namespace Twinsanity.AgentLab.Analyzers;

public interface IAnalyzer
{
    void Analyze(IAgentLabTreeNode tree);
}