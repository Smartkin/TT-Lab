using System.Text.Json.Nodes;
using GlmSharp;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Extensions;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Tests.Animations;

// OGIs keep their animations in their file next to the skeleton
[Collection(ProjectCollection.Name)]
public sealed class OgiAnimationTests : IDisposable
{
    private const int Frames = 8;
    private const int Joints = 3;

    private readonly TestProject _project = new();
    private readonly OGI _ogi;
    private readonly OGIData _ogiData;
    private readonly AnimationData _walk;
    private readonly AnimationData _run;

    public OgiAnimationTests()
    {
        _ogi = _project.Add(new OGI(), "Skeleton", 0x5);
        _ogiData = CreateSkeleton(_ogi);
        _ogi.SetData(_ogiData);
        // Walk makes the first bone ignore its parent's scale, Run makes the second one use its additional rotation
        _walk = CreateAnimationData("Walk", 0x1, 0, joint => joint == 1, _ => false);
        _run = CreateAnimationData("Run", 0x2, 1, _ => false, joint => joint == 2);
        _ogiData.Animations = [_walk, _run];
    }

    public void Dispose() => _project.Dispose();

    private static OGIData CreateSkeleton(OGI ogi)
    {
        var data = new OGIData(ogi);
        var additionalRotation = new quat(new vec3(0, 0.5f, 0));
        data.Joints =
        [
            new TwinJoint { Index = 0, ParentIndex = 255, LocalTranslation = new Vector4(0, 0, 0, 1), LocalRotation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) },
            new TwinJoint { Index = 1, ParentIndex = 0, LocalTranslation = new Vector4(0, 1, 0, 1), LocalRotation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) },
            new TwinJoint
            {
                Index = 2, ParentIndex = 1, LocalTranslation = new Vector4(0, 0, 1, 1), LocalRotation = new Vector4(0, 0, 0, 1),
                AdditionalAnimationRotation = new Vector4(additionalRotation.x, additionalRotation.y, additionalRotation.z, additionalRotation.w)
            }
        ];
        data.SkinInverseMatrices = [System.Numerics.Matrix4x4.Identity.ToTwin(), System.Numerics.Matrix4x4.CreateTranslation(0, -1, 0).ToTwin(), System.Numerics.Matrix4x4.CreateTranslation(0, -1, -1).ToTwin()];
        return data;
    }

    private static AnimationData CreateAnimationData(string name, uint id, int variant, Func<int, bool> independentScaling, Func<int, bool> additionalRotation)
    {
        return new AnimationData
        {
            ID = id,
            Name = name,
            TotalFrames = Frames,
            DefaultFPS = 25,
            MainAnimation = TestAnimations.CreateTwinAnimation(Frames, Joints, variant, independentScaling, additionalRotation)
        };
    }

    private static List<JsonObject> AnimationJsons(TlmFile file)
    {
        return file.Root!.FindChild(OGIData.ArmatureKind)!["animations"]!.AsArray().OfType<JsonObject>().ToList();
    }

    // Saved and loaded again, as Blender writes it back
    private static TlmFile Reopen(TlmFile file)
    {
        using var stream = new MemoryStream();
        file.WriteTo(stream);
        stream.Position = 0;
        return TlmFile.Read(stream);
    }

    private OGIData Read(TlmFile file, out bool changed)
    {
        var ogi = new OGIData(_ogi);
        changed = ogi.ReadTlm(Reopen(file));
        return ogi;
    }

    [Fact]
    public void FileHasEveryAnimationWithItsFlagsPerJoint()
    {
        var animations = AnimationJsons(_ogiData.WriteTlm());

        Assert.Equal(["Walk", "Run"], animations.Select(animation => animation.GetString("name")));
        Assert.Equal([_walk.ID, _run.ID], animations.Select(animation => animation.GetUInt("id")));
        Assert.All(animations, animation => Assert.Equal((25, Frames), (animation.GetInt("fps"), animation.GetInt("frames"))));
        Assert.Equal([false, true, false], Flags(animations[0], "independent_scaling"));
        Assert.Equal([false, false, true], Flags(animations[1], "additional_rotation"));

        static IEnumerable<bool> Flags(JsonObject animation, string key) => animation["joints"]!.AsArray().OfType<JsonObject>().Select(joint => joint.GetBool(key));
    }

    [Fact]
    public void UnchangedAnimationsStayAsTheGameStoresThem()
    {
        var ogi = Read(_ogiData.WriteTlm(), out var changed);

        Assert.False(changed);
        Assert.Equal(_ogiData.Animations.Select(TlmAnimations.GetExactBytes), ogi.Animations.Select(TlmAnimations.GetExactBytes));
        Assert.Equal(_ogiData.Animations.Select(animation => (animation.ID, animation.Name)), ogi.Animations.Select(animation => (animation.ID, animation.Name)));
    }

    // Blender writes the keys of an edited animation next to the game's bytes it was made from
    [Fact]
    public void EditedAnimationsAreMadeFromTheirKeys()
    {
        var editedWalk = CreateAnimationData("Walk", _walk.ID, 2, joint => joint == 1, _ => false);
        _ogiData.Animations = [editedWalk, _run];
        var file = _ogiData.WriteTlm();
        var walk = AnimationJsons(file)[0];
        walk["exact"] = file.Write(TlmAnimations.GetExactBytes(_walk).AsSpan());

        var ogi = Read(file, out var changed);

        Assert.True(changed);
        Assert.Equal([_walk.ID, _run.ID], ogi.Animations.Select(animation => animation.ID));
        Assert.Equal(TlmAnimations.GetExactBytes(editedWalk), TlmAnimations.GetExactBytes(ogi.Animations[0]));
        Assert.Equal(TlmAnimations.GetExactBytes(_run), TlmAnimations.GetExactBytes(ogi.Animations[1]));
    }

    [Fact]
    public void NewAnimationsGetIdsTheGameDoesntUse()
    {
        var file = _ogiData.WriteTlm();
        var run = AnimationJsons(file)[1];
        run.Remove("id");
        run.Remove("exact");
        run["name"] = "Dance";

        var ogi = Read(file, out var changed);

        Assert.True(changed);
        var dance = ogi.Animations.Single(animation => animation.Name == "Dance");
        // Told apart from other models' ones when the chunk gets built
        Assert.Equal(0x8000U, dance.ID);
        Assert.Equal(TlmAnimations.GetExactBytes(_run), TlmAnimations.GetExactBytes(dance));
    }

    // Blender copies the custom properties along with the action
    [Fact]
    public void CopiesOfAnAnimationBecomeNewOnes()
    {
        var file = _ogiData.WriteTlm();
        var copy = AnimationJsons(file)[0].DeepClone().AsObject();
        copy["name"] = "Walk copy";
        file.Root!.FindChild(OGIData.ArmatureKind)!["animations"]!.AsArray().Add(copy);

        var ogi = Read(file, out var changed);

        Assert.True(changed);
        Assert.Equal([_walk.ID, _run.ID, 0x8000U], ogi.Animations.Select(animation => animation.ID));
        Assert.Equal(TlmAnimations.GetExactBytes(_walk), TlmAnimations.GetExactBytes(ogi.Animations[2]));
    }

    // Loading rewrites the file, so animations edited in Blender are kept as the game stores them and new ones keep their IDs
    [Fact]
    public void LoadingKeepsWhatWasImported()
    {
        _ogi.Serialize(SerializationFlags.SaveData);
        var file = TlmFile.Load(_ogi.FullDataPath);
        AnimationJsons(file)[1].Remove("id");
        AnimationJsons(file)[1].Remove("exact");
        file.Save(_ogi.FullDataPath);

        var data = ((IAsset)_ogi).GetData<OGIData>();

        Assert.Equal([_walk.ID, 0x8000U], data.Animations.Select(animation => animation.ID));
        var rewritten = AnimationJsons(TlmFile.Load(_ogi.FullDataPath));
        Assert.Equal([_walk.ID, 0x8000U], rewritten.Select(animation => animation.GetUInt("id")));
        Assert.All(rewritten, animation => Assert.NotNull(animation["exact"]));
    }

    [Fact]
    public void AnimationsKeepTheirIdInTheChunk()
    {
        var section = new PS2AnyAnimationsSection();

        var id = _ogiData.ResolveAnimation(new PS2ItemFactory(), section, _walk.ID);

        Assert.Equal(_walk.ID, id);
        Assert.Equal(_walk.TotalFrames, section.GetItem<ITwinAnimation>(_walk.ID).TotalFrames);
    }

    // Every OGI keeps its own copy of the animations played on it, the same animation goes into the chunk once
    [Fact]
    public void SameAnimationsOfDifferentOgisGoIntoTheChunkOnce()
    {
        var section = new PS2AnyAnimationsSection();
        var other = new OGIData(_ogi);
        other.SetAnimations([CreateAnimationData("Walk", _walk.ID, 0, joint => joint == 1, _ => false)]);

        var id = _ogiData.ResolveAnimation(new PS2ItemFactory(), section, _walk.ID);
        var otherId = other.ResolveAnimation(new PS2ItemFactory(), section, _walk.ID);

        Assert.Equal(id, otherId);
        Assert.Equal(1, section.GetItemsAmount());
    }

    [Fact]
    public void EditedCopiesOfAnAnimationGetTheirOwnIdInTheChunk()
    {
        var section = new PS2AnyAnimationsSection();
        var other = new OGIData(_ogi);
        other.SetAnimations([CreateAnimationData("Walk", _walk.ID, 2, joint => joint == 1, _ => false)]);

        var id = _ogiData.ResolveAnimation(new PS2ItemFactory(), section, _walk.ID);
        var otherId = other.ResolveAnimation(new PS2ItemFactory(), section, _walk.ID);

        Assert.Equal(_walk.ID, id);
        Assert.Equal(0x8000U, otherId);
        Assert.Equal(2, section.GetItemsAmount());
    }

    [Fact]
    public void MissingAnimationsArentResolved()
    {
        var section = new PS2AnyAnimationsSection();

        Assert.Null(_ogiData.ResolveAnimation(new PS2ItemFactory(), section, 0x77));
        Assert.Equal(0, section.GetItemsAmount());
    }
}
