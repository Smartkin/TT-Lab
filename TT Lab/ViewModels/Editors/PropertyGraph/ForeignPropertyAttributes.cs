using System;
using System.Collections.Generic;
using System.Reflection;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;

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
    };

    public static IReadOnlyList<Attribute> Of(PropertyInfo property)
    {
        return property.DeclaringType != null && Attributes.TryGetValue((property.DeclaringType, property.Name), out var attributes) ? attributes : [];
    }
}
