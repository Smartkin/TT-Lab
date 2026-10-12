using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Tests.Assets;

// StandardLit and the shaders drawing its color light a rigid part by its vertexes' emit colors, which a mesh made in Blender has none of:
// the game lights it with what the model drawn before it left there. TT Lab warns when the model's file is read and when it's built
[Collection(ProjectCollection.Name)]
public sealed class LitPartsTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public LitPartsTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    // The test model's rigid model has a part with emit colors and one without
    private OGI OgiDrawnWith(Material material)
    {
        var ogi = _assets.AddOgi();
        var rigidModel = AssetManager.Get().GetAssetData<RigidModelData>(((IAsset)ogi).GetData<OGIData>().RigidModelIds[0]);
        rigidModel.Materials = rigidModel.Materials.Select(_ => material.URI).ToList();
        return ogi;
    }

    // Like a build writes a chunk: in a data scope of its own, which keeps the data the assets have
    private static void Build(IAsset asset, ITwinItemFactory build, string chunk)
    {
        var factory = build.ForChunk();
        factory.ChunkPath = chunk;
        using var scope = new AssetDataScope();
        asset.ResolveChunkResources(factory, factory.GenerateRM().GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_OGIS_SECTION));
    }

    [Fact]
    public void PartsDrawnLitWithoutEmitColorsAreWarnedAbout()
    {
        var ogi = OgiDrawnWith(_assets.AddMaterial("Shiny"));

        var warning = Assert.Single(((IAsset)ogi).GetData<OGIData>().PartsLitWithoutEmitColors());

        Assert.StartsWith("Crash's part drawn with Shiny has no emit colors, which its StandardLit shader lights", warning);
        // And how to fix it in Blender
        Assert.Contains("give the mesh drawn with Shiny a color attribute named EmitColor", warning);
        Assert.Contains("Add Emit Colors", warning);
        Assert.Contains("Standard Unlit", warning);
    }

    // Opening the model reads its file, the parts of its rigid models made again of it
    [Fact]
    public void ModelsReadFromTheirFileAreChecked()
    {
        var ogi = OgiDrawnWith(_assets.AddMaterial("Shiny"));

        var data = _assets.Reload<OGIData>(ogi);

        Assert.True(AssetManager.Get().GetAsset(data.RigidModelIds[0]).IsInternal);
        Assert.StartsWith("Crash's part drawn with Shiny has no emit colors", Assert.Single(data.PartsLitWithoutEmitColors()));
    }

    [Fact]
    public void UnlitPartsNeedNoEmitColors()
    {
        var plain = _assets.AddMaterial("Plain");
        ((IAsset)plain).GetData<MaterialData>().Shaders.ForEach(shader => shader.ShaderType = TwinShader.Type.StandardUnlit);
        var ogi = OgiDrawnWith(plain);

        Assert.Empty(((IAsset)ogi).GetData<OGIData>().PartsLitWithoutEmitColors());
    }

    // Every chunk with the model would tell it again
    [Fact]
    public void ABuildTellsItOnce()
    {
        var ogi = OgiDrawnWith(_assets.AddMaterial("Shiny"));
        var build = new PS2ItemFactory { GlobalPackage = _project.Project.GlobalPackagePS2 };

        Build(ogi, build, "levels/test/first");
        Build(ogi, build, "levels/test/second");

        Assert.StartsWith("Crash's part drawn with Shiny has no emit colors", Assert.Single(build.Warned.Keys));
    }

    // The Xbox version's models have one color, which its shaders light
    [Fact]
    public void XboxModelsNeedNoEmitColors()
    {
        var ogi = OgiDrawnWith(_assets.AddMaterial("Shiny"));
        var build = new XboxItemFactory { GlobalPackage = _project.Project.GlobalPackagePS2 };

        Build(ogi, build, "levels/test/first");

        Assert.Empty(build.Warned);
    }
}
