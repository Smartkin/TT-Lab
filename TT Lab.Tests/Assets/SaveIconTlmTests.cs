using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;

namespace TT_Lab.Tests.Assets;

// The save icon is kept as a TT Lab model file the Blender add-on edits: its mesh upright, its shapes after the first as offsets, the
// texture as the file's material, the animation's keys, and the game's icon, which comes back while the file still has all of it
[Collection(ProjectCollection.Name)]
public sealed class SaveIconTlmTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public SaveIconTlmTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    private static byte[] Bytes(PS2SaveIcon icon) => SaveIconTlm.ToBytes(icon);

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheIconComesBackFromItsFile(bool compressed)
    {
        var original = TestAssets.MakeSaveIcon(compressed);
        var asset = _assets.AddSaveIcon("Crash", original);
        asset.Serialize(SerializationFlags.SaveData);

        Assert.EndsWith(".tlm", asset.Data);
        Assert.Equal(Bytes(original), ((IAsset)asset).GetData<SaveIconData>().ToIco());
    }

    [AvaloniaFact]
    public void TheFileHasTheIconUprightWithItsTextureAndAnimation()
    {
        var file = SaveIconTlm.Write("Crash", TestAssets.MakeSaveIcon());
        var part = (JsonObject)file.Root!["mesh"]!["parts"]![0]!;
        var positions = file.Read<float>(part["position"]);

        Assert.Equal("save_icon", file.Root!.GetKind());
        // 12 corners, the ones the same in everything one vertex
        Assert.Equal(12, file.Read<uint>(part["faces"]).Length);
        Assert.Equal(6, part.GetInt("vertices"));
        // The roof's top (Y -12288 in the icon, which goes down) is 3 units up
        Assert.Equal(3.0f, Enumerable.Range(0, 6).Max(vertex => positions[vertex * 3 + 1]));
        var shape = file.Read<float>(((JsonArray)part["shapes"]!)[0]);
        Assert.Equal(0.5f, Enumerable.Range(0, 6).Max(vertex => shape[vertex * 3 + 1]));
        // Facing the icon's -Z, which is the file's +Z
        Assert.Equal(1.0f, file.Read<float>(part["normal"])[2]);
        var image = file.Read<byte>(((JsonObject)file.Materials[0]!)["image"]!["png"]);
        var (_, width, height) = TextureData.DecodePng(new MemoryStream(image));
        Assert.Equal((128, 128), (width, height));
        var frames = (JsonArray)file.Root["animation"]!["frames"]!;
        Assert.Equal([0f, 0f, 30f, 1f, 60f, 0f], file.Read<float>(frames[1]!["keys"]));
        Assert.Equal(1.5f, file.Root.GetData().GetFloat("AnimationSpeed"));
    }

    // What Blender changed is what the icon gets, the corners keep the order of the file's triangles
    [AvaloniaFact]
    public void EditsOfTheFileMakeANewIcon()
    {
        var original = TestAssets.MakeSaveIcon();
        var file = SaveIconTlm.Write("Crash", original);
        var part = (JsonObject)file.Root!["mesh"]!["parts"]![0]!;
        var positions = file.Read<float>(part["position"]);
        positions[0] += 0.5f;
        part["position"] = file.Write(positions.AsSpan());
        var frames = (JsonArray)file.Root["animation"]!["frames"]!;
        frames[1]!["keys"] = file.Write(new[] { 0f, 0f, 30f, 0.5f, 60f, 0f }.AsSpan());
        var smaller = Enumerable.Repeat(0xFF0000FFu, 64 * 64).ToArray();
        ((JsonObject)file.Materials[0]!)["image"]!["png"] = file.Write(TextureData.EncodePng(smaller, 64, 64).AsSpan());

        var icon = SaveIconTlm.Read(Reload(file));

        // The first vertex is the first and the fourth corner, half a unit is 2048
        Assert.Equal([(short)-2048, (short)-2048], new[] { icon.Vertexes[0].Positions[0], icon.Vertexes[3].Positions[0] });
        Assert.Equal((short)-2048, icon.Vertexes[0].Positions[4]);
        Assert.Equal(original.Vertexes[1].Positions, icon.Vertexes[1].Positions);
        Assert.Equal(0.5f, icon.Frames[1].Keys[1].Value);
        // The picture resized to 128x128, blue
        Assert.All(icon.Texture, texel => Assert.Equal(PS2SaveIcon.FromRgba(0, 0, 255, 255), texel));
    }

    // A file made in Blender has no game's icon, its mesh and picture make one
    [AvaloniaFact]
    public void AFileWithoutTheGamesIconIsAnIcon()
    {
        var original = TestAssets.MakeSaveIcon();
        var file = SaveIconTlm.Write("Crash", original);
        file.Root!.Remove("exact");
        var part = (JsonObject)file.Root["mesh"]!["parts"]![0]!;
        part.Remove("twin_normal");

        var icon = SaveIconTlm.Read(Reload(file));

        Assert.Equal(Bytes(original), Bytes(icon));
    }

    [AvaloniaFact]
    public void BuildingWritesTheIcon()
    {
        var original = TestAssets.MakeSaveIcon(true);
        var asset = _assets.AddSaveIcon("Crash", original);
        var directory = Directory.CreateTempSubdirectory();
        var previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(directory.FullName);
            asset.ExportToFile(_assets.Factory);
            Assert.Equal(Bytes(original), File.ReadAllBytes(Path.Combine(directory.FullName, "Crash.ico")));
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            directory.Delete(true);
        }
    }

    private static TlmFile Reload(TlmFile file)
    {
        using var stream = new MemoryStream();
        file.WriteTo(stream);
        stream.Position = 0;
        return TlmFile.Read(stream);
    }
}
