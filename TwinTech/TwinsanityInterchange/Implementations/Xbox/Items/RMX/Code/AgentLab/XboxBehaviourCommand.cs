using System;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab
{
    public class XboxBehaviourCommand : PS2BehaviourCommand, ITwinBehaviourCommand
    {
        static UInt32[] xboxCommandSizes = null;

        public override AgentLabVersion Version => AgentLabVersion.Xbox;

        protected override AgentLabDefs Defs => XboxBehaviourGraph.GetAgentLabDefs();

        protected override UInt32 GetSize(UInt16 index)
        {
            xboxCommandSizes ??= BuildSizeMap(XboxBehaviourGraph.GetAgentLabDefs());
            return index < xboxCommandSizes.Length ? xboxCommandSizes[index] : 0;
        }

        protected override PS2BehaviourCommand CreateNext()
        {
            return new XboxBehaviourCommand();
        }
    }
}
