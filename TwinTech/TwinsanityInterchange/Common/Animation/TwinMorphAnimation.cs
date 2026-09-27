using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Animation
{
    public class TwinMorphAnimation : ITwinSerializable
    {
        UInt32 dataPacker;
        public UInt16 TotalFrames { get; set; }

        public List<MorphJointSettings> JointSettings { get; set; } = new();
        public List<Transformation> StaticTransformations { get; set; } = new();
        public List<AnimatedTransformation> AnimatedTransformations { get; set; } = new();

        public Int32 GetLength()
        {
            return 6 + JointSettings.Sum(d => d.GetLength()) + StaticTransformations.Count * 2 + AnimatedTransformations.Sum(r => r.GetLength());
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            dataPacker = reader.ReadUInt32();
            TotalFrames = reader.ReadUInt16();
            var jointSettings = (dataPacker & 0x7F);
            var staticTransformations = ((dataPacker >> 0xA) & 0xFFE) / 2;
            var animatedTransformations = (dataPacker >> 0x16);

            JointSettings.Clear();
            JointSettings.Capacity = (Int32)jointSettings;
            for (Int32 i = 0; i < jointSettings; i++)
            {
                var setting = new MorphJointSettings();
                setting.Read(reader, length);
                JointSettings.Add(setting);
            }

            StaticTransformations.Clear();
            StaticTransformations.Capacity = (Int32)staticTransformations;
            for (Int32 i = 0; i < staticTransformations; i++)
            {
                var staticTransformation = new Transformation();
                staticTransformation.Read(reader, length);
                StaticTransformations.Add(staticTransformation);
            }

            AnimatedTransformations.Clear();
            AnimatedTransformations.Capacity = TotalFrames;
            for (Int32 i = 0; i < TotalFrames; i++)
            {
                var animatedTransformation = new AnimatedTransformation((UInt16)animatedTransformations);
                animatedTransformation.Read(reader, length);
                AnimatedTransformations.Add(animatedTransformation);
            }
        }

        public void Write(BinaryWriter writer)
        {
            UInt32 packer = (UInt32)JointSettings.Count & 0x7F;
            packer |= (UInt32)(((StaticTransformations.Count * 2) & 0xFFE) << 0xA);
            if (AnimatedTransformations.Count > 0)
            {
                packer |= (UInt32)(AnimatedTransformations[0].Count << 0x16);
            }
            // Amount of each joint's shapes
            packer |= (UInt32)((JointSettings.Count == 0 ? 0 : JointSettings[0].FacialShapesAmount) << 0x7);

            dataPacker = packer;
            writer.Write(packer);

            writer.Write(TotalFrames);

            foreach (var setting in JointSettings)
            {
                setting.Write(writer);
            }

            foreach (var staticTransformation in StaticTransformations)
            {
                staticTransformation.Write(writer);
            }

            foreach (var animatedTransformation in AnimatedTransformations)
            {
                animatedTransformation.Write(writer);
            }
        }
    }
}
