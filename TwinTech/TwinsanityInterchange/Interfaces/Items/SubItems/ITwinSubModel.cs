using System;
using System.Collections.Generic;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems
{
    public interface ITwinSubModel : ITwinSerializable
    {
        /// <summary>
        /// Vertex positions, batch after batch
        /// </summary>
        List<Vector4> Vertexes { get; set; }
        /// <summary>
        /// UV map
        /// </summary>
        List<Vector4> UVW { get; set; }
        /// <summary>
        /// Vertex colors, the game's 7 bit alpha is stored doubled with the bit above it as the alpha blending flag
        /// </summary>
        List<Vector4> Colors { get; set; }
        /// <summary>
        /// Vertex emit colors
        /// </summary>
        List<Vector4> EmitColor { get; set; }
        /// <summary>
        /// Vertex normals as the game stores them, they aren't always normalized and their lowest bits hold flags
        /// </summary>
        List<Vector4> Normals { get; set; }
        /// <summary>
        /// Whether each vertex draws the triangle it ends, the ones that don't restart or swap the strip
        /// </summary>
        List<bool> Connection { get; set; }
        /// <summary>
        /// The amount of verticies in the batch that form triangle strip/strips
        /// </summary>
        List<Int32> GroupSizes { get; set; }
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
