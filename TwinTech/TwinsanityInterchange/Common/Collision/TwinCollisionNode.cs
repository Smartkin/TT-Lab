using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Collision
{
    /// <summary>
    /// A node of the collision's tree (the game's CollisionNode, the tools' collision trigger): its box, the index of its first child in the
    /// min's W and of its second in the max's. A leaf has the bitwise not of its group in both
    /// </summary>
    public class TwinCollisionNode : ITwinSerializable
    {
        public Vector3 Min;
        public Int32 FirstChild;
        public Vector3 Max;
        public Int32 SecondChild;

        public TwinCollisionNode()
        {
            Min = new Vector3();
            Max = new Vector3();
        }

        public Int32 GetLength()
        {
            return Constants.SIZE_VECTOR3 + Constants.SIZE_VECTOR3 + 8;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Min.Read(reader, Constants.SIZE_VECTOR3);
            FirstChild = reader.ReadInt32();
            Max.Read(reader, Constants.SIZE_VECTOR3);
            SecondChild = reader.ReadInt32();
        }

        public void Write(BinaryWriter writer)
        {
            Min.Write(writer);
            writer.Write(FirstChild);
            Max.Write(writer);
            writer.Write(SecondChild);
        }
    }
}
