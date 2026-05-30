using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace Twinsanity.TwinsanityInterchange.Common.DynamicScenery
{
    public class DynamicModelSettings : ITwinSerializable
    {
        UInt16 flags;
        public UInt16 UnknownValue { get; set; }
        public UInt16 UnusedRotationRelatedParameter { get; set; }

        UInt16 transformationChoice;
        public TransformType TranslateX { get; set; }
        public TransformType TranslateY { get; set; }
        public TransformType TranslateZ { get; set; }
        public TransformType RotateX { get; set; }
        public TransformType RotateY { get; set; }
        public TransformType RotateZ { get; set; }
        public TransformType RotateW { get; set; }

        public UInt16 StaticTransformationIndex { get; set; }
        public UInt16 AnimationTransformationIndex { get; set; }

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
                UnknownValue = (UInt16)(flags & 0xFF);
                UnusedRotationRelatedParameter = (UInt16)((flags >> 0x8) & 0xF);
            }
            transformationChoice = reader.ReadUInt16();
            {
                TranslateX = (TransformType)(transformationChoice & 0x1);
                TranslateY = (TransformType)((transformationChoice & 0x2) >> 0x1);
                TranslateZ = (TransformType)((transformationChoice & 0x4) >> 0x2);
                RotateX = (TransformType)((transformationChoice & 0x8) >> 0x3);
                RotateY = (TransformType)((transformationChoice & 0x10) >> 0x4);
                RotateZ = (TransformType)((transformationChoice & 0x20) >> 0x5);
                RotateW = (TransformType)((transformationChoice & 0x40) >> 0x6);
            }
            StaticTransformationIndex = reader.ReadUInt16();
            AnimationTransformationIndex = reader.ReadUInt16();
        }

        public void Write(BinaryWriter writer)
        {
            flags = UnknownValue;
            flags |= (UInt16)((UnusedRotationRelatedParameter & 0xF) << 0x8);
            writer.Write(flags);

            UInt16 newTransformationChoice = (UInt16)TranslateX;
            newTransformationChoice |= (UInt16)((UInt16)TranslateY << 0x1);
            newTransformationChoice |= (UInt16)((UInt16)TranslateZ << 0x2);
            newTransformationChoice |= (UInt16)((UInt16)RotateX << 0x3);
            newTransformationChoice |= (UInt16)((UInt16)RotateY << 0x4);
            newTransformationChoice |= (UInt16)((UInt16)RotateZ << 0x5);
            newTransformationChoice |= (UInt16)((UInt16)RotateW << 0x6);
            transformationChoice = newTransformationChoice;
            writer.Write(transformationChoice);

            writer.Write(StaticTransformationIndex);
            writer.Write(AnimationTransformationIndex);
        }
    }
}
