using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2
{
    public class PS2AnyDynamicScenery : BaseTwinItem, ITwinDynamicScenery
    {

        /// <summary>
        /// The version word of every retail dynamic scenery
        /// </summary>
        public const Int32 GameVersion = 0x10009;
        /// <summary>
        /// The data's version word, <see cref="GameVersion"/> everywhere, read into a field nothing reads
        /// </summary>
        public Int32 Version { get; set; }
        public List<TwinDynamicSceneryModel> DynamicModels { get; set; }

        public PS2AnyDynamicScenery()
        {
            DynamicModels = new List<TwinDynamicSceneryModel>();
        }

        public override Int32 GetLength()
        {
            return 6 + DynamicModels.Sum(d => d.GetLength());
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            Version = reader.ReadInt32();
            var models = reader.ReadInt16();
            for (var i = 0; i < models; ++i)
            {
                var m = new TwinDynamicSceneryModel();
                DynamicModels.Add(m);
                m.Read(reader, length);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write((Int16)DynamicModels.Count);
            foreach (var model in DynamicModels)
            {
                model.Write(writer);
            }
        }

        public override String GetName()
        {
            return $"Dynamic scenery {id:X}";
        }
    }
}
