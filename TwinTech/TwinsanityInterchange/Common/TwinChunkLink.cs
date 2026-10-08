using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinChunkLink : ITwinSerializable
    {
        UInt32 type;
        UInt32 flags;
        /// <summary>
        /// A link with loading hulls only loads its chunk while the player is inside one of them. This makes it load while there
        /// is no player yet either (LoadLinkedChunks 0x2aa118), the game's tools set it on 3 links
        /// </summary>
        public Boolean LoadsWithoutPlayer { get; set; }
        /// <summary>
        /// Path to the linked chunk
        /// </summary>
        public String Path { get; set; }
        /// <summary>
        /// Whether and how the linked chunk's scenery is drawn from this chunk. The game reads the flags' low 7 bits as one number:
        /// 0 draws nothing, 1 draws it always and anything above draws it culled by the load wall as a portal
        /// </summary>
        public ChunkLinkVisibility Visibility { get; set; }
        /// <summary>
        /// Marks if the load wall is collidable, when turned off the linked chunk can not be transitioned into and only the scenery of the linked chunk will be rendered.
        /// The loader reads it together with <see cref="KeepLoaded"/> (bits 7-13 of the flags): a link with either set loads
        /// the linked chunk's own links one level deeper, one without leaves them at the last level
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
        public List<TwinChunkLinkHull> ChunkLinksCollisionData { get; set; }

        public TwinChunkLink()
        {
            ObjectMatrix = new Matrix4();
            ChunkMatrix = new Matrix4();
            LoadingWall = null;
            ChunkLinksCollisionData = new List<TwinChunkLinkHull>();
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
                LoadsWithoutPlayer = (type & 0x2) != 0;
            }
            int pathLen = reader.ReadInt32();
            Path = GameText.ReadString(reader, pathLen);
            flags = reader.ReadUInt32();
            {
                Visibility = (ChunkLinkVisibility)(flags & 0x7F);
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
                var clOgi3 = new TwinChunkLinkHull();
                Boolean hasNext;
                do
                {
                    clOgi3.Read(reader, length);
                    ChunkLinksCollisionData.Add(clOgi3);
                    hasNext = (clOgi3.Type & 0x1) != 0;
                    if (hasNext)
                    {
                        clOgi3 = new TwinChunkLinkHull();
                    }
                } while (hasNext);
            }
        }

        public void Write(BinaryWriter writer)
        {
            flags = (UInt32)Visibility & 0x7F;
            type = 0;
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
            if (LoadsWithoutPlayer)
            {
                type |= 0x2;
            }
            writer.Write(type);
            writer.Write(Path.Length);
            GameText.Write(writer, Path.Replace(System.IO.Path.DirectorySeparatorChar, '\\'));
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

    /// <summary>
    /// How a chunk link's scenery is drawn from the linking chunk (the low 7 bits of the link's flags, verified in the PAL
    /// executable's chunk renderer 0x1ea3a8). Values above 2 draw like <see cref="ThroughLoadWall"/>, the game's tools never wrote any
    /// </summary>
    public enum ChunkLinkVisibility : byte
    {
        /// <summary>
        /// The linked chunk isn't drawn
        /// </summary>
        Hidden = 0,
        /// <summary>
        /// The linked chunk is always drawn
        /// </summary>
        Always = 1,
        /// <summary>
        /// The linked chunk is drawn through its load wall, which culls it like a portal
        /// </summary>
        ThroughLoadWall = 2
    }
}
