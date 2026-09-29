using System;
using System.Linq;
using Caliburn.Micro;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Composite
{
    // The vertexes of a hull are edited here, the planes, axes and edges the game reads get worked out again when they move. The
    // faces stay what they are, Blender is where a hull's shape gets changed
    public class CollisionHullViewModel : Conductor<Vector4ViewModel>.Collection.OneActive, ISaveableViewModel<TwinCollisionHull>, IHaveChildrenEditors
    {
        private readonly BindableCollection<Vector4ViewModel> vertexes = new();
        private readonly TwinCollisionHull hull;
        private readonly DirtyTracker dirtyTracker;

        public CollisionHullViewModel() : this(TwinCollisionHull.CreateBox(new Vector4(0, 0, 0, 1), new Vector4(1, 1, 1, 1)))
        {
        }

        public CollisionHullViewModel(TwinCollisionHull hull)
        {
            this.hull = hull;
            dirtyTracker = new DirtyTracker(this);
            dirtyTracker.AddBindableCollection(vertexes);
            foreach (var vertex in hull.Vertexes)
            {
                vertexes.Add(new Vector4ViewModel(vertex));
            }

            ResetDirty();
        }

        public void ResetDirty()
        {
            dirtyTracker.ResetDirty();
        }

        public bool IsDirty => dirtyTracker.IsDirty;

        public void Save(TwinCollisionHull o)
        {
            var moved = o.Vertexes.Count != vertexes.Count || o.Vertexes.Zip(vertexes).Any(pair => pair.First.X != pair.Second.X || pair.First.Y != pair.Second.Y || pair.First.Z != pair.Second.Z);
            o.Faces = hull.Faces.Select(face => face.ToList()).ToList();
            o.Vertexes = vertexes.Select(v => new Vector4(v.X, v.Y, v.Z, v.W)).ToList();
            if (moved || o.Planes.Count != o.Faces.Count)
            {
                o.ComputeFromFaces();
            }
            else
            {
                o.Planes = hull.Planes.ToList();
                o.EdgeDirections = hull.EdgeDirections.ToList();
                o.FaceNormals = hull.FaceNormals.ToList();
                o.Edges = hull.Edges.Select(edge => edge.ToList()).ToList();
            }

            dirtyTracker.ResetDirty();
        }

        public override String ToString()
        {
            return "Collision Hull";
        }

        public BindableCollection<Vector4ViewModel> Vertexes => vertexes;

        public String Summary => $"{hull.Faces.Count} faces, {hull.Edges.Count} edges. The planes and axes the game reads follow the vertexes";

        public DirtyTracker DirtyTracker => dirtyTracker;
    }
}
