using Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

namespace Twinsanity.AgentLab.AbstractSyntaxTree;

internal class BehaviourBodyNode : IAgentLabTreeNode
{
    public ConstDeclarationListNode Consts { get; }
    public StateListNode States { get; }
    public ControlPacketListNode ControlPackets { get; }
    public StarterNode Starter { get; } // Can be null
    
    public BehaviourBodyNode(ConstDeclarationListNode consts, StateListNode states, ControlPacketListNode controlPackets, StarterNode starter)
    {
        Starter = starter;
        Consts = consts;
        States = states;
        ControlPackets = controlPackets;
    }
}