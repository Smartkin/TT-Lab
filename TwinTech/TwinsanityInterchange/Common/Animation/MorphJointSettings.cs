using System;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace Twinsanity.TwinsanityInterchange.Common.Animation
{
    public class MorphJointSettings : ITwinSerializable
    {
        UInt16 flags;
        public Byte FacialShapesAmount { get; set; }
        public Boolean UnusedFlag { get; set; }
        public Boolean UseAdditionalRotation { get; set; }
        public TransformType[] AnimationMorph { get; set; }
        public UInt16 TransformationIndex { get; set; }
        public UInt16 AnimationTransformationIndex { get; set; }

        public MorphJointSettings()
        {
            AnimationMorph = new TransformType[15];
        }

        public void Compile()
        {
            return;
        }

        public Int32 GetLength()
        {
            return 4 * 2;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            flags = reader.ReadUInt16();
            {
                FacialShapesAmount = (Byte)((flags >> 0x8) & 0xF);
                UnusedFlag = ((flags >> 0xD) & 0x1) != 0;
                UseAdditionalRotation = ((flags >> 0xC) & 0x1) != 0;
            }
            var transformationChoice = reader.ReadUInt16();
            AnimationMorph = new TransformType[15];
            for (var i = 0; i < FacialShapesAmount; ++i)
            {
                AnimationMorph[i] = (TransformType)(transformationChoice & 0x1);
                transformationChoice >>= 1;
            }
            TransformationIndex = reader.ReadUInt16();
            AnimationTransformationIndex = reader.ReadUInt16();
        }

        public void Write(BinaryWriter writer)
        {
            UInt16 newFlags = (UInt16)(FacialShapesAmount << 0x8);
            var hasUnusedFlag = UnusedFlag ? 1 : 0;
            newFlags |= (UInt16)(hasUnusedFlag << 0xD);
            var hasUseAdditionalRotation = UseAdditionalRotation ? 1 : 0;
            newFlags |= (UInt16)(hasUseAdditionalRotation << 0xC);
            newFlags |= JointSettings.CountChannels(AnimationMorph.Take(FacialShapesAmount).ToArray());
            flags = newFlags;
            writer.Write(flags);

            UInt16 newTransformationChoice = (UInt16)AnimationMorph[0];
            for (var i = 1; i < FacialShapesAmount; ++i)
            {
                newTransformationChoice |= (UInt16)((UInt16)AnimationMorph[i] << i);
            }
            writer.Write(newTransformationChoice);

            writer.Write(TransformationIndex);
            writer.Write(AnimationTransformationIndex);
        }
    }
}
