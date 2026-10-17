using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Object;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// What the game objects drawing an OGI need of its model, checked before a model from Blender takes its place: a character's model the
/// exit points, joint IDs and animator the character code reads with no check (refused), the exit points and joint IDs an object's header
/// makes of its first model and the animations its slots play (warned about: the game reads past what the model has, which some of its own
/// objects do)
/// </summary>
public static class ModelUsers
{
    public sealed record Problem(string Text, bool Refused, IReadOnlyList<string> Objects);

    /// <summary>
    /// The game objects with the OGI in a model slot
    /// </summary>
    public static List<GameObject> ObjectsUsing(LabURI ogi)
    {
        var assetManager = AssetManager.Get();
        return assetManager.GetAllAssetsOf<GameObject>()
            .Where(gameObject => !gameObject.IsInternal && gameObject.References.Contains(ogi))
            .Where(gameObject => ((IAsset)gameObject).GetData<GameObjectData>().ModelSlots.Any(slot => slot.Ogi == ogi))
            .OrderBy(gameObject => gameObject.Alias)
            .ToList();
    }

    public static List<Problem> Check(LabURI ogi, OGIData model)
    {
        var problems = new List<Problem>();
        var animations = model.Animations.Select(animation => animation.ID).ToHashSet();
        foreach (var gameObject in ObjectsUsing(ogi))
        {
            var data = ((IAsset)gameObject).GetData<GameObjectData>();
            var name = gameObject.Alias;
            if (data.ModelSlots.Count > 0 && data.ModelSlots[0].Ogi == ogi)
            {
                CheckFirstModel(problems, name, data, model);
            }

            for (var slot = 0; slot < data.ModelSlots.Count; slot++)
            {
                var animation = data.ModelSlots[slot].Animation;
                if (data.ModelSlots[slot].Ogi == ogi && animation != ModelSlot.NoAnimation && !animations.Contains(animation))
                {
                    problems.Add(new Problem($"{name}'s model slot {slot} plays animation 0x{animation:X} of the model, which it doesn't have any more: " +
                                             "the slot plays none", false, [name]));
                }
            }
        }

        return problems;
    }

    // The game makes an instance's exit points and binds its joint IDs by its object's header, of its first model, by place with no check
    private static void CheckFirstModel(List<Problem> problems, string name, GameObjectData data, OGIData model)
    {
        if (data.Type == ITwinObject.ObjectType.Character)
        {
            var kind = data.IntProperties.Count > ObjectTypes.CharacterKindProperty ? data.IntProperties[ObjectTypes.CharacterKindProperty] : -1;
            if (ObjectTypes.IsPlayableCharacter(kind))
            {
                var (exitPoints, jointIds) = ObjectTypes.CharacterNeeds(kind);
                var character = ObjectTypes.PlayableCharacters[kind].Name;
                if (model.ExitPoints.Count < exitPoints)
                {
                    problems.Add(new Problem($"{name} is {character}, whose character code reads exit points 0 to {exitPoints - 1} of its model with no check: the model has " +
                                             $"{model.ExitPoints.Count}", true, [name]));
                }

                if (model.JointIdCount < jointIds)
                {
                    problems.Add(new Problem($"{name} is {character}, whose character code finds joints by IDs 0 to {jointIds - 1} with no check: the model's joints have " +
                                             $"{model.JointIdCount} IDs", true, [name]));
                }

                if (!model.GetsAnimator)
                {
                    problems.Add(new Problem($"{name} is {character}, which the character code moves by its model's animator: a model of one joint and no exit points " +
                                             "gets none", true, [name]));
                }
            }
        }

        if (data.ExitPointAmount > model.ExitPoints.Count)
        {
            problems.Add(new Problem($"{name} makes {data.ExitPointAmount} exit points of its first model, which has {model.ExitPoints.Count}: the game binds them by place " +
                                     "with no check (lower its exit points in TT Lab or add them to the model)", false, [name]));
        }

        if (data.CameraReactJointAmount > model.JointIdCount)
        {
            problems.Add(new Problem($"{name} binds {data.CameraReactJointAmount} joint IDs of its first model, whose joints have {model.JointIdCount}: its animations and " +
                                     "scripts find joints past them with no check (lower its joint IDs in TT Lab or give the model's joints IDs)", false, [name]));
        }
    }
}
