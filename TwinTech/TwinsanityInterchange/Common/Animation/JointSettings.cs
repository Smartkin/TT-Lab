using System;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace Twinsanity.TwinsanityInterchange.Common.Animation
{
    public class JointSettings : ITwinSerializable
    {
        UInt16 flags;
        public Boolean IndependentScaling { get; set; }
        public Boolean UseAdditionalRotation { get; set; }

        UInt16 transformationChoice;
        public TransformType TranslateX { get; set; }
        public TransformType TranslateY { get; set; }
        public TransformType TranslateZ { get; set; }
        public TransformType RotateX { get; set; }
        public TransformType RotateY { get; set; }
        public TransformType RotateZ { get; set; }
        public TransformType ScaleX { get; set; }
        public TransformType ScaleY { get; set; }
        public TransformType ScaleZ { get; set; }

        public UInt16 TransformationIndex { get; set; }
        public UInt16 AnimationTransformationIndex { get; set; }

        /// <summary>
        /// Amount of transformation channels of every joint, the game keeps it in each joint's and in the animation's flags
        /// </summary>
        public const Byte ChannelsAmount = 9;

        public Int32 GetLength()
        {
            return 4 * 2;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            flags = reader.ReadUInt16();
            {
                IndependentScaling = ((flags >> 0xD) & 0x1) != 0;
                UseAdditionalRotation = ((flags >> 0xC) & 0x1) != 0;
            }
            transformationChoice = reader.ReadUInt16();
            {
                TranslateX = (TransformType)(transformationChoice & 0x1);
                TranslateY = (TransformType)((transformationChoice & 0x2) >> 0x1);
                TranslateZ = (TransformType)((transformationChoice & 0x4) >> 0x2);
                RotateX = (TransformType)((transformationChoice & 0x8) >> 0x3);
                RotateY = (TransformType)((transformationChoice & 0x10) >> 0x4);
                RotateZ = (TransformType)((transformationChoice & 0x20) >> 0x5);
                ScaleX = (TransformType)((transformationChoice & 0x40) >> 0x6);
                ScaleY = (TransformType)((transformationChoice & 0x80) >> 0x7);
                ScaleZ = (TransformType)((transformationChoice & 0x100) >> 0x8);
            }
            TransformationIndex = reader.ReadUInt16();
            AnimationTransformationIndex = reader.ReadUInt16();
        }

        public void Write(BinaryWriter writer)
        {
            var channels = new[] { TranslateX, TranslateY, TranslateZ, RotateX, RotateY, RotateZ, ScaleX, ScaleY, ScaleZ };
            UInt16 newFlags = (UInt16)(ChannelsAmount << 0x8);
            var hasUseParentJointScale = IndependentScaling ? 1 : 0;
            newFlags |= (UInt16)(hasUseParentJointScale << 0xD);
            var hasUseAdditionalRotation = UseAdditionalRotation ? 1 : 0;
            newFlags |= (UInt16)(hasUseAdditionalRotation << 0xC);
            newFlags |= CountChannels(channels);
            flags = newFlags;
            writer.Write(flags);

            UInt16 newTransformationChoice = (UInt16)TranslateX;
            newTransformationChoice |= (UInt16)((UInt16)TranslateY << 0x1);
            newTransformationChoice |= (UInt16)((UInt16)TranslateZ << 0x2);
            newTransformationChoice |= (UInt16)((UInt16)RotateX << 0x3);
            newTransformationChoice |= (UInt16)((UInt16)RotateY << 0x4);
            newTransformationChoice |= (UInt16)((UInt16)RotateZ << 0x5);
            newTransformationChoice |= (UInt16)((UInt16)ScaleX << 0x6);
            newTransformationChoice |= (UInt16)((UInt16)ScaleY << 0x7);
            newTransformationChoice |= (UInt16)((UInt16)ScaleZ << 0x8);
            transformationChoice = newTransformationChoice;
            writer.Write(transformationChoice);

            writer.Write(TransformationIndex);
            writer.Write(AnimationTransformationIndex);
        }

        /// <summary>
        /// The low byte of a joint's flags, the amount of animated channels in the upper nibble and of static ones in the lower
        /// </summary>
        internal static UInt16 CountChannels(TransformType[] channels)
        {
            var animated = channels.Count(channel => channel == TransformType.Animated);
            return (UInt16)((animated << 0x4) | (channels.Length - animated));
        }
    }
}
