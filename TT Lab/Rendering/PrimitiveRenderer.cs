using System;
using System.Collections.Generic;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Shaders;

namespace TT_Lab.Rendering;

public enum PrimitiveLayer
{
    // Hidden behind the scene like anything else in it
    World,
    // Like World, but what is hidden behind the scene still faintly shows through
    WorldXRay,
    // Drawn over the whole scene, used by gizmos
    Overlay,
}

public enum PrimitiveShape
{
    // Cube from -1 to 1
    Box,
    // Radius of 1
    Sphere,
    // Base of radius 1 at y = 0 and the tip at y = 1
    Cone,
    // Radius of 1 going from y = 0 to y = 1
    Cylinder,
    // Square from -1 to 1 on XY, visible from both sides
    Quad,
    // Radius of 1 on XY, visible from both sides
    Disc,
}

/// <summary>
/// Anything that submits primitives every frame while it's visible, gets collected when registered for rendering
/// </summary>
public interface IPrimitiveRenderable
{
    void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera);
}

/// <summary>
/// Immediate mode renderer for editor visuals. Everything submitted on the render thread during a frame gets drawn at the end of it
/// with one instanced draw per kind of primitive and layer
/// </summary>
public sealed class PrimitiveRenderer : IDisposable
{
    private const int LineFloats = 16;
    private const int ShapeFloats = 20;
    private const int GridFloats = 32;
    private const uint LineBinding = 1;
    private const uint ShapeBinding = 2;
    private const uint GridBinding = 3;
    private const int LayerCount = 3;
    private const int ShapeCount = 6;
    private const int RoundSegments = 48;
    private const float XRayOpacity = 0.25f;
    private const float WorldLineDepthBias = 0.002f;

    private readonly RenderContext _context;
    private readonly FloatList[] _lines = new FloatList[LayerCount];
    private readonly FloatList[] _grids = new FloatList[LayerCount];
    // Opaque and transparent shapes get drawn separately so transparent ones don't hide what's behind them
    private readonly FloatList[] _opaqueShapes = new FloatList[LayerCount * ShapeCount];
    private readonly FloatList[] _transparentShapes = new FloatList[LayerCount * ShapeCount];
    private readonly (int First, int Count)[] _shapeRanges = new (int, int)[ShapeCount];
    private readonly List<(int Base, int Count)> _lineRanges = new(LayerCount);
    private readonly List<(int Base, int Count)> _gridRanges = new(LayerCount);
    private readonly (int Base, int Count)[] _opaqueShapeRanges = new (int, int)[LayerCount * ShapeCount];
    private readonly (int Base, int Count)[] _transparentShapeRanges = new (int, int)[LayerCount * ShapeCount];
    private readonly vec3[] _circle = new vec3[RoundSegments + 1];
    private StreamStorageBuffer? _lineBuffer;
    private StreamStorageBuffer? _shapeBuffer;
    private StreamStorageBuffer? _gridBuffer;
    private uint _shapeVao;
    private uint _shapeVbo;
    private uint _lineVao;

    public PrimitiveRenderer(RenderContext context)
    {
        _context = context;
        for (var i = 0; i < _lines.Length; i++)
        {
            _lines[i] = new FloatList();
            _grids[i] = new FloatList();
        }

        for (var i = 0; i < _opaqueShapes.Length; i++)
        {
            _opaqueShapes[i] = new FloatList();
            _transparentShapes[i] = new FloatList();
        }
    }

    public void DrawLine(vec3 start, vec3 end, vec4 color, float width = 2.0f, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        DrawLine(start, end, color, color, width, layer);
    }

    public void DrawLine(vec3 start, vec3 end, vec4 startColor, vec4 endColor, float width, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        var line = _lines[(int)layer].Add(LineFloats);
        line[0] = start.x;
        line[1] = start.y;
        line[2] = start.z;
        line[3] = width;
        line[4] = end.x;
        line[5] = end.y;
        line[6] = end.z;
        line[7] = 0.0f;
        StreamStorageBuffer.Write(line[8..], startColor);
        StreamStorageBuffer.Write(line[12..], endColor);
    }

    public void DrawPolyline(ReadOnlySpan<vec3> points, vec4 color, float width = 2.0f, PrimitiveLayer layer = PrimitiveLayer.World, bool closed = false)
    {
        for (var i = 1; i < points.Length; i++)
        {
            DrawLine(points[i - 1], points[i], color, color, width, layer);
        }

        if (closed && points.Length > 2)
        {
            DrawLine(points[^1], points[0], color, color, width, layer);
        }
    }

    public void DrawCircle(vec3 center, vec3 normal, float radius, vec4 color, float width = 2.0f, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        var (u, v) = GetPerpendiculars(normal);
        for (var i = 0; i <= RoundSegments; i++)
        {
            var angle = MathF.Tau * i / RoundSegments;
            _circle[i] = center + (u * MathF.Cos(angle) + v * MathF.Sin(angle)) * radius;
        }

        DrawPolyline(_circle, color, width, layer);
    }

    /// <summary>
    /// Edges of the cube from -1 to 1 transformed by the given matrix
    /// </summary>
    public void DrawWireBox(in mat4 transform, vec4 color, float width = 1.5f, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        Span<vec3> corners = stackalloc vec3[8];
        for (var i = 0; i < 8; i++)
        {
            var corner = new vec4((i & 1) == 0 ? -1.0f : 1.0f, (i & 2) == 0 ? -1.0f : 1.0f, (i & 4) == 0 ? -1.0f : 1.0f, 1.0f);
            corners[i] = (transform * corner).xyz;
        }

        for (var i = 0; i < 8; i++)
        {
            for (var axis = 1; axis < 8; axis <<= 1)
            {
                if ((i & axis) == 0)
                {
                    DrawLine(corners[i], corners[i | axis], color, color, width, layer);
                }
            }
        }
    }

    public void DrawWireSphere(vec3 center, float radius, vec4 color, float width = 1.5f, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        DrawCircle(center, vec3.UnitX, radius, color, width, layer);
        DrawCircle(center, vec3.UnitY, radius, color, width, layer);
        DrawCircle(center, vec3.UnitZ, radius, color, width, layer);
    }

    public void DrawShape(PrimitiveShape shape, in mat4 transform, vec4 color, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        var index = (int)layer * ShapeCount + (int)shape;
        var shapes = color.w < 0.999f ? _transparentShapes[index] : _opaqueShapes[index];
        var data = shapes.Add(ShapeFloats);
        StreamStorageBuffer.Write(data, transform);
        StreamStorageBuffer.Write(data[16..], color);
    }

    public void DrawBox(vec3 center, vec3 halfExtents, vec4 color, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        DrawShape(PrimitiveShape.Box, mat4.Translate(center) * mat4.Scale(halfExtents), color, layer);
    }

    public void DrawBox(vec3 center, vec3 halfExtents, quat rotation, vec4 color, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        DrawShape(PrimitiveShape.Box, mat4.Translate(center) * rotation.ToMat4 * mat4.Scale(halfExtents), color, layer);
    }

    public void DrawSphere(vec3 center, float radius, vec4 color, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        DrawShape(PrimitiveShape.Sphere, mat4.Translate(center) * mat4.Scale(radius), color, layer);
    }

    public void DrawCone(vec3 baseCenter, vec3 tip, float radius, vec4 color, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        DrawShape(PrimitiveShape.Cone, AlongDirection(baseCenter, tip - baseCenter, radius), color, layer);
    }

    public void DrawCylinder(vec3 start, vec3 end, float radius, vec4 color, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        DrawShape(PrimitiveShape.Cylinder, AlongDirection(start, end - start, radius), color, layer);
    }

    /// <summary>
    /// Parallelogram spanning from center - u - v to center + u + v
    /// </summary>
    public void DrawQuad(vec3 center, vec3 halfU, vec3 halfV, vec4 color, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        var normal = vec3.Cross(halfU, halfV).NormalizedSafe;
        DrawShape(PrimitiveShape.Quad, new mat4(new vec4(halfU, 0.0f), new vec4(halfV, 0.0f), new vec4(normal, 0.0f), new vec4(center, 1.0f)), color, layer);
    }

    public void DrawDisc(vec3 center, vec3 normal, float radius, vec4 color, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        var (u, v) = GetPerpendiculars(normal);
        DrawShape(PrimitiveShape.Disc, new mat4(new vec4(u * radius, 0.0f), new vec4(v * radius, 0.0f), new vec4(normal.NormalizedSafe, 0.0f), new vec4(center, 1.0f)), color, layer);
    }

    /// <summary>
    /// Grid on the plane through the center spanned by the two unit axes, fading out towards its radius. The lines through the center get
    /// the colors of the axes they go along
    /// </summary>
    public void DrawGrid(vec3 center, vec3 firstAxis, vec3 secondAxis, float radius, float cellSize, int cellsPerMajorLine, vec4 color,
        vec4 firstAxisColor, vec4 secondAxisColor, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        if (radius <= 0.0f || cellSize <= 0.0f)
        {
            return;
        }

        var grid = _grids[(int)layer].Add(GridFloats);
        var normal = vec3.Cross(firstAxis, secondAxis).NormalizedSafe;
        StreamStorageBuffer.Write(grid, new mat4(new vec4(firstAxis * radius, 0.0f), new vec4(secondAxis * radius, 0.0f), new vec4(normal, 0.0f), new vec4(center, 1.0f)));
        StreamStorageBuffer.Write(grid[16..], color);
        StreamStorageBuffer.Write(grid[20..], firstAxisColor);
        StreamStorageBuffer.Write(grid[24..], secondAxisColor);
        StreamStorageBuffer.Write(grid[28..], new vec4(radius / cellSize, Math.Max(cellsPerMajorLine, 1), 0.0f, 0.0f));
    }

    public void DrawArrow(vec3 start, vec3 end, vec4 color, float width, float headLength, float headRadius, PrimitiveLayer layer = PrimitiveLayer.World)
    {
        var direction = end - start;
        var length = direction.Length;
        if (length < 1e-6f)
        {
            return;
        }

        direction /= length;
        headLength = Math.Min(headLength, length);
        var headBase = end - direction * headLength;
        DrawLine(start, headBase, color, color, width, layer);
        DrawCone(headBase, end, headRadius, color, layer);
    }

    public void Render(in FrameCamera camera)
    {
        var hasLines = false;
        for (var i = 0; i < LayerCount; i++)
        {
            hasLines |= _lines[i].Count > 0 || _grids[i].Count > 0;
        }

        var hasShapes = false;
        for (var i = 0; i < _opaqueShapes.Length; i++)
        {
            hasShapes |= _opaqueShapes[i].Count > 0 || _transparentShapes[i].Count > 0;
        }

        if (!hasLines && !hasShapes)
        {
            return;
        }

        EnsureResources();
        UploadLines();
        UploadShapes();
        UploadGrids();

        var state = _context.State;
        state.SetBlend(true);
        state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
        state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        state.SetPolygonMode(PolygonMode.Fill);
        state.SetDepthTest(true);

        var lineProgram = _context.GetProgram("PrimitiveLine");
        lineProgram.Use();
        lineProgram.SetUniform(KnownUniform.StartView, camera.View);
        lineProgram.SetUniform(KnownUniform.StartProjection, camera.Projection);
        lineProgram.SetUniform(KnownUniform.ViewportSize, camera.ViewportSize);
        var shapeProgram = _context.GetProgram("PrimitiveShape");
        shapeProgram.Use();
        shapeProgram.SetUniform(KnownUniform.StartView, camera.View);
        shapeProgram.SetUniform(KnownUniform.StartProjection, camera.Projection);
        var gridProgram = _context.GetProgram("PrimitiveGrid");
        gridProgram.Use();
        gridProgram.SetUniform(KnownUniform.StartView, camera.View);
        gridProgram.SetUniform(KnownUniform.StartProjection, camera.Projection);

        state.SetDepthFunc(DepthFunction.Lequal);
        DrawLayer(PrimitiveLayer.World, 1.0f, WorldLineDepthBias, true);
        DrawLayer(PrimitiveLayer.WorldXRay, 1.0f, WorldLineDepthBias, true);
        // Drawn again only where it's hidden
        state.SetDepthFunc(DepthFunction.Greater);
        DrawLayer(PrimitiveLayer.WorldXRay, XRayOpacity, WorldLineDepthBias, false);

        // The scene's depth isn't needed anymore, the overlay only has to sort its own parts
        state.SetDepthMask(true);
        _context.Gl.Clear(ClearBufferMask.DepthBufferBit);
        state.SetDepthFunc(DepthFunction.Lequal);
        DrawLayer(PrimitiveLayer.Overlay, 1.0f, 0.0f, true);

        state.SetCullFace(false);
        state.SetDepthMask(true);
        state.SetDepthFunc(DepthFunction.Lequal);
        _context.Gl.BindVertexArray(0);
        Clear();
    }

    public void Clear()
    {
        for (var i = 0; i < LayerCount; i++)
        {
            _lines[i].Clear();
            _grids[i].Clear();
        }

        for (var i = 0; i < _opaqueShapes.Length; i++)
        {
            _opaqueShapes[i].Clear();
            _transparentShapes[i].Clear();
        }
    }

    private void DrawLayer(PrimitiveLayer layer, float opacity, float lineDepthBias, bool writeDepth)
    {
        var state = _context.State;
        var gl = _context.Gl;
        var shapeProgram = _context.GetProgram("PrimitiveShape");
        shapeProgram.Use();
        shapeProgram.SetUniform(KnownUniform.Opacity, opacity);
        gl.BindVertexArray(_shapeVao);
        state.SetCullFace(true);
        state.SetDepthMask(writeDepth);
        DrawShapes(layer, _opaqueShapeRanges);
        state.SetDepthMask(false);
        if (DrawGrids(layer, opacity, lineDepthBias))
        {
            shapeProgram.Use();
            gl.BindVertexArray(_shapeVao);
        }

        DrawShapes(layer, _transparentShapeRanges);
        state.SetCullFace(false);

        var (lineBase, lineCount) = _lineRanges[(int)layer];
        if (lineCount == 0)
        {
            return;
        }

        var lineProgram = _context.GetProgram("PrimitiveLine");
        lineProgram.Use();
        lineProgram.SetUniform(KnownUniform.Opacity, opacity);
        lineProgram.SetUniform(KnownUniform.DepthBias, lineDepthBias);
        gl.BindVertexArray(_lineVao);
        gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, 0, 6, (uint)lineCount, (uint)lineBase);
    }

    // Grids are seen from both sides and don't write depth, like the transparent shapes drawn after them
    private bool DrawGrids(PrimitiveLayer layer, float opacity, float depthBias)
    {
        var (gridBase, gridCount) = _gridRanges[(int)layer];
        if (gridCount == 0)
        {
            return false;
        }

        var gridProgram = _context.GetProgram("PrimitiveGrid");
        gridProgram.Use();
        gridProgram.SetUniform(KnownUniform.Opacity, opacity);
        gridProgram.SetUniform(KnownUniform.DepthBias, depthBias);
        _context.State.SetCullFace(false);
        _context.Gl.BindVertexArray(_lineVao);
        _context.Gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, 0, 6, (uint)gridCount, (uint)gridBase);
        _context.State.SetCullFace(true);
        return true;
    }

    private void DrawShapes(PrimitiveLayer layer, (int Base, int Count)[] ranges)
    {
        for (var shape = 0; shape < ShapeCount; shape++)
        {
            var (instanceBase, count) = ranges[(int)layer * ShapeCount + shape];
            if (count == 0)
            {
                continue;
            }

            var (first, vertexCount) = _shapeRanges[shape];
            _context.Gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, first, (uint)vertexCount, (uint)count, (uint)instanceBase);
        }
    }

    private void UploadLines()
    {
        _lineBuffer!.Clear();
        _lineRanges.Clear();
        foreach (var lines in _lines)
        {
            _lineRanges.Add((_lineBuffer.Count, lines.Count / LineFloats));
            CopyInto(_lineBuffer, lines, LineFloats);
        }

        _lineBuffer.Upload();
    }

    private void UploadGrids()
    {
        _gridBuffer!.Clear();
        _gridRanges.Clear();
        foreach (var grids in _grids)
        {
            _gridRanges.Add((_gridBuffer.Count, grids.Count / GridFloats));
            CopyInto(_gridBuffer, grids, GridFloats);
        }

        _gridBuffer.Upload();
    }

    private void UploadShapes()
    {
        _shapeBuffer!.Clear();
        for (var i = 0; i < _opaqueShapes.Length; i++)
        {
            _opaqueShapeRanges[i] = (_shapeBuffer.Count, _opaqueShapes[i].Count / ShapeFloats);
            CopyInto(_shapeBuffer, _opaqueShapes[i], ShapeFloats);
            _transparentShapeRanges[i] = (_shapeBuffer.Count, _transparentShapes[i].Count / ShapeFloats);
            CopyInto(_shapeBuffer, _transparentShapes[i], ShapeFloats);
        }

        _shapeBuffer.Upload();
    }

    private static void CopyInto(StreamStorageBuffer buffer, FloatList source, int floatsPerElement)
    {
        var data = source.AsSpan();
        if (data.Length == 0)
        {
            return;
        }

        data.CopyTo(buffer.Allocate(data.Length / floatsPerElement, out _));
    }

    private unsafe void EnsureResources()
    {
        if (_lineBuffer != null)
        {
            return;
        }

        var gl = _context.Gl;
        _lineBuffer = new StreamStorageBuffer(_context, LineBinding, LineFloats);
        _shapeBuffer = new StreamStorageBuffer(_context, ShapeBinding, ShapeFloats);
        _gridBuffer = new StreamStorageBuffer(_context, GridBinding, GridFloats);
        // Core profile doesn't draw without a vertex array even when the shader makes up its vertices
        _lineVao = gl.GenVertexArray();

        var vertices = new List<float>();
        _shapeRanges[(int)PrimitiveShape.Box] = PrimitiveMeshes.AddBox(vertices);
        _shapeRanges[(int)PrimitiveShape.Sphere] = PrimitiveMeshes.AddSphere(vertices);
        _shapeRanges[(int)PrimitiveShape.Cone] = PrimitiveMeshes.AddCone(vertices);
        _shapeRanges[(int)PrimitiveShape.Cylinder] = PrimitiveMeshes.AddCylinder(vertices);
        _shapeRanges[(int)PrimitiveShape.Quad] = PrimitiveMeshes.AddQuad(vertices);
        _shapeRanges[(int)PrimitiveShape.Disc] = PrimitiveMeshes.AddDisc(vertices);

        _shapeVao = gl.GenVertexArray();
        gl.BindVertexArray(_shapeVao);
        _shapeVbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _shapeVbo);
        var vertexData = vertices.ToArray();
        fixed (float* data = vertexData)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertexData.Length * sizeof(float)), data, BufferUsageARB.StaticDraw);
        }

        const uint stride = PrimitiveMeshes.VertexFloats * sizeof(float);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(1);
        gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _lineBuffer?.Dispose();
        _shapeBuffer?.Dispose();
        _gridBuffer?.Dispose();
        if (_shapeVao != 0)
        {
            _context.Gl.DeleteVertexArray(_shapeVao);
            _context.Gl.DeleteVertexArray(_lineVao);
            _context.Gl.DeleteBuffer(_shapeVbo);
        }
    }

    /// <summary>
    /// Maps local Y onto the given direction and X/Z onto the radius around it
    /// </summary>
    private static mat4 AlongDirection(vec3 origin, vec3 direction, float radius)
    {
        var (u, v) = GetPerpendiculars(direction);
        // V x direction = U keeps the basis right handed, a mirrored one would flip which faces get culled
        return new mat4(new vec4(v * radius, 0.0f), new vec4(direction, 0.0f), new vec4(u * radius, 0.0f), new vec4(origin, 1.0f));
    }

    public static (vec3 U, vec3 V) GetPerpendiculars(vec3 direction)
    {
        var normal = direction.NormalizedSafe;
        var helper = MathF.Abs(normal.y) < 0.99f ? vec3.UnitY : vec3.UnitX;
        var u = vec3.Cross(helper, normal).Normalized;
        var v = vec3.Cross(normal, u);
        return (u, v);
    }

    private sealed class FloatList
    {
        private float[] _data = new float[256];

        public int Count { get; private set; }

        public Span<float> Add(int floats)
        {
            if (Count + floats > _data.Length)
            {
                Array.Resize(ref _data, Math.Max(Count + floats, _data.Length * 2));
            }

            var span = _data.AsSpan(Count, floats);
            Count += floats;
            return span;
        }

        public ReadOnlySpan<float> AsSpan() => _data.AsSpan(0, Count);

        public void Clear()
        {
            Count = 0;
        }
    }
}
