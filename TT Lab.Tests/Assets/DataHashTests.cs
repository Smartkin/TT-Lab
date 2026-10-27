using System.Text;
using Avalonia.Headless.XUnit;
using Newtonsoft.Json.Linq;
using SharpHash.Checksum;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Tests.Assets;

// The IDs of graphics made of their data (SerializableAsset.GetDataHash) are made of their data files as saved and their salt, whatever is
// loaded: the loaded data's text was hashed while it was loaded and kept for the session, so a build's IDs followed what editors and scenes
// had loaded, and an edit saved kept the ID of before. A material's salt is its URI
[Collection(ProjectCollection.Name)]
public sealed class DataHashTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public DataHashTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    // The CRC32 of the file's bytes and the salt's after them
    private static UInt32 HashOfFile(string path, string salt = "")
    {
        byte[] bytes = [..File.ReadAllBytes(path), ..Encoding.UTF8.GetBytes(salt)];
        return SharpHash.Base.HashFactory.Checksum.CreateCRC(CRCStandard.CRC32).ComputeBytes(bytes).GetUInt32();
    }

    // The asset read again from its files, as nothing has loaded it
    private static T Read<T>(T asset) where T : SerializableAsset, new()
    {
        var read = new T();
        read.Deserialize(File.ReadAllText(Path.Combine(asset.FullPath, $"{asset.Name}.json")));
        return read;
    }

    [Fact]
    public void AnAssetsIdIsOfItsFileLoadedOrNot()
    {
        var material = _assets.AddMaterial("Glass");
        _project.Project.Serialize();
        var file = HashOfFile(material.FullDataPath, material.URI.ToString());

        var unloaded = Read(material);
        Assert.False(((IAsset)unloaded).IsLoaded);
        Assert.Equal(file, ((IAsset)unloaded).GetDataHash());

        var loaded = Read(material);
        var data = ((IAsset)loaded).GetData<MaterialData>();
        Assert.Equal(file, ((IAsset)loaded).GetDataHash());

        // An edit the file doesn't have, which the hash leaves as it is
        var edited = Read(material);
        ((IAsset)edited).GetData<MaterialData>().DmaChainIndex = 7;
        Assert.Equal(file, ((IAsset)edited).GetDataHash());
        Assert.Equal(7U, ((IAsset)edited).GetData<MaterialData>().DmaChainIndex);
        Assert.NotEqual(SerializableAsset.HashOf(data.GetStringified() + material.URI), file);
    }

    // Saved, the asset has the ID of its new file in the same session
    [Fact]
    public void SavingGivesTheIdOfTheNewFile()
    {
        var material = _assets.AddMaterial("Glass");
        material.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        var path = material.FullDataPath;
        // Written a while ago: the hash is kept while the file stays as it is
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-5));
        var before = ((IAsset)material).GetDataHash();
        ((IAsset)material).GetData<MaterialData>().DmaChainIndex = 7;
        Assert.Equal(before, ((IAsset)material).GetDataHash());

        material.Save();

        var after = ((IAsset)material).GetDataHash();
        Assert.NotEqual(before, after);
        Assert.Equal(HashOfFile(path, material.URI.ToString()), after);
    }

    // A file this recent can be written again to as many bytes at the same time, which only its bytes tell: its hash isn't kept
    [Fact]
    public void AFileWrittenAgainAtTheSameTimeIsHashedAgain()
    {
        var material = _assets.AddMaterial("Glass");
        material.Serialize(SerializationFlags.SaveData);
        var path = material.FullDataPath;
        var written = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(path, written);
        var before = ((IAsset)material).GetDataHash();

        var text = File.ReadAllText(path);
        File.WriteAllText(path, text.Replace("\"DmaChainIndex\": 2", "\"DmaChainIndex\": 7"));
        File.SetLastWriteTimeUtc(path, written);

        Assert.NotEqual(text, File.ReadAllText(path));
        Assert.Equal(HashOfFile(path, material.URI.ToString()), ((IAsset)material).GetDataHash());
        Assert.NotEqual(before, ((IAsset)material).GetDataHash());
    }

    // Two materials of byte-identical files are two materials: the parts drawing them are named after their materials' hashes, and a model
    // drawing a copy of another material's settings made in Blender became a part drawing the other one, which lost the copy
    [Fact]
    public void MaterialsOfTheSameFileGetIdsOfTheirOwn()
    {
        var first = _assets.AddMaterial("Glass");
        var second = _assets.AddMaterial("Glass copy");
        ((IAsset)second).GetData<MaterialData>().Name = "Glass";
        _project.Project.Serialize();
        Assert.Equal(File.ReadAllBytes(first.FullDataPath), File.ReadAllBytes(second.FullDataPath));

        Assert.NotEqual(((IAsset)first).GetDataHash(), ((IAsset)second).GetDataHash());
        Assert.Equal(HashOfFile(second.FullDataPath, second.URI.ToString()), ((IAsset)second).GetDataHash());
    }

    // The parts of a picture (a PSM's or a font's PTCs) of the same pixels get IDs of their own, their files' salted with their URIs: the game's
    // HUD icons crashed when they shared them. Salted whether their data is loaded or not, which the build does before it loads them
    [AvaloniaFact]
    public void PartsOfAPictureOfTheSamePixelsGetIdsOfTheirOwn()
    {
        var parts = Enumerable.Range(0, 2).Select(i =>
        {
            var texture = _assets.AddTexture($"Icon {i}_Texture", 0xFF305070);
            texture.Serialize(SerializationFlags.SaveData);
            var material = _assets.AddMaterial($"Icon {i}_Material", texture.URI);
            material.Serialize(SerializationFlags.SaveData);
            var ptc = _project.Add(new PTC { GlobalPath = "Startup" }, $"Icon {i}");
            ptc.SetData(new PTCData(ptc) { TextureID = texture.URI, MaterialID = material.URI });
            return (Texture: (IAsset)texture, Material: (IAsset)material, Ptc: (IAsset)ptc);
        }).ToList();
        _project.Project.Serialize();
        Assert.Equal(File.ReadAllBytes(parts[0].Texture.FullDataPath), File.ReadAllBytes(parts[1].Texture.FullDataPath));

        var built = parts.Select(part => (ITwinPTC)part.Ptc.GetData<PTCData>().Export(_assets.Factory)).ToList();

        Assert.NotEqual(built[0].TexID, built[1].TexID);
        Assert.NotEqual(built[0].MatID, built[1].MatID);
        foreach (var (part, item) in parts.Zip(built))
        {
            Assert.Equal(HashOfFile(part.Texture.FullDataPath, part.Texture.URI.ToString()), item.TexID);
            Assert.Equal(HashOfFile(part.Material.FullDataPath, part.Material.URI.ToString()), item.MatID);
            part.Texture.UnloadData();
            Assert.False(part.Texture.IsLoaded);
            Assert.Equal(item.TexID, part.Texture.GetDataHash());
        }

        // Read again, the salt is set before anything is loaded
        var reopened = Read((Texture)parts[0].Texture);
        reopened.HashSalt = reopened.URI;
        Assert.Equal(built[0].TexID, ((IAsset)reopened).GetDataHash());
    }

    // A chunk's view of a material shares its asset's file, which the chunk's own values aren't in, and an internal asset has no file of its
    // own: their IDs are their data's text
    [Fact]
    public void AViewAndAnInternalAssetHashTheirData()
    {
        var material = _assets.AddMaterial("Glass");
        material.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        var view = AssetOverrides.CreateView(material, new Dictionary<string, JToken> { ["AssetData.DmaChainIndex"] = 7 });
        var viewData = ((IAsset)view).GetData<MaterialData>();
        Assert.Equal(7U, viewData.DmaChainIndex);
        Assert.Equal(SerializableAsset.HashOf(viewData.GetStringified() + material.URI), ((IAsset)view).GetDataHash());
        Assert.Equal(HashOfFile(material.FullDataPath, material.URI.ToString()), ((IAsset)material).GetDataHash());

        var crash = _project.Add(new OGI(), "Crash");
        var rock = new RigidModel { Package = crash.Package, InvariantName = "Rock", Alias = "Rock", IsInternal = true, InternalOwner = crash };
        var rockData = new RigidModelData(rock) { Model = _assets.AddModel("Rock Model").URI, Materials = [material.URI] };
        rock.SetData(rockData);
        var text = SerializableAsset.HashOf(rockData.GetStringified());
        Assert.Equal($"Rock_{text:X}", rock.InvariantName);
        Assert.Equal(text, ((IAsset)rock).GetDataHash());
    }
}
