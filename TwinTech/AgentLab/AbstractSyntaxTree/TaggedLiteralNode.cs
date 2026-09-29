namespace Twinsanity.AgentLab.AbstractSyntaxTree;

/// <summary>
/// A tagged argument written as a call: Prop(index), Raw(bits), Float(x), Int(x) or Angle(degrees)
/// </summary>
internal class TaggedLiteralNode : IAgentLabTreeNode
{
    public AgentLabToken Token { get; }
    public string Name { get; }
    public IAgentLabTreeNode Value { get; }

    public TaggedLiteralNode(AgentLabToken token, IAgentLabTreeNode value)
    {
        Token = token;
        Name = token.GetValue<string>();
        Value = value;
    }

    public static readonly string[] Names = { "Prop", "Raw", "Float", "Int", "Angle" };
}
