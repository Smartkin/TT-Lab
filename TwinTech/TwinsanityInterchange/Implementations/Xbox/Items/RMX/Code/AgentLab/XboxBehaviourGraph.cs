using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
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
                string path = Assembly.GetExecutingAssembly().Location;
                using FileStream stream = new(Path.Combine(Path.GetDirectoryName(path), "AgentLabDefsXbox.json"), FileMode.Open, FileAccess.Read);
                using StreamReader reader = new(stream);
                JsonSerializerOptions options = new()
                {
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                XboxAgentLabDefs = JsonSerializer.Deserialize<AgentLabDefs>(reader.ReadToEnd(), options);
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
