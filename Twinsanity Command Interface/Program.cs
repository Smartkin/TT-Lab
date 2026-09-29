using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Twinsanity.AgentLab;
using Twinsanity.AgentLab.AgentLabObjectDescs.PS2;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Compiler;
using Twinsanity.AgentLab.Resolvers.Decompiler;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;
using Twinsanity.AgentLab.SymbolTable;
using Twinsanity.Libraries;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using DefaultGraphResolver = Twinsanity.AgentLab.Resolvers.Decompiler.DefaultGraphResolver;

namespace Twinsanity_Command_Interface;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "psmdiff")
        {
            PsmDiff(args[1], args[2]);
            return;
        }

        if (args.Length >= 3 && args[0] == "psfdiff")
        {
            PsfDiff(args[1], args[2]);
            return;
        }

        if (args.Length >= 3 && args[0] == "psmdump")
        {
            PsmDump(args[1], int.Parse(args[2]));
            return;
        }

        if (args.Length >= 2 && args[0] == "dupes")
        {
            Dupes(args[1]);
            return;
        }

        if (args.Length >= 2 && args[0] == "psfdump")
        {
            var psf = new PS2PSF();
            using (var fontReader = new BinaryReader(File.OpenRead(args[1])))
            {
                psf.Read(fontReader, (int)fontReader.BaseStream.Length);
            }

            Console.WriteLine($"space {psf.SpaceIdentifier} characters {psf.CharacterData.Count} pages {psf.FontPages.Count}");
            for (var i = 0; i < psf.CharacterData.Count; i++)
            {
                var c = psf.CharacterData[i];
                var code = psf.SpaceIdentifier + i;
                Console.WriteLine($"{code} 0x{code:X2} '{(code is >= 0x20 and < 0x7F or >= 0xA0 ? (char)code : '?')}' page {c.FontPageSpecifier} uv {c.PageUv.X},{c.PageUv.Y} size {c.Size.X}x{c.Size.Y}");
            }

            return;
        }

        if (args.Length >= 2 && args[0] == "gfxlist")
        {
            GfxList(args[1]);
            return;
        }

        if (args.Length >= 2 && args[0] == "bvhstats")
        {
            BvhStatsOf(args[1]);
            return;
        }

        if (args.Length >= 2 && args[0] == "neardupes")
        {
            NearDupes(args[1]);
            return;
        }

        if (args.Length >= 4 && args[0] == "graphdump")
        {
            GraphDump(args[1], args[2], args.Skip(3).ToArray());
            return;
        }

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
        // var test = Vector4.GetCosSin(0x7B7A);
        // var reconvert = Vector4.GetAngle(0, 0);
        // var result = new Vector4(0xFFFF851D, 0xFFFFEB90, 0x7B7A);
        // Console.WriteLine($"Result quat {result}");
        using var defaultRm2File = new FileStream(args[0], FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(defaultRm2File);
        var moddedModel = new PS2AnyModel();
        moddedModel.Read(reader, (Int32)reader.BaseStream.Length);
        foreach (var subModel in moddedModel.SubModels)
        {
            subModel.CalculateData();
            for (var i = 0; i < subModel.Vertexes.Count; i++)
            {
                Console.WriteLine($"Vertex {i}: {subModel.Vertexes[i]}");
                // Console.WriteLine($"Color {i}: {subModel.Colors[i]}");
                // Console.WriteLine($"UV {i}: {subModel.UVW[i]}");
                if (subModel.Normals.Count == subModel.Vertexes.Count)
                {
                    Console.WriteLine($"Normal {i}: {subModel.Normals[i]}");
                }
                //
                // if (subModel.EmitColor.Count == subModel.Vertexes.Count)
                // {
                //     Console.WriteLine($"Emit Color {i}: {subModel.EmitColor[i]}");
                // }
                //
                // Console.WriteLine($"{i} Part of strip {subModel.Connection[i]}");
            }
        }
        
        using var defaultRm2File2 = new FileStream(args[1], FileMode.Open, FileAccess.Read);
        using var reader2 = new BinaryReader(defaultRm2File2);
        var vanillaModel = new PS2AnyModel();
        vanillaModel.Read(reader2, (Int32)reader2.BaseStream.Length);
        foreach (var subModel in vanillaModel.SubModels)
        {
            subModel.CalculateData();
            for (var i = 0; i < subModel.Vertexes.Count; i++)
            {
                // Console.WriteLine($"Vertex {i}: {subModel.Vertexes[i]}");
                // Console.WriteLine($"Color {i}: {subModel.Colors[i]}");
                // Console.WriteLine($"UV {i}: {subModel.UVW[i]}");
                if (subModel.Normals.Count == subModel.Vertexes.Count)
                {
                    Console.WriteLine($"Normal {i}: {subModel.Normals[i]}");
                }
                //
                // if (subModel.EmitColor.Count == subModel.Vertexes.Count)
                // {
                //     Console.WriteLine($"Emit Color {i}: {subModel.EmitColor[i]}");
                // }
                //
                // Console.WriteLine($"{i} Part of strip {subModel.Connection[i]}");
            }
        }
        return;
        // var behaviours = frontend.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION);
        // var symbols = new AgentLabSymbolTableBuilder();
        // symbols.BuildBuiltInTypes().BuildConditions().BuildActions("ActionDefinitionsPs2.lab");
        // var compilerGraphResolver = new Twinsanity.AgentLab.Resolvers.Compiler.DefaultGraphResolver();
        // for (var i = 0; i < behaviours.GetItemsAmount(); ++i)
        // {
        //     if (behaviours.GetItem(i) is not ITwinAgentLab behaviour)
        //     {
        //         continue;
        //     }
        //
        //     IResolver resolver = null;
        //     if (behaviour is ITwinBehaviourGraph graph && (behaviours.GetItem(i - 1) is TwinBehaviourStarter))
        //     {
        //         compilerGraphResolver.AddNewGraphRef(graph.Name, (short)graph.GetID());
        //         var starter = (TwinBehaviourStarter)behaviours.GetItem(i - 1);
        //         var globalObjectIdResolver = new DefaultStarterAssignerGlobalObjectIdResolversList(starter.Assigners.Select(assigner => new DefaultStarterAssignerGlobalObjectIdResolver(assigner.RefListIndex)).Cast<IStarterAssignerGlobalObjectIdResolver>().ToArray());
        //         var stateList = new List<IStateResolver>();
        //         for (var j = 0; j < graph.ScriptStates.Count; j++)
        //         {
        //             stateList.Add(new DefaultStateResolver(graph.GetName()));
        //         }
        //         var stateResolver = new DefaultStateResolversList(stateList.ToArray());
        //         resolver = new DefaultGraphResolver(new DefaultStarterResolver(starter, globalObjectIdResolver), stateResolver);
        //     }
        //     var script = AgentLabDecompiler.Decompile(behaviour, resolver);
        //     using var scriptFs = new FileStream(((ITwinItem)behaviour).GetName() + ".lab", FileMode.Create, FileAccess.Write);
        //     using var writer = new StreamWriter(scriptFs);
        //     writer.Write(script);
        //     if (behaviour is not TwinBehaviourStarter)
        //     {
        //         var compilerOptions = new AgentLabCompiler.CompilerOptions
        //         {
        //             Command = new PS2CommandDesc(),
        //             State = new PS2StateDesc(),
        //             StateBody = new PS2StateBodyDesc(),
        //             ActionDefinitionsFile = "ActionDefinitionsPs2.lab",
        //             Graph = new PS2GraphDesc(),
        //             CommandPack = new PS2CommandPackDesc(),
        //             CommandsSequence = new PS2CommandsSequenceDesc(),
        //             Resolver = new DefaultCompilerResolver(compilerGraphResolver, new DefaultGlobalObjectIdResolver())
        //         };
        //         var recompiledBehaviour = AgentLabCompiler.Compile(script, compilerOptions);
        //         if (recompiledBehaviour.CompilerStatus.IsError)
        //         {
        //             Console.WriteLine($"Compilation error: {recompiledBehaviour.CompilerStatus.Message}");
        //         }
        //         else
        //         {
        //             Console.WriteLine("Behaviour recompiled successfully!");
        //         }
        //     }
        // }

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

    // Compares two PSM files (Startup/Icons.psm of the disc and of a build) part by part: texture headers, texture data, the decoded
    // pixels (the same, flipped, or re-palettized) and the materials
    static void PsmDiff(string leftPath, string rightPath)
    {
        static PS2PSM Load(string path)
        {
            var psm = new PS2PSM();
            using var reader = new BinaryReader(File.OpenRead(path));
            psm.Read(reader, (int)reader.BaseStream.Length);
            return psm;
        }

        static byte[] Bytes(ITwinSerializable item)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            item.Write(writer);
            writer.Flush();
            return stream.ToArray();
        }

        var left = Load(leftPath);
        var right = Load(rightPath);
        Console.WriteLine($"PTCs {left.PTCs.Count} vs {right.PTCs.Count}");
        for (var i = 0; i < Math.Min(left.PTCs.Count, right.PTCs.Count); i++)
        {
            ComparePtc(i, (PS2PTC)left.PTCs[i], (PS2PTC)right.PTCs[i]);
        }
    }

    static byte[] ItemBytes(ITwinSerializable item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    static void ComparePtc(int i, PS2PTC a, PS2PTC b)
    {
        var ta = (PS2AnyTexture)a.Texture;
        var tb = (PS2AnyTexture)b.Texture;
        var headerA = ItemBytes(ta).Take(100).ToArray();
        var headerB = ItemBytes(tb).Take(100).ToArray();
        var header = headerA.SequenceEqual(headerB) ? "same" : "DIFFERS at " + string.Join(",", headerA.Select((v, k) => (v, k)).Where(p => p.v != headerB[p.k]).Select(p => p.k.ToString("X")));
        var data = ta.TextureData.SequenceEqual(tb.TextureData) ? "same" : $"differs ({ta.TextureData.Zip(tb.TextureData).Count(p => p.First != p.Second)} of {ta.TextureData.Length} bytes)";
        ta.CalculateData();
        tb.CalculateData();
        var w = 1 << ta.ImageWidthPower;
        var h = 1 << ta.ImageHeightPower;
        var pa = ta.Colors.Select(c => c.ToARGB()).ToArray();
        var pb = tb.Colors.Select(c => c.ToARGB()).ToArray();
        string pixels;
        if (pa.Length != pb.Length)
        {
            pixels = $"count {pa.Length} vs {pb.Length}";
        }
        else if (pa.SequenceEqual(pb))
        {
            pixels = "same";
        }
        else
        {
            var flippedV = Enumerable.Range(0, pa.Length).All(k => pa[k] == pb[(h - 1 - k / w) * w + k % w]);
            var flippedH = Enumerable.Range(0, pa.Length).All(k => pa[k] == pb[k / w * w + (w - 1 - k % w)]);
            var differing = Enumerable.Range(0, pa.Length).Count(k => pa[k] != pb[k]);
            var rgbSame = Enumerable.Range(0, pa.Length).All(k => (pa[k] & 0xFFFFFF) == (pb[k] & 0xFFFFFF));
            var alphaSame = Enumerable.Range(0, pa.Length).All(k => (pa[k] >> 24) == (pb[k] >> 24));
            pixels = $"{differing} of {pa.Length} differ, flippedV {flippedV}, flippedH {flippedH}, rgb same {rgbSame}, alpha same {alphaSame}, distinct {pa.Distinct().Count()} vs {pb.Distinct().Count()}";
        }

        // The GS memory image itself, color by color through each file's own palette: the same picture laid out the same way in memory
        // gives the same colors at every address whatever order the palettes have
        string layout;
        try
        {
            var rawA = RawImage(ta);
            var rawB = RawImage(tb);
            var paletteA = Palette(ta, rawA);
            var paletteB = Palette(tb, rawB);
            var imageBytes = Math.Min(ta.ClutBufferBasePointer * 256, Math.Min(rawA.Length, rawB.Length));
            var differing = 0;
            var firstDifference = -1;
            for (var k = 0; k < imageBytes; k++)
            {
                if (paletteA[rawA[k]] != paletteB[rawB[k]])
                {
                    if (firstDifference < 0)
                    {
                        firstDifference = k;
                    }

                    differing++;
                }
            }

            layout = differing == 0 ? "same" : $"{differing} of {imageBytes} addresses differ, first at {firstDifference:X}";
        }
        catch (Exception exception)
        {
            layout = "unknown: " + exception.Message;
        }

        var ma = ItemBytes(a.Material);
        var mb = ItemBytes(b.Material);
        var material = ma.SequenceEqual(mb) ? "same" : $"differs ({(ma.Length == mb.Length ? ma.Zip(mb).Count(p => p.First != p.Second) + " bytes at " + string.Join(",", ma.Select((v, k) => (v, k)).Where(p => p.v != mb[p.k]).Select(p => $"{p.k:X}:{p.v:X2}->{mb[p.k]:X2}")) : $"length {ma.Length} vs {mb.Length}")})";
        Console.WriteLine($"[{i}] tex {a.TexID:X}/{b.TexID:X} {w}x{h} fmt {ta.TextureFormat}/{tb.TextureFormat} mips {ta.MipLevels}/{tb.MipLevels} header {header}, data {data}, pixels {pixels}, gs layout {layout}; mat {a.MatID:X}/{b.MatID:X} '{((PS2AnyMaterial)a.Material).Name}' {material}");
        if (header != "same")
        {
            Console.WriteLine($"    reserved2 {BitConverter.ToString(ta.Reserved2)}->{BitConverter.ToString(tb.Reserved2)} tbp {ta.TextureBasePointer}->{tb.TextureBasePointer} tbw {ta.TextureBufferWidth}->{tb.TextureBufferWidth} cbp {ta.ClutBufferBasePointer}->{tb.ClutBufferBasePointer}");
            Console.WriteLine($"    mipTBP {string.Join(",", ta.MipLevelsTBP)} -> {string.Join(",", tb.MipLevelsTBP)}");
            Console.WriteLine($"    mipTBW {string.Join(",", ta.MipLevelsTBW)} -> {string.Join(",", tb.MipLevelsTBW)}");
            Console.WriteLine($"    sizeWords {BitConverter.ToString(ta.SizeWords)}->{BitConverter.ToString(tb.SizeWords)} blocks {BitConverter.ToString(ta.ReservedBlocks)}->{BitConverter.ToString(tb.ReservedBlocks)} meta {BitConverter.ToString(ta.UnusedMetadata)}->{BitConverter.ToString(tb.UnusedMetadata)}");
        }
    }

    static void PsfDiff(string leftPath, string rightPath)
    {
        static PS2PSF Load(string path)
        {
            var psf = new PS2PSF();
            using var reader = new BinaryReader(File.OpenRead(path));
            psf.Read(reader, (int)reader.BaseStream.Length);
            return psf;
        }

        var left = Load(leftPath);
        var right = Load(rightPath);
        Console.WriteLine($"space {left.SpaceIdentifier} vs {right.SpaceIdentifier}, characters {left.CharacterData.Count} vs {right.CharacterData.Count}, pages {left.FontPages.Count} vs {right.FontPages.Count}");
        var differing = 0;
        for (var i = 0; i < Math.Min(left.CharacterData.Count, right.CharacterData.Count); i++)
        {
            var a = left.CharacterData[i];
            var b = right.CharacterData[i];
            if (a.PageUv.X != b.PageUv.X || a.PageUv.Y != b.PageUv.Y || a.Size.X != b.Size.X || a.Size.Y != b.Size.Y || a.FontPageSpecifier != b.FontPageSpecifier)
            {
                if (differing++ < 5)
                {
                    Console.WriteLine($"  char {i}: page {a.FontPageSpecifier}->{b.FontPageSpecifier} uv ({a.PageUv.X},{a.PageUv.Y})->({b.PageUv.X},{b.PageUv.Y}) size ({a.Size.X},{a.Size.Y})->({b.Size.X},{b.Size.Y})");
                }
            }
        }

        Console.WriteLine($"  {differing} characters differ");
        for (var i = 0; i < Math.Min(left.FontPages.Count, right.FontPages.Count); i++)
        {
            ComparePtc(i, (PS2PTC)left.FontPages[i], (PS2PTC)right.FontPages[i]);
        }
    }

    // The texture's GS memory image: the tags' transfer written into memory the way the game's upload does
    static byte[] RawImage(PS2AnyTexture texture)
    {
        var interpreter = VIFInterpreter.InterpretCode(texture.TextureData);
        var data = interpreter.GetGifMem();
        var gifData = EzSwizzle.TagToBytes(data[1]);
        var rrw = (int)((data[0].Data[1].Output >> 0) & 0xFFFFFFFF);
        var rrh = (int)((data[0].Data[1].Output >> 32) & 0xFFFFFFFF);
        return EzSwizzle.writeTexPSMCT32(0, 1, 0, 0, rrw, rrh, gifData);
    }

    static uint[] Palette(PS2AnyTexture texture, byte[] raw)
    {
        var paletteData = EzSwizzle.readTexPSMCT32(texture.ClutBufferBasePointer, 1, 0, 0, 16, 16, raw, false);
        var palette = EzSwizzle.BytesToColors(paletteData);
        for (var i = 0; i < 8; i++)
        {
            for (var j = 8; j < 16; j++)
            {
                (palette[j + i * 32], palette[j + i * 32 + 8]) = (palette[j + i * 32 + 8], palette[j + i * 32]);
            }
        }

        return palette.Select(c => c.ToARGB()).ToArray();
    }

    // The whole GS memory region up to the palette of a PSM's texture as rows of the declared width, every address through the palette:
    // what's beyond the declared height is what the tools left there
    static void PsmDump(string path, int index)
    {
        var psm = new PS2PSM();
        using (var reader = new BinaryReader(File.OpenRead(path)))
        {
            psm.Read(reader, (int)reader.BaseStream.Length);
        }

        var ptc = (PS2PTC)psm.PTCs[index];
        var texture = (PS2AnyTexture)ptc.Texture;
        var raw = RawImage(texture);
        var palette = Palette(texture, raw);
        var width = 1 << texture.ImageWidthPower;
        var height = 1 << texture.ImageHeightPower;
        var rows = texture.ClutBufferBasePointer * 256 / width;
        Console.WriteLine($"{((PS2AnyMaterial)ptc.Material).Name}: {width}x{height} declared, tbw {texture.TextureBufferWidth}, cbp {texture.ClutBufferBasePointer}, {rows} rows of memory before the palette, raw {raw.Length} bytes");
        var pixels = EzSwizzle.readTexPSMT8(0, texture.TextureBufferWidth, 0, 0, width, rows, raw, false);
        for (var y = 0; y < rows; y++)
        {
            var line = new StringBuilder();
            for (var x = 0; x < width; x++)
            {
                var color = palette[pixels[y * width + x]];
                var alpha = color >> 24;
                var luma = ((color >> 16 & 0xFF) + (color >> 8 & 0xFF) + (color & 0xFF)) / 3;
                line.Append(alpha == 0 ? '.' : luma > 170 ? '#' : luma > 85 ? '+' : ':');
            }

            Console.WriteLine($"{y,3} {line}");
        }
    }

    // Writes the decompiled scripts of the chunk's behaviour graphs of the IDs into the folder, <file name>_<id>.lab each, to diff them
    static void GraphDump(string chunkPath, string outputFolder, string[] idsOrNames)
    {
        var names = idsOrNames.Where(value => value.StartsWith("COM_")).ToHashSet();
        var ids = idsOrNames.Where(value => !value.StartsWith("COM_")).Select(id => Convert.ToUInt32(id, 16)).ToArray();
        var data = File.ReadAllBytes(chunkPath);
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        var root = new PS2AnyTwinsanityRM2();
        root.Read(reader, data.Length);
        var behaviours = root.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
        Directory.CreateDirectory(outputFolder);
        var prefix = Path.GetFileNameWithoutExtension(chunkPath);
        for (var i = 0; i < behaviours.GetItemsAmount(); i++)
        {
            if (behaviours.GetItem(i) is not ITwinBehaviourGraph graph || (!ids.Contains(graph.GetID()) && !names.Contains(graph.Name)))
            {
                continue;
            }

            IResolver resolver = null;
            if (i > 0 && behaviours.GetItem(i - 1) is TwinBehaviourStarter starter)
            {
                var globalObjectIdResolver = new DefaultStarterAssignerGlobalObjectIdResolversList(starter.Assigners.Select(assigner => new DefaultStarterAssignerGlobalObjectIdResolver(assigner.RefListIndex)).Cast<IStarterAssignerGlobalObjectIdResolver>().ToArray());
                var stateList = new List<IStateResolver>();
                for (var j = 0; j < graph.ScriptStates.Count; j++)
                {
                    stateList.Add(new DefaultStateResolver(graph.GetName()));
                }

                resolver = new DefaultGraphResolver(new DefaultStarterResolver(starter, globalObjectIdResolver), new DefaultStateResolversList(stateList.ToArray()));
            }

            var script = AgentLabDecompiler.Decompile(graph, resolver);
            File.WriteAllText(Path.Combine(outputFolder, $"{prefix}_{graph.GetID():X}.lab"), script);
            Console.WriteLine($"{graph.GetID():X} {graph.Name}: {graph.ScriptStates.Count} states, {script.Length} chars");
        }
    }

    // How many items of each graphics section of a chunk have the same bytes as another one (their IDs aside): what a build that keeps
    // one asset per content merges
    // The game's collision tree of a chunk against the one TT Lab builds of the same triangles, under the game's ray walk
    static void BvhStatsOf(string chunkPath)
    {
        var data = File.ReadAllBytes(chunkPath);
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        var root = new PS2AnyTwinsanityRM2();
        root.Read(reader, data.Length);
        var collision = root.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData>(Constants.LEVEL_COLLISION_ITEM);
        var faces = collision.Triangles.Select(triangle => new TT_Lab.AssetData.Instance.Collision.BvhBuilder.Face(triangle.Vector1Index, triangle.Vector2Index, triangle.Vector3Index)).ToList();
        var retail = TT_Lab.AssetData.Instance.Collision.BvhBuilder.FromTwin(collision.Triggers, collision.Groups, collision.Triangles.Count);
        Console.WriteLine($"{Path.GetFileName(chunkPath)}: {collision.Triangles.Count} triangles, {collision.Vectors.Count} vertexes");
        Console.WriteLine($"  game:   {TT_Lab.AssetData.Instance.Collision.BvhStats.Measure(retail, faces, collision.Vectors)}");
        foreach (var (strategy, leaf) in new[]
                 {
                     (TT_Lab.AssetData.Instance.Collision.BvhBuilder.Strategy.MedianWidestAxis, 30),
                     (TT_Lab.AssetData.Instance.Collision.BvhBuilder.Strategy.MedianBestAxis, 30),
                     (TT_Lab.AssetData.Instance.Collision.BvhBuilder.Strategy.MedianBestAxis, 16),
                     (TT_Lab.AssetData.Instance.Collision.BvhBuilder.Strategy.Sah, 30),
                     (TT_Lab.AssetData.Instance.Collision.BvhBuilder.Strategy.Sah, 16),
                     (TT_Lab.AssetData.Instance.Collision.BvhBuilder.Strategy.Sah, 8),
                 })
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var ours = TT_Lab.AssetData.Instance.Collision.BvhBuilder.Build(faces, collision.Vectors, leaf, strategy);
            var built = watch.Elapsed;
            Console.WriteLine($"  {strategy,-16} {leaf,2}: {TT_Lab.AssetData.Instance.Collision.BvhStats.Measure(ours, faces, collision.Vectors)} (built in {built.TotalMilliseconds:F0} ms)");
        }
    }

    static void Dupes(string chunkPath)
    {
        var data = File.ReadAllBytes(chunkPath);
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        var root = new PS2AnyTwinsanityRM2();
        root.Read(reader, data.Length);
        var graphics = root.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        for (var s = 0; s < graphics.GetItemsAmount(); s++)
        {
            if (graphics.GetItem(s) is not ITwinSection section)
            {
                continue;
            }

            var contents = new Dictionary<string, List<uint>>();
            for (var i = 0; i < section.GetItemsAmount(); i++)
            {
                var item = section.GetItem(i);
                var bytes = ItemBytes(item);
                var key = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(bytes));
                if (!contents.TryGetValue(key, out var ids))
                {
                    contents[key] = ids = new List<uint>();
                }

                ids.Add(item.GetID());
            }

            var duplicates = contents.Values.Where(ids => ids.Count > 1).ToList();
            Console.WriteLine($"section {graphics.GetItem(s).GetID():X} ({section.GetItemsAmount()} items, {contents.Count} distinct): {string.Join(" ", duplicates.Select(ids => string.Join("=", ids.Select(id => id.ToString("X")))))}");
        }
    }

    // Pairs of items of a chunk's graphics sections whose bytes differ in only a few places: what a build merges when it tells them
    // apart by less than their bytes
    static void NearDupes(string chunkPath)
    {
        var data = File.ReadAllBytes(chunkPath);
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        var root = new PS2AnyTwinsanityRM2();
        root.Read(reader, data.Length);
        var graphics = root.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        for (var s = 0; s < graphics.GetItemsAmount(); s++)
        {
            if (graphics.GetItem(s) is not ITwinSection section)
            {
                continue;
            }

            var items = Enumerable.Range(0, section.GetItemsAmount()).Select(section.GetItem).Select(item => (Item: item, Bytes: ItemBytes(item))).ToList();
            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    var a = items[i].Bytes;
                    var b = items[j].Bytes;
                    if (a.Length != b.Length)
                    {
                        continue;
                    }

                    var differing = Enumerable.Range(0, a.Length).Where(k => a[k] != b[k]).ToList();
                    if (differing.Count == 0 || differing.Count > 80)
                    {
                        continue;
                    }

                    var name = items[i].Item is PS2AnyMaterial material ? $" '{material.Name}'/'{((PS2AnyMaterial)items[j].Item).Name}'" : "";
                    Console.WriteLine($"section {graphics.GetItem(s).GetID():X}: {items[i].Item.GetID():X} vs {items[j].Item.GetID():X}{name} ({a.Length} bytes) differ at {string.Join(",", differing.Take(24).Select(k => $"{k:X}:{a[k]:X2}/{b[k]:X2}"))}{(differing.Count > 24 ? $" ... {differing.Count} bytes" : "")}");
                }
            }
        }
    }

    // Every item of the chunk's graphics sections with its length and, for materials, its name and texture, to match up the items of a
    // build with the disc's when their IDs differ
    static void GfxList(string chunkPath)
    {
        var data = File.ReadAllBytes(chunkPath);
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        var root = new PS2AnyTwinsanityRM2();
        root.Read(reader, data.Length);
        var graphics = root.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        for (var s = 0; s < graphics.GetItemsAmount(); s++)
        {
            if (graphics.GetItem(s) is not ITwinSection section)
            {
                continue;
            }

            for (var i = 0; i < section.GetItemsAmount(); i++)
            {
                var item = section.GetItem(i);
                var extra = item switch
                {
                    PS2AnyMaterial material => $" '{material.Name}' dma {material.DmaChainIndex} flags {material.ActivatedShaders} shaders {material.Shaders.Count} tex {string.Join("/", material.Shaders.Select(shader => shader.TextureId.ToString("X")))}",
                    PS2AnyTexture texture => $" {1 << texture.ImageWidthPower}x{1 << texture.ImageHeightPower} {texture.TextureFormat}/{texture.DestinationTextureFormat} mips {texture.MipLevels} tbw {texture.TextureBufferWidth} cbp {texture.ClutBufferBasePointer} fun {texture.TexFun} data {texture.TextureData.Length}",
                    PS2AnyRigidModel rigid => $" model {rigid.Model:X} materials {string.Join("/", rigid.Materials.Select(id => id.ToString("X")))}",
                    _ => ""
                };
                Console.WriteLine($"{graphics.GetItem(s).GetID():X} {item.GetID():X} {item.GetType().Name} {item.GetLength()}{extra}");
            }
        }
    }
}
