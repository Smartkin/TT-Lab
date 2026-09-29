using System.Collections.Generic;
using System.Linq;

namespace TT_Lab.Assets.Graphics;

public record RenderBucket(uint Id, string Name, string Description);

/// <summary>
/// The game's 28 render buckets (its DMA chain managers) a material's draws go into, drawn in bucket order. The names come from what
/// the game's materials and code use each for, buckets 0-4 are in DMA chain 0, 5-20 in chain 1 and 21-27 in chain 2. A bucket has no
/// GS state of its own: every material sends its shaders' settings (PRMODE, FBA, ZBUF with the depth mask, ALPHA, TEST with the depth
/// test always on, TEX1, CLAMP) with every draw. What's fixed is what the game's code puts into some of them, found in the PAL
/// executable: the frame's setup and clear at the head of 0, the debug drawing in 4, a darkening pass in 21, the particle pages'
/// shaders it makes itself in 22, the Distortion particles in 23 and the movies in 27
/// </summary>
public static class RenderBuckets
{
    public const uint Count = 28;

    public static readonly IReadOnlyList<RenderBucket> All = Enumerable.Range(0, (int)Count).Select(id => Describe((uint)id)).ToList();

    public static RenderBucket Find(uint id)
    {
        return id < Count ? All[(int)id] : new RenderBucket(id, "Not a bucket", "The game only has buckets 0 to 27");
    }

    private static RenderBucket Describe(uint id)
    {
        var chain = id < 5 ? 0 : id < 21 ? 1 : 2;
        var (name, use) = id switch
        {
            0 => ("Skydomes", "Every skydome's materials. The frame starts here: ahead of them the game sets up the frame and depth buffers and clears the screen"),
            2 => ("Opaque", "Opaque scenery and objects, most of the game's materials"),
            3 => ("Opaque global", "Opaque materials of the startup chunk's objects"),
            4 => ("Debug", "The engine's debug drawing goes here, off in the retail game. No material uses it"),
            >= 6 and <= 19 => ($"Blended {id - 5}", "Alpha-blended materials, later buckets draw over earlier ones"),
            20 => ("Decals", "The startup decal's material and a few levels' layered materials"),
            21 => ("Darkening pass", "For some chunks the game draws extra geometry into a second buffer here and takes a quarter of it away from the screen. One global material is in it"),
            22 => ("Particles", "The particle pages' shaders, which the game makes itself and puts here whatever the page materials say, and the default chunk's decals"),
            23 => ("Distortion", "The Distortion particles, drawn over a copy of the frame. No material uses it"),
            24 => ("UI", "The menus' and HUD's materials"),
            26 => ("Fonts", "The fonts' pages and two level materials"),
            27 => ("Movies", "The game draws the movies' frames here, last. No material uses it"),
            _ => ("Unused", "No material of the game uses it")
        };
        return new RenderBucket(id, name, $"{use}. DMA chain {chain}");
    }
}
