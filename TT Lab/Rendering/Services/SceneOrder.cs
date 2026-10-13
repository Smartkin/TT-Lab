using System;
using System.Collections.Generic;

namespace TT_Lab.Rendering.Services;

/// <summary>
/// The order the game draws a frame's scene in (the decomp's buckets.cpp, materials.cpp, models.cpp), nothing sorted by depth: its 28
/// buckets one after the other, every shader type mixed and blended or not. In a bucket the skins and blend skins come first, as they're
/// submitted (drawn directly into the bucket), then every other material in the order of its first draw, all of its draws together: model
/// after model in the order of their first draw, a block of a model's instances its parts one after the other, each with every shader of
/// the material in turn. The game submits the scenery in its tree's order, the linked chunks' after it, then the instances, the order the
/// chunk's objects are made in and TT Lab makes their batches in, so the order the batches were made in stands for the game's
/// </summary>
public static class SceneOrder
{
    /// <summary>
    /// What a draw is ordered by: its material's bucket, whether it's drawn directly (skins), its material, the model's rank (the order its
    /// batch was made in), the shader of the material it draws, and the game's model it draws a part of (every placement of a scenery's
    /// mesh is a batch of its own) with the part's place in it
    /// </summary>
    public readonly record struct Draw(uint Bucket, bool Direct, object Material, long ModelRank, int Shader, int? GameModel = null, int Part = 0);

    /// <summary>
    /// The draws' indexes in the order they're drawn
    /// </summary>
    public static int[] Order(IReadOnlyList<Draw> draws)
    {
        // A material is listed at its first draw, its later ones join it there. A model's instances go into a block that opens at its first
        // draw (models.cpp's WriteSubModels): every placement of it is drawn there
        var materialRanks = new Dictionary<object, long>(ReferenceEqualityComparer.Instance);
        var gameModelRanks = new Dictionary<int, long>();
        foreach (var draw in draws)
        {
            if (draw.Direct)
            {
                continue;
            }

            if (!materialRanks.TryGetValue(draw.Material, out var rank) || draw.ModelRank < rank)
            {
                materialRanks[draw.Material] = draw.ModelRank;
            }

            if (draw.GameModel is { } gameModel && (!gameModelRanks.TryGetValue(gameModel, out rank) || draw.ModelRank < rank))
            {
                gameModelRanks[gameModel] = draw.ModelRank;
            }
        }

        var keys = new (uint Bucket, int Group, long MaterialRank, long GameModelRank, int Part, long ModelRank, int Shader)[draws.Count];
        for (var i = 0; i < draws.Count; i++)
        {
            var draw = draws[i];
            keys[i] = draw.Direct
                ? (draw.Bucket, 0, draw.ModelRank, draw.ModelRank, 0, draw.ModelRank, draw.Shader)
                : (draw.Bucket, 1, materialRanks[draw.Material], draw.GameModel is { } gameModel ? gameModelRanks[gameModel] : draw.ModelRank, draw.Part,
                    draw.ModelRank, draw.Shader);
        }

        var order = new int[draws.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        // The index last keeps draws of the same key (the same model's shader twice) as they came
        Array.Sort(order, (a, b) =>
        {
            var compared = keys[a].CompareTo(keys[b]);
            return compared != 0 ? compared : a.CompareTo(b);
        });
        return order;
    }
}
