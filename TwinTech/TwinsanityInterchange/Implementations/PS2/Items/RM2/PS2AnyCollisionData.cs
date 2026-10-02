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
        public List<TwinCollisionNode> Nodes { get; set; }
        public List<TwinCollisionGroup> Groups { get; set; }
        public List<TwinCollisionTriangle> Triangles { get; set; }
        public List<Vector4> Vertexes { get; set; }

        public PS2AnyCollisionData()
        {
            Nodes = new List<TwinCollisionNode>();
            Groups = new List<TwinCollisionGroup>();
            Triangles = new List<TwinCollisionTriangle>();
            Vertexes = new List<Vector4>();
        }

        public override Int32 GetLength()
        {
            return 20 + Nodes.Sum(t => t.GetLength()) + Groups.Sum(g => g.GetLength())
                + Triangles.Sum(t => t.GetLength()) + Vertexes.Sum(v => v.GetLength());
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            Version = reader.ReadUInt32();
            var nodeAmt = reader.ReadUInt32();
            var grpAmt = reader.ReadUInt32();
            var triAmt = reader.ReadUInt32();
            var vertexAmt = reader.ReadUInt32();
            Nodes.Clear();
            Groups.Clear();
            Triangles.Clear();
            Vertexes.Clear();
            for (var i = 0; i < nodeAmt; ++i)
            {
                var node = new TwinCollisionNode();
                node.Read(reader, node.GetLength());
                Nodes.Add(node);
            }
            for (var i = 0; i < grpAmt; ++i)
            {
                var grp = new TwinCollisionGroup();
                grp.Read(reader, grp.GetLength());
                Groups.Add(grp);
            }
            for (var i = 0; i < triAmt; ++i)
            {
                var tri = new TwinCollisionTriangle();
                tri.Read(reader, tri.GetLength());
                Triangles.Add(tri);
            }
            for (var i = 0; i < vertexAmt; ++i)
            {
                var vec = new Vector4();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                Vertexes.Add(vec);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(Nodes.Count);
            writer.Write(Groups.Count);
            writer.Write(Triangles.Count);
            writer.Write(Vertexes.Count);
            foreach (var t in Nodes)
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
            foreach (var v in Vertexes)
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
