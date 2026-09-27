using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetResolvers;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Tests.Assets;

[Collection(ProjectCollection.Name)]
public sealed class ResolverTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    // Model files refer to the project's materials, so a level chunk's materials and their textures have to be saved with the project.
    // They used to be internal, living on only in the glb files of the models using them
    [AvaloniaFact]
    public void LevelMaterialsAndTexturesAreSavedWithTheProject()
    {
        var graphics = ReadGraphics(0x10, 0x20);
        var materials = graphics.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        var chunk = new BaseTwinSection();
        chunk.AddItem(graphics);
        var resolver = new MaterialResolver(new TextureResolver(false), false);

        resolver.CreateAssetFromId(chunk, materials, _project.Project.Ps2Package, 0x20);
        resolver.FinalizeResolve();
        var material = _project.AssetManager.GetAllAssetsOf<Material>().Single(asset => asset.ID == 0x20);
        var texture = _project.AssetManager.GetAllAssetsOf<Texture>().Single(asset => asset.ID == 0x10);
        texture.Import();
        material.Import();
        _project.Project.Serialize();

        Assert.Contains(material, _project.AssetManager.GetAssets());
        Assert.Contains(texture, _project.AssetManager.GetAssets());
        Assert.False(material.IsInternal);
        Assert.False(texture.IsInternal);
        Assert.Equal(texture.URI, ((MaterialData)material.GetData()).Shaders.Single().TextureId);
        Assert.True(File.Exists(material.FullDataPath), "The material's data wasn't saved");
        Assert.True(File.Exists(texture.FullDataPath), "The texture's image wasn't saved");
    }

    // Skies use the same texture IDs for other pictures (AltEarth's sun isn't the one of the other skies), each sky's materials have to
    // get the texture of their own sky. A third sky's picture used to get lost and every material got the first sky's
    [AvaloniaFact]
    public void SkiesKeepTheirOwnPictureOfATextureIdOthersUseToo()
    {
        var resolver = new SkydomeResolver();
        for (var sky = 0u; sky < 3; sky++)
        {
            var model = new PS2AnyModel();
            model.SetID(0x30 + sky);
            var mesh = new PS2AnyMesh { Materials = [0x20 + sky], Model = 0x30 + sky };
            mesh.SetID(0x40 + sky);
            var skydome = new PS2AnySkydome { Meshes = [0x40 + sky] };
            skydome.SetID(0x50 + sky);
            var graphics = CreateGraphics(Constants.SCENERY_GRAPHICS_SECTION, 0x10, 0x20 + sky, Red(sky));
            graphics.AddItem(Section(new PS2AnyModelsSection(), Constants.GRAPHICS_MODELS_SECTION, model));
            graphics.AddItem(Section(new PS2AnyMeshesSection(), Constants.GRAPHICS_MESHES_SECTION, mesh));
            graphics.AddItem(Section(new PS2AnySkydomesSection(), Constants.GRAPHICS_SKYDOMES_SECTION, skydome));
            var chunk = new BaseTwinSection();
            chunk.AddItem(ReadBack(graphics));

            var skydomes = chunk.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_SKYDOMES_SECTION);
            resolver.CreateAssetFromId(chunk, skydomes, _project.Project.Ps2Package, 0x50 + sky);
        }

        resolver.FinalizeResolve();

        var textures = _project.AssetManager.GetAllAssetsOf<Texture>().Where(asset => asset.ID == 0x10).ToList();
        Assert.Equal(3, textures.Count);
        textures.ForEach(texture => texture.Import());
        for (var sky = 0u; sky < 3; sky++)
        {
            var material = _project.AssetManager.GetAllAssetsOf<Material>().Single(asset => asset.ID == 0x20 + sky);
            material.Import();
            var texture = _project.AssetManager.GetAsset<Texture>(((MaterialData)material.GetData()).Shaders.Single().TextureId);
            // Converting the colors for the PS2 rounds them a little
            Assert.InRange((Int32)(((TextureData)texture.GetData()).GetPixels()[0] >> 16 & 0xFF), Red(sky) - 4, Red(sky) + 4);
        }

        static Byte Red(UInt32 sky) => (Byte)(0x40 * (sky + 1));
    }

    private static ITwinSection ReadGraphics(UInt32 textureId, UInt32 materialId)
    {
        return ReadBack(CreateGraphics(Constants.LEVEL_GRAPHICS_SECTION, textureId, materialId, 0));
    }

    // One texture with its pixels' red at the given value and a material using it
    private static PS2AnyGraphicsSection CreateGraphics(UInt32 sectionId, UInt32 textureId, UInt32 materialId, Byte red)
    {
        var texture = new PS2AnyTexture();
        texture.FromBitmap(Enumerable.Range(0, 16 * 16).Select(i => new Color(red, (Byte)i, 0, 255)).ToList(), 16, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.PSMT8);
        texture.SetID(textureId);
        var material = new PS2AnyMaterial { Name = "lambert1", Shaders = [new TwinShader { TextureId = textureId, TxtMapping = TwinShader.TextureMapping.ON }] };
        material.SetID(materialId);
        var graphics = new PS2AnyGraphicsSection();
        graphics.SetID(sectionId);
        graphics.AddItem(Section(new PS2AnyTexturesSection(), Constants.GRAPHICS_TEXTURES_SECTION, texture));
        graphics.AddItem(Section(new PS2AnyMaterialsSection(), Constants.GRAPHICS_MATERIALS_SECTION, material));
        return graphics;
    }

    private static ITwinSection Section(BaseTwinSection section, UInt32 id, ITwinItem item)
    {
        section.SetID(id);
        section.AddItem(item);
        return section;
    }

    // Read back like a chunk, which is what gives items their hashes
    private static ITwinSection ReadBack(PS2AnyGraphicsSection graphics)
    {
        graphics.Compile();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        graphics.Write(writer);
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        var read = new PS2AnyGraphicsSection();
        read.SetID(graphics.GetID());
        read.Read(reader, (Int32)stream.Length);
        return read;
    }
}
