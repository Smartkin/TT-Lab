using System;
using System.Collections.Generic;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems
{
    public interface ITwinSubSkin : ITwinSerializable
    {
        /// <summary>
        /// Material to render model with
        /// </summary>
        UInt32 Material { get; set; }
        /// <summary>
        /// Vertex positions, W is the X of the vertex's normal
        /// </summary>
        List<Vector4> Vertexes { get; set; }
        /// <summary>
        /// UV map, Z and W are the Y and Z of the vertex's normal
        /// </summary>
        List<Vector4> UVW { get; set; }
        /// <summary>
        /// Vertex colors
        /// </summary>
        List<Vector4> Colors { get; set; }
        /// <summary>
        /// Model's joints
        /// </summary>
        List<VertexJointInfo> SkinJoints { get; set; }
        /// <summary>
        /// The amount of verticies in the batch that form triangle strip/strips
        /// </summary>
        List<Int32> GroupSizes { get; set; }
        /// <summary>
        /// How positions and UVs get packed, null lets compiling pick settings that fit
        /// </summary>
        TwinSkinCompression Compression { get; set; }
        /// <summary>
        /// How the compiled packet gets padded
        /// </summary>
        TwinVifPadding Padding { get; set; }


        /// <summary>
        /// Converts VIF code into vertex data
        /// </summary>
        void CalculateData();
    }
}
