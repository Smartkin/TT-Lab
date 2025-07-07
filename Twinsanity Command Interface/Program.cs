using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Twinsanity.AgentLab;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.AgentLab.SymbolTable;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity_Command_Interface
{
    class Program
    {
        private class Vertex
        {
            public Vertex()
            {
                Position = new Vector3();
                Color = new Vector4();
                UV = new Vector3();
                Normal = new Vector4();
                EmitColor = new Vector4();
                JointInfo = new VertexJointInfo();
            }
            public Vertex(Vector4 pos) : this()
            {
                Position.X = pos.X;
                Position.Y = pos.Y;
                Position.Z = pos.Z;
            }
            public Vertex(Vector4 pos, Vector4 color) : this(pos)
            {
                Color.X = color.X;
                Color.Y = color.Y;
                Color.Z = color.Z;
                Color.W = color.W;
            }
            public Vertex(Vector4 pos, Vector4 color, Vector4 uv) : this(pos, color)
            {
                UV.X = uv.X;
                UV.Y = uv.Y;
                UV.Z = uv.Z;
            }
            public Vertex(Vector4 pos, Vector4 color, Vector4 uv, Vector4 emitColor) : this(pos, color, uv)
            {
                EmitColor.X = emitColor.X;
                EmitColor.Y = emitColor.Y;
                EmitColor.Z = emitColor.Z;
                EmitColor.W = emitColor.W;
            }
            public Vector3 Position { get; set; }
            public Vector4 Color { get; set; }
            public Vector4 Normal
            {
                get => _normal;
                set
                {
                    _normal = value;
                    HasNormals = true;
                }
            }
            public Vector4 EmitColor { get; set; }
            public Vector3 UV { get; set; }
            public VertexJointInfo JointInfo { get; set; }

            public bool HasNormals { get; private set; }

            private Vector4 _normal;

            public override String ToString()
            {
                var r = (Byte)Math.Round(Color.X * 255.0f);
                var g = (Byte)Math.Round(Color.Y * 255.0f);
                var b = (Byte)Math.Round(Color.Z * 255.0f);
                var a = (Byte)Math.Round(Color.W * 255.0f);
                return $"{Position.X} {Position.Y} {Position.Z} {UV.X} {UV.Y} {UV.Z} {r} {g} {b} {a} {EmitColor.X} {EmitColor.Y} {EmitColor.Z} {EmitColor.W}";
            }
        }

        private class IndexedFace
        {
            public Int32[] Indexes { get; set; }
            public IndexedFace()
            {
                Indexes = null;
            }
            public IndexedFace(Int32[] indexes)
            {
                Indexes = indexes;
            }
            public override String ToString()
            {
                Debug.Assert(Indexes != null, "Indexes must be created at this point of time");

                StringBuilder builder = new();
                builder.Append(Indexes.Length);
                foreach (var index in Indexes)
                {
                    builder.Append($" {index}");
                }
                return builder.ToString();
            }
        }

        static void Main(string[] args)
        {
            /* [GlobalIndex(2)]
             * [InstanceType(Projectile)]
             * behaviour LinearBehaviour {
             *      ActionCall(param_1, param_2);
             *      ActionCall2(param_1);
             *      ActionCall3();
             * }
             */
            var libraryLexer = new AgentLabLexer("""
                                                 [GlobalIndex(0)]
                                                 [InstanceType(Pickup)]
                                                 library MyBehaviourLibrary {
                                                    behaviour MyLinearBehaviour {
                                                        ActionCall1(param_1, param_2);
                                                        ActionCall2(param_1, param_2);
                                                    }
                                                    
                                                    behaviour MyLinearBehaviour2 {
                                                    }
                                                    
                                                    RequiredActionCall();
                                                 }
                                                 """);
            var agentLabLexer = new AgentLabLexer("""
                                                  // This is a comment
                                                  [StartFrom(FirstState)]
                                                  [Priority(100)] // This is another comment
                                                  behaviour COM_MY_BEHAVIOUR {
                                                    // And this is another comment
                                                    const my_const = 10.0 + 12.0;
                                                    const another_const = (10 - 5) * 6 + 0xF1;
                                                    const my_const_bool = false;
                                                    const my_const_string = 'This is a custom string';
                                                    
                                                    starter {
                                                        GlobalObjectId = -1;
                                                    }
                                                    
                                                    [ControlPacket(my_control_packet)]
                                                    state FirstState() {
                                                        if Next(0) >= 0.5 {
                                                            interval = 1.0;
                                                            MakeInert();
                                                            MakeInert();
                                                            MakeInert();
                                                        }
                                                    }
                                                    
                                                    packet my_control_packet {
                                                        settings {
                                                            Stalls = 1;
                                                        }
                                                        data {
                                                            Delay = 1.0;
                                                        }
                                                    }
                                                    
                                                  }
                                                  """);
            // var parser = new AgentLabParser(agentLabLexer);
            // var libraryParser = new AgentLabParser(libraryLexer);
            // var result = parser.Parse();
            // var symbols = new AgentLabSymbolTableBuilder(result, "ActionDefinitionsPs2.lab");
            // var libraryResult = libraryParser.Parse();
            // Console.WriteLine(result.ToString());
            //
            // return;
            using var defaultRm2File = new FileStream(args[0], FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(defaultRm2File);
            var defaultRm2 = new PS2AnyTwinsanityRM2();
            defaultRm2.Read(reader, (Int32)reader.BaseStream.Length);
            var behaviours = defaultRm2.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
            var symbols = new AgentLabSymbolTableBuilder();
            symbols.BuildBuiltInTypes().BuildConditions().BuildActions("ActionDefinitionsPs2.lab");
            for (var i = 0; i < behaviours.GetItemsAmount(); ++i)
            {
                if (behaviours.GetItem(i) is not ITwinAgentLab behaviour)
                {
                    continue;
                }

                IResolver resolver = null;
                if (behaviour is ITwinBehaviourGraph && (behaviours.GetItem(i - 1) is TwinBehaviourStarter))
                {
                    var starter = (TwinBehaviourStarter)behaviours.GetItem(i - 1);
                    resolver = new DefaultStarterAssignerGlobalObjectIdResolversList(starter.Assigners.Select(assigner => new DefaultStarterAssignerGlobalObjectIdResolver(assigner.GlobalObjectId)).Cast<IStarterAssignerGlobalObjectIdResolver>().ToArray());
                }
                var script = AgentLabDecompiler.Decompile(behaviour, resolver);
                Console.WriteLine(script);
                var parser = new AgentLabParser(new AgentLabLexer(script));
                if (behaviour is not TwinBehaviourStarter)
                {
                    var ast = parser.Parse();
                    symbols.BuildFromAst(ast);
                    Console.WriteLine("Parsed and built symbols for behaviour successfully!");
                }
            }

            return;
            /*if (args.Length != 2)
            {
                Console.WriteLine("Must provide a path to the model in the format of FBX and model type (0 for static models, 1 for rigged models). Other formats like OBJ and GLTF aren't tested but could work.");
                Console.WriteLine("Usage: TwinsanityCommandInterface.exe \"C:/Models/TestModel.fbx\"");
                return;
            }
            String modelPath = args[0];
            using var assimp = new AssimpContext();
            var scene = assimp.ImportFile(modelPath);
            var modelType = Int32.Parse(args[1]);
            if (modelType == 0)
            {
                List<List<Vertex>> totalVertexes = new();
                List<List<IndexedFace>> totalFaces = new();
                PS2AnyModel model = new();
                foreach (var mesh in scene.Meshes)
                {
                    var submodel = new List<Vertex>();
                    for (Int32 i = 0; i < mesh.VertexCount; i++)
                    {
                        var vertex = new Vertex(
                            new Vector4(-mesh.Vertices[i].X, mesh.Vertices[i].Y, mesh.Vertices[i].Z, 0.0f),
                            new Vector4(1f, 1f, 1f, 1f),
                            new Vector4(mesh.TextureCoordinateChannels[0][i].X, mesh.TextureCoordinateChannels[0][i].Y, 1.0f, 0.0f)
                            )
                        {
                            Normal = new Vector4(-mesh.Normals[i].X, mesh.Normals[i].Y, mesh.Normals[i].Z, 0f),
                            EmitColor = new Vector4(1f, 1f, 1f, 1f)
                        };
                        submodel.Add(vertex);
                    }

                    var faces = new List<IndexedFace>();
                    for (var i = 0; i < mesh.FaceCount; ++i)
                    {
                        faces.Add(new IndexedFace(mesh.Faces[i].Indices.ToArray()));
                    }

                    totalVertexes.Add(submodel);
                    totalFaces.Add(faces);
                }

                var vertexBatchIndex = 0;
                foreach (var faces in totalFaces)
                {
                    var submodel = new PS2SubModel
                    {
                        UnusedBlob = Array.Empty<Byte>(),
                        Vertexes = new(),
                        UVW = new(),
                        Colors = new(),
                        EmitColor = new(),
                        Normals = new(),
                        Connection = new()
                    };
                    var vertexBatch = totalVertexes[vertexBatchIndex++];
                    var i = 0;
                    foreach (var face in faces)
                    {
                        i %= TwinVIFCompiler.VertexBatchAmount;

                        var idx0 = (i % 2 == 0) ? 0 : 1;
                        var idx1 = (i % 2 == 0) ? 1 : 0;
                        submodel.Vertexes.Add(new Vector4(vertexBatch[face.Indexes[idx0]].Position, 1f));
                        submodel.Vertexes.Add(new Vector4(vertexBatch[face.Indexes[idx1]].Position, 1f));
                        submodel.Vertexes.Add(new Vector4(vertexBatch[face.Indexes[2]].Position, 1f));
                        submodel.UVW.Add(new Vector4(vertexBatch[face.Indexes[idx0]].UV, 0f));
                        submodel.UVW.Add(new Vector4(vertexBatch[face.Indexes[idx1]].UV, 0f));
                        submodel.UVW.Add(new Vector4(vertexBatch[face.Indexes[2]].UV, 0f));
                        submodel.Colors.Add(vertexBatch[face.Indexes[idx0]].Color);
                        submodel.Colors.Add(vertexBatch[face.Indexes[idx1]].Color);
                        submodel.Colors.Add(vertexBatch[face.Indexes[2]].Color);
                        submodel.Normals.Add(vertexBatch[face.Indexes[idx0]].Normal);
                        submodel.Normals.Add(vertexBatch[face.Indexes[idx1]].Normal);
                        submodel.Normals.Add(vertexBatch[face.Indexes[2]].Normal);
                        submodel.EmitColor.Add(vertexBatch[face.Indexes[idx0]].EmitColor);
                        submodel.EmitColor.Add(vertexBatch[face.Indexes[idx1]].EmitColor);
                        submodel.EmitColor.Add(vertexBatch[face.Indexes[2]].EmitColor);
                        submodel.Connection.Add(false);
                        submodel.Connection.Add(false);
                        submodel.Connection.Add(true);

                        ++i;
                    }
                    model.SubModels.Add(submodel);
                }

                using var ps2Model = File.Create(Directory.GetCurrentDirectory() + "/compiled_ps2_model");
                using var writer = new BinaryWriter(ps2Model);
                model.Write(writer);
                writer.Flush();
                ps2Model.Flush();
                writer.Close();
            }
            else
            {
                List<List<Vertex>> totalVertexes = new();
                List<List<IndexedFace>> totalFaces = new();
                PS2AnySkin skin = new();

                foreach (var mesh in scene.Meshes)
                {
                    var submodel = new List<Vertex>();

                    // Convert bone -> vertex index to vertex -> bone id
                    List<Dictionary<int, (int, float)>> vertexToBone = new();
                    for (Int32 i = 0; i < mesh.Bones.Count; i++)
                    {
                        var bone = mesh.Bones[i];
                        vertexToBone.Add(new());
                        foreach (var vertexWeight in bone.VertexWeights)
                        {
                            vertexToBone[^1].Add(vertexWeight.VertexID, new(i, vertexWeight.Weight));
                        }
                    }

                    for (Int32 i = 0; i < mesh.VertexCount; i++)
                    {
                        var vertex = new Vertex(
                            new Vector4(-mesh.Vertices[i].X, mesh.Vertices[i].Y, mesh.Vertices[i].Z, 0.0f),
                            new Vector4(1f, 1f, 1f, 1f),
                            new Vector4(mesh.TextureCoordinateChannels[0][i].X, mesh.TextureCoordinateChannels[0][i].Y, 1.0f, 0.0f)
                            )
                        {
                            JointInfo = new VertexJointInfo()
                            {
                                Connection = false
                            }
                        };

                        List<(int, float)> joints = new();
                        foreach (var vertexMap in vertexToBone)
                        {
                            if (vertexMap.ContainsKey(i))
                            {
                                joints.Add(vertexMap[i]);
                            }
                        }
                        if (joints.Count == 0)
                        {
                            Console.WriteLine("Vertex must be connected to at least 1 joint");
                            return;
                        }
                        if (joints.Count > 3)
                        {
                            Console.WriteLine("Vertex can only have up to 3 joints connected to it");
                            return;
                        }
                        vertex.JointInfo.JointIndex1 = joints[0].Item1;
                        vertex.JointInfo.Weight1 = joints[0].Item2;
                        if (joints.Count >= 2)
                        {
                            vertex.JointInfo.JointIndex2 = joints[1].Item1;
                            vertex.JointInfo.Weight2 = joints[1].Item2;
                        }
                        if (joints.Count == 3)
                        {
                            vertex.JointInfo.JointIndex3 = joints[2].Item1;
                            vertex.JointInfo.Weight3 = joints[2].Item2;
                        }
                        submodel.Add(vertex);
                    }

                    var faces = new List<IndexedFace>();
                    for (var i = 0; i < mesh.FaceCount; ++i)
                    {
                        faces.Add(new IndexedFace(mesh.Faces[i].Indices.ToArray()));
                    }

                    totalVertexes.Add(submodel);
                    totalFaces.Add(faces);
                }

                var vertexBatchIndex = 0;
                foreach (var faces in totalFaces)
                {
                    var subskin = new PS2SubSkin
                    {
                        Vertexes = new(),
                        UVW = new(),
                        Colors = new(),
                        SkinJoints = new(),
                        Material = 0xB37BBFB3,
                    };
                    var vertexBatch = totalVertexes[vertexBatchIndex++];
                    var i = 0;
                    foreach (var face in faces)
                    {
                        i %= TwinVIFCompiler.VertexBatchAmount;

                        var idx0 = (i % 2 == 0) ? 0 : 1;
                        var idx1 = (i % 2 == 0) ? 1 : 0;
                        subskin.Vertexes.Add(new Vector4(vertexBatch[face.Indexes[idx0]].Position, 1f));
                        subskin.Vertexes.Add(new Vector4(vertexBatch[face.Indexes[idx1]].Position, 1f));
                        subskin.Vertexes.Add(new Vector4(vertexBatch[face.Indexes[2]].Position, 1f));
                        subskin.UVW.Add(new Vector4(vertexBatch[face.Indexes[idx0]].UV, 0f));
                        subskin.UVW.Add(new Vector4(vertexBatch[face.Indexes[idx1]].UV, 0f));
                        subskin.UVW.Add(new Vector4(vertexBatch[face.Indexes[2]].UV, 0f));
                        subskin.Colors.Add(vertexBatch[face.Indexes[idx0]].Color);
                        subskin.Colors.Add(vertexBatch[face.Indexes[idx1]].Color);
                        subskin.Colors.Add(vertexBatch[face.Indexes[2]].Color);
                        subskin.SkinJoints.Add(vertexBatch[face.Indexes[idx0]].JointInfo);
                        subskin.SkinJoints.Add(vertexBatch[face.Indexes[idx1]].JointInfo);
                        vertexBatch[face.Indexes[2]].JointInfo.Connection = true;
                        subskin.SkinJoints.Add(vertexBatch[face.Indexes[2]].JointInfo);

                        ++i;
                    }
                    skin.SubSkins.Add(subskin);
                }

                using var ps2Skin = File.Create(Directory.GetCurrentDirectory() + "/compiled_ps2_skin");
                using var writer = new BinaryWriter(ps2Skin);
                skin.Write(writer);
                writer.Flush();
                ps2Skin.Flush();
                writer.Close();
            }*/
        }
    }
}
