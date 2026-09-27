using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SubItems
{
    /// <summary>
    /// Strips of skinned vertexes the Xbox version's skins and blend skins store
    /// </summary>
    /// <remarks>
    /// Every strip has a palette of joints. A vertex refers to up to 3 of them by the vertex shader register of their matrix, the
    /// palette's matrices start at register 16 and take 4 registers each
    /// </remarks>
    public class XboxSkinnedGeometry
    {
        public const Int32 VertexLength = 0x30;
        private const Int32 FirstJointRegister = 16;
        private const Int32 RegistersPerJoint = 4;
        private const UInt16 NoJoint = 0xFFFF;

        private List<UInt16[]> registers = new();
        private List<Single[]> leftoverWeights = new();
        private List<UInt16> flags = new();

        public List<Int32> GroupSizes { get; set; } = new();
        public List<List<Int32>> JointPalettes { get; set; } = new();
        /// <summary>
        /// Positions with the normal's X in W like the PS2 version's skins keep their normals
        /// </summary>
        public List<Vector4> Vertexes { get; set; } = new();
        public List<VertexJointInfo> SkinJoints { get; set; } = new();
        public List<Vector4> Colors { get; set; } = new();
        /// <summary>
        /// UVs with the normal's Y and Z in Z and W
        /// </summary>
        public List<Vector4> UVW { get; set; } = new();

        public List<Vector4> Normals
        {
            get => Vertexes.Select((vertex, i) => new Vector4(vertex.W, i < UVW.Count ? UVW[i].Z : 0, i < UVW.Count ? UVW[i].W : 0, 0)).ToList();
            set
            {
                for (var i = 0; i < value.Count && i < Vertexes.Count && i < UVW.Count; i++)
                {
                    Vertexes[i].W = value[i].X;
                    UVW[i].Z = value[i].Y;
                    UVW[i].W = value[i].Z;
                }
            }
        }

        public Int32 GetLength()
        {
            return 16 + GroupSizes.Count * 8 + JointPalettes.Sum(p => p.Count) * 4 + Vertexes.Count * VertexLength;
        }

        public void Read(BinaryReader reader)
        {
            reader.ReadInt32(); // Length of the vertexes
            var vertexAmount = reader.ReadInt32();
            reader.ReadInt32(); // Amount of joints in all palettes
            var groupAmount = reader.ReadInt32();
            GroupSizes = new List<Int32>(groupAmount);
            for (var i = 0; i < groupAmount; i++)
            {
                GroupSizes.Add(reader.ReadInt32());
            }

            var paletteSizes = new List<Int32>(groupAmount);
            for (var i = 0; i < groupAmount; i++)
            {
                paletteSizes.Add(reader.ReadInt32());
            }

            JointPalettes = new List<List<Int32>>(groupAmount);
            foreach (var size in paletteSizes)
            {
                var palette = new List<Int32>(size);
                for (var i = 0; i < size; i++)
                {
                    palette.Add(reader.ReadInt32());
                }

                JointPalettes.Add(palette);
            }

            Vertexes = new List<Vector4>(vertexAmount);
            SkinJoints = new List<VertexJointInfo>(vertexAmount);
            Colors = new List<Vector4>(vertexAmount);
            UVW = new List<Vector4>(vertexAmount);
            registers = new List<UInt16[]>(vertexAmount);
            leftoverWeights = new List<Single[]>(vertexAmount);
            flags = new List<UInt16>(vertexAmount);
            var group = 0;
            var inGroup = 0;
            for (var i = 0; i < vertexAmount; i++)
            {
                while (group < GroupSizes.Count - 1 && inGroup >= GroupSizes[group])
                {
                    group++;
                    inGroup = 0;
                }

                var palette = group < JointPalettes.Count ? JointPalettes[group] : new List<Int32>();
                var position = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0.0f);
                var weights = new[] { reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() };
                var vertexRegisters = new[] { reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16() };
                registers.Add(vertexRegisters);
                flags.Add(reader.ReadUInt16());
                // Slots the vertex doesn't use refer to joint 0 with no weight like the PS2 version's, the leftovers are written back
                var amount = CountJoints(vertexRegisters, weights, palette);
                var joints = vertexRegisters.Select((register, slot) => slot < amount ? ToJoint(register, palette) : 0).ToArray();
                leftoverWeights.Add(weights);
                SkinJoints.Add(new VertexJointInfo
                {
                    Weight1 = weights[0],
                    Weight2 = amount > 1 ? weights[1] : 0,
                    Weight3 = amount > 2 ? weights[2] : 0,
                    JointIndex1 = joints[0],
                    JointIndex2 = joints[1],
                    JointIndex3 = joints[2],
                    WeightsAmount = amount
                });
                var normal = XboxSubModel.UnpackNormal(reader.ReadUInt32());
                position.W = normal.X;
                Vertexes.Add(position);
                Colors.Add(new Vector4(reader.ReadByte() / 255f, reader.ReadByte() / 255f, reader.ReadByte() / 255f, reader.ReadByte() / 255f));
                UVW.Add(new Vector4(reader.ReadSingle(), reader.ReadSingle(), normal.Y, normal.Z));
                inGroup++;
            }

            var connections = XboxSubModel.GetConnections(GroupSizes, Vertexes);
            for (var i = 0; i < SkinJoints.Count; i++)
            {
                SkinJoints[i].Connection = connections[i];
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Vertexes.Count * VertexLength);
            writer.Write(Vertexes.Count);
            writer.Write(JointPalettes.Sum(p => p.Count));
            writer.Write(GroupSizes.Count);
            foreach (var size in GroupSizes)
            {
                writer.Write(size);
            }

            foreach (var palette in JointPalettes)
            {
                writer.Write(palette.Count);
            }

            foreach (var palette in JointPalettes)
            {
                foreach (var joint in palette)
                {
                    writer.Write(joint);
                }
            }

            var group = 0;
            var inGroup = 0;
            for (var i = 0; i < Vertexes.Count; i++)
            {
                while (group < GroupSizes.Count - 1 && inGroup >= GroupSizes[group])
                {
                    group++;
                    inGroup = 0;
                }

                var palette = group < JointPalettes.Count ? JointPalettes[group] : new List<Int32>();
                var joint = SkinJoints[i];
                writer.Write(Vertexes[i].X);
                writer.Write(Vertexes[i].Y);
                writer.Write(Vertexes[i].Z);
                var amount = joint.GetJointConnectionsAmount();
                var leftovers = i < leftoverWeights.Count ? leftoverWeights[i] : null;
                writer.Write(joint.Weight1);
                writer.Write(amount > 1 || leftovers == null ? joint.Weight2 : leftovers[1]);
                writer.Write(amount > 2 || leftovers == null ? joint.Weight3 : leftovers[2]);
                var joints = new[] { joint.JointIndex1, joint.JointIndex2, joint.JointIndex3 };
                for (var j = 0; j < 3; j++)
                {
                    var previous = i < registers.Count ? registers[i][j] : NoJoint;
                    writer.Write(j < amount ? ToRegister(joints[j], palette, previous) : previous);
                }

                writer.Write(i < flags.Count ? flags[i] : (UInt16)0);
                var uvw = i < UVW.Count ? UVW[i] : new Vector4();
                writer.Write(XboxSubModel.PackNormal(new Vector4(Vertexes[i].W, uvw.Z, uvw.W, 0)));
                var color = i < Colors.Count ? Colors[i] : new Vector4(1, 1, 1, 1);
                writer.Write(ToByte(color.X));
                writer.Write(ToByte(color.Y));
                writer.Write(ToByte(color.Z));
                writer.Write(ToByte(color.W));
                var uv = i < UVW.Count ? UVW[i] : new Vector4();
                writer.Write(uv.X);
                writer.Write(uv.Y);
                inGroup++;
            }
        }

        // The game leaves leftovers in the slots it doesn't use, registers past the palette and weights close to 0
        private static Int32 CountJoints(UInt16[] vertexRegisters, Single[] weights, List<Int32> palette)
        {
            var amount = 1;
            while (amount < 3 && IsInPalette(vertexRegisters[amount], palette) && weights[amount] > 1e-4f)
            {
                amount++;
            }

            return amount;
        }

        private static Boolean IsInPalette(UInt16 register, List<Int32> palette)
        {
            var slot = (register - FirstJointRegister) / RegistersPerJoint;
            return register != NoJoint && register >= FirstJointRegister && slot < palette.Count;
        }

        private static Int32 ToJoint(UInt16 register, List<Int32> palette)
        {
            var slot = (register - FirstJointRegister) / RegistersPerJoint;
            return register == NoJoint || slot < 0 || slot >= palette.Count ? 0 : palette[slot];
        }

        // The register the game had is kept while it still refers to the joint, a palette can list a joint more than once
        private static UInt16 ToRegister(Int32 joint, List<Int32> palette, UInt16 previous)
        {
            if (previous != NoJoint && ToJoint(previous, palette) == joint)
            {
                return previous;
            }

            var slot = palette.IndexOf(joint);
            if (slot < 0)
            {
                throw new InvalidOperationException($"Joint {joint} isn't in the palette of its strip");
            }

            return (UInt16)(FirstJointRegister + slot * RegistersPerJoint);
        }

        private static Byte ToByte(Single value)
        {
            return (Byte)Math.Clamp(Math.Round(value * 255.0f), 0, 255);
        }
    }
}
