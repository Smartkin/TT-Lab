using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.Rendering;

/// <summary>
/// Pictures of some of a scene's objects on their own: the rest hidden, the editor's visuals left out and the camera put at a three
/// quarter view framing them. Runs on the render thread between two frames and puts everything back before the next
/// </summary>
public static class PreviewRender
{
    // Where the camera looks from, relative to the objects' center
    private static readonly vec3 ViewDirection = new vec3(1.0f, 0.8f, 1.0f).Normalized;
    private const float Margin = 1.25f;

    public static Bitmap? Render(RenderContext context, Renderer renderer, Scene.Scene scene, IReadOnlyList<ViewportObject> objects, Func<ViewportObject, bool> isShown, int size)
    {
        return Render(context, renderer, scene, objects.Select(viewportObject => viewportObject.Render).ToList(),
            objects.Where(isShown).Select(viewportObject => viewportObject.Render).ToList(), size);
    }

    /// <summary>
    /// The picture of the shown objects, every other object of the scene's hidden while it's taken
    /// </summary>
    public static Bitmap? Render(RenderContext context, Renderer renderer, Scene.Scene scene, IReadOnlyList<EditableObject> objects, IReadOnlyList<EditableObject> shown, int size)
    {
        var width = (int)context.ViewportSize.x;
        var height = (int)context.ViewportSize.y;
        if (shown.Count == 0 || width < 2 || height < 2)
        {
            return null;
        }

        var camera = scene.Camera;
        var savedCamera = camera.LocalTransform;
        var saved = objects.Select(editableObject => (Object: editableObject, editableObject.IsVisible, editableObject.IsSelected)).ToList();
        var savedPrimitives = renderer.DrawEditorPrimitives;
        // The models' collision hulls are the editor's wireframes, not what the models look like
        var hulls = shown.SelectMany(ModelsUnder).Where(model => model.ShowHulls).ToList();
        try
        {
            foreach (var model in hulls)
            {
                model.ShowHulls = false;
            }

            foreach (var (editableObject, _, _) in saved)
            {
                editableObject.IsVisible = false;
            }

            // Selected objects are tinted, the picture shows them as they are
            foreach (var editableObject in shown)
            {
                editableObject.IsVisible = true;
                if (editableObject.IsSelected)
                {
                    editableObject.Deselect();
                }
            }

            var (center, radius) = Bounds(shown);
            var frameCamera = camera.GetFrameCamera();
            // The picture is the middle square of the frame, framed by the narrower of the two fields of view
            var fov = frameCamera.FovY;
            if (width < height)
            {
                fov = 2.0f * MathF.Atan(MathF.Tan(fov * 0.5f) * width / height);
            }

            var distance = Math.Max(radius, 0.25f) / MathF.Tan(fov * 0.5f) * Margin;
            camera.SetLocalTransform(mat4.LookAt(center + ViewDirection * distance, center, vec3.UnitY).Inverse);
            scene.UpdateRenderTransform();
            renderer.DrawEditorPrimitives = false;
            renderer.RenderNow(0.0f);
            return ReadFrame(context, width, height, size);
        }
        finally
        {
            renderer.DrawEditorPrimitives = savedPrimitives;
            foreach (var model in hulls)
            {
                model.ShowHulls = true;
            }

            foreach (var (editableObject, wasVisible, wasSelected) in saved)
            {
                editableObject.IsVisible = wasVisible;
                if (wasSelected && !editableObject.IsSelected)
                {
                    editableObject.Select();
                }
            }

            camera.SetLocalTransform(savedCamera);
            scene.UpdateRenderTransform();
        }
    }

    private static IEnumerable<OGI> ModelsUnder(Renderable renderable)
    {
        if (renderable is OGI model)
        {
            yield return model;
        }

        foreach (var child in renderable.Children)
        {
            foreach (var under in ModelsUnder(child))
            {
                yield return under;
            }
        }
    }

    private static (vec3 Center, float Radius) Bounds(IEnumerable<EditableObject> objects)
    {
        var min = new vec3(float.MaxValue);
        var max = new vec3(float.MinValue);
        foreach (var bounds in objects.Select(editableObject => editableObject.GetBoundsTransform()))
        {
            var extent = vec3.Abs(bounds.Column0.xyz) + vec3.Abs(bounds.Column1.xyz) + vec3.Abs(bounds.Column2.xyz);
            min = vec3.Min(min, bounds.Column3.xyz - extent);
            max = vec3.Max(max, bounds.Column3.xyz + extent);
        }

        return ((min + max) * 0.5f, ((max - min) * 0.5f).Length);
    }

    // The frame's middle square, GL's rows go from the bottom up
    private static unsafe Bitmap ReadFrame(RenderContext context, int width, int height, int size)
    {
        var gl = context.Gl;
        var pixels = new byte[width * height * 4];
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, context.GetOutputBuffer());
        gl.ReadBuffer(ReadBufferMode.ColorAttachment0);
        fixed (byte* pointer = pixels)
        {
            gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Bgra, PixelType.UnsignedByte, pointer);
        }

        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
        var side = Math.Min(width, height);
        return PreviewImage.FromPixels(pixels, width, height, (width - side) / 2, (height - side) / 2, side, size);
    }
}
