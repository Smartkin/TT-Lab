using System;
using System.Collections.Generic;
using GlmSharp;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering;

public class PrimitiveRenderer(RenderContext context)
{
    private readonly List<SphereRequest> _sphereDrawRequests = [];
    private readonly List<BoxRequest> _boxDrawRequests = [];
    
    public void DrawSphere(vec3 center, float radius, vec4 color)
    {
        _sphereDrawRequests.Add(new SphereRequest(center, radius, color));
    }

    public void DrawBox(vec3 center, vec3 halfExtents, vec4 color)
    {
        _boxDrawRequests.Add(new BoxRequest(center, halfExtents, color));
    }

    private record SphereRequest(vec3 Position, float Radius, vec4 Color);

    private record BoxRequest(vec3 Center, vec3 HalfExtents, vec4 Color);

    public void Render()
    {
        var isBlendingEnabled = context.Gl.IsEnabled(EnableCap.Blend);
        if (!isBlendingEnabled)
        {
            context.Gl.Enable(EnableCap.Blend);
        }

        context.Gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        context.Gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);

        var isDepthWriteEnabled = context.Gl.GetBoolean(GetPName.DepthWritemask);
        if (!isDepthWriteEnabled)
        {
            context.Gl.DepthMask(true);
        }

        context.Gl.DepthFunc(DepthFunction.Lequal);
        
        foreach (var sphere in _sphereDrawRequests)
        {
            RenderSphere(sphere);
        }

        foreach (var box in _boxDrawRequests)
        {
            RenderBox(box);
        }
        
        if (!isBlendingEnabled)
        {
            context.Gl.Disable(EnableCap.Blend);
        }

        if (!isDepthWriteEnabled)
        {
            context.Gl.DepthMask(false);
        }
        
        _sphereDrawRequests.Clear();
        _boxDrawRequests.Clear();
    }

    private void RenderSphere(SphereRequest sphere)
    {
        var program = context.CurrentPass.Program;
        // var spherePosLoc = program.GetUniformLocation("SpherePosition");
        // var sphereRadiusLoc = program.GetUniformLocation("SphereRadius");
        // var colorLoc = program.GetUniformLocation("Diffuse");
        // context.Gl.Uniform1(sphereRadiusLoc, sphere.Radius);
        // context.Gl.Uniform3(spherePosLoc, sphere.Position.Values);
        // context.Gl.Uniform4(colorLoc, sphere.Color.Values);
        // context.Gl.DrawArrays(PrimitiveType.TriangleFan,  0, 4);
    }

    private void RenderBox(BoxRequest box)
    {
        
    }

    //public void Init(EmbedContext gl, GLWindow window)
    //{
    //    GL = gl;
    //    this.window = window;
    //    boxBuffer = BufferGeneration.GetCubeBuffer(gl, vec3.Zero, vec3.Ones, new quat(vec3.Zero, 1.0f), new List<System.Drawing.Color>
    //    {
    //        System.Drawing.Color.White
    //    });
    //    ringBuffer = new IndexedBufferArray[RING_SEGMENT_RESOLUTION];
    //    for (int i = 0; i < RING_SEGMENT_RESOLUTION; ++i)
    //    {
    //        ringBuffer[i] = BufferGeneration.GetCircleBuffer(gl, System.Drawing.Color.White, i / (float)(RING_SEGMENT_RESOLUTION - 1));
    //    }
    //    lineBuffer = BufferGeneration.GetLineBuffer(gl, System.Drawing.Color.White);
    //    simpleAxisBuffer = BufferGeneration.GetSimpleAxisBuffer();
    //}

    //public void Delete()
    //{
    //    boxBuffer?.Delete();
    //    boxBuffer = null;
    //    window = null;
    //}

    //public void DrawBox(mat4 transform, vec4 color)
    //{
    //    if (window == null || window.Renderer == null)
    //    {
    //        return;
    //    }

    //    window.Renderer.RenderProgram.SetUniform1("Opacity", color.w);
    //    window.Renderer.RenderProgram.SetUniform3("AmbientMaterial", color.x, color.y, color.z);
    //    window.Renderer.RenderProgram.SetUniformMatrix4("StartModel", transform.Values1D);
    //    if (boxBuffer != null)
    //    {
    //        boxBuffer.Bind();
    //        DrawElements(GL, OpenGL.GL_TRIANGLES, boxBuffer.Indices.Length, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
    //        boxBuffer.Unbind();
    //    }
    //}

    //public void DrawCircle(mat4 transform, vec4 color, float segment = 1.0f)
    //{
    //    if (window == null || window.Renderer == null)
    //    {
    //        return;
    //    }

    //    window.Renderer.RenderProgram.SetUniform1("Opacity", color.w);
    //    window.Renderer.RenderProgram.SetUniform3("AmbientMaterial", color.x, color.y, color.z);
    //    window.Renderer.RenderProgram.SetUniformMatrix4("StartModel", transform.Values1D);
    //    if (ringBuffer != null)
    //    {
    //        var idx = (int)Math.Ceiling(segment * (RING_SEGMENT_RESOLUTION - 1));
    //        ringBuffer[idx].Bind();
    //        unsafe
    //        {
    //            DrawElements(GL, OpenGL.GL_TRIANGLES, ringBuffer[idx].Indices.Length, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
    //        }
    //        ringBuffer[idx].Unbind();
    //    }
    //}

    //public void DrawLine(mat4 transform, vec4 color)
    //{
    //    if (window == null || window.Renderer == null)
    //    {
    //        return;
    //    }

    //    window.Renderer.RenderProgram.SetUniform1("Opacity", color.w);
    //    window.Renderer.RenderProgram.SetUniform3("AmbientMaterial", color.x, color.y, color.z);
    //    window.Renderer.RenderProgram.SetUniformMatrix4("StartModel", transform.Values1D);
    //    if (lineBuffer != null)
    //    {
    //        lineBuffer.Bind();
    //        unsafe
    //        {
    //            DrawElements(GL, OpenGL.GL_TRIANGLES, lineBuffer.Indices.Length, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
    //        }
    //        lineBuffer.Unbind();
    //    }
    //}

    //public void DrawSimpleAxis(mat4 transform)
    //{
    //    if (window == null || window.Renderer == null)
    //    {
    //        return;
    //    }

    //    window.Renderer.RenderProgram.SetUniform1("Opacity", 1.0f);
    //    window.Renderer.RenderProgram.SetUniform3("AmbientMaterial", 1.0f, 1.0f, 1.0f);
    //    window.Renderer.RenderProgram.SetUniformMatrix4("StartModel", transform.Values1D);
    //    if (simpleAxisBuffer != null)
    //    {
    //        simpleAxisBuffer.Bind();
    //        unsafe
    //        {
    //            DrawElements(GL, OpenGL.GL_TRIANGLES, simpleAxisBuffer.Indices.Length, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
    //        }
    //        simpleAxisBuffer.Unbind();
    //    }
    //}

    //const int RING_SEGMENT_RESOLUTION = 16;
    //private MeshPtr? boxBuffer;
    //private MeshPtr? lineBuffer;
    //private MeshPtr? simpleAxisBuffer;
    //private MeshPtr[]? ringBuffer;
    //private GLWindow? window;
    //private EmbedContext? GL;
}