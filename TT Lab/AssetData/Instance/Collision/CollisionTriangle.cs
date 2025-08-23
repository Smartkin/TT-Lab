using Newtonsoft.Json;
using System;
using TT_Lab.AssetData.Graphics.SubModels;
using Twinsanity.TwinsanityInterchange.Common.Collision;

namespace TT_Lab.AssetData.Instance.Collision
{
    [JsonObject]
    // TODO: Add [ReferencesAssets] attribute
    public class CollisionTriangle
    {
        [JsonProperty(Required = Required.Always)]
        public IndexedFace Face { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Int32 SurfaceIndex { get; set; } // TODO: Replace with CollisionSurface LabURI reference

        public CollisionTriangle()
        {
            Face = new IndexedFace();
        }

        public CollisionTriangle(TwinCollisionTriangle triangle)
        {
            Face = new IndexedFace(triangle.Vector1Index, triangle.Vector2Index, triangle.Vector3Index);
            SurfaceIndex = triangle.SurfaceIndex;
        }
    }
}
