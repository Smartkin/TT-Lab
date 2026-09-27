using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.DynamicScenery
{
    public class TwinDynamicSceneryAnimation : ITwinSerializable
    {
        UInt32 dataPacker;
        const UInt32 FlagBits = 0x780;
        public UInt16 TotalFrames { get; set; }
        /// <summary>
        /// Bits 7 to 10 of the header, only the Xbox version sets them
        /// </summary>
        public UInt32 HeaderFlags { get; set; }

        public List<DynamicModelSettings> ModelSettings { get; set; } = new();
        public List<Transformation> StaticTransformations { get; set; } = new();
        public List<AnimatedTransformation> AnimatedTransformations { get; set; } = new();

        public Int32 GetLength()
        {
            return 6 + ModelSettings.Sum(d => d.GetLength()) + StaticTransformations.Sum(s => s.GetLength()) + AnimatedTransformations.Sum(r => r.GetLength());
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            dataPacker = reader.ReadUInt32();
            HeaderFlags = dataPacker & FlagBits;
            TotalFrames = reader.ReadUInt16();
            var modelSettings = (dataPacker & 0x7F);
            var staticTransformations = ((dataPacker >> 0x9) & 0x1FFC) / 4;
            var animatedTransformations = (dataPacker >> 0x16);

            ModelSettings.Clear();
            ModelSettings.Capacity = (Int32)modelSettings;
            for (Int32 i = 0; i < modelSettings; i++)
            {
                var setting = new DynamicModelSettings();
                setting.Read(reader, length);
                ModelSettings.Add(setting);
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
            UInt32 packer = (UInt32)ModelSettings.Count & 0x7F;
            packer |= (UInt32)(((StaticTransformations.Count * 4) & 0x1FFC) << 0x9);
            if (AnimatedTransformations.Count > 0)
            {
                packer |= (UInt32)(AnimatedTransformations[0].TransformationValues.Count << 0x16);
            }

            packer |= HeaderFlags & FlagBits;
            dataPacker = packer;
            writer.Write(packer);

            writer.Write(TotalFrames);

            foreach (var setting in ModelSettings)
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
