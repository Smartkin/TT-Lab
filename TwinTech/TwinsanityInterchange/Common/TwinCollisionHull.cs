using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    /// <summary>
    /// A convex hull the game collides a model with, its ModelCollisionData: OGIs keep them on their joints, dynamic scenery models and
    /// chunk links carry them too. Next to the vertexes and the faces it holds what the collision code reads without working it out:
    /// every face's plane, the unique face normals and edge directions (the separating axes of hull against hull tests) and the edges.
    /// The blob starts with the vertexes, then the planes, the edge directions, the face normals, every face's offset in the face
    /// bytes, the faces (their vertex count and vertexes) and the edges.
    /// </summary>
    public class TwinCollisionHull : ITwinSerializable
    {
        /// <summary>
        /// Two vectors closer than this are one direction to the game's hull builder
        /// </summary>
        public const Single SameDirectionTolerance = 0.001f;
        /// <summary>
        /// How far off a plane the game's builder lets a vertex be
        /// </summary>
        public const Single PlaneTolerance = 0.001f;
        private const Int32 HeaderShorts = 11;

        /// <summary>
        /// Corners of the hull, W is 1
        /// </summary>
        public List<Vector4> Vertexes { get; set; }
        /// <summary>
        /// One per face: its unit normal pointing out of the hull and the plane's constant in W, so a point is inside where
        /// X*x + Y*y + Z*z + W is at most 0
        /// </summary>
        public List<Vector4> Planes { get; set; }
        /// <summary>
        /// The directions of the edges, each once whichever way it points, W is 1
        /// </summary>
        public List<Vector4> EdgeDirections { get; set; }
        /// <summary>
        /// The normals of the faces, each once whichever way it points, W is 1
        /// </summary>
        public List<Vector4> FaceNormals { get; set; }
        /// <summary>
        /// Every face's vertexes, counter-clockwise seen from outside
        /// </summary>
        public List<List<Byte>> Faces { get; set; }
        /// <summary>
        /// The two vertexes of every edge
        /// </summary>
        public List<List<Byte>> Edges { get; set; }

        public TwinCollisionHull()
        {
            Vertexes = new List<Vector4>();
            Planes = new List<Vector4>();
            EdgeDirections = new List<Vector4>();
            FaceNormals = new List<Vector4>();
            Faces = new List<List<Byte>>();
            Edges = new List<List<Byte>>();
        }

        /// <summary>
        /// A box the way the game makes one for models without hulls, corners from the bottom of the near side around
        /// </summary>
        public static TwinCollisionHull CreateBox(Vector4 min, Vector4 max)
        {
            var hull = new TwinCollisionHull();
            hull.Vertexes.AddRange(new[]
            {
                new Vector4(min.X, min.Y, min.Z, 1), new Vector4(max.X, min.Y, min.Z, 1), new Vector4(max.X, min.Y, max.Z, 1), new Vector4(min.X, min.Y, max.Z, 1),
                new Vector4(min.X, max.Y, min.Z, 1), new Vector4(max.X, max.Y, min.Z, 1), new Vector4(max.X, max.Y, max.Z, 1), new Vector4(min.X, max.Y, max.Z, 1)
            });
            hull.Planes.AddRange(new[]
            {
                new Vector4(0, -1, 0, min.Y), new Vector4(0, 0, -1, min.Z), new Vector4(1, 0, 0, -max.X),
                new Vector4(0, 0, 1, -max.Z), new Vector4(-1, 0, 0, min.X), new Vector4(0, 1, 0, -max.Y)
            });
            hull.EdgeDirections.AddRange(new[] { new Vector4(-1, 0, 0, 1), new Vector4(0, 0, -1, 1), new Vector4(0, -1, 0, 1) });
            hull.FaceNormals.AddRange(new[] { new Vector4(0, -1, 0, 1), new Vector4(0, 0, -1, 1), new Vector4(1, 0, 0, 1) });
            hull.Faces.AddRange(new[]
            {
                new List<Byte> { 0, 1, 2, 3 }, new List<Byte> { 0, 4, 5, 1 }, new List<Byte> { 1, 5, 6, 2 },
                new List<Byte> { 3, 2, 6, 7 }, new List<Byte> { 3, 7, 4, 0 }, new List<Byte> { 5, 4, 7, 6 }
            });
            hull.Edges.AddRange(new[]
            {
                new List<Byte> { 0, 1 }, new List<Byte> { 1, 2 }, new List<Byte> { 2, 3 }, new List<Byte> { 3, 0 }, new List<Byte> { 0, 4 }, new List<Byte> { 4, 5 },
                new List<Byte> { 5, 1 }, new List<Byte> { 5, 6 }, new List<Byte> { 6, 2 }, new List<Byte> { 6, 7 }, new List<Byte> { 7, 3 }, new List<Byte> { 7, 4 }
            });
            return hull;
        }

        /// <summary>
        /// Works the planes, the face normals, the edge directions and the edges out of the vertexes and faces the way the game's
        /// hull builder does, turning faces around to face outward
        /// </summary>
        public void ComputeFromFaces()
        {
            Planes = new List<Vector4>();
            EdgeDirections = new List<Vector4>();
            FaceNormals = new List<Vector4>();
            Edges = new List<List<Byte>>();
            foreach (var face in Faces)
            {
                var normal = FaceNormal(face);
                var first = Vertexes[face[0]];
                var constant = -(normal.X * first.X + normal.Y * first.Y + normal.Z * first.Z);
                var above = false;
                var below = false;
                foreach (var vertex in Vertexes)
                {
                    var distance = normal.X * vertex.X + normal.Y * vertex.Y + normal.Z * vertex.Z + constant;
                    above |= distance > PlaneTolerance;
                    below |= distance < -PlaneTolerance;
                }

                // A face with the hull on its front faces inward, a face with the hull on both sides isn't part of a convex hull
                if (above && !below)
                {
                    normal = new Vector3(-normal.X, -normal.Y, -normal.Z);
                    constant = -constant;
                    face.Reverse();
                }

                Planes.Add(new Vector4(normal.X, normal.Y, normal.Z, constant));
                AddDirection(FaceNormals, normal);
            }

            foreach (var face in Faces)
            {
                for (var i = 0; i < face.Count; i++)
                {
                    var from = face[i];
                    var to = face[(i + 1) % face.Count];
                    AddDirection(EdgeDirections, Normalized(new Vector3(Vertexes[from].X - Vertexes[to].X, Vertexes[from].Y - Vertexes[to].Y, Vertexes[from].Z - Vertexes[to].Z)));
                    if (!Edges.Any(edge => edge[0] == from && edge[1] == to || edge[0] == to && edge[1] == from))
                    {
                        Edges.Add(new List<Byte> { from, to });
                    }
                }
            }
        }

        /// <summary>
        /// Whether the planes and edges still describe the vertexes and faces: every vertex is inside every plane, every face lies on
        /// one of the planes and the edges are the faces' edges. Vertexes moved or faces changed leave them behind
        /// </summary>
        public Boolean DescribesFaces()
        {
            if (Planes.Count != Faces.Count || Vertexes.Count == 0 || Faces.Any(face => face.Count < 3 || face.Any(index => index >= Vertexes.Count)))
            {
                return false;
            }

            var tolerance = PlaneTolerance * Math.Max(1.0f, Vertexes.Max(vertex => Math.Max(Math.Abs(vertex.X), Math.Max(Math.Abs(vertex.Y), Math.Abs(vertex.Z)))));
            if (Vertexes.Any(vertex => Planes.Any(plane => Distance(plane, vertex) > tolerance)))
            {
                return false;
            }

            if (Faces.Any(face => !Planes.Any(plane => face.All(index => Math.Abs(Distance(plane, Vertexes[index])) <= tolerance))))
            {
                return false;
            }

            var faceEdges = new HashSet<(Byte, Byte)>();
            foreach (var face in Faces)
            {
                for (var i = 0; i < face.Count; i++)
                {
                    faceEdges.Add(Ordered(face[i], face[(i + 1) % face.Count]));
                }
            }

            return Edges.All(edge => edge.Count == 2) && faceEdges.SetEquals(Edges.Select(edge => Ordered(edge[0], edge[1])));
        }

        private static (Byte, Byte) Ordered(Byte a, Byte b)
        {
            return a <= b ? (a, b) : (b, a);
        }

        private static Single Distance(Vector4 plane, Vector4 vertex)
        {
            return plane.X * vertex.X + plane.Y * vertex.Y + plane.Z * vertex.Z + plane.W;
        }

        // The game's plane from a face's first three vertexes: the cross product of its first two edges, one of them or the up axis
        // when they're too short
        private Vector3 FaceNormal(List<Byte> face)
        {
            var a = Vertexes[face[0]];
            var b = Vertexes[face[1]];
            var c = Vertexes[face[2]];
            var first = new Vector3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
            var second = new Vector3(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
            var cross = new Vector3(first.Y * second.Z - first.Z * second.Y, first.Z * second.X - first.X * second.Z, first.X * second.Y - first.Y * second.X);
            const Single tiny = 5e-5f;
            if (Math.Abs(cross.X) <= tiny && Math.Abs(cross.Y) <= tiny && Math.Abs(cross.Z) <= tiny)
            {
                cross = Math.Abs(first.X) <= tiny && Math.Abs(first.Y) <= tiny && Math.Abs(first.Z) <= tiny
                    ? Math.Abs(second.X) <= tiny && Math.Abs(second.Y) <= tiny && Math.Abs(second.Z) <= tiny ? new Vector3(0, 1, 0) : second
                    : first;
            }

            return Normalized(cross);
        }

        private static Vector3 Normalized(Vector3 vector)
        {
            var length = (Single)Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
            return length < 5e-5f ? new Vector3(0, 1, 0) : new Vector3(vector.X / length, vector.Y / length, vector.Z / length);
        }

        private static void AddDirection(List<Vector4> directions, Vector3 direction)
        {
            foreach (var known in directions)
            {
                if (Apart(known, direction.X, direction.Y, direction.Z) < SameDirectionTolerance || Apart(known, -direction.X, -direction.Y, -direction.Z) < SameDirectionTolerance)
                {
                    return;
                }
            }

            directions.Add(new Vector4(direction.X, direction.Y, direction.Z, 1));
        }

        private static Single Apart(Vector4 known, Single x, Single y, Single z)
        {
            return (Single)Math.Sqrt((known.X - x) * (known.X - x) + (known.Y - y) * (known.Y - y) + (known.Z - z) * (known.Z - z));
        }

        public int GetLength()
        {
            return HeaderShorts * 2 + 4 + (Vertexes.Count + Planes.Count + EdgeDirections.Count + FaceNormals.Count) * Constants.SIZE_VECTOR4
                + Faces.Count + Faces.Sum(face => 1 + face.Count) + Edges.Count * 2;
        }

        public void Compile()
        {
        }

        public void Read(BinaryReader reader, int length)
        {
            var header = new UInt16[HeaderShorts];
            for (var i = 0; i < HeaderShorts; ++i)
            {
                header[i] = reader.ReadUInt16();
            }

            reader.ReadInt32(); // Size of the blob
            Vertexes = ReadVectors(reader, header[0]);
            Planes = ReadVectors(reader, header[2]);
            EdgeDirections = ReadVectors(reader, header[3]);
            FaceNormals = ReadVectors(reader, header[4]);
            // Where every face starts among the face bytes, the faces themselves say
            reader.ReadBytes(header[9] - header[8]);
            var faceBytes = reader.ReadBytes(header[10] - header[9]);
            Faces = new List<List<Byte>>();
            for (var offset = 0; offset < faceBytes.Length; offset += 1 + faceBytes[offset])
            {
                Faces.Add(faceBytes.Skip(offset + 1).Take(faceBytes[offset]).ToList());
            }

            Edges = new List<List<Byte>>();
            for (var i = 0; i < header[1]; i++)
            {
                var edge = reader.ReadUInt16();
                Edges.Add(new List<Byte> { (Byte)(edge & 0xFF), (Byte)(edge >> 8) });
            }
        }

        private static List<Vector4> ReadVectors(BinaryReader reader, Int32 count)
        {
            var vectors = new List<Vector4>(count);
            for (var i = 0; i < count; ++i)
            {
                var vector = new Vector4();
                vector.Read(reader, Constants.SIZE_VECTOR4);
                vectors.Add(vector);
            }

            return vectors;
        }

        public void Write(BinaryWriter writer)
        {
            // Edges keep their vertexes' indexes in a byte each and the faces are found by byte offsets
            if (Vertexes.Count > 256)
            {
                throw new InvalidOperationException($"A collision hull has {Vertexes.Count} vertexes, the game's hulls index 256 at most");
            }

            if (Faces.Count > 0 && Faces.Take(Faces.Count - 1).Sum(face => 1 + face.Count) > Byte.MaxValue)
            {
                throw new InvalidOperationException($"A collision hull's {Faces.Count} faces have {Faces.Sum(face => face.Count)} corners, the game finds the faces by byte offsets up to 255");
            }

            var header = new UInt16[HeaderShorts];
            header[0] = (UInt16)Vertexes.Count;
            header[1] = (UInt16)Edges.Count;
            header[2] = (UInt16)Planes.Count;
            header[3] = (UInt16)EdgeDirections.Count;
            header[4] = (UInt16)FaceNormals.Count;
            header[5] = (UInt16)(Vertexes.Count * Constants.SIZE_VECTOR4);
            header[6] = (UInt16)(header[5] + Planes.Count * Constants.SIZE_VECTOR4);
            header[7] = (UInt16)(header[6] + EdgeDirections.Count * Constants.SIZE_VECTOR4);
            header[8] = (UInt16)(header[7] + FaceNormals.Count * Constants.SIZE_VECTOR4);
            header[9] = (UInt16)(header[8] + Faces.Count);
            header[10] = (UInt16)(header[9] + Faces.Sum(face => 1 + face.Count));
            for (var i = 0; i < HeaderShorts; ++i)
            {
                writer.Write(header[i]);
            }

            writer.Write(header[10] + Edges.Count * 2);
            foreach (var vector in Vertexes.Concat(Planes).Concat(EdgeDirections).Concat(FaceNormals))
            {
                vector.Write(writer);
            }

            var offset = 0;
            foreach (var face in Faces)
            {
                writer.Write((Byte)offset);
                offset += 1 + face.Count;
            }

            foreach (var face in Faces)
            {
                writer.Write((Byte)face.Count);
                writer.Write(face.ToArray());
            }

            foreach (var edge in Edges)
            {
                writer.Write((UInt16)(edge[0] | edge[1] << 8));
            }
        }
    }
}
