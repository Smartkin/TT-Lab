using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// What a camera trigger's camera follows. Every kind gives a position for the target's (the player's) position, the ones made of
    /// a line, a path or a spline slide along it to the point nearest the target plus <see cref="Offset"/>. Verified in the PAL
    /// executable's subtype methods.
    /// </summary>
    public abstract class CameraSubBase : ITwinSerializable
    {
        /// <summary>
        /// The tools' flags word, 1 on every retail camera but the zones' 0, set by the game's constructors as well
        /// </summary>
        public UInt32 Flags { get; set; }
        /// <summary>
        /// Leftover memory, never read
        /// </summary>
        public Single Leftover { get; set; }
        /// <summary>
        /// How far along the line, path or spline the camera leads (positive) or trails the target's nearest point, in units. A
        /// spline only takes it when its flags say so, points, zones and boss cameras never read it.
        /// </summary>
        public Single Offset { get; set; }

        public CameraSubBase()
        {
            Flags = 1;
        }

        public virtual int GetLength()
        {
            return 12;
        }

        public void Compile()
        {
            return;
        }

        public virtual void Read(BinaryReader reader, int length)
        {
            Flags = reader.ReadUInt32();
            Leftover = reader.ReadSingle();
            Offset = reader.ReadSingle();
        }

        public virtual void Write(BinaryWriter writer)
        {
            writer.Write(Flags);
            writer.Write(Leftover);
            writer.Write(Offset);
        }

        public virtual ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.Null;
        }
    }
}
