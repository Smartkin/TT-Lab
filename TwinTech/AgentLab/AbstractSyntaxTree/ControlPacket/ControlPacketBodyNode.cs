using System.Collections.Generic;

namespace Twinsanity.AgentLab.AbstractSyntaxTree.ControlPacket;

internal class ControlPacketBodyNode : IAgentLabTreeNode
{
    public IEnumerable<ControlPacketSettingNode> SettingsNodes { get; } // Can be null
    public IEnumerable<ControlPacketDataNode> DataNodes { get; } // Can be null
    
    public ControlPacketBodyNode(IEnumerable<ControlPacketSettingNode> settingsNodes, IEnumerable<ControlPacketDataNode> dataNodes)
    {
        DataNodes = dataNodes;
        SettingsNodes = settingsNodes;
    }
}