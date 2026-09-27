using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class VertexJointInfo : ITwinSerializable
    {
        private const UInt32 JointIndexMask = 0x1FF;
        private const UInt32 AdcBit = 0x8000;

        public Single Weight1;
        public Single Weight2;
        public Single Weight3;
        public Int32 JointIndex1;
        public Int32 JointIndex2;
        public Int32 JointIndex3;
        public Boolean Connection;
        /// <summary>
        /// How many of the joints influence the vertex, 0 counts the joints with a weight
        /// </summary>
        public Int32 WeightsAmount;

        public Int32 GetLength()
        {
            return Constants.SIZE_VECTOR4;
        }

        public Int32 GetJointConnectionsAmount()
        {
            if (WeightsAmount > 0)
            {
                return WeightsAmount;
            }

            if (Weight2 == 0)
            {
                return 1;
            }

            return Weight3 == 0 ? 2 : 3;
        }

        /// <summary>
        /// Reads the joints the way PS2 skins pack them: the weights are floats with the joint index times 4 in their lowest 9 bits
        /// and the fourth word holds the amount of weights and the ADC bit
        /// </summary>
        public static VertexJointInfo FromPackedWords(UInt32[] words)
        {
            var amount = (Int32)(words[3] & 0xFF);
            var result = new VertexJointInfo
            {
                WeightsAmount = amount,
                Connection = (words[3] & AdcBit) == 0
            };
            if (amount > 0)
            {
                result.JointIndex1 = (Int32)((words[0] & JointIndexMask) / 4);
                result.Weight1 = BitConverter.UInt32BitsToSingle(words[0] & ~JointIndexMask);
            }

            if (amount > 1)
            {
                result.JointIndex2 = (Int32)((words[1] & JointIndexMask) / 4);
                result.Weight2 = BitConverter.UInt32BitsToSingle(words[1] & ~JointIndexMask);
            }

            if (amount > 2)
            {
                result.JointIndex3 = (Int32)((words[2] & JointIndexMask) / 4);
                result.Weight3 = BitConverter.UInt32BitsToSingle(words[2] & ~JointIndexMask);
            }

            return result;
        }

        /// <summary>
        /// Packs the joints the way PS2 skins read them, the unused weights are left zero
        /// </summary>
        public UInt32[] GetPackedWords()
        {
            var amount = GetJointConnectionsAmount();
            var words = new UInt32[4];
            words[0] = PackWeight(Weight1, JointIndex1);
            if (amount > 1)
            {
                words[1] = PackWeight(Weight2, JointIndex2);
            }

            if (amount > 2)
            {
                words[2] = PackWeight(Weight3, JointIndex3);
            }

            words[3] = (UInt32)amount | (Connection ? 0 : AdcBit);
            return words;
        }

        private static UInt32 PackWeight(Single weight, Int32 jointIndex)
        {
            return BitConverter.SingleToUInt32Bits(weight) & ~JointIndexMask | (UInt32)(jointIndex * 4) & JointIndexMask;
        }

        public Vector4 GetVector4()
        {
            var words = GetPackedWords();
            var v = new Vector4();
            v.SetBinaryX(words[0]);
            v.SetBinaryY(words[1]);
            v.SetBinaryZ(words[2]);
            v.SetBinaryW(words[3]);
            return v;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            throw new NotImplementedException();
        }

        public void Write(BinaryWriter writer)
        {
            var v = GetVector4();
            v.Write(writer);
        }
    }
}
