using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinChunkLink : ITwinSerializable
    {
        UInt32 type;
        UInt32 flags;

        /// <summary>
        /// Unknown flag which is related to how chunk is loaded
        /// </summary>
        public Boolean UnkFlag { get; set; }
        /// <summary>
        /// Path to the linked chunk
        /// </summary>
        public String Path { get; set; }
        /// <summary>
        /// Marks if the linked chunk should be rendered
        /// </summary>
        public Boolean IsRendered { get; set; }
        /// <summary>
        /// Purpose currently unknown. Only 6 bits are used
        /// </summary>
        public Byte UnkNum { get; set; }
        /// <summary>
        /// Marks if the load wall is collidable, when turned off the linked chunk can not be transitioned into and only the scenery of the linked chunk will be rendered
        /// </summary>
        public Boolean IsLoadWallActive { get; set; }
        /// <summary>
        /// Marks whether the chunk should be preloaded/kept in memory. Used for chunks that are not directly linked and are seperated by another chunk
        /// </summary>
        public Boolean KeepLoaded { get; set; }
        /// <summary>
        /// How object is translated when crossing the load wall as well as how camera occlussion culling is calculated
        /// </summary>
        public Matrix4 ObjectMatrix { get; set; }
        /// <summary>
        /// How linked chunk is rendered before crossing the load wall
        /// </summary>
        public Matrix4 ChunkMatrix { get; set; }
        /// <summary>
        /// How load wall is positioned, touching it will move you into the linked chunk
        /// </summary>
        public Matrix4 LoadingWall { get; set; }
        /// <summary>
        /// Loading bounding boxes. When set will create a bounding box that the playable must be in for the chunk to start loading/be loaded.
        /// </summary>
        public List<TwinChunkLinkBoundingBoxBuilder> ChunkLinksCollisionData { get; set; }

        public TwinChunkLink()
        {
            ObjectMatrix = new Matrix4();
            ChunkMatrix = new Matrix4();
            LoadingWall = null;
            ChunkLinksCollisionData = new List<TwinChunkLinkBoundingBoxBuilder>();
        }
        public int GetLength()
        {
            return 4 + 4 + Path.Length + 4 + Constants.SIZE_MATRIX4 * 2
                + (LoadingWall != null ? Constants.SIZE_MATRIX4 : 0)
                + ChunkLinksCollisionData.Sum(l => l.GetLength());
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, int length)
        {
            type = reader.ReadUInt32();
            {
                UnkFlag = (type & 0x2) != 0;
            }
            int pathLen = reader.ReadInt32();
            Path = new String(reader.ReadChars(pathLen));
            flags = reader.ReadUInt32();
            {
                IsRendered = (flags & 0x1) != 0;
                UnkNum = (Byte)((flags >> 0x1) & 0x3F);
                KeepLoaded = (flags & 0x80) != 0;
                IsLoadWallActive = (flags & 0x100) != 0;
            }
            ObjectMatrix.Read(reader, Constants.SIZE_MATRIX4);
            ChunkMatrix.Read(reader, Constants.SIZE_MATRIX4);
            if ((flags & 0x80000) != 0)
            {
                LoadingWall = new Matrix4();
                LoadingWall.Read(reader, Constants.SIZE_MATRIX4);
            }
            if ((type & 0x1) != 0)
            {
                var clOgi3 = new TwinChunkLinkBoundingBoxBuilder();
                Boolean hasNext;
                do
                {
                    clOgi3.Read(reader, length);
                    ChunkLinksCollisionData.Add(clOgi3);
                    hasNext = (clOgi3.Type & 0x1) != 0;
                    if (hasNext)
                    {
                        clOgi3 = new TwinChunkLinkBoundingBoxBuilder();
                    }
                } while (hasNext);
            }
        }

        public void Write(BinaryWriter writer)
        {
            flags = (UInt32)((UnkNum & 0x3F) << 1);
            type = 0;
            if (IsRendered)
            {
                flags |= 0x1;
            }
            if (KeepLoaded)
            {
                flags |= 0x80;
            }
            if (IsLoadWallActive)
            {
                flags |= 0x100;
            }
            if (LoadingWall != null)
            {
                flags |= 0x80000;
            }
            if (ChunkLinksCollisionData.Count != 0)
            {
                type |= 0x1;
            }
            if (UnkFlag)
            {
                type |= 0x2;
            }
            writer.Write(type);
            writer.Write(Path.Length);
            writer.Write(Path.Replace(System.IO.Path.DirectorySeparatorChar, '\\').ToCharArray());
            writer.Write(flags);
            ObjectMatrix.Write(writer);
            ChunkMatrix.Write(writer);
            LoadingWall?.Write(writer);
            if ((type & 0x1) != 0)
            {
                foreach (var colData in ChunkLinksCollisionData)
                {
                    colData.Type &= ~0x1;
                    if (!colData.Equals(ChunkLinksCollisionData.Last()))
                    {
                        colData.Type |= 0x1;
                    }
                    colData.Write(writer);
                }
            }
        }
    }
}
