using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Passes;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Services;

public enum PassPriority
{
    Skydome = -10,
    // The scene in the game's order (SceneOrder): the buckets up to the particles', the particles, the buckets after them
    Scene = 1,
    Particles = 2,
    LateScene = 3,
    // TT Lab's own, after the game's
    VolumeInsides = 40000,
    VolumeOutsides = 40001,
    Preview = 60000,
    PreviewSilhouette = 60001,
    Billboards = 100000,
}

/// <summary>
/// One of a pass's draws: a renderable drawing itself (<see cref="Shader"/> -1) or a shader of a batch's material, the first draw of a
/// material reading a copy of the frame taking the copy first (<see cref="ScreenCopy"/>)
/// </summary>
public readonly record struct PassEntry(Renderable Renderable, int Shader, bool CopiesScreen = false)
{
    public void Draw(float delta)
    {
        if (Shader >= 0 && Renderable is RenderBatch batch)
        {
            batch.RenderShader(Shader, delta);
            return;
        }

        Renderable.Render(delta);
    }
}

public class PassService
{
    public const string EVERY_PASS = "EveryPass";
    // Every part of the sky draws in this one pass, blended or not
    public const string SkydomePassName = nameof(TwinShader.Type.UnlitSkydome);
    public const string ScenePassName = "SCENE";
    public const string LateScenePassName = "LATE_SCENE";
    public const string ParticlesPass = "PARTICLES";
    public const string VolumeInsidesPass = "VOLUME_INSIDES";
    public const string VolumeOutsidesPass = "VOLUME_OUTSIDES";
    public const string PreviewPassName = "PREVIEW";
    public const string PreviewSilhouettePassName = "PREVIEW_SILHOUETTE";
    public const string BillboardsPassName = "CUSTOM_BILLBOARDS";

    // The game draws the particle pages in bucket 22, after the level's own materials of the bucket (UpdateAndDrawParticles comes after
    // the scene is submitted): the scene's first pass has the buckets up to it, the later ones go after the particles
    private const uint ParticlesBucket = 22;

    private readonly Dictionary<string, RenderPass> _passes = [];
    private readonly Dictionary<RenderPass, PassContent> _contents = [];
    private readonly List<(PassPriority Priority, RenderPass Pass)> _sortedPasses = [];

    private sealed class PassContent
    {
        // Ordered by their priority, equal ones in the order they came
        public readonly List<(long Priority, PassEntry Entry)> Fixed = [];
        public readonly List<(RenderBatch Batch, int Shader)> SceneDraws = [];
        public PassEntry[]? Entries;
        public int EntriesDrawnAloneVersion = -1;
    }

    public PassService(RenderContext context)
    {
        var generic = context.GetProgram("Generic");
        RegisterPass(new SkydomePass(context, SkydomePassName, generic), PassPriority.Skydome);
        RegisterPass(new GenericPass(context, ScenePassName, generic, TwinShader.Type.StandardUnlit), PassPriority.Scene);
        RegisterPass(new ParticlePass(context, ParticlesPass, context.GetProgram("Particle")), PassPriority.Particles);
        RegisterPass(new GenericPass(context, LateScenePassName, generic, TwinShader.Type.StandardUnlit), PassPriority.LateScene);
        RegisterPass(new VolumePass(context, VolumeInsidesPass, generic, TriangleFace.Front), PassPriority.VolumeInsides);
        RegisterPass(new VolumePass(context, VolumeOutsidesPass, generic, TriangleFace.Back), PassPriority.VolumeOutsides);
        RegisterPass(new PreviewPass(context, PreviewPassName, generic, false), PassPriority.Preview);
        RegisterPass(new PreviewPass(context, PreviewSilhouettePassName, generic, true), PassPriority.PreviewSilhouette);
        RegisterPass(new BillboardPass(context, BillboardsPassName, generic), PassPriority.Billboards);

        SkydomePasses = [_passes[SkydomePassName]];
        ScenePass = _passes[ScenePassName];
        ParticlePass = _passes[ParticlesPass];
        LateScenePass = _passes[LateScenePassName];
        OverlayPasses = [_passes[VolumeInsidesPass], _passes[VolumeOutsidesPass], _passes[PreviewPassName], _passes[PreviewSilhouettePassName]];
        BillboardPasses = [_passes[BillboardsPassName]];
        DrawnPasses = _sortedPasses.Select(entry => entry.Pass).ToArray();
    }

    /// <summary>
    /// Changes whenever a renderable goes into or out of a pass
    /// </summary>
    public int Version { get; private set; }

    public IList<RenderPass> SkydomePasses { get; }

    /// <summary>
    /// The scene's draws in the game's order (<see cref="SceneOrder"/>) up to the particles' bucket
    /// </summary>
    public RenderPass ScenePass { get; }

    public RenderPass ParticlePass { get; }

    /// <summary>
    /// The scene's draws past the particles' bucket, and TT Lab's own see-through visuals after them
    /// </summary>
    public RenderPass LateScenePass { get; }

    /// <summary>
    /// TT Lab's own draws after the game's: trigger and camera volumes, what's about to be placed
    /// </summary>
    public IList<RenderPass> OverlayPasses { get; }

    public IList<RenderPass> BillboardPasses { get; }

    /// <summary>
    /// Every pass in the order the frame draws them
    /// </summary>
    public IList<RenderPass> DrawnPasses { get; }

    /// <summary>
    /// A renderable drawing itself in the passes it names, by their priority, <see cref="EVERY_PASS"/> for all of them (the camera)
    /// </summary>
    public void RegisterRenderableInPasses(Renderable renderable, (string, int)[] passPriorities)
    {
        Version++;
        if (passPriorities is [(EVERY_PASS, var everyPriority)])
        {
            foreach (var content in _contents.Values)
            {
                AddFixed(content, everyPriority, new PassEntry(renderable, -1));
            }

            return;
        }

        foreach (var (passName, priority) in passPriorities)
        {
            if (!_passes.TryGetValue(passName, out var pass))
            {
                Log.WriteLine($"No pass {passName} to draw {renderable.Name} in", Log.LogType.Warning);
                continue;
            }

            AddFixed(_contents[pass], priority, new PassEntry(renderable, -1));
        }
    }

    /// <summary>
    /// A batch's draws: a preview draws in the preview passes as its material would, every other batch each shader of its material in the
    /// pass the shader goes in (the sky's, one a shader names for itself, the scene's)
    /// </summary>
    public void RegisterBatch(RenderBatch batch)
    {
        Version++;
        if (batch.IsPreview)
        {
            AddFixed(_contents[_passes[PreviewPassName]], 0, new PassEntry(batch, -1));
            AddFixed(_contents[_passes[PreviewSilhouettePassName]], 0, new PassEntry(batch, -1));
            return;
        }

        var buffer = batch.Buffer;
        for (var shader = 0; shader < buffer.ShaderCount; shader++)
        {
            var named = buffer.NamedPassOf(shader);
            if (named == SkydomePassName)
            {
                AddFixed(_contents[_passes[SkydomePassName]], buffer.SkyPriorityOf(shader), new PassEntry(batch, shader));
                continue;
            }

            Debug.Assert(named == null || _passes.ContainsKey(named), $"No pass {named} for a shader to draw in");
            if (named != null && _passes.TryGetValue(named, out var pass))
            {
                AddFixed(_contents[pass], 0, new PassEntry(batch, shader));
                continue;
            }

            var scene = buffer.Bucket <= ParticlesBucket ? ScenePass : LateScenePass;
            var content = _contents[scene];
            content.SceneDraws.Add((batch, shader));
            content.Entries = null;
        }
    }

    /// <summary>
    /// Takes every draw of the renderable out of every pass
    /// </summary>
    public void UnregisterRenderableInPasses(Renderable renderable)
    {
        Version++;
        foreach (var content in _contents.Values)
        {
            var removed = content.Fixed.RemoveAll(entry => entry.Entry.Renderable == renderable);
            removed += content.SceneDraws.RemoveAll(draw => draw.Batch == renderable);
            if (removed > 0)
            {
                content.Entries = null;
            }
        }
    }

    /// <summary>
    /// Takes every draw of the batches out of every pass at once, the batches of the models a scene let go of: a scenery's thousands one
    /// after another would go through every pass's draws for each
    /// </summary>
    public void UnregisterBatches(IReadOnlyCollection<RenderBatch> batches)
    {
        if (batches.Count == 0)
        {
            return;
        }

        Version++;
        var gone = new HashSet<Renderable>(batches, ReferenceEqualityComparer.Instance);
        foreach (var content in _contents.Values)
        {
            var removed = content.Fixed.RemoveAll(entry => gone.Contains(entry.Entry.Renderable));
            removed += content.SceneDraws.RemoveAll(draw => gone.Contains(draw.Batch));
            if (removed > 0)
            {
                content.Entries = null;
            }
        }
    }

    public RenderPass GetRenderPass(string name)
    {
        Debug.Assert(_passes.ContainsKey(name), $"Attempting to get unregistered pass {name}!");
        return _passes[name];
    }

    /// <summary>
    /// What the pass draws, in order: what draws itself by its priority (the camera first), then a scene's draws in the game's order
    /// </summary>
    public IReadOnlyList<PassEntry> GetEntries(RenderPass pass)
    {
        var content = _contents[pass];
        // Whether a batch has skins decides where its draws go
        var drawnAlone = RenderBatch.DrawnAloneVersion;
        if (content.Entries != null && (content.SceneDraws.Count == 0 || content.EntriesDrawnAloneVersion == drawnAlone))
        {
            return content.Entries;
        }

        var entries = new List<PassEntry>(content.Fixed.Count + content.SceneDraws.Count);
        entries.AddRange(content.Fixed.Select(entry => entry.Entry));
        var draws = content.SceneDraws;
        var order = SceneOrder.Order(draws.Select(draw => new SceneOrder.Draw(draw.Batch.Buffer.Bucket, draw.Batch.DrawsAlone, draw.Batch.Buffer.Material,
            draw.Batch.Sequence, draw.Shader, draw.Batch.Buffer.GameModel, draw.Batch.Buffer.PartIndex)).ToList());
        object? previousMaterial = null;
        foreach (var index in order)
        {
            var (batch, shader) = draws[index];
            var material = batch.Buffer.Material;
            // The game copies the frame for a material reading the copy once, as it starts drawing it (WriteScreenCopy)
            var copies = material != previousMaterial && batch.Buffer.ReadsScreenCopy;
            entries.Add(new PassEntry(batch, shader, copies));
            previousMaterial = material;
        }

        content.Entries = entries.ToArray();
        content.EntriesDrawnAloneVersion = drawnAlone;
        return content.Entries;
    }

    private static void AddFixed(PassContent content, long priority, PassEntry entry)
    {
        var index = content.Fixed.FindLastIndex(existing => existing.Priority <= priority) + 1;
        content.Fixed.Insert(index, (priority, entry));
        content.Entries = null;
    }

    private void RegisterPass(RenderPass renderPass, PassPriority priority)
    {
        Debug.Assert(!_passes.ContainsKey(renderPass.Name), $"Attempting to add existing pass {renderPass.Name}!");
        _passes.Add(renderPass.Name, renderPass);
        _contents.Add(renderPass, new PassContent());
        _sortedPasses.Add((priority, renderPass));
        _sortedPasses.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }
}
