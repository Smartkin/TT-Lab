using System;
using System.IO;
using System.Text.Json;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab
{
    /// <summary>
    /// Behaviour graph of the Xbox version, stored like the PS2 one but some of its commands take more arguments
    /// </summary>
    public class XboxBehaviourGraph : PS2BehaviourGraph, ITwinBehaviourGraph
    {
        private static AgentLabDefs XboxAgentLabDefs = null;

        public static new AgentLabDefs GetAgentLabDefs()
        {
            if (XboxAgentLabDefs == null)
            {
                JsonSerializerOptions options = new()
                {
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                XboxAgentLabDefs = JsonSerializer.Deserialize<AgentLabDefs>(EmbeddedFiles.ReadText("AgentLabDefsXbox.json"), options);
            }
            return XboxAgentLabDefs;
        }

        protected override PS2BehaviourState CreateState()
        {
            return new XboxBehaviourState();
        }

        protected override PS2BehaviourStateBody CreateStateBody()
        {
            return new XboxBehaviourStateBody();
        }
    }
}
