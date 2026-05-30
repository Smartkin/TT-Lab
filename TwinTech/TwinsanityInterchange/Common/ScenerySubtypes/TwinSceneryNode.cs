using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes
{
    public class TwinSceneryNode : TwinSceneryBaseType
    {
        public ITwinScenery.SceneryType[] SceneryTypes;

        public TwinSceneryNode()
        {
            SceneryTypes = new ITwinScenery.SceneryType[8];
        }

        public override Int32 GetLength()
        {
            return base.GetLength() + SceneryTypes.Length * 4;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            base.Read(reader, length);
            for (var i = 0; i < SceneryTypes.Length; ++i)
            {
                SceneryTypes[i] = (ITwinScenery.SceneryType)reader.ReadInt32();
            }
        }

        public virtual void Read(BinaryReader reader, Int32 length, IList<TwinSceneryBaseType> sceneries)
        {
            Read(reader, length);
            foreach (var sceneryType in SceneryTypes)
            {
                switch (sceneryType)
                {
                    // This is pretty tough since these are the only 2 types besides the Root type.
                    // Of course a Dictionary Int -> Type could work, but I decided to opt for simple
                    // if checks since I don't see it worth the effort to make a mess out of Read
                    // method calls.
                    //                                                                      ~Smartkin
                    case ITwinScenery.SceneryType.Node:
                    {
                        var newSc = new TwinSceneryNode();
                        sceneries.Add(newSc);
                        newSc.Read(reader, length, sceneries);
                        break;
                    }
                    case ITwinScenery.SceneryType.Leaf:
                    {
                        var newSc = new TwinSceneryLeaf();
                        sceneries.Add(newSc);
                        newSc.Read(reader, length);
                        break;
                    }
                }
            }
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            foreach (var type in SceneryTypes)
            {
                writer.Write((Int32)type);
            }
        }

        public override ITwinScenery.SceneryType GetObjectIndex()
        {
            return ITwinScenery.SceneryType.Node;
        }
    }
}
