using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Animation
{
    public class AnimatedTransformation : ITwinSerializable
    {
        List<Transformation> transformValues;

        public Int32 Count { get => Transforms.Count; }
        public List<Transformation> Transforms { get => transformValues; }

        public AnimatedTransformation() : this(0)
        {
        }

        public AnimatedTransformation(UInt16 amount)
        {
            transformValues = new List<Transformation>(amount);
        }

        public Transformation this[int key]
        {
            get { return transformValues[key]; }
            set { transformValues[key] = value; }
        }

        public Int32 GetLength()
        {
            return transformValues.Sum(t => t.GetLength());
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            for (Int32 i = 0; i < transformValues.Capacity; i++)
            {
                var transformation = new Transformation();
                transformation.Read(reader, length);
                transformValues.Add(transformation);
            }
        }

        public void Write(BinaryWriter writer)
        {
            foreach (var transformation in transformValues)
            {
                transformation.Write(writer);
            }
        }
    }
}
