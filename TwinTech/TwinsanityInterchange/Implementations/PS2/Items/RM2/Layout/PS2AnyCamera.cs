using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyCamera : BaseTwinItem, ITwinCamera
    {
        public static readonly Dictionary<ITwinCamera.CameraType, Type> subCamIdToCamera = new();

        public TwinTrigger CamTrigger { get; set; }
        public ITwinCamera.CameraFlags Flags { get; set; }
        public ITwinCamera.CameraSwitches Switches { get; set; }
        public Single BlendTime { get; set; } // 10
        public Vector4 LeftoverVector1 { get; set; }
        public Vector4 LeftoverVector2 { get; set; } // 42
        public Single LeftoverFloat1 { get; set; }
        public Single LeftoverFloat2 { get; set; } // 50
        public UInt32 FovStart { get; set; }
        public UInt32 FovEnd { get; set; }
        public UInt32 PitchStart { get; set; }
        public UInt32 PitchEnd { get; set; } // 66
        public UInt32 YawStart { get; set; }
        public UInt32 YawEnd { get; set; } // 74
        public Single DistanceStart { get; set; }
        public Single DistanceEnd { get; set; }
        public Single Camera2Value { get; set; }
        public Single Camera1Value { get; set; } // 90
        public UInt32 YawExtra { get; set; }
        public UInt32 BlendInYaw { get; set; } // 98
        public UInt32 BlendInPitch { get; set; }
        public Single BlendInDistance { get; set; } // 106
        public ITwinCamera.CameraType TypeIndex1 { get; set; }
        public ITwinCamera.CameraType TypeIndex2 { get; set; } // 114
        public Byte Group { get; set; } // 115
        public CameraSubBase MainCamera1 { get; set; }
        public CameraSubBase MainCamera2 { get; set; }
        public PS2AnyCamera()
        {
            CamTrigger = new TwinTrigger();
            LeftoverVector1 = new Vector4();
            LeftoverVector2 = new Vector4();
        }

        static PS2AnyCamera()
        {
            subCamIdToCamera.Add(ITwinCamera.CameraType.BossCamera, typeof(BossCamera));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraPoint, typeof(CameraPoint));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraLine, typeof(CameraLine));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraPath, typeof(CameraPath));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraSpline, typeof(CameraSpline));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraSub1C09, typeof(CameraSub1C09));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraPoint2, typeof(CameraPoint2));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraSub1C0C, typeof(CameraSub1C0C));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraLine2, typeof(CameraLine2));
            subCamIdToCamera.Add(ITwinCamera.CameraType.CameraZone, typeof(CameraZone));
        }

        public override int GetLength()
        {
            var mainCam1Len = MainCamera1 == null ? 0 : MainCamera1.GetLength();
            var mainCam2Len = MainCamera2 == null ? 0 : MainCamera2.GetLength();
            return CamTrigger.GetLength() + 115 + mainCam1Len + mainCam2Len;
        }

        public override void Read(BinaryReader reader, int length)
        {
            CamTrigger.Read(reader, length);
            // Camera
            Flags = (ITwinCamera.CameraFlags)reader.ReadUInt32();
            Switches = (ITwinCamera.CameraSwitches)reader.ReadUInt16();
            BlendTime = reader.ReadSingle();
            LeftoverVector1.Read(reader, Constants.SIZE_VECTOR4);
            LeftoverVector2.Read(reader, Constants.SIZE_VECTOR4);
            LeftoverFloat1 = reader.ReadSingle();
            LeftoverFloat2 = reader.ReadSingle();
            FovStart = reader.ReadUInt32();
            FovEnd = reader.ReadUInt32();
            PitchStart = reader.ReadUInt32();
            PitchEnd = reader.ReadUInt32();
            YawStart = reader.ReadUInt32();
            YawEnd = reader.ReadUInt32();
            DistanceStart = reader.ReadSingle();
            DistanceEnd = reader.ReadSingle();
            Camera2Value = reader.ReadSingle();
            Camera1Value = reader.ReadSingle();
            YawExtra = reader.ReadUInt32();
            BlendInYaw = reader.ReadUInt32();
            BlendInPitch = reader.ReadUInt32();
            BlendInDistance = reader.ReadSingle();
            TypeIndex1 = (ITwinCamera.CameraType)reader.ReadUInt32();
            TypeIndex2 = (ITwinCamera.CameraType)reader.ReadUInt32();
            Group = reader.ReadByte();
            if (TypeIndex1 != ITwinCamera.CameraType.Null && subCamIdToCamera.ContainsKey(TypeIndex1))
            {
                MainCamera1 = (CameraSubBase)Activator.CreateInstance(subCamIdToCamera[TypeIndex1]);
                MainCamera1.Read(reader, length);
            }
            if (TypeIndex2 != ITwinCamera.CameraType.Null && subCamIdToCamera.ContainsKey(TypeIndex2))
            {
                MainCamera2 = (CameraSubBase)Activator.CreateInstance(subCamIdToCamera[TypeIndex2]);
                MainCamera2.Read(reader, length);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            CamTrigger.Write(writer);
            //
            writer.Write((UInt32)Flags);
            writer.Write((UInt16)Switches);
            writer.Write(BlendTime);
            LeftoverVector1.Write(writer);
            LeftoverVector2.Write(writer);
            writer.Write(LeftoverFloat1);
            writer.Write(LeftoverFloat2);
            writer.Write(FovStart);
            writer.Write(FovEnd);
            writer.Write(PitchStart);
            writer.Write(PitchEnd);
            writer.Write(YawStart);
            writer.Write(YawEnd);
            writer.Write(DistanceStart);
            writer.Write(DistanceEnd);
            writer.Write(Camera2Value);
            writer.Write(Camera1Value);
            writer.Write(YawExtra);
            writer.Write(BlendInYaw);
            writer.Write(BlendInPitch);
            writer.Write(BlendInDistance);
            writer.Write((UInt32)TypeIndex1);
            writer.Write((UInt32)TypeIndex2);
            writer.Write(Group);
            MainCamera1?.Write(writer);
            MainCamera2?.Write(writer);
        }

        public override String GetName()
        {
            return $"Camera {id:X}";
        }
    }
}
