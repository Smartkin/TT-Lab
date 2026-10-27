using System.Text.Json.Nodes;
using GlmSharp;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Extensions;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code;
using Twinsanity.TwinsanityInterchange.Interfaces;
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
        var factory = new PS2ItemFactory();
        var section = new PS2AnyAnimationsSection();
        var other = new OGIData(_ogi);
        other.SetAnimations([CreateAnimationData("Walk", _walk.ID, 0, joint => joint == 1, _ => false)]);

        var id = _ogiData.ResolveAnimation(factory, section, _walk.ID);
        var otherId = other.ResolveAnimation(factory, section, _walk.ID);

        Assert.Equal(id, otherId);
        Assert.Equal(1, section.GetItemsAmount());
    }

    // The game keeps every loaded chunk's animations in one table at their ID's low 15 bits and keeps the first one of an ID: an edited
    // copy gets an ID of the table no OGI's animation has, the same one in every chunk of the build
    [Fact]
    public void EditedCopiesOfAnAnimationGetTheirOwnIdInEveryChunk()
    {
        var factory = new PS2ItemFactory();
        var beach = new PS2AnyAnimationsSection();
        var sandbox = new PS2AnyAnimationsSection();
        var other = new OGIData(_ogi);
        other.SetAnimations([CreateAnimationData("Walk", _walk.ID, 2, joint => joint == 1, _ => false)]);

        var id = _ogiData.ResolveAnimation(factory.ForChunk(), beach, _walk.ID);
        var otherId = other.ResolveAnimation(factory.ForChunk(), beach, _walk.ID);
        var sandboxId = other.ResolveAnimation(factory.ForChunk(), sandbox, _walk.ID);

        Assert.Equal(_walk.ID, id);
        Assert.NotNull(otherId);
        Assert.InRange(otherId.Value, 0U, AnimationIds.TableSize - 1U);
        Assert.DoesNotContain(otherId.Value, new[] { _walk.ID, _run.ID });
        Assert.Equal(otherId, sandboxId);
        Assert.Equal(2, beach.GetItemsAmount());
    }

    // TT Lab gives animations made in Blender IDs from 0x8000 up in their OGI, which the game would take for the ones from 0 up
    [Fact]
    public void NewAnimationsGetAnIdOfTheGamesTable()
    {
        var dance = CreateAnimationData("Dance", 0x8000, 2, _ => false, _ => false);
        _ogiData.Animations.Add(dance);
        var section = new PS2AnyAnimationsSection();

        var id = _ogiData.ResolveAnimation(new PS2ItemFactory(), section, dance.ID);

        Assert.NotNull(id);
        Assert.InRange(id.Value, 0U, AnimationIds.TableSize - 1U);
        Assert.DoesNotContain(id.Value, new[] { _walk.ID, _run.ID });
        Assert.True(section.ContainsItem(id.Value));
    }

    // Chunks built later, by Play or by another build, give the animations the IDs the builds before gave them: copies no OGI has get their
    // IDs as they ask
    [Fact]
    public void BuildsGiveAnimationsTheIdsTheyGotBefore()
    {
        var ids = LoadIds();
        var walk = ids.KeyOf(_walk);
        var editedWalk = ids.KeyOf(CreateAnimationData("Walk", _walk.ID, 2, joint => joint == 1, _ => false));
        var otherWalk = ids.KeyOf(CreateAnimationData("Walk", _walk.ID, 3, joint => joint == 1, _ => false));
        Assert.Equal(_walk.ID, ids.IdOf(walk, _walk.ID, "Walk"));
        var edited = ids.IdOf(editedWalk, _walk.ID, "Edited walk");
        var other = ids.IdOf(otherWalk, _walk.ID, "Other walk");
        ids.Save();

        var again = LoadIds();

        // The copies come the other way round this time
        Assert.Equal(other, again.IdOf(otherWalk, _walk.ID, "Other walk"));
        Assert.Equal(edited, again.IdOf(editedWalk, _walk.ID, "Edited walk"));
        Assert.Equal(_walk.ID, again.IdOf(walk, _walk.ID, "Walk"));
        Assert.True(again.StillGives(AnimationIds.DependencyOf(editedWalk, _walk.ID), edited.ToString("X")));
        Assert.NotEqual(edited, other);
    }

    // An ID one of the OGIs' animations has by now is that animation's, what got it before gets another and its chunks get built again
    [Fact]
    public void AnIdAnOgisAnimationTakesIsGivenAgain()
    {
        var ids = LoadIds();
        var editedWalk = ids.KeyOf(CreateAnimationData("Walk", _walk.ID, 2, joint => joint == 1, _ => false));
        ids.IdOf(ids.KeyOf(_walk), _walk.ID, "Walk");
        var given = ids.IdOf(editedWalk, _walk.ID, "Edited walk");
        ids.Save();
        var dance = CreateAnimationData("Dance", given, 1, _ => false, _ => false);
        _ogiData.Animations.Add(dance);

        var again = LoadIds();

        Assert.False(again.StillGives(AnimationIds.DependencyOf(editedWalk, _walk.ID), given.ToString("X")));
        Assert.Equal(given, again.IdOf(again.KeyOf(dance), given, "Dance"));
        Assert.NotEqual(given, again.IdOf(editedWalk, _walk.ID, "Edited walk"));
    }

    // Nobody has the OGI loaded, its animations and the IDs it has them by come from its file
    [Fact]
    public void TheIdsOfOgisNobodyLoadedComeFromTheirFiles()
    {
        _ogi.Serialize(SerializationFlags.SaveData);
        Assert.False(_ogi.IsLoaded);

        var ids = LoadIds();

        // An animation only keeps an ID an OGI has it by: what the file has the run as is what it's made of
        Assert.Equal(_run.ID, ids.IdOf(ids.KeyOf(_run), _run.ID, "Run"));
        Assert.NotEqual(0x3U, ids.IdOf(ids.KeyOf([2]), 0x3, "Jump"));
        Assert.False(_ogi.IsLoaded);
    }

    // Once every chunk got built, the IDs of the animations none of them has can be given to others
    [Fact]
    public void IdsNoChunkHasAreLetGo()
    {
        var ids = LoadIds();
        var kept = ids.KeyOf([1]);
        var dropped = ids.KeyOf([2]);
        var keptId = ids.IdOf(kept, 0x8000, "Kept");
        var droppedId = ids.IdOf(dropped, 0x8001, "Dropped");

        ids.KeepOnly([AnimationIds.DependencyOf(kept, 0x8000)]);

        Assert.True(ids.StillGives(AnimationIds.DependencyOf(kept, 0x8000), keptId.ToString("X")));
        Assert.False(ids.StillGives(AnimationIds.DependencyOf(dropped, 0x8001), droppedId.ToString("X")));
    }

    // CrashSpin and CrashSpinWithCortex have one spin, by 3 and 0xA8, and the disc's chunks have it by both: the IDs went to whichever OGI's
    // chunk a build's memory gate started first, and two builds of a project differed in every chunk having the spin
    [Fact]
    public void AnAnimationKeepsEveryIdItsOgisHaveItBy()
    {
        var spin = AddOgi("CrashSpin", 0x10, CreateAnimationData("Spin", 0x3, 1, _ => false, _ => false));
        var spinWithCortex = AddOgi("CrashSpinWithCortex", 0x11, CreateAnimationData("Spin", 0xA8, 1, _ => false, _ => false));

        foreach (var order in new[] { new[] { spin, spinWithCortex }, new[] { spinWithCortex, spin } })
        {
            var factory = new PS2ItemFactory();
            var both = new PS2AnyAnimationsSection();
            foreach (var ogi in order)
            {
                var id = ogi.Animations[0].ID;
                Assert.Equal(id, ogi.ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), id));
                Assert.Equal(id, ogi.ResolveAnimation(factory.ForChunk(), both, id));
            }

            // Two items of the same bytes, like the disc's
            Assert.Equal(2, both.GetItemsAmount());
            Assert.Equal(Bytes(both.GetItem<ITwinAnimation>(0x3)), Bytes(both.GetItem<ITwinAnimation>(0xA8)));
        }
    }

    // OGIs with other animations by one ID: the first in a fixed order (the OGI's URI here) keeps it and the other gets the same ID of its
    // own, whichever chunk asks first
    [Fact]
    public void WhoKeepsAnIdOgisHaveOtherAnimationsByIsTheSameInEitherOrder()
    {
        var alpha = AddOgi("Alpha", 0x10, CreateAnimationData("Idle", 0x40, 1, _ => false, _ => false));
        var beta = AddOgi("Beta", 0x11, CreateAnimationData("Idle", 0x40, 2, _ => false, _ => false));

        var given = BothOrders(alpha, beta);

        Assert.Equal((0x40U, AnimationIds.TableSize - 1U), given[0]);
        Assert.Equal(given[0], given[1]);
    }

    // Animations made in Blender get their IDs in the fixed order, whichever chunk asks first
    [Fact]
    public void NewAnimationsGetTheSameIdsInEitherOrder()
    {
        var alpha = AddOgi("Alpha", 0x10, CreateAnimationData("Dance", 0x8000, 1, _ => false, _ => false));
        var beta = AddOgi("Beta", 0x11, CreateAnimationData("Dance", 0x8000, 2, _ => false, _ => false));

        var given = BothOrders(alpha, beta);

        Assert.Equal((AnimationIds.TableSize - 1U, AnimationIds.TableSize - 2U), given[0]);
        Assert.Equal(given[0], given[1]);
    }

    // The game's OGIs keep the IDs they have their animations by, whatever the URIs of the project's OGIs having other animations by them:
    // only the chunks having the project's animations change
    [Fact]
    public void TheGamesAnimationsKeepTheirIds()
    {
        // The project's own package's URI comes before the Xbox version's package's
        var projects = AddOgi("Retargeted", 0x10, CreateAnimationData("Idle", 0x40, 2, _ => false, _ => false), _project.Project.BasePackage);
        var games = AddOgi("Crash", 0x11, CreateAnimationData("Idle", 0x40, 1, _ => false, _ => false), _project.Project.XboxPackage);

        var given = BothOrders(games, projects);

        Assert.Equal((0x40U, AnimationIds.TableSize - 1U), given[0]);
        Assert.Equal(given[0], given[1]);
    }

    // A version's OGIs keep the IDs they have their animations by, whatever the URIs of the other version's OGIs having other animations by
    // them (its chunks can draw the other version's models)
    [Fact]
    public void TheVersionsOwnAnimationsKeepTheirIds()
    {
        var xbox = AddOgi("Crash", 0x10, CreateAnimationData("Idle", 0x40, 2, _ => false, _ => false), _project.Project.GlobalPackageXbox);
        var ps2 = AddOgi("Crash", 0x11, CreateAnimationData("Idle", 0x40, 1, _ => false, _ => false), _project.Project.Ps2Package);
        var ogis = new[] { _ogi, ps2.GetOwner(), xbox.GetOwner() };

        foreach (var order in new[] { new[] { ps2, xbox }, new[] { xbox, ps2 } })
        {
            var ids = AnimationIds.Load(_project.Project.ProjectPath, TT_Lab.Project.Project.GamePlatform.PS2, ogis,
                ogi => _project.Project.GetPlatform(ogi.Package) == TT_Lab.Project.Project.GamePlatform.PS2);
            var factory = new PS2ItemFactory { AnimationIds = ids };
            var given = order.ToDictionary(ogi => ogi, ogi => ogi.ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), 0x40));

            Assert.Equal(0x40U, given[ps2]);
            Assert.Equal(AnimationIds.TableSize - 1U, given[xbox]);
        }
    }

    // Nobody has the OGIs loaded: what their animations are made of comes from their files, which the data the build loads then has
    [Fact]
    public void WhatTheAnimationsOfOgisNobodyLoadedAreComesFromTheirFiles()
    {
        var alpha = AddOgi("Alpha", 0x10, CreateAnimationData("Idle", 0x40, 1, _ => false, _ => false));
        var beta = AddOgi("Beta", 0x11, CreateAnimationData("Idle", 0x40, 2, _ => false, _ => false));
        alpha.GetOwner().Serialize(SerializationFlags.SaveData);
        beta.GetOwner().Serialize(SerializationFlags.SaveData);
        var ids = AnimationIds.Load(_project.Project.ProjectPath, TT_Lab.Project.Project.GamePlatform.PS2, [_ogi, alpha.GetOwner(), beta.GetOwner()]);
        Assert.False(alpha.GetOwner().IsLoaded);
        var factory = new PS2ItemFactory { AnimationIds = ids };

        // Beta asks first: had Alpha's file another idle than its data, Beta's would keep the ID
        var betaId = beta.GetOwner().GetData<OGIData>().ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), 0x40);
        var alphaId = alpha.GetOwner().GetData<OGIData>().ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), 0x40);

        Assert.Equal(AnimationIds.TableSize - 1U, betaId);
        Assert.Equal(0x40U, alphaId);
    }

    // A file Blender wrote that TT Lab hasn't read since keeps the game's bytes an edited animation was made of: it's read the way TT Lab
    // reads it, so the edited animation is what keeps the ID
    [Fact]
    public void AFileBlenderWroteIsReadTheWayTTLabReadsIt()
    {
        var idle = CreateAnimationData("Idle", 0x40, 1, _ => false, _ => false);
        var alpha = AddOgi("Alpha", 0x10, idle);
        var beta = AddOgi("Beta", 0x11, CreateAnimationData("Idle", 0x40, 2, _ => false, _ => false));
        beta.GetOwner().Serialize(SerializationFlags.SaveData);
        alpha.Animations = [CreateAnimationData("Idle", 0x40, 3, _ => false, _ => false)];
        var file = alpha.WriteTlm();
        AnimationJsons(file)[0]["exact"] = file.Write(TlmAnimations.GetExactBytes(idle).AsSpan());
        file.Json[TlmMaterials.ExportedByKey] = "Twin Tech Tools";
        var path = alpha.GetOwner().FullDataPath;
        alpha.GetOwner().Serialize(SerializationFlags.SaveData);
        file.Save(path);
        var ids = AnimationIds.Load(_project.Project.ProjectPath, TT_Lab.Project.Project.GamePlatform.PS2, [_ogi, alpha.GetOwner(), beta.GetOwner()]);
        var factory = new PS2ItemFactory { AnimationIds = ids };

        var betaId = beta.GetOwner().GetData<OGIData>().ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), 0x40);
        var alphaId = alpha.GetOwner().GetData<OGIData>().ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), 0x40);

        Assert.Equal(AnimationIds.TableSize - 1U, betaId);
        Assert.Equal(0x40U, alphaId);
    }

    // Reading a file Blender wrote for its animations isn't TT Lab taking it: reading it recorded it as TT Lab's own read, the project's
    // watcher then took the export for TT Lab's and the editors showing the model kept what they had
    [Fact]
    public void AFileBlenderWroteStaysAnotherProgramsChange()
    {
        var idle = CreateAnimationData("Idle", 0x40, 1, _ => false, _ => false);
        var alpha = AddOgi("Alpha", 0x10, idle);
        var file = alpha.WriteTlm();
        file.Json[TlmMaterials.ExportedByKey] = "Twin Tech Tools";
        var path = alpha.GetOwner().FullDataPath;
        alpha.GetOwner().Serialize(SerializationFlags.SaveData);
        file.Save(path);
        Assert.False(AssetFileStamps.IsAsRecorded(path));

        var ids = AnimationIds.Load(_project.Project.ProjectPath, TT_Lab.Project.Project.GamePlatform.PS2, [_ogi, alpha.GetOwner()]);

        // Read: the idle keeps its ID
        Assert.Equal(0x40U, ids.IdOf(ids.KeyOf(idle), 0x40, "Idle"));
        Assert.False(AssetFileStamps.IsAsRecorded(path));
        Assert.False(alpha.GetOwner().IsLoaded);
    }

    // An animation by an ID another animation took gets the first ID its animation keeps in the fixed order, in a build from nothing and in the
    // builds after it, the ones it keeps whichever chunk asks first: keyed by the content alone, every spin got the ID of the first one asked
    [Fact]
    public void AnAnimationThatCantKeepItsIdGetsTheSameOneInTheBuildsAfter()
    {
        var alpha = AddOgi("Alpha", 0x10, CreateAnimationData("Spin", 0xA8, 1, _ => false, _ => false));
        var beta = AddOgi("Beta", 0x11, CreateAnimationData("Spin", 0x3, 1, _ => false, _ => false));
        var charlie = AddOgi("Charlie", 0x12, CreateAnimationData("Idle", 0x5, 2, _ => false, _ => false));
        var delta = AddOgi("Delta", 0x13, CreateAnimationData("Spin", 0x5, 1, _ => false, _ => false));
        OGIData[] ogis = [alpha, beta, charlie, delta];

        foreach (var order in new[] { ogis, ogis.Reverse().ToArray() })
        {
            var ids = AnimationIds.Load(_project.Project.ProjectPath, TT_Lab.Project.Project.GamePlatform.PS2, [_ogi, ..ogis.Select(ogi => ogi.GetOwner())]);
            var factory = new PS2ItemFactory { AnimationIds = ids };
            var given = order.ToDictionary(ogi => ogi, ogi => ogi.ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), ogi.Animations[0].ID));
            ids.Save();

            Assert.Equal(new UInt32?[] { 0xA8, 0x3, 0x5, 0xA8 }, ogis.Select(ogi => given[ogi]));
        }
    }

    // The table's room goes to what chunks have: the IDs worked out for the OGIs' animations that can't keep theirs are only given once a chunk
    // asks, and past the room for every animation of the OGIs they're worked out for the animations game objects play first
    [Fact]
    public void OnlyTheAnimationsChunksHaveTakeTheTablesRoom()
    {
        // Every ID but 0 given by the builds before (walk and run have 1 and 2)
        var filled = LoadIds();
        for (var animation = 0; animation < AnimationIds.TableSize - 3; animation++)
        {
            filled.IdOf(filled.KeyOf(BitConverter.GetBytes(animation)), 0x8000, "Filler");
        }

        filled.Save();
        var unused = AddOgi("Alpha", 0x10, CreateAnimationData("Dance", 0x8000, 1, _ => false, _ => false));
        var used = AddOgi("Beta", 0x11, CreateAnimationData("Dance", 0x8000, 2, _ => false, _ => false));
        AddPlayer("Dancer", 0x30, new ModelSlot { Ogi = used.GetOwner().URI, Animation = 0x8000 });
        var ids = AnimationIds.Load(_project.Project.ProjectPath, TT_Lab.Project.Project.GamePlatform.PS2, [_ogi, unused.GetOwner(), used.GetOwner()]);
        var factory = new PS2ItemFactory { AnimationIds = ids };

        Assert.Equal(0U, used.ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), 0x8000));
        Assert.Throws<InvalidOperationException>(() => unused.ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), 0x8000));
    }

    // Past the room of the game's table the animations game objects play get it first, in the fixed order: whichever order the chunks ask in,
    // they get the same IDs and an animation left without one fails. Given as the chunks asked, which one found the table full followed the
    // order the chunks were built in
    [Fact]
    public void PastAFullTableTheIdsAreTheSameInEveryOrder()
    {
        // Room for walk (1), run (2) and two more
        const Int32 tableSize = 4;
        var alpha = AddOgi("Alpha", 0x10, CreateAnimationData("Dance", 0x8000, 1, _ => false, _ => false));
        var beta = AddOgi("Beta", 0x11, CreateAnimationData("Dance", 0x8000, 2, _ => false, _ => false));
        var charlie = AddOgi("Charlie", 0x12, CreateAnimationData("Dance", 0x8000, 3, _ => false, _ => false));
        OGIData[] ogis = [alpha, beta, charlie];

        // A build from nothing, the chunks asking for the OGIs' animations in the order given, none for an animation left without an ID
        UInt32?[] Built(OGIData[] order)
        {
            var ids = AnimationIds.Load(_project.Project.ProjectPath, TT_Lab.Project.Project.GamePlatform.PS2, [_ogi, ..ogis.Select(ogi => ogi.GetOwner())],
                tableSize: tableSize);
            var factory = new PS2ItemFactory { AnimationIds = ids };
            var given = new Dictionary<OGIData, UInt32?>();
            foreach (var ogi in order)
            {
                try
                {
                    given[ogi] = ogi.ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), 0x8000);
                }
                catch (InvalidOperationException)
                {
                    given[ogi] = null;
                }
            }

            return ogis.Select(ogi => given[ogi]).ToArray();
        }

        // Nothing plays them: the fixed order (the OGIs' URIs)
        foreach (var order in Orders(ogis))
        {
            Assert.Equal([3U, 0U, null], Built(order));
        }

        // Objects play Beta's and Charlie's: one of its data, which is loaded, one by a chunk's own value of an object that plays nothing, its
        // data in its file
        AddPlayer("Bee", 0x30, new ModelSlot { Ogi = beta.GetOwner().URI, Animation = 0x8000 });
        var charles = AddPlayer("Charles", 0x31, new ModelSlot());
        charles.Serialize(SerializationFlags.SaveData);
        Assert.False(charles.IsLoaded);
        var chunk = new LevelChunk(_project.Project.Ps2Package.URI, "beach") { AdditionalPath = "levels/earth/hub/beach" };
        chunk.RegenerateUri();
        _project.AssetManager.AddAsset(chunk);
        chunk.Overrides.Add(new AssetOverride
        {
            Asset = charles.URI,
            Values = { ["AssetData.ModelSlots[0].Ogi"] = new JObject { ["_uri"] = charlie.GetOwner().URI.ToString() }, ["AssetData.ModelSlots[0].Animation"] = 0x8000 }
        });
        foreach (var order in Orders(ogis))
        {
            Assert.Equal([null, 3U, 0U], Built(order));
        }
    }

    // Every order of the items
    private static IEnumerable<T[]> Orders<T>(T[] items)
    {
        return items.Length <= 1 ? [items] : items.SelectMany((item, index) => Orders<T>([..items[..index], ..items[(index + 1)..]]).Select(rest => (T[])[item, ..rest]));
    }

    // A game object of the project with its data loaded, of the global package, playing what its model slot does
    private GameObject AddPlayer(string name, UInt32 id, ModelSlot slot)
    {
        var player = _project.Add(new GameObject(), name, id);
        player.SetData(new GameObjectData(player) { Name = name, ModelSlots = [slot] });
        return player;
    }

    private AnimationIds LoadIds() => AnimationIds.Load(_project.Project.ProjectPath, TT_Lab.Project.Project.GamePlatform.PS2, [_ogi]);

    // An OGI of the project with its data loaded, of the global package unless another is given
    private OGIData AddOgi(string name, UInt32 id, AnimationData animation, Package? package = null)
    {
        var ogi = _project.Add(new OGI(), name, id, package);
        var data = CreateSkeleton(ogi);
        data.Animations = [animation];
        ogi.SetData(data);
        return data;
    }

    // The IDs two OGIs' animations get in chunks built one after the other in both orders, a build each, the first OGI's ID first
    private static List<(UInt32?, UInt32?)> BothOrders(OGIData first, OGIData second)
    {
        var given = new List<(UInt32?, UInt32?)>();
        foreach (var order in new[] { new[] { first, second }, new[] { second, first } })
        {
            var factory = new PS2ItemFactory();
            var ids = order.ToDictionary(ogi => ogi, ogi => ogi.ResolveAnimation(factory.ForChunk(), new PS2AnyAnimationsSection(), ogi.Animations[0].ID));
            given.Add((ids[first], ids[second]));
        }

        return given;
    }

    private static byte[] Bytes(ITwinItem item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    [Fact]
    public void MissingAnimationsArentResolved()
    {
        var section = new PS2AnyAnimationsSection();

        Assert.Null(_ogiData.ResolveAnimation(new PS2ItemFactory(), section, 0x77));
        Assert.Equal(0, section.GetItemsAmount());
    }
}
