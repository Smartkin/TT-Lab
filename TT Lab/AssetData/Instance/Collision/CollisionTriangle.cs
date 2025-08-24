using Newtonsoft.Json;
using System;
using System.Collections.Immutable;
using System.Linq;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Common.Collision;

namespace TT_Lab.AssetData.Instance.Collision
{
    [ReferencesAssets]
    public class CollisionTriangle
    {
        public IndexedFace Face { get; set; }
        public LabURI Surface { get; set; }

        public CollisionTriangle()
        {
            Face = new IndexedFace();
        }

        public CollisionTriangle(TwinCollisionTriangle triangle, ImmutableList<IAsset> surfaces)
        {
            Face = new IndexedFace(triangle.Vector1Index, triangle.Vector2Index, triangle.Vector3Index);
            var surface = surfaces.First(s => s.ID == triangle.SurfaceIndex);
            Surface = surface.URI;
        }
    }
}
