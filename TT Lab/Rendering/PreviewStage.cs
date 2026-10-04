using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Controls;
using TT_Lab.Rendering.Objects;

namespace TT_Lab.Rendering;

/// <summary>
/// A viewport nobody sees, taking pictures of objects on their own the way saving a prefab from a scene takes its picture
/// (<see cref="PreviewRender"/>), for what no scene shows
/// </summary>
public sealed class PreviewStage : IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    private readonly ViewportHost _host = new();
    private RenderContext? _context;
    private Renderer? _renderer;
    private Scene.Scene? _scene;

    private PreviewStage()
    {
    }

    /// <summary>
    /// A stage rendering frames of the size, none when no GL context could be made
    /// </summary>
    public static async Task<PreviewStage?> StartAsync(int frameSize)
    {
        var stage = new PreviewStage();
        try
        {
            var context = await stage._host.StartOffscreen(new PixelSize(frameSize, frameSize)).WaitAsync(StartTimeout);
            if (context == null)
            {
                stage.Dispose();
                return null;
            }

            stage._context = context;
            await stage._host.RunAsync(() =>
            {
                var renderer = new Renderer(context) { DrawEditorPrimitives = false };
                var scene = new Scene.Scene(context, "PREVIEW_STAGE");
                scene.UpdateResolution(new vec2(frameSize, frameSize));
                renderer.RegisterForRendering(scene.Camera);
                renderer.Camera = scene.Camera;
                renderer.FireSceneInitialized();
                renderer.RegisterForRendering(scene, true);
                renderer.RegisterForUpdating(scene);
                stage._renderer = renderer;
                stage._scene = scene;
                return true;
            });
            return stage;
        }
        catch (Exception e) when (e is TimeoutException or TaskCanceledException)
        {
            stage.Dispose();
            return null;
        }
    }

    /// <summary>
    /// The PNG of what the factory makes, on its own and framed from a three quarter view, none when it made nothing. The factory runs on
    /// the render thread, the data it reads stays in a scope of its own that's let go of once the picture is taken
    /// </summary>
    public async Task<byte[]?> TakeAsync(Func<RenderContext, EditableObject?> make, int size)
    {
        var pictures = await TakeManyAsync([context => make(context) is { } made ? [made] : []], size);
        return pictures.Length == 0 ? null : pictures[0];
    }

    /// <summary>
    /// The PNGs of what each factory makes, each on its own like <see cref="TakeAsync"/>, none for one that made nothing or failed. The
    /// data they read stays in one scope until every picture is taken, the materials and textures they share are read once
    /// </summary>
    public Task<byte[]?[]> TakeManyAsync(IReadOnlyList<Func<RenderContext, IReadOnlyList<EditableObject>>> makers, int size)
    {
        var context = _context;
        var renderer = _renderer;
        var scene = _scene;
        if (context == null || renderer == null || scene == null)
        {
            return Task.FromResult(new byte[]?[makers.Count]);
        }

        return _host.RunAsync(() =>
        {
            using var scope = new AssetDataScope();
            var pictures = new byte[]?[makers.Count];
            for (var i = 0; i < makers.Count; i++)
            {
                try
                {
                    pictures[i] = Take(context, renderer, scene, makers[i](context), size);
                }
                catch (Exception e)
                {
                    Log.WriteLine($"A picture couldn't be taken: {e.Message}", Log.LogType.Debug);
                }
            }

            return pictures;
        });
    }

    private static byte[]? Take(RenderContext context, Renderer renderer, Scene.Scene scene, IReadOnlyList<EditableObject> shown, int size)
    {
        if (shown.Count == 0)
        {
            return null;
        }

        foreach (var editableObject in shown)
        {
            scene.AddChild(editableObject);
        }

        try
        {
            using var picture = PreviewRender.Render(context, renderer, scene, shown, shown, size);
            if (picture == null)
            {
                return null;
            }

            using var png = new MemoryStream();
            picture.Save(png);
            return png.ToArray();
        }
        finally
        {
            foreach (var editableObject in shown)
            {
                scene.RemoveChild(editableObject);
            }
        }
    }

    public void Dispose()
    {
        _host.Dispose();
    }
}
