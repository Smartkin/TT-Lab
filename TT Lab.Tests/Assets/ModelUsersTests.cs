using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Blender;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Tests.Assets;

// A model from Blender takes the place of an OGI that game objects draw: what they need of it is checked first, a character's model
// refused when the character code would read past it, the rest warned about (some of the game's own objects read past their models)
[Collection(ProjectCollection.Name)]
public sealed class ModelUsersTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;
    private readonly OGI _ogi;

    public ModelUsersTests()
    {
        _assets = new TestAssets(_project);
        _ogi = _assets.AddOgi();
        ((IAsset)_ogi).GetData<OGIData>().Animations = [new AnimationData { ID = 0x10, Name = "Walk", TotalFrames = 2, DefaultFPS = 25 }];
    }

    public void Dispose() => _project.Dispose();

    private GameObject AddObject(string name, ITwinObject.ObjectType type, Byte exitPoints, Byte jointIds, params ModelSlot[] slots)
    {
        var gameObject = _project.Add(new GameObject(), name);
        gameObject.SetData(new GameObjectData(gameObject)
        {
            Type = type,
            IntProperties = type == ITwinObject.ObjectType.Character ? [0, 255] : [],
            ExitPointAmount = exitPoints,
            CameraReactJointAmount = jointIds,
            ModelSlots = [..slots]
        });
        ((IAsset)gameObject).AddReference(_ogi.URI);
        return gameObject;
    }

    [Fact]
    public void ObjectsDrawingTheModelAreItsUsers()
    {
        var crate = AddObject("Crate", ITwinObject.ObjectType.Crate, 1, 1, new ModelSlot { Ogi = _ogi.URI });
        AddObject("Other", ITwinObject.ObjectType.Crate, 0, 0, new ModelSlot());

        Assert.Equal([crate], ModelUsers.ObjectsUsing(_ogi.URI));
    }

    [Fact]
    public void WhatTheObjectsNeedOfTheModelIsChecked()
    {
        var model = ((IAsset)_ogi).GetData<OGIData>();
        AddObject("Crate", ITwinObject.ObjectType.Crate, 1, 1, new ModelSlot { Ogi = _ogi.URI, Animation = 0x10 });
        Assert.Empty(ModelUsers.Check(_ogi.URI, model));

        // Crash's character code reads 11 exit points and 29 joint IDs of his model, and moves it by its animator
        AddObject("Crash", ITwinObject.ObjectType.Character, 11, 29, new ModelSlot { Ogi = _ogi.URI, Animation = 0x11 });
        var problems = ModelUsers.Check(_ogi.URI, model);

        Assert.Equal(2, problems.Count(problem => problem.Refused));
        Assert.Contains(problems, problem => problem.Refused && problem.Text.Contains("exit points 0 to 10"));
        Assert.Contains(problems, problem => problem.Refused && problem.Text.Contains("IDs 0 to 28"));
        // The object's header makes more of them than the model has, and a slot plays an animation the model doesn't have
        Assert.Contains(problems, problem => !problem.Refused && problem.Text.Contains("makes 11 exit points"));
        Assert.Contains(problems, problem => !problem.Refused && problem.Text.Contains("binds 29 joint IDs"));
        Assert.Contains(problems, problem => !problem.Refused && problem.Text.Contains("animation 0x11"));
        Assert.All(problems, problem => Assert.Equal(["Crash"], problem.Objects));
    }
}
