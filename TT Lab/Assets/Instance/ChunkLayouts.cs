using System.Collections.Generic;
using System.Linq;

namespace TT_Lab.Assets.Instance;

public record ChunkLayout(int Id, string Name, string Description);

/// <summary>
/// Layouts a chunk's instances are split into. The game doesn't name them, the names come from what the game keeps in each of them
/// </summary>
public static class ChunkLayouts
{
    // Only the default chunk has collision surfaces, all of them in this layout and nothing else in it
    public const int CollisionSurfaces = 7;

    public static readonly IReadOnlyList<ChunkLayout> All =
    [
        new(0, "Main", "Actors, triggers, positions and paths, used by nearly every chunk"),
        new(1, "Extra cameras", "Cameras besides the ones in layout 4, all path cameras are in here"),
        new(2, "Extra crates", "Crates and Wumpa fruit besides the ones in layout 5, used by a few chunks of the cavern"),
        new(3, "Extra actors", "Chickens, rats, butterflies and seagulls, and a few actors of chases and set pieces"),
        new(4, "Cameras", "Cameras of most chunks"),
        new(5, "Crates", "Crates, Wumpa fruit and gems"),
        new(6, "AI navigation", "AI positions and AI paths"),
        new(CollisionSurfaces, "Collision surfaces", "Only for collision surfaces"),
    ];

    /// <summary>
    /// Layouts the asset can be put in
    /// </summary>
    public static IReadOnlyList<ChunkLayout> GetChoices(IAsset? asset)
    {
        var isSurface = asset is CollisionSurface;
        return All.Where(layout => (layout.Id == CollisionSurfaces) == isSurface).ToList();
    }

    public static ChunkLayout? Find(int? id)
    {
        if (id == null)
        {
            return null;
        }

        return All.FirstOrDefault(layout => layout.Id == id) ?? new ChunkLayout(id.Value, $"Layout {id}", "Not a layout the game has");
    }
}
