using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes
{
    public class TwinSceneryRoot : TwinSceneryNode
    {
        /// <summary>
        /// The tools' depth of the tree (levels under the root, 5 on most retail chunks), handed down the search for the node
        /// holding a box (FUN_001ebbf8) which never tests it
        /// </summary>
        public UInt32 TreeDepth;

        public override Int32 GetLength()
        {
            return base.GetLength() + 4;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            TreeDepth = reader.ReadUInt32();
            base.Read(reader, length);
        }

        public override void Read(BinaryReader reader, Int32 length, IList<TwinSceneryBaseType> sceneries)
        {
            base.Read(reader, length, sceneries);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(TreeDepth);
            base.Write(writer);
        }

        public override ITwinScenery.SceneryType GetObjectIndex()
        {
            return ITwinScenery.SceneryType.Root;
        }
    }
}
