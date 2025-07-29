using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using Twinsanity.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.AssetData.Code.Behaviour
{
    public class BehaviourCommandsSequenceData : AbstractAssetData
    {
        
        private static string _newSequenceTemplate = """
                                                    // IT IS CURRENTLY NOT RECOMMENDED TO CREATE MORE BEHAVIOUR LIBRARIES
                                                    // 1. At a time only 8 can be loaded into game's memory
                                                    // 2. These behaviours are highly unexplored in how they actually function
                                                    // 3. These behaviours can ONLY be used for Pickup and Projectile objects
                                                    // If you wish to continue do remember to look at retail's global index usage to not collide with them
                                                    // otherwise this most likely will result in a game crash or undefined behaviour
                                                    [InstanceType(Pickup)] // Pickup or Projectile
                                                    [GlobalIndex(5)] // From 0 to 7
                                                    library NewBehaviourLibrary {
                                                         ClearTrail(); // Call to any one and only one action is required
                                                    }
                                                    """;
        
        public BehaviourCommandsSequenceData()
        {
            Code = _newSequenceTemplate[..];
        }

        public BehaviourCommandsSequenceData(ITwinBehaviourCommandsSequence codeModel) : this()
        {
            SetTwinItem(codeModel);
        }

        public String Code { get; set; }

        protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
        {
            using FileStream fs = new(dataPath, FileMode.Create, FileAccess.Write);
            using BinaryWriter writer = new(fs);
            writer.Write(Code.ToCharArray());
        }

        protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
        {
            using FileStream fs = new(dataPath, FileMode.Open, FileAccess.Read);
            using StreamReader reader = new(fs);
            Code = reader.ReadToEnd();
        }

        protected override void Dispose(Boolean disposing)
        {
            Code = "";
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            var codeModel = GetTwinItem<ITwinBehaviourCommandsSequence>();
            Code = AgentLabDecompiler.Decompile(codeModel);
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms);
            writer.Write(Code);
            writer.Flush();

            ms.Position = 0;
            return factory.GenerateBehaviourCommandsSequence(ms);
        }
    }
}
