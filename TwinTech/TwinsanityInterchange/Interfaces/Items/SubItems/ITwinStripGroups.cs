using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems
{
    /// <summary>
    /// Geometry of the Xbox version, drawn as one triangle strip per group. Skinned vertexes refer to the joints of their strip's palette
    /// </summary>
    /// <remarks>
    /// The Xbox version's tools joined strips without keeping their triangles facing one way, so it draws them without culling
    /// </remarks>
    public interface ITwinStripGroups
    {
        /// <summary>
        /// Joints each strip's vertexes can use, in the order the game keeps them. Empty for rigid models
        /// </summary>
        List<List<Int32>> JointPalettes { get; set; }
        /// <summary>
        /// Vertex normals
        /// </summary>
        List<Vector4> Normals { get; set; }
    }
}
