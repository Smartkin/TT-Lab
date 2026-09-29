using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using GlmSharp;
using Newtonsoft.Json;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance;

/// <summary>
/// A loading hull of a chunk link: the game only loads the linked chunk while the player is inside one of the link's hulls, a link
/// without any loads it always. Kept as a placement the viewport's gizmo moves and vertexes relative to it: a box hull's placement
/// has its half extents as its scale and the vertexes are the unit cube's corners
/// </summary>
[JsonObject]
public class ChunkLinkHull : IDocumentModel
{
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Where the hull is: its center, how it's turned and, for a box, its half extents")]
    public Matrix4 Placement { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The corners relative to the placement, W is 1. The faces are made of them, so they're moved rather than added or taken out")]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<Vector4> Vertexes { get; set; }

    /// <summary>
    /// Every face's vertexes, counter-clockwise seen from outside
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public List<List<Byte>> Faces { get; set; }

    /// <summary>
    /// The hull as the game has it, written back while the placement and the vertexes still put the corners where it has them
    /// </summary>
    [JsonProperty(Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
    public TwinCollisionHull? Source { get; set; }

    public string DocumentName => "Loading Hull";

    // Read hulls get the file's corners and faces, not the box's
    [OnDeserializing]
    private void OnDeserializing(StreamingContext context)
    {
        Vertexes.Clear();
        Faces.Clear();
    }

    /// <summary>
    /// A box 2 units across at the origin
    /// </summary>
    public ChunkLinkHull()
    {
        var box = TwinCollisionHull.CreateBox(new Vector4(-1, -1, -1, 1), new Vector4(1, 1, 1, 1));
        Placement = mat4.Identity.ToTwin();
        Vertexes = box.Vertexes;
        Faces = box.Faces;
    }

    public ChunkLinkHull(TwinCollisionHull hull)
    {
        Source = CloneUtils.DeepClone(hull);
        Faces = hull.Faces.Select(face => face.ToList()).ToList();
        (Placement, Vertexes) = Place(hull.Vertexes);
    }

    // An axis-aligned box gets its half extents as the placement's scale and the unit cube's corners, anything else its centroid
    // and the corners around it
    private static (Matrix4 Placement, List<Vector4> Vertexes) Place(List<Vector4> vertexes)
    {
        if (vertexes.Count == 0)
        {
            return (mat4.Identity.ToTwin(), []);
        }

        var min = new vec3(vertexes.Min(v => v.X), vertexes.Min(v => v.Y), vertexes.Min(v => v.Z));
        var max = new vec3(vertexes.Max(v => v.X), vertexes.Max(v => v.Y), vertexes.Max(v => v.Z));
        var center = (min + max) * 0.5f;
        var isBox = vertexes.Count == 8 && min != max &&
                    vertexes.All(v => (v.X == min.x || v.X == max.x) && (v.Y == min.y || v.Y == max.y) && (v.Z == min.z || v.Z == max.z)) &&
                    vertexes.Select(v => (v.X == max.x, v.Y == max.y, v.Z == max.z)).Distinct().Count() == 8;
        if (isBox)
        {
            var placement = mat4.Translate(center) * mat4.Scale((max - min) * 0.5f);
            return (placement.ToTwin(), vertexes.Select(v => new Vector4(v.X == max.x ? 1 : -1, v.Y == max.y ? 1 : -1, v.Z == max.z ? 1 : -1, 1)).ToList());
        }

        var centroid = new vec3(vertexes.Average(v => v.X), vertexes.Average(v => v.Y), vertexes.Average(v => v.Z));
        return (mat4.Translate(centroid).ToTwin(), vertexes.Select(v => new Vector4(v.X - centroid.x, v.Y - centroid.y, v.Z - centroid.z, 1)).ToList());
    }

    /// <summary>
    /// The corners in the chunk's space
    /// </summary>
    public List<Vector4> GetCorners()
    {
        var placement = Placement.ToGlm();
        return Vertexes.Select(vertex =>
        {
            var corner = (placement * new vec4(vertex.X, vertex.Y, vertex.Z, 1.0f)).xyz;
            return new Vector4(corner.x, corner.y, corner.z, 1);
        }).ToList();
    }

    /// <summary>
    /// The hull the way the game stores it: the game's own while the corners are still where it has them, otherwise the planes and
    /// axes get worked out for the corners the way the game's hull builder does
    /// </summary>
    public TwinChunkLinkHull ToTwin()
    {
        var corners = GetCorners();
        if (Source != null && Source.Vertexes.Count == corners.Count && Source.Vertexes.Zip(corners).All(pair => IsSameCorner(pair.First, pair.Second)))
        {
            return new TwinChunkLinkHull { Hull = CloneUtils.DeepClone(Source) };
        }

        var hull = new TwinCollisionHull { Vertexes = corners, Faces = Faces.Select(face => face.ToList()).ToList() };
        hull.ComputeFromFaces();
        return new TwinChunkLinkHull { Hull = hull };
    }

    private static bool IsSameCorner(Vector4 source, Vector4 corner)
    {
        var tolerance = 1e-5f * Math.Max(1.0f, Math.Max(Math.Abs(source.X), Math.Max(Math.Abs(source.Y), Math.Abs(source.Z))));
        return Math.Abs(source.X - corner.X) <= tolerance && Math.Abs(source.Y - corner.Y) <= tolerance && Math.Abs(source.Z - corner.Z) <= tolerance;
    }
}
