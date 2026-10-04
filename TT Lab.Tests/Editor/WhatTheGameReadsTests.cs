using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Common.Particles;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Tests.Editor;

// The inspector leaves out what the game never reads and grays out what it only reads with some setting (the decomp's readers), the
// values staying as they are
[Collection(ProjectCollection.Name)]
public sealed class WhatTheGameReadsTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public WhatTheGameReadsTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    private static DocumentViewModel Open(IAsset asset)
    {
        var document = new DocumentViewModel(asset);
        document.Initialize();
        return document;
    }

    private static bool Grayed(DocumentViewModel document, string path) => document.PropertyGraph.Find(path)!.IsReadOnly;

    private static bool Shown(DocumentViewModel document, string path) => EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct().IsVisible;

    [AvaloniaFact]
    public void ParticleSystemsGrayOutWhatTheirGeneratorAndModeDoNotRead()
    {
        var particles = _project.Add(new Particles(), "Particles");
        var data = new ParticleData(particles);
        data.ParticleSystems.Add(new ParticleSystem { Name = "Fire", GenSort = TwinParticleSystem.GenSortType.Box });
        particles.SetData(data);
        var document = Open(particles);
        const string system = "Root.AssetData.ParticleSystems[0]";

        Assert.False(Grayed(document, $"{system}.RandomStart"));
        Assert.False(Grayed(document, $"{system}.Velocity"));
        Assert.True(Grayed(document, $"{system}.StartBase"));
        Assert.True(Grayed(document, $"{system}.RampTime"));
        Assert.True(Grayed(document, $"{system}.StarRadialPoints"));
        Assert.True(Grayed(document, $"{system}.Distortion"));
        Assert.False(Grayed(document, $"{system}.DrawFlag"));
        Assert.True(Grayed(document, $"{system}.GhostSeparation"));
        Assert.True(Grayed(document, $"{system}.CollisionRadius"));

        document.PropertyGraph.Find($"{system}.GenSort")!.SetValue(TwinParticleSystem.GenSortType.Ranges);
        Assert.False(Grayed(document, $"{system}.StartBase"));
        Assert.True(Grayed(document, $"{system}.RandomStart"));
        Assert.True(Grayed(document, $"{system}.Velocity"));
        // The collision spheres take the velocity whatever the generator
        document.PropertyGraph.Find($"{system}.CollisionNumSpheres")!.SetValue((Byte)1);
        Assert.False(Grayed(document, $"{system}.Velocity"));
        Assert.False(Grayed(document, $"{system}.CollisionRadius"));

        document.PropertyGraph.Find($"{system}.BlendMode")!.SetValue(ParticleBlendModes.Distortion);
        Assert.False(Grayed(document, $"{system}.Distortion"));
        Assert.True(Grayed(document, $"{system}.DrawFlag"));
        document.Undo();
        Assert.True(Grayed(document, $"{system}.Distortion"));
        Assert.False(Grayed(document, $"{system}.DrawFlag"));

        foreach (var hidden in new[] { "UnusedByte", "UnusedFloat1", "UnusedFloat5", "TextureFrameCount", "TextureFrameRate", "MinSize", "MaxRotation", "UnusedGradient1" })
        {
            Assert.False(Shown(document, $"{system}.{hidden}"), hidden);
        }

        Assert.True(Shown(document, $"{system}.GenRate"));
    }

    // An emitter's bounce values are only read while the system it plays bounces, the angle only by the vertical plane's
    [AvaloniaFact]
    public void AnEmittersBounceFollowsTheSystemItPlays()
    {
        var particles = _project.Add(new Particles(), "Particles");
        var data = new ParticleData(particles);
        data.ParticleSystems.Add(new ParticleSystem { Name = "Drips", GenSort = TwinParticleSystem.GenSortType.Bounce });
        data.ParticleSystems.Add(new ParticleSystem { Name = "Fire", GenSort = TwinParticleSystem.GenSortType.Box });
        data.ParticleInstances.Add(new ParticleSystemInstance { Name = "Drips" });
        particles.SetData(data);
        var document = Open(particles);
        const string emitter = "Root.AssetData.ParticleInstances[0]";

        Assert.False(Grayed(document, $"{emitter}.PlaneOffset"));
        Assert.False(Grayed(document, $"{emitter}.BounceFactor"));
        Assert.True(Grayed(document, $"{emitter}.BouncePlaneAngle"));

        document.PropertyGraph.Find("Root.AssetData.ParticleSystems[0].GenSort")!.SetValue(TwinParticleSystem.GenSortType.BounceXZ);
        Assert.False(Grayed(document, $"{emitter}.BouncePlaneAngle"));
        document.Undo();
        Assert.True(Grayed(document, $"{emitter}.BouncePlaneAngle"));

        document.PropertyGraph.Find($"{emitter}.Name")!.SetValue("Fire");
        Assert.True(Grayed(document, $"{emitter}.PlaneOffset"));
        Assert.True(Grayed(document, $"{emitter}.BounceFactor"));
        // Playing nothing the chunk can play, nothing's grayed out
        document.PropertyGraph.Find($"{emitter}.Name")!.SetValue("Nothing");
        Assert.False(Grayed(document, $"{emitter}.PlaneOffset"));
    }

    [AvaloniaFact]
    public void ShadersGrayOutWhatTheirTypeAndSettingsDoNotRead()
    {
        var material = _assets.AddMaterial("Cloth");
        var data = ((IAsset)material).GetData<MaterialData>();
        data.Shaders[0].ShaderType = TwinShader.Type.StandardLit;
        // The game's most common K, -69 sixteenths
        data.Shaders[0].LodParamK = 65467;
        var document = Open(material);
        const string shader = "Root.AssetData.Shaders[0]";

        Assert.True(Grayed(document, $"{shader}.IntParam"));
        Assert.True(Grayed(document, $"{shader}.FloatParam"));
        document.PropertyGraph.Find($"{shader}.ShaderType")!.SetValue(TwinShader.Type.UnlitClothDeformation);
        Assert.False(Grayed(document, $"{shader}.IntParam"));
        Assert.False(Grayed(document, $"{shader}.FloatParam"));

        Assert.True(Grayed(document, $"{shader}.SpecOfColA"));
        document.PropertyGraph.Find($"{shader}.UseCustomAlphaRegSettings")!.SetValue(true);
        Assert.False(Grayed(document, $"{shader}.SpecOfColA"));
        Assert.True(Grayed(document, $"{shader}.FixedAlphaValue"));
        document.PropertyGraph.Find($"{shader}.SpecOfAlphaC")!.SetValue(TwinShader.AlphaSpecMethod.FIX);
        Assert.False(Grayed(document, $"{shader}.FixedAlphaValue"));

        Assert.True(Grayed(document, $"{shader}.DAlphaTestMode"));
        Assert.True(Grayed(document, $"{shader}.UvScrollSpeed"));
        document.PropertyGraph.Find($"{shader}.XScrollSettings")!.SetValue(TwinShader.XScrollFormula.Linear);
        Assert.False(Grayed(document, $"{shader}.UvScrollSpeed"));
        Assert.True(Grayed(document, $"{shader}.AnimationDrivesColor"));

        Assert.False(Grayed(document, $"{shader}.DepthTest"));
        document.PropertyGraph.Find($"{shader}.ShaderType")!.SetValue(TwinShader.Type.UnlitSkydome);
        Assert.True(Grayed(document, $"{shader}.DepthTest"));

        // The game reads K signed, in sixteenths
        var k = document.PropertyGraph.Find($"{shader}.LodK")!;
        Assert.Equal(-4.3125f, k.GetValue<Single>());
        k.SetValue(-4.0f);
        Assert.Equal((UInt16)0xFFC0, data.Shaders[0].LodParamK);

        foreach (var hidden in new[] { "LodParamK", "UnusedValue", "UnusedFlag", "LeftoverVector" })
        {
            Assert.False(Shown(document, $"{shader}.{hidden}"), hidden);
        }
    }

    [AvaloniaFact]
    public void SurfacesGrayOutTheirMessageWithoutAMessageBit()
    {
        var surface = _project.Add(new CollisionSurface { Chunk = "default", LayoutID = ChunkLayouts.CollisionSurfaces }, "Ground");
        var data = new CollisionSurfaceData(surface) { CollisionMask = Enums.SurfaceCollisionFlags.SolidToPlayer, UnreadValue8 = 0 };
        surface.SetData(data);
        var document = Open(surface);

        Assert.True(Grayed(document, "Root.AssetData.ContactMessage"));
        document.PropertyGraph.Find("Root.AssetData.CollisionMask")!.SetValue(Enums.SurfaceCollisionFlags.SolidToPlayer | Enums.SurfaceCollisionFlags.SendsContactMessageToPlayer);
        Assert.False(Grayed(document, "Root.AssetData.ContactMessage"));

        Assert.True(Grayed(document, "Root.AssetData.UnreadValue9"));
        document.PropertyGraph.Find("Root.AssetData.UnreadValue8")!.SetValue(35.0f);
        Assert.False(Grayed(document, "Root.AssetData.UnreadValue9"));
    }

    // The game uses a link's load wall whenever it has one (the wall's bit, not the keep bit 8), its loading without the player only
    // with hulls
    [AvaloniaFact]
    public void ALinksWallIsAlwaysReadItsLoadingWithoutThePlayerOnlyWithHulls()
    {
        var links = _project.Add(new ChunkLinks { Chunk = "default" }, "Links");
        var walled = new ChunkLink { IsLoadWallActive = false };
        var hulled = new ChunkLink();
        hulled.Hulls.Add(new ChunkLinkHull(TwinCollisionHull.CreateBox(new Vector4(-1, 0, -1, 1), new Vector4(1, 2, 1, 1))));
        links.SetData(new ChunkLinksData(links) { Links = [walled, hulled] });
        var document = Open(links);

        Assert.False(Grayed(document, "Root.AssetData.Links[0].LoadingWall"));
        Assert.True(Grayed(document, "Root.AssetData.Links[0].LoadsWithoutPlayer"));
        Assert.False(Grayed(document, "Root.AssetData.Links[1].LoadsWithoutPlayer"));

        var hulls = document.PropertyGraph.Find("Root.AssetData.Links[1].Hulls")!;
        hulls.RemoveElement(hulls.Children[0]);
        Assert.True(Grayed(document, "Root.AssetData.Links[1].LoadsWithoutPlayer"));
    }

    // Zones keep no follow values or offset in their files, points never read the offset, a spline only with Takes Offset and every
    // subtype the follow rate only at its rate
    [AvaloniaFact]
    public void CameraSubtypesLeaveOutAndGrayOutWhatTheyDoNotRead()
    {
        var camera = _project.Add(new Camera { Chunk = "default", LayoutID = 4 }, "Camera");
        var spline = new CameraSpline { StepLength = 1 };
        for (var i = 0; i < 4; i++)
        {
            spline.PathPoints.Add(new Vector4(i, 0, 0, BitConverter.UInt32BitsToSingle(CameraGeometry.NeutralKeySample)));
            spline.Tangents.Add(new Vector4(1, 0, 0, 1));
        }

        spline.ArcLengths = [1, 2, 3];
        spline.InverseSteps = [0.2f, 0.2f, 0.2f];
        camera.SetData(new CameraData(camera) { MainCamera1 = new CameraZone(), MainCamera2 = spline });
        var document = Open(camera);

        Assert.False(Shown(document, "Root.AssetData.MainCamera1.Offset"));
        Assert.False(Shown(document, "Root.AssetData.MainCamera1.Follow"));
        Assert.False(Shown(document, "Root.AssetData.MainCamera1.FollowRate"));
        Assert.True(Shown(document, "Root.AssetData.MainCamera2.Offset"));

        Assert.True(Grayed(document, "Root.AssetData.MainCamera2.Offset"));
        document.PropertyGraph.Find("Root.AssetData.MainCamera2.SplineFlags.TakesOffset")!.SetValue(true);
        Assert.False(Grayed(document, "Root.AssetData.MainCamera2.Offset"));

        Assert.True(Grayed(document, "Root.AssetData.MainCamera2.FollowRate"));
        document.PropertyGraph.Find("Root.AssetData.MainCamera2.Follow")!.SetValue(CameraSubBase.FollowMode.AtRate);
        Assert.False(Grayed(document, "Root.AssetData.MainCamera2.FollowRate"));
    }

    // A subtype picked in the inspector gets its fields linked as well: they were looked for under a node outside the graph
    [AvaloniaFact]
    public void ASubtypePickedLaterGraysOutWhatItDoesNotRead()
    {
        var camera = _project.Add(new Camera { Chunk = "default", LayoutID = 4 }, "Camera");
        camera.SetData(new CameraData(camera));
        var document = Open(camera);

        document.PropertyGraph.Find("Root.AssetData.MainCamera1")!.SetValue(new CameraLine());

        Assert.True(Grayed(document, "Root.AssetData.MainCamera1.FollowRate"));
        document.PropertyGraph.Find("Root.AssetData.MainCamera1.Follow")!.SetValue(CameraSubBase.FollowMode.AtRate);
        Assert.False(Grayed(document, "Root.AssetData.MainCamera1.FollowRate"));
    }

    // A trigger of kind 0 is a box of its chunk's second reverb, which only takes where the box is; a camera's kind is its priority
    [AvaloniaFact]
    public void ASoundBoxGraysOutWhatOnlyTriggersRead()
    {
        var trigger = _project.Add(new Trigger { Chunk = "default", LayoutID = 0 }, "Trigger");
        trigger.SetData(new TriggerData(trigger));
        var document = Open(trigger);

        Assert.False(Grayed(document, "Root.AssetData.ObjectActivatorMask"));
        Assert.True(Grayed(document, "Root.AssetData.TriggerMessage1"));
        document.PropertyGraph.Find("Root.AssetData.TriggerArgument1Enabled")!.SetValue(true);
        Assert.False(Grayed(document, "Root.AssetData.TriggerMessage1"));

        document.PropertyGraph.Find("Root.AssetData.Kind")!.SetValue((Byte)0);
        foreach (var field in new[] { "ObjectActivatorMask", "Instances", "CheckInterval", "NotPolled", "TriggerArgument1Enabled", "TriggerMessage1" })
        {
            Assert.True(Grayed(document, $"Root.AssetData.{field}"), field);
        }

        Assert.False(Grayed(document, "Root.AssetData.Scale"));
        Assert.False(Grayed(document, "Root.AssetData.Kind"));

        document.Undo();
        Assert.False(Grayed(document, "Root.AssetData.TriggerMessage1"));
        Assert.False(Grayed(document, "Root.AssetData.CheckInterval"));

        // New cameras' triggers have priority 0
        var camera = _project.Add(new Camera { Chunk = "default", LayoutID = 4 }, "Camera");
        camera.SetData(new CameraData(camera));
        var cameraDocument = Open(camera);
        Assert.Equal((Byte)0, ((CameraData)camera.GetData()).Trigger.Kind);
        Assert.False(Grayed(cameraDocument, "Root.AssetData.Trigger.ObjectActivatorMask"));
        Assert.False(Grayed(cameraDocument, "Root.AssetData.Trigger.Instances"));
        Assert.False(Grayed(cameraDocument, "Root.AssetData.Trigger.CheckInterval"));
    }

    // The message has 10 bits of the word, a bigger one went into the starter's
    [Fact]
    public void ATriggerBehavioursMessageKeepsToItsBits()
    {
        var packed = new TwinObjectTriggerBehaviour { MessageID = 1500, TriggerBehaviour = 5 }.Compress();

        Assert.Equal(5U, packed >> 10 & 0x3FFF);
        Assert.Equal(1500U & 0x3FF, packed & 0x3FF);
    }
}
