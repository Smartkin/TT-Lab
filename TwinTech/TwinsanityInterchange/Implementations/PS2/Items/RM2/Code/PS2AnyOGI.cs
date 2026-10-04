using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code
{
    public class PS2AnyOGI : BaseTwinItem, ITwinOGI
    {

        Byte[] headerData;
        public List<TwinJoint> Joints { get; set; }
        public Byte JointIdCount { get; set; }
        public List<TwinExitPoint> ExitPoints { get; set; }
        public Vector4[] BoundingBox { get; set; }
        public List<Byte> JointIndices { get; set; }
        public List<UInt32> RigidModelIds { get; set; }
        public List<Matrix4> SkinInverseBindMatrices { get; set; }
        public UInt32 SkinID { get; set; }
        public UInt32 BlendSkinID { get; set; }
        public List<TwinCollisionHull> CollisionHulls { get; set; }
        public List<Byte> CollisionJointIndices { get; set; }

        public PS2AnyOGI()
        {
            headerData = new byte[0x10];
            BoundingBox = new Vector4[2];
            BoundingBox[0] = new Vector4();
            BoundingBox[1] = new Vector4();
            Joints = new List<TwinJoint>();
            ExitPoints = new List<TwinExitPoint>();
            JointIndices = new List<byte>();
            RigidModelIds = new List<uint>();
            SkinInverseBindMatrices = new List<Matrix4>();
            CollisionHulls = new List<TwinCollisionHull>();
            CollisionJointIndices = new List<byte>();
        }

        public override int GetLength()
        {
            int dynamic_size = 0;
            foreach (ITwinSerializable e in CollisionHulls)
            {
                dynamic_size += e.GetLength();
            }
            return headerData.Length + Constants.SIZE_VECTOR4 * 2
                + Constants.SIZE_JOINT * Joints.Count
                + Constants.SIZE_EXIT_POINT * ExitPoints.Count
                + JointIndices.Count
                + Constants.SIZE_UINT32 * RigidModelIds.Count
                + Constants.SIZE_MATRIX4 * SkinInverseBindMatrices.Count
                + 8 + dynamic_size + CollisionJointIndices.Count;
        }

        public override void Read(BinaryReader reader, int length)
        {
            reader.Read(headerData, 0, headerData.Length);
            Byte jointAmount = headerData[(int)ITwinOGI.HeaderInfo.JOINT_AMOUNT];
            Byte exitPointAmount = headerData[(int)ITwinOGI.HeaderInfo.EXIT_POINT_AMOUNT];
            JointIdCount = headerData[(int)ITwinOGI.HeaderInfo.REACT_JOINT_AMOUNT];
            Byte rigidModelsAmount = headerData[(int)ITwinOGI.HeaderInfo.RIGID_MODELS_AMOUNT];
            Byte skinFlag = headerData[(int)ITwinOGI.HeaderInfo.HAS_SKIN];
            Byte blendFlag = headerData[(int)ITwinOGI.HeaderInfo.HAS_BLEND_SKIN];
            Byte collidersAmount = headerData[(int)ITwinOGI.HeaderInfo.COLLISIONS_AMOUNT];
            BoundingBox[0].Read(reader, Constants.SIZE_VECTOR4);
            BoundingBox[1].Read(reader, Constants.SIZE_VECTOR4);
            Joints.Clear();
            for (int i = 0; i < jointAmount; ++i)
            {
                TwinJoint joint = new();
                joint.Read(reader, joint.GetLength());
                Joints.Add(joint);
            }
            ExitPoints.Clear();
            for (int i = 0; i < exitPointAmount; ++i)
            {
                TwinExitPoint exitPoint = new();
                exitPoint.Read(reader, exitPoint.GetLength());
                ExitPoints.Add(exitPoint);
            }
            JointIndices.Clear();
            for (int i = 0; i < rigidModelsAmount; ++i)
            {
                JointIndices.Add(reader.ReadByte());
            }
            RigidModelIds.Clear();
            for (int i = 0; i < rigidModelsAmount; ++i)
            {
                RigidModelIds.Add(reader.ReadUInt32());
            }
            SkinInverseBindMatrices.Clear();
            for (int i = 0; i < jointAmount; ++i)
            {
                Matrix4 m = new();
                m.Read(reader, m.GetLength());
                SkinInverseBindMatrices.Add(m);
            }
            SkinID = reader.ReadUInt32();
            BlendSkinID = reader.ReadUInt32();
            CollisionHulls.Clear();
            for (int i = 0; i < collidersAmount; ++i)
            {
                TwinCollisionHull hull = new();
                hull.Read(reader, length);
                CollisionHulls.Add(hull);
            }
            CollisionJointIndices.Clear();
            for (int i = 0; i < collidersAmount; ++i)
            {
                CollisionJointIndices.Add(reader.ReadByte());
            }
        }

        public override void Write(BinaryWriter writer)
        {
            headerData[(int)ITwinOGI.HeaderInfo.JOINT_AMOUNT] = (Byte)Joints.Count;
            headerData[(int)ITwinOGI.HeaderInfo.EXIT_POINT_AMOUNT] = (Byte)ExitPoints.Count;
            headerData[(int)ITwinOGI.HeaderInfo.REACT_JOINT_AMOUNT] = JointIdCount;
            headerData[(int)ITwinOGI.HeaderInfo.RIGID_MODELS_AMOUNT] = (Byte)RigidModelIds.Count;
            headerData[(int)ITwinOGI.HeaderInfo.HAS_SKIN] = (Byte)((SkinID == 0) ? 0 : 1);
            headerData[(int)ITwinOGI.HeaderInfo.HAS_BLEND_SKIN] = (Byte)((BlendSkinID == 0) ? 0 : 1);
            headerData[(int)ITwinOGI.HeaderInfo.COLLISIONS_AMOUNT] = (Byte)CollisionHulls.Count;
            writer.Write(headerData);
            BoundingBox[0].Write(writer);
            BoundingBox[1].Write(writer);
            foreach (ITwinSerializable item in Joints)
            {
                item.Write(writer);
            }
            foreach (ITwinSerializable item in ExitPoints)
            {
                item.Write(writer);
            }
            foreach (Byte item in JointIndices)
            {
                writer.Write(item);
            }
            foreach (UInt32 item in RigidModelIds)
            {
                writer.Write(item);
            }
            foreach (ITwinSerializable item in SkinInverseBindMatrices)
            {
                item.Write(writer);
            }
            writer.Write(SkinID);
            writer.Write(BlendSkinID);
            foreach (ITwinSerializable item in CollisionHulls)
            {
                item.Write(writer);
            }
            foreach (Byte item in CollisionJointIndices)
            {
                writer.Write(item);
            }
        }

        public override String GetName()
        {
            return $"OGI {id:X}";
        }
    }
}
