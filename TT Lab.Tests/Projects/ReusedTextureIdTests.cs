using Avalonia.Headless.XUnit;
using Newtonsoft.Json.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static TT_Lab.Tests.Support.TestGeometry;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Projects;

// The game reuses texture IDs for other pictures in other chunks, under a material, an OGI, its parts and an object other chunks have the same
// of (43 of the PAL version's chunks have their own BAC3703A, and a disc TT Lab built has a material the same in every chunk, a chunk's own
// picture of the ID under it): unpacking resolved only what a chunk named that no earlier chunk had the same of, never saw a later chunk's
// own picture, and the chunk built with the first chunk's. What an item names is the chunk's whatever is the same above it, and every chunk
// of a picture another chunk has another one of gets its materials linked to one variant of the picture: the chunks that found it by its
// content, and the chunks whose texture is the same picture with other leftovers (a texture of other settings keeps a variant of its own)
[Collection(ProjectCollection.Name)]
public sealed class ReusedTextureIdTests : IDisposable
{
    private const UInt16 ObjectId = 0x10;
    private const UInt16 OgiId = 0x20;
    private const UInt32 RigidModelId = 0x30;
    private const UInt32 ModelId = 0x40;
    private const UInt32 MaterialId = 0x50;
    private const UInt32 TextureId = 0x60;

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    // A level's resource file of an object drawn by an OGI of one rigid model, whose material draws a picture of one color: the same items in
    // every chunk but the picture, the texture's slot in the tools' memory and its format
    private static ITwinSection Chunk(Byte red, Byte blue, UInt32 toolSlot = 0, ITwinTexture.TexturePixelFormat format = ITwinTexture.TexturePixelFormat.PSMT8)
    {
        var chunk = new PS2ItemFactory().GenerateRM();
        var graphics = chunk.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var texture = new PS2AnyTexture();
        // Colors of their own: the picture's are halved in place for the GS
        texture.FromBitmap(Enumerable.Range(0, 16 * 16).Select(_ => new Color(red, 0, blue, 255)).ToList(), 16, ITwinTexture.TextureFunction.MODULATE, format);
        texture.Leftovers = new TwinTextureLeftovers { ToolSlot = toolSlot };
        texture.SetID(TextureId);
        graphics.GetItem<ITwinSection>(Constants.GRAPHICS_TEXTURES_SECTION).AddItem(texture);
        var material = new PS2AnyMaterial { Name = "lambert50", Shaders = [new TwinShader { ShaderType = TwinShader.Type.StandardUnlit, TextureId = TextureId }] };
        material.SetID(MaterialId);
        graphics.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION).AddItem(material);
        var model = new PS2AnyModel { SubModels = [RigidSubModel(new Random(31), TwinVifPadding.QuadWord, [12], false)] };
        model.Compile();
        model.SetID(ModelId);
        graphics.GetItem<ITwinSection>(Constants.GRAPHICS_MODELS_SECTION).AddItem(model);
        var rigidModel = new PS2AnyRigidModel { Model = ModelId, Materials = [MaterialId] };
        rigidModel.SetID(RigidModelId);
        graphics.GetItem<ITwinSection>(Constants.GRAPHICS_RIGID_MODELS_SECTION).AddItem(rigidModel);

        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var ogi = new PS2AnyOGI
        {
            Joints =
            [
                new TwinJoint { Index = 0, ParentIndex = 255, Id = 255, LocalTranslation = new Vector4(0, 0, 0, 1), LocalRotation = new Vector4(0, 0, 0, 1),
                    WorldTranslation = new Vector4(0, 0, 0, 1), UnusedRotation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) }
            ],
            RigidModelIds = [RigidModelId],
            JointIndices = [0],
            SkinInverseBindMatrices = [new Matrix4()]
        };
        ogi.SetID(OgiId);
        code.GetItem<ITwinSection>(Constants.CODE_OGIS_SECTION).AddItem(ogi);
        var gameObject = new PS2AnyObject
        {
            Name = "HUD", RefOGIs = [OgiId], FloatProperties = [1], IntProperties = [0, 255], BehaviourPack = new PS2BehaviourCommandPack()
        };
        gameObject.SetID(ObjectId);
        code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION).AddItem(gameObject);

        var collision = new PS2AnyCollisionData { Version = PS2AnyCollisionData.GameVersion };
        collision.SetID(Constants.LEVEL_COLLISION_ITEM);
        chunk.AddItem(collision);
        var particles = new PS2AnyParticleData();
        particles.SetID(Constants.LEVEL_PARTICLES_ITEM);
        chunk.AddItem(particles);
        return chunk;
    }

    // The first hub and the fourth have the red picture, the others the blue one, the fifth's texture of another slot, the sixth's of 32 bits.
    // Unpacked, written and merged the way creating a project does it, the PS2 package's folder
    private string Unpack()
    {
        var chunks = new (string Path, ITwinSection Chunk)[]
        {
            ("Levels\\Earth\\Hub\\huba.rm2", Chunk(255, 0)),
            ("Levels\\Earth\\Hub\\hubb.rm2", Chunk(0, 255)),
            ("Levels\\Earth\\Hub\\hubc.rm2", Chunk(0, 255)),
            ("Levels\\Earth\\Hub\\hubd.rm2", Chunk(255, 0)),
            ("Levels\\Earth\\Hub\\hube.rm2", Chunk(0, 255, 7)),
            ("Levels\\Earth\\Hub\\hubf.rm2", Chunk(0, 255, format: ITwinTexture.TexturePixelFormat.PSMCT32))
        };
        var files = chunks.Select(chunk =>
        {
            var bytes = Serialize(chunk.Chunk);
            return new TT_Lab.Project.Project.DiscFile(chunk.Path, () => bytes);
        }).ToList();
        var project = _project.Project;
        project.GlobalPackagePS2.Enabled = true;
        project.Ps2Package.Enabled = true;
        using var gate = new MemoryGate(1L << 30);
        var writer = new CreationWriter(project.AssetManager, gate);

        project.UnpackAssets(GamePlatform.PS2, files, project.GlobalPackagePS2, project.Ps2Package, gate);
        writer.ImportAndWrite();
        writer.RemoveInternalAssets();
        new VariantMerger(project.AssetManager, _project.AssetsPath).Merge();
        project.Serialize(writer.IsWritten);

        return Path.Combine(_project.AssetsPath, project.Ps2Package.Name);
    }

    private static JObject ReadJson(string file) => JObject.Parse(File.ReadAllText(file));

    // The chunk's own values of each asset by the asset's URI, as written
    private static Dictionary<string, JObject> OverridesOf(string package, string chunk)
    {
        var file = Directory.EnumerateFiles(package, $"{chunk}.json", SearchOption.AllDirectories).Single();
        return ((JArray)ReadJson(file)[nameof(LevelChunk.Overrides)]!).Cast<JObject>()
            .ToDictionary(entry => (string)entry[nameof(AssetOverride.Asset)]!["_uri"]!, entry => (JObject)entry[nameof(AssetOverride.Values)]!);
    }

    [AvaloniaFact]
    public void EveryChunkOfAPictureOfAReusedIdDrawsItsOwn()
    {
        var package = Unpack();

        // The red picture is the texture of the ID, the blue one the variant of the first hub having it, and of its own settings the sixth's
        var textures = Directory.EnumerateFiles(Path.Combine(package, "Texture"), "*.json", SearchOption.AllDirectories)
            .Select(file => (File: file, Metadata: ReadJson(file))).Where(texture => (UInt32)texture.Metadata[nameof(IAsset.ID)]! == TextureId)
            .ToDictionary(texture => (string?)texture.Metadata[nameof(IAsset.Variation)] ?? string.Empty);
        Assert.Equal(["", "levels_earth_hub_hubb", "levels_earth_hub_hubf"], textures.Keys.Order());
        Assert.Equal(File.ReadAllBytes(Path.ChangeExtension(textures["levels_earth_hub_hubb"].File, ".png")),
            File.ReadAllBytes(Path.ChangeExtension(textures["levels_earth_hub_hubf"].File, ".png")));
        var variantUri = (string)textures["levels_earth_hub_hubb"].Metadata[nameof(IAsset.URI)]!["_uri"]!;
        var material = (string)ReadJson(Directory.EnumerateFiles(Path.Combine(package, "Material"), "*.json", SearchOption.AllDirectories).Single())[nameof(IAsset.URI)]!["_uri"]!;

        // Every blue hub draws the material with it: the second found the variant by its content, the third's variant is merged into it
        foreach (var hub in new[] { "hubb", "hubc", "hube" })
        {
            var overrides = OverridesOf(package, hub);
            Assert.True(overrides.TryGetValue(material, out var values), $"{hub} has no values of the material");
            Assert.Contains(values!.Descendants().OfType<JValue>(), value => value.Type == JTokenType.String && (string)value! == variantUri);
        }

        Assert.Empty(OverridesOf(package, "huba"));
        Assert.Empty(OverridesOf(package, "hubd"));
        Assert.Contains((string)textures["levels_earth_hub_hubf"].Metadata[nameof(IAsset.URI)]!["_uri"]!, OverridesOf(package, "hubf")[material].ToString());
    }
}
