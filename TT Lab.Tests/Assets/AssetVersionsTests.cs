using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Tests.Assets;

// A package of one version can use the other version's assets, depending on their package, but for game objects, their instances and
// behaviours (some behaviour commands differ). A package is the version of its first dependency, the one it was made on, whatever else it
// depends on, and what's related to it, in scope of its scripts or offered to its version-bound links stays its version's. Dependencies can
// go round in a circle
[Collection(ProjectCollection.Name)]
public sealed class AssetVersionsTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private TT_Lab.Project.Project Project => _project.Project;

    private Package AddPackage(string name, params Package[] dependencies)
    {
        var package = new Package(name, "Test");
        package.RegenerateLinks();
        foreach (var dependency in dependencies)
        {
            package.AddDependency(dependency.URI);
        }

        _project.AssetManager.AddAsset(package);
        return package;
    }

    // An Xbox mod using the PS2 version's assets
    private Package XboxMod() => AddPackage("XboxMod", Project.XboxPackage, Project.Ps2Package);

    [Fact]
    public void APackageIsTheVersionOfItsFirstDependency()
    {
        var xboxMod = XboxMod();
        var ps2Mod = AddPackage("Ps2Mod", Project.Ps2Package, Project.XboxPackage);
        var onTheXboxMod = AddPackage("OnTheXboxMod", xboxMod);

        Assert.Equal(GamePlatform.Xbox, Project.GetPlatform(xboxMod.URI));
        Assert.Equal(GamePlatform.PS2, Project.GetPlatform(ps2Mod.URI));
        Assert.Equal(GamePlatform.Xbox, Project.GetPlatform(onTheXboxMod.URI));
        Assert.Equal(GamePlatform.Xbox, Project.GetPlatform(Project.GlobalPackageXbox.URI));
        Assert.Equal(GamePlatform.PS2, Project.GetPlatform(Project.BasePackage.URI));
    }

    [Fact]
    public void WhatsRelatedStaysInItsVersion()
    {
        var xboxMod = XboxMod();
        var ps2Surface = _project.Add(new CollisionSurface { Chunk = "default" }, "Stone", 0x1);
        var xboxSurface = _project.Add(new CollisionSurface { Chunk = "default" }, "Stone", 0x1, Project.GlobalPackageXbox);

        Assert.Equal([xboxSurface], _project.AssetManager.GetRelatedAssetsOf<CollisionSurface>(xboxMod.URI));
        Assert.DoesNotContain(ps2Surface, _project.AssetManager.GetRelatedAssetsOf<CollisionSurface>(xboxMod.URI));
        Assert.False(_project.AssetManager.IsRelated(xboxMod.URI, Project.GlobalPackagePS2.URI));
        // It can still use the PS2 version's other assets
        Assert.True(_project.AssetManager.IsOwnOrDependency(xboxMod.URI, Project.Ps2Package.URI));
    }

    // The versions' assets share IDs: an asset of a package depending on both finds its own version's by an ID
    [Fact]
    public void IdsFindTheRequestersVersionFirst()
    {
        var xboxMod = XboxMod();
        _project.Add(new Texture(), "Wood", 0x7, Project.Ps2Package);
        var xboxWood = _project.Add(new Texture(), "Wood", 0x7, Project.XboxPackage);
        var varnish = _project.Add(new Material(), "Varnish", 0x2, xboxMod);

        Assert.Equal(xboxWood.URI, _project.AssetManager.GetUriByTwinId<Texture>(varnish, 0x7));
    }

    [Fact]
    public void ScriptsOnlyNameTheirVersionsBehaviours()
    {
        var xboxMod = XboxMod();
        var ps2Walk = _project.Add(new BehaviourGraph(), "COM_WALK", 0x101, Project.Ps2Package);
        var xboxWalk = _project.Add(new BehaviourGraph(), "COM_WALK", 0x101, Project.XboxPackage);
        _project.Add(new BehaviourGraph(), "COM_PS2_ONLY", 0x103, Project.Ps2Package);
        var script = _project.Add(new BehaviourGraph(), "COM_MOD", 0x105, xboxMod);

        Assert.Same(xboxWalk, _project.AssetManager.FindByName<BehaviourGraph>(script, "COM_WALK"));
        Assert.Null(_project.AssetManager.FindByName<BehaviourGraph>(script, "COM_PS2_ONLY"));
        Assert.DoesNotContain(ps2Walk, _project.AssetManager.GetAssetsInScopeOf<BehaviourGraph>(script));
    }

    [Fact]
    public void CircularDependenciesEndEveryLookup()
    {
        var first = AddPackage("First");
        var second = AddPackage("Second", first, Project.Ps2Package);
        first.AddDependency(second.URI);
        first.AddDependency(first.URI);
        var graph = _project.Add(new BehaviourGraph(), "COM_CIRCLE", 0x201, second);
        var requester = _project.Add(new BehaviourGraph(), "COM_REQUESTER", 0x203, first);
        var assets = _project.AssetManager;

        var lookups = Task.Run(() =>
        {
            Assert.Equal(graph.URI, assets.GetUriByTwinId<BehaviourGraph>(requester, 0x201));
            Assert.Same(graph, assets.FindByName<BehaviourGraph>(requester, "COM_CIRCLE"));
            Assert.Contains(graph, assets.GetRelatedAssetsOf<BehaviourGraph>(first.URI));
            Assert.True(assets.IsOwnOrDependency(second.URI, first.URI));
            Project.GetPlatform(first.URI);
            Project.GetPlatform(second.URI);
        });

        Assert.True(lookups.Wait(TimeSpan.FromSeconds(10)), "a lookup went round the circle");
    }

    private GameObject AddObject(Package package)
    {
        var gameObject = _project.Add(new GameObject(), "CRATE", 0x10, package);
        gameObject.SetData(new GameObjectData(gameObject) { Name = "CRATE" });
        return gameObject;
    }

    private (DocumentViewModel Document, UriLinkViewModel Link) LinkOf(IAsset asset, string path)
    {
        var document = new DocumentViewModel(asset);
        document.Initialize();
        var link = Assert.IsType<UriLinkViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct());
        link.Activator.Activate();
        return (document, link);
    }

    [AvaloniaFact]
    public void LinksOfferTheOtherVersionsAssetsButItsObjectsInstancesAndBehaviours()
    {
        var xboxMod = XboxMod();
        var ps2Object = AddObject(Project.Ps2Package);
        var xboxObject = AddObject(Project.XboxPackage);
        var ps2Texture = _project.Add(new Texture(), "Wood", 0x20, Project.Ps2Package);
        ps2Texture.SetData(TextureData.CreateSolidColor(ps2Texture, 16, 0xFF8B5A2B));
        var xboxTexture = _project.Add(new Texture(), "Wood", 0x20, Project.XboxPackage);
        xboxTexture.SetData(TextureData.CreateSolidColor(xboxTexture, 16, 0xFF8B5A2B));

        var instance = _project.Add(new ObjectInstance { Chunk = "levels/mod", LayoutID = 0 }, "Instance", 0x0, xboxMod);
        instance.SetData(new ObjectInstanceData(instance) { ObjectId = xboxObject.URI });
        var (_, objectLink) = LinkOf(instance, "Root.AssetData.ObjectId");
        var offered = objectLink.GetBrowseCandidates();
        Assert.Contains(xboxObject.URI, offered);
        Assert.DoesNotContain(ps2Object.URI, offered);

        var material = _project.Add(new Material(), "Mod material", 0x30, xboxMod);
        material.SetData(new MaterialData(material) { Shaders = [new LabShader { TextureId = xboxTexture.URI }] });
        var (_, textureLink) = LinkOf(material, "Root.AssetData.Shaders[0].TextureId");
        Assert.Contains(ps2Texture.URI, textureLink.GetBrowseCandidates());
        Assert.Contains(xboxTexture.URI, textureLink.GetBrowseCandidates());
    }

    [AvaloniaFact]
    public void ALinkToTheOtherVersionsObjectIsWarnedAboutWithoutADependencyToAdd()
    {
        var xboxMod = AddPackage("XboxMod", Project.XboxPackage);
        var ps2Object = AddObject(Project.Ps2Package);
        var ps2Texture = _project.Add(new Texture(), "Wood", 0x20, Project.Ps2Package);
        ps2Texture.SetData(TextureData.CreateSolidColor(ps2Texture, 16, 0xFF8B5A2B));

        var instance = _project.Add(new ObjectInstance { Chunk = "levels/mod", LayoutID = 0 }, "Instance", 0x0, xboxMod);
        instance.SetData(new ObjectInstanceData(instance) { ObjectId = ps2Object.URI });
        var (_, objectLink) = LinkOf(instance, "Root.AssetData.ObjectId");
        Assert.True(objectLink.IsMissingDependency);
        Assert.False(objectLink.CanAddDependency);
        Assert.Contains("PS2 version's", objectLink.MissingDependencyText);

        // Other assets of the other version are used the way any other package's are, through a dependency
        var material = _project.Add(new Material(), "Mod material", 0x30, xboxMod);
        material.SetData(new MaterialData(material) { Shaders = [new LabShader { TextureId = ps2Texture.URI }] });
        var (_, textureLink) = LinkOf(material, "Root.AssetData.Shaders[0].TextureId");
        Assert.True(textureLink.IsMissingDependency);
        Assert.True(textureLink.CanAddDependency);
        Assert.Equal(UriLinkViewModel.MissingDependencyWarning, textureLink.MissingDependencyText);

        PackageDependencies.Add(xboxMod, Project.Ps2Package);
        Assert.False(textureLink.IsMissingDependency);
        Assert.Equal(GamePlatform.Xbox, Project.GetPlatform(xboxMod.URI));
        Assert.True(objectLink.IsMissingDependency);
    }

    [Fact]
    public void BuildingRefusesTheOtherVersionsObjectsAndBehaviours()
    {
        var ps2Object = _project.Add(new GameObject(), "CRATE", 0x10, Project.Ps2Package);
        ps2Object.SetData(new GameObjectData(ps2Object) { Name = "CRATE" });
        var factory = new XboxItemFactory { GlobalPackage = Project.GlobalPackageXbox }.ForChunk();

        var refusal = Assert.Throws<InvalidOperationException>(() => ps2Object.ResolveChunkResources(factory, null!));
        Assert.Contains("PS2 version's", refusal.Message);
    }
}
