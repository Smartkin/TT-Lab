using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyPath : BaseTwinItem, ITwinPath
    {
        public List<Vector4> PointList { get; set; }
        public List<Single> ArcLengths { get; set; }
        public List<Single> InverseSteps { get; set; }
        public PS2AnyPath()
        {
            PointList = new List<Vector4>();
            ArcLengths = new List<Single>();
            InverseSteps = new List<Single>();
        }

        public override int GetLength()
        {
            return 8 + Constants.SIZE_VECTOR4 * PointList.Count + 4 * (ArcLengths.Count + InverseSteps.Count);
        }

        public override void Read(BinaryReader reader, int length)
        {
            Int32 points = reader.ReadInt32();
            PointList.Clear();
            for (int i = 0; i < points; ++i)
            {
                Vector4 point = new Vector4();
                point.Read(reader, Constants.SIZE_VECTOR4);
                PointList.Add(point);
            }
            TwinPathParameters.Read(reader, reader.ReadInt32(), ArcLengths, InverseSteps);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PointList.Count);
            foreach (ITwinSerializable e in PointList)
            {
                e.Write(writer);
            }
            writer.Write(ArcLengths.Count);
            TwinPathParameters.Write(writer, ArcLengths, InverseSteps);
        }

        public override String GetName()
        {
            return $"Path {id:X}";
        }
    }
}
