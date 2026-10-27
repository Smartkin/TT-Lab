using System.Text.Json.Nodes;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static TT_Lab.Tests.Support.TestGeometry;

namespace TT_Lab.Tests.Projects;

// Creating a project writes every asset as soon as it's imported and lets go of its data, internal assets once no write uses them
[Collection(ProjectCollection.Name)]
public sealed class CreationWriterTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    // Resolvers make a model internal and give it the game's item, like creating a project does
    private Model AddInternalModel(UInt32 id)
    {
        var original = new PS2AnyModel { SubModels = [RigidSubModel(new Random(31), TwinVifPadding.QuadWord, [12], false)] };
        original.Compile();
        var item = Deserialize(new PS2AnyModel(), Serialize(original));
        var model = new Model(_project.Project.Ps2Package.URI, false, string.Empty, id, $"Model {id:X}", item) { IsInternal = true };
        _project.AssetManager.AddAsset(model);
        return model;
    }

    private RigidModel AddRigidModel(string name, UInt32 id, UInt32 model)
    {
        var rigidModel = new RigidModel(_project.Project.Ps2Package.URI, false, string.Empty, id, name, new PS2AnyRigidModel { Model = model, Materials = [] });
        _project.AssetManager.AddAsset(rigidModel);
        return rigidModel;
    }

    private static int WrittenVertexCount(IAsset asset)
    {
        var file = TlmFile.Load(asset.FullDataPath);
        return TlmMeshes.ReadMesh(file, file.Root![TlmNodes.MeshKey] as JsonObject, false).Sum(part => part.Part.Vertexes.Count);
    }

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
}
