using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common.Particles;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM
{
    /// <summary>
    /// Special particle data section only used in Default: the systems every chunk can play, the texture pages they draw
    /// with and the decal system's data
    /// </summary>
    public interface ITwinDefaultParticle : ITwinParticle
    {
        /// <summary>
        /// Texture IDs bank
        /// </summary>
        UInt32[] TextureIDs { get; set; }
        /// <summary>
        /// Material IDs bank
        /// </summary>
        UInt32[] MaterialIDs { get; set; }
        /// <summary>
        /// Decal texture
        /// </summary>
        UInt32 DecalTextureID { get; set; }
        /// <summary>
        /// Decal material
        /// </summary>
        UInt32 DecalMaterialID { get; set; }
        /// <summary>
        /// Read into a global the retail game never reads (1 in its data)
        /// </summary>
        Int32 UnusedDecalInt { get; set; }
        /// <summary>
        /// The packet that sets the decals up for drawing
        /// </summary>
        TwinDecalUvPacket DecalUvPacket { get; set; }
        /// <summary>
        /// One per possible decal type, a type follows for every entry that isn't 0 (the tools left their memory's
        /// addresses in them)
        /// </summary>
        Int32[] DecalTypeMarkers { get; set; }
        /// <summary>
        /// The decal types, in the order of their markers
        /// </summary>
        List<TwinDecalType> DecalTypes { get; set; }
    }
}
