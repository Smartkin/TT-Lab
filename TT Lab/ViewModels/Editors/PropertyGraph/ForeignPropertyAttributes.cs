using System;
using System.Collections.Generic;
using System.Reflection;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Common.Lights;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

/// <summary>
/// Editor attributes of the properties of foreign types (TwinTech's, see <see cref="DocumentMetadataCache"/>), which can't carry
/// TT Lab's attributes themselves
/// </summary>
public static class ForeignPropertyAttributes
{
    private static readonly Dictionary<(Type Type, string Property), Attribute[]> Attributes = new()
    {
        // The game reads both and uses each as the other's inverse (BossCameraAt 0x279810 takes the target into the arena through one
        // and the camera back out through the other), so only the arena's own matrix is edited, here or by dragging the arena
        [(typeof(BossCamera), nameof(BossCamera.ArenaToWorld))] =
        [
            new EditableAttribute { Hint = "Where the arena is in the world, the camera goes around its Y axis. Dragging the arena in the viewport moves it, World To Arena follows" },
        ],
        [(typeof(BossCamera), nameof(BossCamera.WorldToArena))] =
        [
            new EditableAttribute { Hint = "The inverse of Arena To World, which the game takes the target into the arena's space with. It follows Arena To World" },
            new EditorLinkedFieldAttribute(typeof(InverseMatrixChange), nameof(BossCamera.ArenaToWorld)),
            new EditorReadOnlyAttribute(),
        ],
        // Scenery lights (verified in the PAL executable, GatherStrongestLights 0x1c7f50 and the lights' functions)
        [(typeof(Light), nameof(Light.Intensity))] =
        [
            new EditableAttribute { Hint = "Multiplies the color. Of the directional, point and spot lights the game lights an object by the 3 with the highest intensity at its position, point and spot lights' intensity falls off with the distance" },
        ],
        [(typeof(Light), nameof(Light.Color))] =
        [
            new EditableAttribute { Hint = "Red, green and blue in X, Y and Z, W unused. The game's lights have colors adding up to 1 and their brightness in the intensity" },
        ],
        [(typeof(Light), nameof(Light.Position))] =
        [
            new EditableAttribute { Hint = "Where the light is, W 1. Ambient and directional lights light everything wherever they are" },
        ],
        // Bit 8 of the header and the tools' box around the light, which the game works out again at load. Neither is ever read
        [(typeof(Light), nameof(Light.Enabled))] = [new EditorHiddenAttribute()],
        [(typeof(Light), nameof(Light.BoundsMin))] = [new EditorHiddenAttribute()],
        [(typeof(Light), nameof(Light.BoundsMax))] = [new EditorHiddenAttribute()],
        [(typeof(Light), nameof(Light.Type))] = [new EditorHiddenAttribute()],
        [(typeof(DirectionalLight), nameof(DirectionalLight.Direction))] =
        [
            new EditableAttribute { Hint = "Points at where the light comes from, W 0. The game takes it as it is, a shorter vector lights less" },
        ],
        [(typeof(DirectionalLight), nameof(DirectionalLight.Leftover))] = [new EditorHiddenAttribute()],
        [(typeof(PointLight), nameof(PointLight.AttenuationPower))] =
        [
            new EditableAttribute { Hint = "How many times the falloff 25 / (d² + 25) multiplies the intensity d units away, 0 for none (the retail lights have 0 to 2). The way to the light, which the game lights the faces by, is times the falloff too" },
        ],
        [(typeof(SpotLight), nameof(SpotLight.Direction))] =
        [
            new EditableAttribute { Hint = "The way the light shines, W 0" },
        ],
        [(typeof(SpotLight), nameof(SpotLight.ConeAngle))] =
        [
            new EditableAttribute { Caption = "Cone Angle (degrees)", Hint = "The whole cone the light shines at full strength in, the inner cone cosine is made from it", EditorDescType = typeof(AngleEditorDesc) },
        ],
        [(typeof(SpotLight), nameof(SpotLight.FalloffAngle))] =
        [
            new EditableAttribute { Caption = "Falloff Angle (degrees)", Hint = "How far past the cone the light fades out, the outer cone cosine is made from both angles", EditorDescType = typeof(AngleEditorDesc) },
        ],
        // The game lights with the cosines (SpotLightAt 0x1c8f18), comparing them with the dot product of its direction and the way to the
        // light times the falloff, which isn't a unit vector: the cone gets wider a few units away
        [(typeof(SpotLight), nameof(SpotLight.InnerConeCosine))] =
        [
            new EditableAttribute { Hint = "What the game lights with, made from the cone angle: full strength above it" },
            new EditorLinkedFieldAttribute(typeof(SpotConeCosineChange), nameof(SpotLight.ConeAngle)),
            new EditorReadOnlyAttribute(),
        ],
        [(typeof(SpotLight), nameof(SpotLight.OuterConeCosine))] =
        [
            new EditableAttribute { Hint = "What the game lights with, made from the cone and falloff angles: nothing below it, fading in up to the inner cone cosine" },
            new EditorLinkedFieldAttribute(typeof(SpotConeCosineChange), nameof(SpotLight.ConeAngle)),
            new EditorLinkedFieldAttribute(typeof(SpotConeCosineChange), nameof(SpotLight.FalloffAngle)),
            new EditorReadOnlyAttribute(),
        ],
        [(typeof(SpotLight), nameof(SpotLight.AttenuationPower))] =
        [
            new EditableAttribute { Hint = "How many times the falloff 25 / (d² + 25) multiplies the intensity d units away, like a point light's" },
        ],
        [(typeof(SpotLight), nameof(SpotLight.SpotExponent))] =
        [
            new EditableAttribute { Hint = "The power the game raises the cone's dot product to before multiplying the intensity by it, the low 8 bits (0 in the retail data)" },
        ],
        [(typeof(SpotLight), nameof(SpotLight.ConeAngleDegrees))] = [new EditorHiddenAttribute()],
        [(typeof(SpotLight), nameof(SpotLight.FalloffAngleDegrees))] = [new EditorHiddenAttribute()],
    };

    // An overridden property is declared by the class overriding it, its attributes are its base class's
    public static IReadOnlyList<Attribute> Of(PropertyInfo property)
    {
        for (var type = property.DeclaringType; type != null; type = type.BaseType)
        {
            if (Attributes.TryGetValue((type, property.Name), out var attributes))
            {
                return attributes;
            }
        }

        return [];
    }
}
