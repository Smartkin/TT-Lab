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
        // Camera subtypes (the decomp's CameraPoint2At, CameraLine2At and CameraSplineCamera)
        [(typeof(CameraPoint2), nameof(CameraPoint2.Mode))] =
        [
            new EditableAttribute { Hint = "Where between the target and the point the camera stands: the distance's share of the way from the target to the point, the distance in units from the target towards the point, or the same but no further than the point (the game's levels use the first two)" },
        ],
        [(typeof(CameraPoint2), nameof(CameraPoint2.Distance))] =
        [
            new EditableAttribute { Hint = "The share of the way from the target to the point (mode 0, 0 to 1) or the units from the target (modes 1 and 2)" },
        ],
        [(typeof(CameraLine2), nameof(CameraLine2.NearDistance))] =
        [
            new EditableAttribute { Hint = "Up to this flat distance of the target from the line's start the camera stays at the start. Keep it 0, like every camera of the game's levels: between the distances the game adds it to the share along the line, which puts the camera past the line" },
        ],
        [(typeof(CameraLine2), nameof(CameraLine2.FarDistance))] =
        [
            new EditableAttribute { Hint = "From this flat distance of the target from the line's start on the camera stays at the line's end, closer it goes along by the share of the way between the distances" },
        ],
        [(typeof(CameraSpline), nameof(CameraSpline.PathPoints))] =
        [
            new EditableAttribute { Hint = "The samples the camera slides along, each a place and whether it's a key: a key has an offset along the curve and a share of the way toward the target, which the camera takes between the keys around where it is, the other samples pass over (their W is a word of the game's holding these). New samples pass over, new ends are keys of no offset and share" },
            new EditorParamAttribute(DocumentCollectionViewModel.ElementEditor, typeof(SplineSampleEditorDesc)),
        ],
        [(typeof(CameraSpline), nameof(CameraSpline.SplineFlags))] =
        [
            new EditableAttribute { Hint = "Takes Offset takes the subtype's offset as the offset along the curve instead of the samples'. The word's other bits are leftovers of the tools the game never reads, kept as they are" },
        ],
        // Scenery lights (verified in the PAL executable, GatherStrongestLights 0x1c7f50 and the lights' functions)
        [(typeof(Light), nameof(Light.Intensity))] =
        [
            new EditableAttribute { Hint = "Multiplies the color. Of the directional, point and spot lights the game lights an object by the 3 with the highest intensity at its position, point and spot lights' intensity falls off with the distance" },
        ],
        [(typeof(Light), nameof(Light.Color))] =
        [
            new EditableAttribute { EditorDescType = typeof(LightColorEditorDesc), Hint = "The light's color. The game's lights have colors whose red, green and blue add up to 1 and their brightness in the intensity: the picker shows the hue at full brightness and keeps that total" },
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
        // What camera subtypes never read (the decomp's subtypes: only lines, paths, splines and 1C09 read the offset, zones don't even
        // have the follow values in their files) and what they read only with a flag
        [(typeof(CameraSubBase), nameof(CameraSubBase.Follow))] =
        [
            new EditableAttribute { Hint = "How the camera rig's followers move to the subtype's points: straight there, their own way at their default rate, or at the follow rate. 1 (own way) on every camera of the game's levels but the zones" },
        ],
        [(typeof(CameraSubBase), nameof(CameraSubBase.FollowRate))] =
        [
            new EditableAttribute { Hint = "With At Rate, the share of the way a second the followers move to the subtype's points" },
            new EditorLinkedFieldAttribute(typeof(FollowRateRead), nameof(CameraSubBase.Follow)),
        ],
        [(typeof(CameraSpline), nameof(CameraSubBase.Offset))] =
        [
            new EditableAttribute { Hint = "With Takes Offset, how far along the spline the camera leads (positive) or trails the target's nearest point, in units" },
            new EditorLinkedFieldAttribute(typeof(SplineOffsetRead), nameof(CameraSpline.SplineFlags)),
        ],
        [(typeof(CameraPoint), nameof(CameraSubBase.Offset))] = [new EditorHiddenAttribute()],
        [(typeof(CameraPoint2), nameof(CameraSubBase.Offset))] = [new EditorHiddenAttribute()],
        [(typeof(CameraLine2), nameof(CameraSubBase.Offset))] = [new EditorHiddenAttribute()],
        [(typeof(BossCamera), nameof(CameraSubBase.Offset))] = [new EditorHiddenAttribute()],
        [(typeof(CameraZone), nameof(CameraSubBase.Offset))] = [new EditorHiddenAttribute()],
        [(typeof(CameraZone), nameof(CameraSubBase.Follow))] = [new EditorHiddenAttribute()],
        [(typeof(CameraZone), nameof(CameraSubBase.FollowRate))] = [new EditorHiddenAttribute()],
        [(typeof(BossCamera), nameof(BossCamera.RadiusBlend))] =
        [
            new EditableAttribute { Hint = "With Uses Distance Curves, the radius becomes the target's distance from the axis plus (radius - distance) times this" },
            new EditorLinkedFieldAttribute(typeof(BossCurvesRead), nameof(BossCamera.UsesDistanceCurves)),
        ],
        [(typeof(BossCamera), nameof(BossCamera.NearHeightOffset))] =
        [
            new EditableAttribute { Hint = "With Uses Distance Curves, the height above the target at the arena's axis, fading to the far one at the radius" },
            new EditorLinkedFieldAttribute(typeof(BossCurvesRead), nameof(BossCamera.UsesDistanceCurves)),
        ],
        [(typeof(BossCamera), nameof(BossCamera.FarHeightOffset))] =
        [
            new EditableAttribute { Hint = "With Uses Distance Curves, the height above the target at the radius" },
            new EditorLinkedFieldAttribute(typeof(BossCurvesRead), nameof(BossCamera.UsesDistanceCurves)),
        ],
        [(typeof(BossCamera), nameof(BossCamera.DistanceIncludesHeight))] =
        [
            new EditableAttribute { Hint = "With Uses Distance Curves, whether the target's distance from the axis counts its height too" },
            new EditorLinkedFieldAttribute(typeof(BossCurvesRead), nameof(BossCamera.UsesDistanceCurves)),
        ],
    };

    // The class the property is shown for first, a subclass can have its own attributes for a property of its base class. An overridden
    // property is declared by the class overriding it, its attributes are its base class's
    public static IReadOnlyList<Attribute> Of(PropertyInfo property)
    {
        for (var type = property.ReflectedType ?? property.DeclaringType; type != null; type = type.BaseType)
        {
            if (Attributes.TryGetValue((type, property.Name), out var attributes))
            {
                return attributes;
            }
        }

        return [];
    }
}
