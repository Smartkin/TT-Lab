using System.Text.Json.Nodes;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Extensions;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.Util;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static TT_Lab.Tests.Support.TestGeometry;
using Scenery = TT_Lab.Assets.Instance.Scenery;

namespace TT_Lab.Tests.Projects;

// Creating a project writes every asset as soon as it's imported and lets go of its data, internal assets once no write uses them
[Collection(ProjectCollection.Name)]
public sealed class CreationWriterTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    // Resolvers make a model internal and give it the game's item, like creating a project does
    private Model AddInternalModel(UInt32 id, int vertexes = 12)
    {
        var original = new PS2AnyModel { SubModels = [RigidSubModel(new Random(31), TwinVifPadding.QuadWord, [vertexes], false)] };
        original.Compile();
        var item = Deserialize(new PS2AnyModel(), Serialize(original));
        var model = new Model(_project.Project.Ps2Package.URI, false, string.Empty, id, $"Model {id:X}", item) { IsInternal = true };
        _project.AssetManager.AddAsset(model);
        return model;
    }

    private RigidModel AddRigidModel(string name, UInt32 id, UInt32 model, bool isInternal = false)
    {
        var rigidModel = new RigidModel(_project.Project.Ps2Package.URI, false, string.Empty, id, name, new PS2AnyRigidModel { Model = model, Materials = [] })
        {
            IsInternal = isInternal
        };
        _project.AssetManager.AddAsset(rigidModel);
        return rigidModel;
    }

    private RigidModel AddInternalRigidModel(UInt32 id, UInt32 model) => AddRigidModel($"Rigid Model {id:X}", id, model, true);

    private Mesh AddInternalMesh(UInt32 id, UInt32 model)
    {
        var mesh = new Mesh(_project.Project.Ps2Package.URI, false, string.Empty, id, $"Mesh {id:X}", new PS2AnyMesh { Model = model, Materials = [] }) { IsInternal = true };
        _project.AssetManager.AddAsset(mesh);
        return mesh;
    }

    // An OGI of one joint with the rigid models on it, the disc's item
    private OGI AddOgi(string name, UInt32 id, params UInt32[] rigidModels)
    {
        var item = new PS2AnyOGI
        {
            Joints =
            [
                new TwinJoint { Index = 0, ParentIndex = 255, Id = 255, LocalTranslation = new Vector4(0, 0, 0, 1), LocalRotation = new Vector4(0, 0, 0, 1),
                    WorldTranslation = new Vector4(0, 0, 0, 1), UnusedRotation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) }
            ],
            RigidModelIds = [..rigidModels],
            JointIndices = [..rigidModels.Select(_ => (Byte)0)]
        };
        item.SetID(id);
        var ogi = new OGI(_project.Project.Ps2Package.URI, false, string.Empty, id, name, item);
        _project.AssetManager.AddAsset(ogi);
        return ogi;
    }

    // A scenery of the disc placing the meshes side by side, a mesh as often as it's given
    private Scenery AddScenery(string name, UInt32 id, params UInt32[] meshes)
    {
        var placements = meshes.Select((_, i) => new SceneryPlacement
        {
            Matrix = System.Numerics.Matrix4x4.CreateTranslation(i * 10 - 40, 0, 0).ToTwin(),
            Box = new BoundingBox { V1 = new Vector4(-1, -1, -1, 2), V2 = new Vector4(1, 1, 1, 0) }
        }).ToList();
        var tree = SceneryTree.Build(new SceneryTree.Box(new System.Numerics.Vector3(-100), new System.Numerics.Vector3(100)), 2, placements, [],
            new Boolean[SceneryData.MaxLights], placement => meshes[placements.IndexOf(placement)]);
        var item = new PS2AnyScenery { Name = name, Sceneries = tree };
        item.SetID(id);
        var scenery = new Scenery(_project.Project.Ps2Package.URI, id, name, "levels/test", item, LabURI.Empty);
        _project.AssetManager.AddAsset(scenery);
        return scenery;
    }

    // The vertexes of every mesh the owner's model file has, in the file's order
    private static List<int> WrittenMeshVertexes(IAsset owner)
    {
        var file = TlmFile.Load(owner.FullDataPath);
        return file.Root.Traverse().Where(node => node[TlmNodes.MeshKey] is JsonObject)
            .Select(node => TlmMeshes.ReadMesh(file, (JsonObject)node[TlmNodes.MeshKey]!, false).Sum(part => part.Part.Vertexes.Count)).ToList();
    }

    // The vertexes of the one mesh the asset's model file has (a model creation kept in its file has it until the internal assets go)
    private static int WrittenVertexCount(IAsset asset) => Assert.Single(WrittenMeshVertexes(asset));

    // A gate that never has room: one piece of work runs at a time, the rest wait at it
    private sealed class NoRoom : MemoryGate.IProbe
    {
        public long WorkingSet => 1L << 40;

        public long Milliseconds => 0;

        public void Collect()
        {
        }
    }

    private static MemoryGate GateWithoutRoom() => new(1L << 30, 0.6, new NoRoom(), false);

    // An asset whose import waits until it's let go of: the first one through the gate keeps its one place
    private sealed class Held(ManualResetEventSlim release, ManualResetEventSlim importing) : SerializableAsset
    {
        public override UInt32 Section => 0;

        public override Type GetEditorType() => throw new NotSupportedException();

        public override AbstractAssetData GetData() => new HeldData(this);

        public override void Import()
        {
            importing.Set();
            release.Wait();
        }

        public override void Serialize(SerializationFlags serializationFlags = SerializationFlags.None)
        {
        }
    }

    private sealed class HeldData(IAsset owner) : AbstractAssetData(owner)
    {
        protected override void Dispose(bool disposing)
        {
        }

        public override void Import(LabURI package, string? variant, int? layoutId)
        {
        }

        public override ITwinItem Export(ITwinItemFactory factory) => throw new NotSupportedException();
    }

    // Creating a project writes its assets in parallel, every write waiting at the memory gate while there's no room. Unbounded, the pool took
    // the writes waiting for starved and added a thread every half a second, and a PS2+Xbox creation stalled for good with 1,730 threads:
    // however many threads the pool has to spare, the writes waiting stay below the cores' count
    [Fact]
    public async Task CreationsWorkWaitingAtAGateWithNoRoomStaysBelowTheCoresCount()
    {
        var cores = Environment.ProcessorCount;
        ThreadPool.GetMinThreads(out var workers, out var completion);
        // Threads to spare right away: unbounded, every write would take one and wait
        ThreadPool.SetMinThreads(Math.Max(workers, 4 * cores), completion);
        using var release = new ManualResetEventSlim();
        using var importing = new ManualResetEventSlim();
        try
        {
            for (var i = 0; i < 2 * cores; i++)
            {
                _project.Add(new Held(release, importing), $"Held {i}", (UInt32)i);
            }

            using var writes = GateWithoutRoom();
            var writer = new CreationWriter(_project.AssetManager, writes);
            var writing = Task.Run(writer.ImportAndWrite);
            // A held import keeps the gate's one place, the other writes wait at it
            Assert.True(importing.Wait(TimeSpan.FromSeconds(30)));
            for (var tries = 0; tries < 1000 && writes.MostWaiting < cores - 1; tries++)
            {
                await Task.Delay(10);
            }

            // Time for more to come and wait, which they would without a bound
            await Task.Delay(300);
            release.Set();
            await writing.WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(cores - 1, writes.MostWaiting);
        }
        finally
        {
            release.Set();
            ThreadPool.SetMinThreads(workers, completion);
        }
    }

    // One write at a time, the second one needs the model after the first let go of it
    [Fact]
    public void InternalAssetsUsedByTwoWritesComeBackForTheSecond()
    {
        var model = AddInternalModel(0x20);
        var first = AddRigidModel("First", 0x10, 0x20);
        var second = AddRigidModel("Second", 0x11, 0x20);
        using var gate = new MemoryGate(1);
        var writer = new CreationWriter(_project.AssetManager, gate);

        writer.ImportAndWrite();

        Assert.True(writer.IsWritten(first));
        Assert.True(writer.IsWritten(second));
        Assert.False(writer.IsWritten(model));
        Assert.True(WrittenVertexCount(first) > 0);
        Assert.Equal(WrittenVertexCount(first), WrittenVertexCount(second));
        Assert.True(File.Exists(model.FullDataPath), "The model wasn't kept for the second write");

        writer.RemoveInternalAssets();

        Assert.False(_project.AssetManager.DoesAssetExist(model.URI));
        Assert.False(File.Exists(model.FullDataPath));
        Assert.True(File.Exists(first.FullDataPath));
    }

    // A game TT Lab built has one model of identical ones (the build merges them), drawn by every mesh that drew one of them. The scenery's
    // write lets go of the model after the first mesh drawn with it and keeps the next mesh in its file after that: the mesh needs the
    // model again, which is read back from the file it was kept in. It was loaded the way an asset's own data loads, named after its empty
    // data and looked for in a file nothing wrote, and creating a project from TT Lab's own build failed every time
    [Theory]
    [InlineData(new UInt32[] { 0x30, 0x31 })]
    [InlineData(new UInt32[] { 0x31, 0x30 })]
    [InlineData(new UInt32[] { 0x30, 0x32, 0x31, 0x30 })]
    [InlineData(new UInt32[] { 0x32, 0x31, 0x32, 0x30 })]
    public void MeshesOfASceneryDrawnWithOneModelAreAllWritten(UInt32[] placed)
    {
        var models = new Dictionary<UInt32, Model> { [0x30] = AddInternalModel(0x20), [0x32] = AddInternalModel(0x21, 9) };
        models[0x31] = models[0x30];
        AddInternalMesh(0x30, 0x20);
        AddInternalMesh(0x31, 0x20);
        AddInternalMesh(0x32, 0x21);
        var scenery = AddScenery("Scenery", 0x50, placed);
        using var gate = new MemoryGate(1);
        var writer = new CreationWriter(_project.AssetManager, gate);

        writer.ImportAndWrite();

        Assert.True(writer.IsWritten(scenery));
        var expected = placed.Select(mesh => WrittenVertexCount(models[mesh])).ToList();
        Assert.Equal(expected, WrittenMeshVertexes(scenery));
        writer.RemoveInternalAssets();
        Assert.All(models.Values, model => Assert.False(File.Exists(model.FullDataPath)));
    }

    // An OGI's rigid models drawn with one model, the same in an OGI's write
    [Fact]
    public void RigidModelsOfAnOgiDrawnWithOneModelAreAllWritten()
    {
        var shared = AddInternalModel(0x20);
        var other = AddInternalModel(0x21, 9);
        AddInternalRigidModel(0x40, 0x20);
        AddInternalRigidModel(0x41, 0x21);
        AddInternalRigidModel(0x42, 0x20);
        var ogi = AddOgi("Crate", 0x60, 0x40, 0x41, 0x42);
        using var gate = new MemoryGate(1);
        var writer = new CreationWriter(_project.AssetManager, gate);

        writer.ImportAndWrite();

        Assert.Equal([WrittenVertexCount(shared), WrittenVertexCount(other), WrittenVertexCount(shared)], WrittenMeshVertexes(ogi));
        writer.RemoveInternalAssets();
    }

    // OGIs drawn with one model through rigid models of their own and through rigid models they share: whichever OGI is written first, every
    // one of their bodies has the model
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OgisDrawnWithOneModelAreWrittenWhicheverComesFirst(bool reversed)
    {
        var model = AddInternalModel(0x20);
        AddInternalRigidModel(0x40, 0x20);
        AddInternalRigidModel(0x41, 0x20);
        (string Name, UInt32 Id, UInt32[] Bodies)[] owners = [("First", 0x60, [0x40]), ("Second", 0x61, [0x41]), ("Both", 0x62, [0x41, 0x40])];
        var ogis = (reversed ? owners.Reverse() : owners).Select(owner => AddOgi(owner.Name, owner.Id, owner.Bodies)).ToList();
        using var gate = new MemoryGate(1);
        var writer = new CreationWriter(_project.AssetManager, gate);

        writer.ImportAndWrite();

        var vertexes = WrittenVertexCount(model);
        Assert.Equal([[vertexes], [vertexes], [vertexes, vertexes]], ogis.OrderBy(ogi => ogi.ID).Select(WrittenMeshVertexes));
        writer.RemoveInternalAssets();
    }

    // A gate that always has room: the writes run side by side, as many as there are cores
    private sealed class Roomy : MemoryGate.IProbe
    {
        public long WorkingSet => 0;

        public long Milliseconds => Environment.TickCount64;

        public void Collect()
        {
        }
    }

    // Sceneries and OGIs sharing models every way they can, written side by side in another order every round: their parts are let go of
    // and read back in whatever order the writes finish in
    [Fact]
    public void OwnersSharingModelsAreWrittenSideBySide()
    {
        using var gate = new MemoryGate(1L << 40, 0.6, new Roomy(), false);
        var writer = new CreationWriter(_project.AssetManager, gate);
        var random = new Random(7);
        for (var round = 0; round < 6; round++)
        {
            var id = (UInt32)(0x1000 * (round + 1));
            // Two meshes and two rigid models drawn with the first model, one of each with the second
            var shared = AddInternalModel(id + 0x20);
            var other = AddInternalModel(id + 0x21, 9);
            foreach (var (part, model) in new[] { (0x30u, 0x20u), (0x31u, 0x20u), (0x32u, 0x21u) })
            {
                AddInternalMesh(id + part, id + model);
                AddInternalRigidModel(id + part + 0x10, id + model);
            }

            var owners = new List<(IAsset Owner, UInt32[] Parts)>();
            var adding = new List<Action>();
            for (var i = 0; i < 8; i++)
            {
                var parts = Enumerable.Range(0, random.Next(1, 5)).Select(_ => 0x30u + (UInt32)random.Next(3)).ToArray();
                var name = $"Round {round} Owner {i}";
                var ownerId = id + 0x100 + (UInt32)i;
                adding.Add(i % 2 == 0
                    ? () => owners.Add((AddScenery(name, ownerId, parts.Select(part => id + part).ToArray()), parts))
                    : () => owners.Add((AddOgi(name, ownerId, parts.Select(part => id + part + 0x10).ToArray()), parts)));
            }

            foreach (var add in adding.OrderBy(_ => random.Next()))
            {
                add();
            }

            writer.ImportAndWrite();

            var vertexes = new[] { WrittenVertexCount(shared), WrittenVertexCount(other) };
            Assert.All(owners, owner => Assert.Equal(owner.Parts.Select(part => vertexes[part == 0x32 ? 1 : 0]), WrittenMeshVertexes(owner.Owner)));
            writer.RemoveInternalAssets();
        }
    }
}
