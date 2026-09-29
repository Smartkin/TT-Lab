using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Collision;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2
{
    public class PS2AnyCollisionData : BaseTwinItem, ITwinCollision
    {
        /// <summary>
        /// The value the game's collision constructor (FUN_002823c8) gives new data, 3001 on every retail chunk
        /// </summary>
        public const UInt32 GameVersion = 0xBB9;
        /// <summary>
        /// The data's version word, <see cref="GameVersion"/> everywhere, read and never checked
        /// </summary>
        public UInt32 Version { get; set; }
        public List<TwinCollisionTrigger> Triggers { get; set; }
        public List<TwinGroupInformation> Groups { get; set; }
        public List<TwinCollisionTriangle> Triangles { get; set; }
        public List<Vector4> Vectors { get; set; }

        public PS2AnyCollisionData()
        {
            Triggers = new List<TwinCollisionTrigger>();
            Groups = new List<TwinGroupInformation>();
            Triangles = new List<TwinCollisionTriangle>();
            Vectors = new List<Vector4>();
        }

        public override Int32 GetLength()
        {
            return 20 + Triggers.Sum(t => t.GetLength()) + Groups.Sum(g => g.GetLength())
                + Triangles.Sum(t => t.GetLength()) + Vectors.Sum(v => v.GetLength());
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            Version = reader.ReadUInt32();
            var trgAmt = reader.ReadUInt32();
            var grpAmt = reader.ReadUInt32();
            var triAmt = reader.ReadUInt32();
            var vecAmt = reader.ReadUInt32();
            Triggers.Clear();
            Groups.Clear();
            Triangles.Clear();
            Vectors.Clear();
            for (var i = 0; i < trgAmt; ++i)
            {
                var trg = new TwinCollisionTrigger();
                trg.Read(reader, trg.GetLength());
                Triggers.Add(trg);
            }
            for (var i = 0; i < grpAmt; ++i)
            {
                var grp = new TwinGroupInformation();
                grp.Read(reader, grp.GetLength());
                Groups.Add(grp);
            }
            for (var i = 0; i < triAmt; ++i)
            {
                var tri = new TwinCollisionTriangle();
                tri.Read(reader, tri.GetLength());
                Triangles.Add(tri);
            }
            for (var i = 0; i < vecAmt; ++i)
            {
                var vec = new Vector4();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                Vectors.Add(vec);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(Triggers.Count);
            writer.Write(Groups.Count);
            writer.Write(Triangles.Count);
            writer.Write(Vectors.Count);
            foreach (var t in Triggers)
            {
                t.Write(writer);
            }
            foreach (var g in Groups)
            {
                g.Write(writer);
            }
            foreach (var t in Triangles)
            {
                t.Write(writer);
            }
            foreach (var v in Vectors)
            {
                v.Write(writer);
            }
        }

        public override String GetName()
        {
            return $"Collision data {id:X}";
        }
    }
}
